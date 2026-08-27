using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orders.Core;
using Serilog;
using TallaEgg.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.Requests.Order;
using TallaEgg.Core.Responses.Order;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.Core.ErrorHandling;

namespace Orders.Application;

public class OrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IWalletApiClient _walletApiClient;
    private readonly IMatchingEngine _matchingEngine;
    private readonly ILogger<OrderService> _logger;
    private readonly UsersApiClient _usersApiClient;

    /// <summary>
    /// The residual-collateral calculation is shared with the outbox processor. Two copies of this
    /// formula would drift apart sooner or later, and "several formulas for one quantity" is
    /// exactly what caused #52.
    /// </summary>
    private readonly Services.OrderCollateralReconciler _collateralReconciler;

    /// <summary>In dealer mode the best price comes from the quote, not the order book (issue #48).</summary>
    private readonly IQuoteRepository _quoteRepository;
    private readonly Services.MarketModeProvider _marketMode;

    public OrderService(
        IOrderRepository orderRepository,
        IWalletApiClient walletApiClient,
        IMatchingEngine matchingEngine,
        ILogger<OrderService> logger,
        UsersApiClient UsersApiClient,
        Services.OrderCollateralReconciler collateralReconciler,
        IQuoteRepository quoteRepository,
        Services.MarketModeProvider marketMode)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _walletApiClient = walletApiClient ?? throw new ArgumentNullException(nameof(walletApiClient));
        _matchingEngine = matchingEngine ?? throw new ArgumentNullException(nameof(matchingEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _usersApiClient = UsersApiClient;
        _collateralReconciler = collateralReconciler ?? throw new ArgumentNullException(nameof(collateralReconciler));
        _quoteRepository = quoteRepository ?? throw new ArgumentNullException(nameof(quoteRepository));
        _marketMode = marketMode ?? throw new ArgumentNullException(nameof(marketMode));
    }

    /// <summary>
    /// Creates a single order, determining the maker/taker role automatically.
    /// </summary>
    public async Task<CreateOrderResponse> CreateOrderAsync(OrderDto request)
    {
        try
        {
            _logger.LogInformation("Creating unified order for user {UserId} with symbol {Symbol}, side {Side}, type {Type}",
                request.UserId, request.Symbol, request.Side, request.Type);

            // 1. Validate authorization
            var canCreateOrder = true;
            if (!canCreateOrder)
            {
                throw new UnauthorizedAccessException("شما مجوز ثبت سفارش ندارید");
            }

            // 2. Determine trading parameters
            var orderSide = request.Side == TallaEgg.Core.Enums.Order.OrderSide.Buy ? TallaEgg.Core.Enums.Order.OrderSide.Buy : TallaEgg.Core.Enums.Order.OrderSide.Sell;
            var tradingType = request.TradingType;

            // 3. Validate user balance before creating order
            var userId = request.UserId;
            var assetToCheck = request.Side == TallaEgg.Core.Enums.Order.OrderSide.Buy
                ? request.Symbol.Split('/')[1] : request.Symbol.Split('/')[0];

            // The price is rounded to the column's precision before anything else uses it.
            //
            // The bot derives the price by dividing the mesghal price by 4.3318, which runs to 28
            // decimal places. If the lock is computed from that full value but the order is stored
            // with two decimals, settlement — which reads the price back from the database — works
            // from a different number and the difference stays in LockedBalance forever (issue #52).
            //
            // This has a useful side effect: the locked amount is now exactly
            // RoundToCurrencyPrecision(Amount x Price) over the stored order, so it can be
            // recomputed without adding a column to persist it.
            if (request.Price <= 0)
                throw new BusinessRuleException("قیمت باید بزرگ‌تر از صفر باشد");

            request.Price = CurrenciesConstant.RoundOrderPrice(request.Price);

            var (_, amountToCheck) = ComputeCollateral(request.Symbol, orderSide, request.Quantity, request.Price);

            _logger.LogInformation("Validating balance for user {UserId}: {Amount} {Asset}",
                userId, amountToCheck, assetToCheck);

            
            var validateCreditAndBalance =
                await _walletApiClient.ValidateCreditAndBalanceAsync(request.UserId, request.Symbol, request.Quantity, request.Price);

            var hasSufficientBalance = request.Side == OrderSide.Buy
                ? validateCreditAndBalance.HasSufficientCreditAndBalanceQuote : validateCreditAndBalance.HasSufficientCreditAndBalanceBase;

            var balanceCheckSuccess = validateCreditAndBalance.Success;
            var balanceMessage = validateCreditAndBalance.Message;


            var user = await _usersApiClient.GetUserByIdAsync(userId);
            var isadmin = user?.Role == UserRole.Admin;

            if (!isadmin)
            if (!balanceCheckSuccess)
            {
                _logger.LogWarning("Balance validation failed for user {UserId}: {Message}", userId, balanceMessage);
                throw new BusinessRuleException($"خطا در بررسی موجودی: {balanceMessage}");
            }

            if (!isadmin)
            if (!hasSufficientBalance)
            {
                _logger.LogWarning("Insufficient balance for user {UserId}: {Message}", userId, balanceMessage);
                throw new BusinessRuleException($"موجودی ناکافی: {balanceMessage}");
            }

            // 4. Create appropriate order command based on order type
            Order order;
            OrderRole determinedRole;
            List<TradeDto> executedTrades = new();


            // Limit orders start as Makers
            var limitCommand = new CreateOrderCommand(
                request.Symbol,
                request.Quantity,
                request.Price,
                userId,
                orderSide,
                tradingType,
                request.Notes
            );

            // Collateral is no longer locked here; it happens inside CreateOrderAsync, before the
            // order is confirmed. An unconfirmed order is not matchable, so no trade can exist
            // before its collateral is locked (audit finding C-5).
            order = await CreateOrderAsync(limitCommand);

            // Determine role based on order status
            determinedRole = order.Status == OrderStatus.Completed || order.Status == OrderStatus.Partially
                ? OrderRole.Mixed
                : OrderRole.Maker;

            // 6. Build response
            var response = new CreateOrderResponse
            {
                Order = new OrderHistoryDto
                {
                    Id = order.Id,
                    Asset = order.Asset,
                    Amount = order.Amount,
                    Price = order.Price,
                    Type = orderSide,
                    Status = order.Status,
                    Role = determinedRole,
                    TradingType = order.TradingType,
                    CreatedAt = order.CreatedAt,
                    Notes = order.Notes
                },
                ExecutedTrades = executedTrades,
                Role = determinedRole,
                Message = GetOrderCreationMessage(determinedRole, order.Status)
            };

            _logger.LogInformation("Unified order created successfully with ID: {OrderId}, Role: {Role}",
                order.Id, determinedRole);

            return response;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not ArgumentException and not InvalidOperationException)
        {
            _logger.LogError(ex, "Error creating unified order for user {UserId}", request.UserId);
            throw new BusinessRuleException("خطا در ایجاد سفارش", ex);
        }
    }

    /// <summary>
    /// The collateral an order requires: which asset, and how much.
    ///
    /// The only definition of this calculation in the system. Validation and locking both use it;
    /// two versions would drift apart sooner or later, and "several formulas for one quantity" is
    /// what caused #52.
    ///
    /// A buy amount rounds up while each trade's consumption rounds down. Those opposite directions
    /// are what keep "total consumed <= amount locked" true (full explanation on
    /// CeilingToCurrencyPrecision). The sell side needs no rounding, since its collateral is the
    /// order quantity itself.
    /// </summary>
    private static (string Asset, decimal Amount) ComputeCollateral(
        string symbol, OrderSide side, decimal quantity, decimal price)
    {
        var parts = symbol.Split('/');
        var baseAsset = parts[0];
        var quoteAsset = parts.Length > 1 ? parts[1] : parts[0];

        return side == OrderSide.Buy
            ? (quoteAsset, CurrenciesConstant.CeilingToCurrencyPrecision(quantity * price, quoteAsset))
            : (baseAsset, CurrenciesConstant.RoundToCurrencyPrecision(quantity, baseAsset));
    }

    /// <summary>
    /// Creates the order, locks its collateral and confirms it — but does not match it.
    ///
    /// That order — save as Pending, which is invisible to the matcher, then lock, then confirm —
    /// is the structural guarantee behind audit finding C-5. It is deliberately separated from
    /// matching so the quote-fill path can create <b>two</b> orders and match once, without
    /// duplicating this logic; duplicating a formula for one job is what caused #52.
    /// </summary>
    private async Task<(Order Order, bool Confirmed)> CreateLockedAndConfirmedOrderAsync(CreateOrderCommand command)
    {
        // Create order with Pending status
        var order = Order.CreateMakerOrder(
            command.Asset,
            command.Amount,
            command.Price,
            command.UserId,
            command.Type,
            command.TradingType,
            command.Notes
        );

        // The order is saved as Pending, and in that state the matching engine cannot see it —
        // which is what makes the sequence below possible.
        var createdOrder = await _orderRepository.AddAsync(order);

        // Lock the collateral before confirming the order.
        //
        // Locking used to happen after matching (audit finding C-5): the trade was recorded and
        // committed, and only then was the collateral locked. Because the trade had committed in
        // its own transaction, a failed lock rolled nothing back and left a recorded trade with no
        // collateral behind it, which could never settle.
        //
        // Now a failed lock means the order is never confirmed, so it is never matchable and there
        // is no trade to get stuck. The ordering becomes a structural guarantee rather than a
        // behavioural contract.
        var (collateralAsset, collateralAmount) =
            ComputeCollateral(command.Asset, command.Type, command.Amount, command.Price);

        var (lockSuccess, lockMessage, _) = await _walletApiClient.LockBalanceAsync(
            command.UserId, collateralAsset, collateralAmount);

        if (!lockSuccess)
        {
            _logger.LogWarning(
                "Failed to lock {Amount} {Asset} for order {OrderId} (user {UserId}): {Message}",
                collateralAmount, collateralAsset, createdOrder.Id, command.UserId, lockMessage);

            // The order is explicitly marked Failed rather than left Pending; otherwise the manual
            // confirmation endpoint could later activate it with no collateral behind it.
            await _orderRepository.UpdateStatusAsync(createdOrder.Id, OrderStatus.Failed,
                $"قفل وثیقه انجام نشد: {lockMessage}");

            throw new BusinessRuleException($"خطا در قفل کردن موجودی: {lockMessage}");
        }

        _logger.LogInformation("Locked {Amount} {Asset} for order {OrderId} (user {UserId}).",
            collateralAmount, collateralAsset, createdOrder.Id, command.UserId);

        // Confirm the order. From here it is matchable, and its collateral is already locked.
        var confirmSuccess = await ConfirmOrderIfPendingAsync(createdOrder.Id);

        if (!confirmSuccess)
            _logger.LogWarning("Order {OrderId} was not confirmed.", createdOrder.Id);

        return (createdOrder, confirmSuccess);
    }

    /// <summary>
    /// For the quote-fill path: creates, locks and confirms the order, and <b>does not match</b> it.
    ///
    /// The quote path creates two orders and then matches once. If creating each one ran matching
    /// itself, the first order could match against something else in the book and break the pairing
    /// of the two sides of the quote.
    /// </summary>
    public async Task<Order?> CreateLockedAndConfirmedOrderForQuoteAsync(CreateOrderCommand command)
    {
        var (order, confirmed) = await CreateLockedAndConfirmedOrderAsync(command);
        return confirmed ? order : null;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderCommand command)
    {
        var (createdOrder, confirmed) = await CreateLockedAndConfirmedOrderAsync(command);

        if (confirmed)
        {
            await _matchingEngine.ProcessOrderAsync(createdOrder);
        }
        else
        {
            _logger.LogWarning("Order {OrderId} was not confirmed, skipping matching engine", createdOrder.Id);
        }

        return createdOrder;
    }

    /// <summary>
    /// Confirm order status from Pending to Confirmed with concurrency safety
    /// Moves an order from Pending to Confirmed, safely under concurrency.
    /// </summary>
    public async Task<bool> ConfirmOrderIfPendingAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            
            var order = await _orderRepository.GetByIdAsync(orderId);
            
            // Idempotent: only update if Status == Pending
            if (order == null || order.Status != OrderStatus.Pending)
            {
                _logger.LogDebug("Order {OrderId} is not in Pending status or not found. Current status: {Status}", 
                    orderId, order?.Status);
                return false;
            }

            
            // Change status from Pending to Confirmed
            var updateSuccess = await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Confirmed, "تایید شده");
            
            if (updateSuccess)
            {
                _logger.LogInformation("Order {OrderId} status changed: Pending → Confirmed", orderId);
                
            }
            
            return updateSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming order {OrderId}", orderId);
            
            
            return false;
        }
    }

    public async Task<Order?> GetOrderByIdAsync(Guid orderId)
    {
        return await _orderRepository.GetByIdAsync(orderId);
    }

    public async Task<PagedResult<OrderHistoryDto>> GetOrdersByUserIdAsync(Guid userId, int pageNumber, int pageSize)
    {
        return await _orderRepository.GetOrdersByUserIdAsync(userId, pageNumber, pageSize);
    }

    public async Task<BestPricesDto> GetBestBidAskAsync(string asset, TradingType tradingType)
    {
        Log.Information(">--------------------- GetBestBidAskAsync({asset}, {tradingType}) ---------------------<", asset, tradingType);

        // In dealer mode prices come from the published quote, not the order book (issue #48).
        //
        // In that mode no orders rest in the book, so reading from the book would report "no price"
        // between trades — while the admin has in fact published one and is ready to deal.
        if (_marketMode.GetMode(asset) == MarketMode.Dealer)
        {
            var quote = await _quoteRepository.GetActiveAsync(asset);

            if (quote is null)
            {
                Log.Information("No active quote published for {Asset}.", asset);
                return new BestPricesDto { Symbol = asset, BestBidPrice = null, BestAskPrice = null };
            }

            Log.Information("Quote prices for {Asset}: bid {Bid}, ask {Ask}", asset, quote.BuyPrice, quote.SellPrice);

            // Bid is the price the admin buys at and Ask the price they sell at — the same meaning
            // the order book gave, so consumers see no change.
            return new BestPricesDto
            {
                Symbol = asset,
                BestBidPrice = quote.BuyPrice,
                BestAskPrice = quote.SellPrice
            };
        }

        var orders = await _orderRepository.GetOrdersByAssetAsync(asset);

        // The o.IsMaker() condition was removed.
        //
        // Order.Role is always Maker — no path sets it to anything else — so that condition was
        // always true and filtered nothing, while looking as though it did. The danger was that if
        // Role were ever set correctly, this method would silently drop taker orders from the
        // best-price calculation and show the user a wrong price.
        //
        // What is actually needed is the two remaining conditions: an open order, in that market.
        var activeOrders = orders.Where(o =>
            o.IsActive() &&
            o.TradingType == tradingType)
            .ToList();

        Log.Information("activeOrders:\n" + JsonConvert.SerializeObject(activeOrders, Formatting.Indented));

        decimal? bestBid = null;
        decimal? bestAsk = null;

        var buyOrders = activeOrders.Where(o => o.Side == OrderSide.Buy).ToList();
        if (buyOrders.Any())
        {
            bestBid = buyOrders.OrderByDescending(o => o.Price).First().Price;

            Log.Information("Best Bid found: {bestBid}", bestBid);
        }
        else
        {
            Log.Information("No active buy orders found for best bid.");
        }   

        var sellOrders = activeOrders.Where(o => o.Side == OrderSide.Sell).ToList();
        if (sellOrders.Any())
        {
            bestAsk = sellOrders.OrderBy(o => o.Price).First().Price;

            Log.Information("Best Ask found: {bestAsk}", bestAsk);
        }
        else
        {
            Log.Information("No active sell orders found for best ask.");
        }

        return new BestPricesDto
        {
            Symbol = asset,
            TradingType = tradingType,
            BestBidPrice = bestBid,
            BestAskPrice = bestAsk,
            Spread = bestBid.HasValue && bestAsk.HasValue ? bestAsk.Value - bestBid.Value : null
        };
    }

    public async Task<bool> CancelOrderAsync(Guid orderId, string? reason = null)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            return false;
        }

        if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Failed)
        {
            throw new BusinessRuleException("سفارشات کامل شده یا رد شده قابل کنسل شدن نیستند");
        }

        var success = await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Cancelled, reason);

        if (success)
        {
            try
            {
                // "What was locked" minus "what the trades consumed" — not an independent recalculation.
                //
                // This used to compute RemainingAmount x Price without rounding — a third formula,
                // separate from the lock's and from settlement's. Three different ways of computing
                // one quantity guaranteed that cancelling a partially-filled order left a residue
                // behind, and in the other direction could release more than was ever locked
                // (issue #52).
                var (assetToUnlock, amountToUnlock) = await _collateralReconciler.ComputeResidualLockAsync(order);

                if (amountToUnlock > 0)
                {
                    var (unlockSuccess, unlockMessage) = await _walletApiClient.UnlockBalanceAsync(
                        order.UserId,
                        assetToUnlock,
                        amountToUnlock);

                    if (!unlockSuccess)
                    {
                        _logger.LogError("CRITICAL: Failed to unlock balance for order {OrderId} user {UserId}. Asset: {Asset}, Amount: {Amount}. Message: {Message}",
                           orderId, order.UserId, assetToUnlock, amountToUnlock, unlockMessage);
                    }
                    else
                    {
                        _logger.LogInformation("Successfully unlocked {Amount} {Asset} for order {OrderId}", amountToUnlock, assetToUnlock, orderId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while unlocking balance for cancelled order {OrderId}", orderId);
            }
        }

        return success;
    }

    /// <summary>
    /// Returns every active order belonging to one user.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <returns>The user's active orders.</returns>
    /// <remarks>
    /// Delegates to the repository.
    /// </remarks>
    public async Task<List<Order>> GetActiveOrdersByUserIdAsync(Guid userId)
    {
        return await _orderRepository.GetActiveOrdersByUserIdAsync(userId);
    }

    public async Task<List<Order>> GetAllActiveOrdersAsync()
    {
        return await _orderRepository.GetActiveOrdersAsync();
    }

    /// <summary>
    /// Cancels every active order belonging to one user.
    /// </summary>
    /// <param name="userId">The user whose orders should be cancelled.</param>
    /// <param name="reason">Optional cancellation reason.</param>
    /// <returns>How many orders were cancelled successfully.</returns>
    /// <remarks>
    /// Fetches the user's active orders, cancels them one at a time, and returns how many
    /// succeeded. A failure on one order is logged and does not stop the rest.
    /// </remarks>
    public async Task<int> CancelAllActiveOrdersByUserIdAsync(Guid userId, string? reason = null)
    {
        var activeOrders = await GetActiveOrdersByUserIdAsync(userId);
        int cancelledCount = 0;

        foreach (var order in activeOrders)
        {
            try
            {
                var success = await CancelOrderAsync(order.Id, reason);
                if (success)
                    cancelledCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel order {OrderId} while cancelling every active order of user {UserId}; continuing with the rest.", order.Id, userId);
                continue;
            }
        }

        return cancelledCount;
    }

    private static string GetOrderCreationMessage(OrderRole role, OrderStatus status)
    {
        return role switch
        {
            OrderRole.Maker when status == OrderStatus.Pending =>
                "سفارش شما با موفقیت در Order Book قرار گرفت و منتظر تطبیق است",
            OrderRole.Taker when status == OrderStatus.Completed =>
                "سفارش شما فوراً اجرا شد",
            OrderRole.Mixed when status == OrderStatus.Partially =>
                "بخشی از سفارش شما فوراً اجرا شد و بقیه در Order Book قرار گرفت",
            OrderRole.Mixed when status == OrderStatus.Completed =>
                "سفارش شما به طور کامل اجرا شد",
            _ => "سفارش شما با موفقیت ثبت شد"
        };
    }
}
