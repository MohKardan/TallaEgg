using Microsoft.Extensions.Logging;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Infrastructure.Clients;

namespace Orders.Application.Services;

/// <summary>
/// A customer accepts the admin's quote and the trade executes immediately.
///
/// <para>
/// This replaces the "admin places two 1000-gram orders" model (issue #48). Instead of liquidity
/// resting in the book with its collateral locked, two orders are created <b>for exactly the
/// requested quantity</b> and consumed in the same instant.
/// </para>
///
/// <para>
/// <b>Why it creates orders rather than a trade directly:</b> the <c>Trade</c> table requires a buy
/// and a sell order id (foreign keys). Creating a trade without orders would mean either a schema
/// change or a second trade-creation path — and a second path is precisely what issue #40 is about.
/// This design deliberately reuses the <c>ExecuteAtomicMatchAsync</c> that already works, so the
/// outbox, settlement, history and reporting all stay untouched.
/// </para>
/// </summary>
public class QuoteFillService
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly OrderService _orderService;
    private readonly MarketModeProvider _marketMode;
    private readonly OrderMatchingRepository _matchingRepository;
    private readonly IWalletApiClient _walletApiClient;
    private readonly ILogger<QuoteFillService> _logger;

    public QuoteFillService(
        IQuoteRepository quoteRepository,
        OrderService orderService,
        MarketModeProvider marketMode,
        OrderMatchingRepository matchingRepository,
        IWalletApiClient walletApiClient,
        ILogger<QuoteFillService> logger)
    {
        _quoteRepository = quoteRepository;
        _orderService = orderService;
        _marketMode = marketMode;
        _matchingRepository = matchingRepository;
        _walletApiClient = walletApiClient;
        _logger = logger;
    }

    /// <summary>
    /// The customer trades a given quantity against the current quote. They do not enter a price —
    /// it comes from the quote, which also removes the mesghal/gram ambiguity from their flow.
    /// </summary>
    public async Task<(bool Success, string Message, Trade? Trade)> AcceptQuoteAsync(
        Guid customerUserId, string symbol, OrderSide customerSide, decimal quantity)
    {
        if (quantity <= 0)
            return (false, "مقدار باید بزرگ‌تر از صفر باشد.", null);

        // Rounded once, here, before anything is created — and this value is what both orders
        // and the match then use.
        //
        // The order columns hold two decimal places, so a customer's 1.2345 grams was stored
        // as 1.23 while the in-memory order kept 1.2345, and the match ran on the unrounded
        // figure. Rounding at the boundary means the order the customer gets is the order the
        // confirmation showed them: the confirmation already displays the rounded amount, so
        // 1.23 is the number they agreed to (issue #74).
        var baseAsset = symbol.Split('/')[0];
        quantity = CurrenciesConstant.RoundToCurrencyPrecision(quantity, baseAsset);

        if (quantity <= 0)
            return (false, $"مقدار وارد‌شده از حداقل قابل معامله کمتر است.", null);

        // This message used to say "this symbol is not in dealer mode", which reads to a customer
        // like a product rule when it always means a missing configuration (issue #73). There is
        // nothing the customer can do; they only need to know to try again later. The detail an
        // operator needs is logged in MarketModeStartupValidator, not here.
        if (_marketMode.GetMode(symbol) != MarketMode.Dealer)
            return (false, "این نماد موقتاً در دسترس نیست. لطفاً کمی بعد دوباره تلاش کنید.", null);

        var quote = await _quoteRepository.GetActiveAsync(symbol);

        if (quote is null)
        {
            // The only thing that can name a counterparty is a published quote, so its absence
            // is now a hard stop rather than a detail. Logged as a warning because on a running
            // shop it means nobody has posted a price — the customer cannot fix it and the
            // operator needs to know.
            _logger.LogWarning(
                "Refusing quote fill for {UserId}: no active quote is published for {Symbol}.",
                customerUserId, symbol);

            return (false, "در حال حاضر مظنه‌ای منتشر نشده است.", null);
        }

        // The counterparty is whoever published this quote.
        //
        // This used to be an id in the configuration file. That value is unknowable against a
        // database built from scratch, so deployment became "start it, register, copy the id,
        // restart" — and a wrong value in between was not harmless: the system came up and refused
        // every trade.
        //
        // The quote already knows who published it, so this never needed storing anywhere else. The
        // economics are also more correct: the customer trades at a price that person announced, so
        // the trade belongs on that person's book. With one admin the behaviour is exactly as
        // before; with several, each keeps their own book with no extra configuration.
        var marketMakerUserId = quote.PublishedByUserId;

        // An admin cannot fill their own quote: both sides would be the same user and settlement
        // would refuse it anyway — better to stop it here with a clear message.
        if (customerUserId == marketMakerUserId)
            return (false, "بازارگردان نمی‌تواند مظنهٔ خودش را بپذیرد.", null);

        var price = quote.PriceFor(customerSide);

        // Check the customer's balance and credit before creating any order.
        //
        // In the order-book model this check lived inside SubmitOrderAsync, and the quote-fill path
        // (issue #48) does not go through there — so it was silently skipped. All that remained was
        // the collateral lock, and LockBalance performs no balance check at all: its guard in
        // Wallet.cs is deliberately disabled so the market maker's account can go negative.
        //
        // The result was that any brand-new user with no credit at all could trade and go negative.
        // It was invisible on the old database because everyone already had credit; on an empty one
        // it showed up immediately.
        //
        // The market maker is deliberately exempt: in the dealer model they are always the
        // counterparty, and their negative position is the shop's book, not an error.
        var (checkSucceeded, checkMessage, hasBaseAsset, hasQuoteAsset) =
            await _walletApiClient.ValidateCreditAndBalanceAsync(customerUserId, symbol, quantity, price);

        if (!checkSucceeded)
        {
            _logger.LogWarning(
                "Refusing quote fill for {UserId} on {Symbol}: balance check failed — {Message}",
                customerUserId, symbol, checkMessage);

            return (false, "بررسی موجودی انجام نشد. لطفاً دوباره تلاش کنید.", null);
        }

        // A buy is paid for in the quote asset, a sell in the base asset.
        var hasEnough = customerSide == OrderSide.Buy ? hasQuoteAsset : hasBaseAsset;

        if (!hasEnough)
        {
            _logger.LogInformation(
                "Refusing quote fill for {UserId}: not enough {Side} funds for {Quantity} {Symbol} at {Price}.",
                customerUserId, customerSide, quantity, symbol, price);

            return (false, "موجودی یا اعتبار شما برای این معامله کافی نیست.", null);
        }

        // Both orders are created at the same price and quantity, so the match is always complete
        // and nothing is left resting in the book.
        var customerOrder = await CreateSideAsync(customerUserId, symbol, customerSide, quantity, price,
            $"پذیرش مظنه {quote.Id}");

        if (customerOrder is null)
            return (false, "ثبت سفارش شما انجام نشد.", null);

        var adminSide = customerSide == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        var adminOrder = await CreateSideAsync(marketMakerUserId, symbol, adminSide, quantity, price,
            $"طرف مقابل مظنه {quote.Id}");

        if (adminOrder is null)
        {
            // The customer's order exists and is locked but has no counterparty. Cancel it to
            // release the collateral, or their money stays locked for no reason.
            await _orderService.CancelOrderAsync(customerOrder.Id, "طرف مقابل مظنه ثبت نشد");
            return (false, "در حال حاضر امکان انجام این معامله نیست.", null);
        }

        var buyOrder = customerSide == OrderSide.Buy ? customerOrder : adminOrder;
        var sellOrder = customerSide == OrderSide.Buy ? adminOrder : customerOrder;

        var (success, trade, error) =
            await _matchingRepository.ExecuteAtomicMatchAsync(buyOrder, sellOrder, quantity);

        if (!success)
        {
            _logger.LogError(
                "Quote fill failed to match for user {UserId} on {Symbol}: {Error}",
                customerUserId, symbol, error);

            // No trade was recorded, so both orders must be cancelled to release both sides'
            // collateral.
            await _orderService.CancelOrderAsync(customerOrder.Id, "تطبیق مظنه انجام نشد");
            await _orderService.CancelOrderAsync(adminOrder.Id, "تطبیق مظنه انجام نشد");

            return (false, error ?? "انجام معامله ممکن نشد.", null);
        }

        _logger.LogInformation(
            "Quote fill: user {UserId} {Side} {Quantity} {Symbol} at {Price} against market maker {MarketMaker}.",
            customerUserId, customerSide, quantity, symbol, price, marketMakerUserId);

        return (true, "معامله با موفقیت انجام شد.", trade);
    }

    /// <summary>
    /// Builds one side of the trade: the order is created, its collateral locked and the order
    /// confirmed — but not matched. Matching happens once, for both orders together.
    /// </summary>
    private async Task<Order?> CreateSideAsync(
        Guid userId, string symbol, OrderSide side, decimal quantity, decimal price, string notes)
    {
        try
        {
            var command = new CreateOrderCommand(
                symbol, quantity, price, userId, side, TradingType.Spot, notes);

            return await _orderService.CreateLockedAndConfirmedOrderForQuoteAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Could not create the {Side} side of a quote fill for user {UserId} on {Symbol}.",
                side, userId, symbol);
            return null;
        }
    }
}
