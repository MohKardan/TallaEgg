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

namespace Orders.Application;

public class OrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IWalletApiClient _walletApiClient;
    private readonly IMatchingEngine _matchingEngine;
    private readonly ILogger<OrderService> _logger;
    private readonly UsersApiClient _usersApiClient;

    /// <summary>
    /// محاسبهٔ باقی‌ماندهٔ وثیقه اینجا و در پردازشگر outbox مشترک است. اگر دو کپی از این
    /// فرمول وجود داشته باشد، دیر یا زود از هم جدا می‌شوند — و «چند فرمول برای یک
    /// کمیت» دقیقاً همان چیزی بود که #52 را ساخت.
    /// </summary>
    private readonly Services.OrderCollateralReconciler _collateralReconciler;

    public OrderService(
        IOrderRepository orderRepository,
        IWalletApiClient walletApiClient,
        IMatchingEngine matchingEngine,
        ILogger<OrderService> logger,
        UsersApiClient UsersApiClient,
        Services.OrderCollateralReconciler collateralReconciler)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _walletApiClient = walletApiClient ?? throw new ArgumentNullException(nameof(walletApiClient));
        _matchingEngine = matchingEngine ?? throw new ArgumentNullException(nameof(matchingEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _usersApiClient = UsersApiClient;
        _collateralReconciler = collateralReconciler ?? throw new ArgumentNullException(nameof(collateralReconciler));
    }

    /// <summary>
    /// ایجاد سفارش واحد با تشخیص خودکار نقش (Maker/Taker)
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

            // قیمت پیش از هر استفاده‌ای به دقت ستون گرد می‌شود.
            //
            // ربات قیمت را از تقسیم «قیمت مثقال ÷ ۴٫۳۳۱۸» می‌سازد که تا ۲۸ رقم اعشار
            // ادامه دارد. اگر قفل از آن مقدار کامل حساب شود ولی سفارش با دو رقم اعشار
            // ذخیره گردد، تسویه — که قیمت را از دیتابیس می‌خواند — با عدد دیگری کار
            // می‌کند و اختلاف تا ابد در LockedBalance می‌ماند (issue #52).
            //
            // این کار یک اثر جانبی مهم هم دارد: از این پس «مبلغ قفل‌شده» دقیقاً برابر
            // RoundToCurrencyPrecision(Amount × Price) روی همان سفارشِ ذخیره‌شده است،
            // پس بدون افزودن ستون جدید قابل بازمحاسبه است.
            if (request.Price <= 0)
                throw new ArgumentException("قیمت باید بزرگ‌تر از صفر باشد");

            request.Price = CurrenciesConstant.RoundOrderPrice(request.Price);

            var (_, amountToCheck) = ComputeCollateral(request.Symbol, orderSide, request.Quantity, request.Price);

            _logger.LogInformation("Validating balance for user {UserId}: {Amount} {Asset}",
                userId, amountToCheck, assetToCheck);

            //var (balanceCheckSuccess, balanceMessage, hasSufficientBalance) =
            //    await _walletApiClient.ValidateBalanceAsync(
            //    userId,
            //    assetToCheck,
            //    amountToCheck);

            
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
                throw new InvalidOperationException($"خطا در بررسی موجودی: {balanceMessage}");
            }

            if (!isadmin)
            if (!hasSufficientBalance)
            {
                _logger.LogWarning("Insufficient balance for user {UserId}: {Message}", userId, balanceMessage);
                throw new InvalidOperationException($"موجودی ناکافی: {balanceMessage}");
            }

            // 4. Create appropriate order command based on order type
            Order order;
            OrderRole determinedRole;
            List<TradeDto> executedTrades = new();


            if (request.Price == null)
            {
                throw new ArgumentException("قیمت برای سفارش محدود الزامی است");
            }

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

            // قفل وثیقه دیگر اینجا انجام نمی‌شود؛ داخل CreateOrderAsync و پیش از تأیید
            // سفارش است. تا وقتی سفارش تأیید نشده قابل تطبیق نیست، پس هیچ معامله‌ای
            // نمی‌تواند پیش از قفل شدن وثیقه‌اش وجود داشته باشد (یافتهٔ ممیزی C-5).
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
            throw new InvalidOperationException("خطا در ایجاد سفارش", ex);
        }
    }

    /// <summary>
    /// وثیقهٔ لازم برای یک سفارش: کدام دارایی و چه مقدار.
    ///
    /// تنها تعریف این محاسبه در سیستم. اعتبارسنجی و قفل هر دو از همین استفاده می‌کنند؛
    /// اگر دو نسخه وجود داشته باشد دیر یا زود از هم جدا می‌شوند — و «چند فرمول برای یک
    /// کمیت» همان چیزی بود که #52 را ساخت.
    ///
    /// مبلغ خرید رو به بالا گرد می‌شود، در حالی که مصرفِ هر معامله رو به پایین. این
    /// جهت‌های مخالف تضمین می‌کنند «مجموع مصرف ≤ مقدار قفل‌شده» همیشه برقرار بماند
    /// (توضیح کامل روی CeilingToCurrencyPrecision). سمت فروش گرد کردن لازم ندارد،
    /// چون وثیقه‌اش خودِ مقدار سفارش است.
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

    public async Task<Order> CreateOrderAsync(CreateOrderCommand command)
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

        // سفارش با وضعیت Pending ذخیره می‌شود و در این وضعیت برای موتور تطبیق نامرئی
        // است — این همان چیزی است که ترتیب زیر را ممکن می‌کند.
        var createdOrder = await _orderRepository.AddAsync(order);

        // ► قفل وثیقه پیش از تأیید سفارش.
        //
        // پیش‌تر قفل پس از تطبیق انجام می‌شد (یافتهٔ ممیزی C-5): معامله ثبت و commit
        // می‌شد و تازه بعد وثیقه قفل می‌گردید. چون معامله در تراکنش خودش commit شده بود،
        // شکست قفل چیزی را برنمی‌گرداند و یک معاملهٔ ثبت‌شدهٔ بدون وثیقه باقی می‌ماند که
        // تسویه‌اش هرگز موفق نمی‌شد.
        //
        // حالا اگر قفل شکست بخورد، سفارش هرگز تأیید نمی‌شود، پس هرگز قابل تطبیق نیست و
        // هیچ معامله‌ای وجود ندارد که بخواهد گیر کند. ترتیب از یک قرارداد رفتاری به یک
        // تضمین ساختاری تبدیل می‌شود.
        var (collateralAsset, collateralAmount) =
            ComputeCollateral(command.Asset, command.Type, command.Amount, command.Price);

        var (lockSuccess, lockMessage, _) = await _walletApiClient.LockBalanceAsync(
            command.UserId, collateralAsset, collateralAmount);

        if (!lockSuccess)
        {
            _logger.LogWarning(
                "Failed to lock {Amount} {Asset} for order {OrderId} (user {UserId}): {Message}",
                collateralAmount, collateralAsset, createdOrder.Id, command.UserId, lockMessage);

            // سفارش به‌صراحت Failed علامت می‌خورد تا در وضعیت Pending رها نشود؛ در آن
            // صورت endpoint تأیید دستی می‌توانست بعداً بدون وثیقه فعالش کند.
            await _orderRepository.UpdateStatusAsync(createdOrder.Id, OrderStatus.Failed,
                $"قفل وثیقه انجام نشد: {lockMessage}");

            throw new InvalidOperationException($"خطا در قفل کردن موجودی: {lockMessage}");
        }

        _logger.LogInformation("Locked {Amount} {Asset} for order {OrderId} (user {UserId}).",
            collateralAmount, collateralAsset, createdOrder.Id, command.UserId);

        // تأیید سفارش — از این لحظه قابل تطبیق می‌شود، و وثیقه‌اش از قبل قفل است.
        var confirmSuccess = await ConfirmOrderIfPendingAsync(createdOrder.Id);

        if (confirmSuccess)
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
    /// تایید وضعیت سفارش از Pending به Confirmed با ایمنی همزمانی
    /// </summary>
    public async Task<bool> ConfirmOrderIfPendingAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: If database transaction support is needed, wrap in transaction
            // using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            
            var order = await _orderRepository.GetByIdAsync(orderId);
            
            // Idempotent: only update if Status == Pending
            if (order == null || order.Status != OrderStatus.Pending)
            {
                _logger.LogDebug("Order {OrderId} is not in Pending status or not found. Current status: {Status}", 
                    orderId, order?.Status);
                return false;
            }

            // TODO: Add business validation if needed
            // var validationResult = await ValidateOrderForConfirmationAsync(order);
            // if (!validationResult.IsValid) { return false; }
            
            // Change status from Pending to Confirmed
            var updateSuccess = await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Confirmed, "تایید شده");
            
            if (updateSuccess)
            {
                _logger.LogInformation("Order {OrderId} status changed: Pending → Confirmed", orderId);
                
                // TODO: If transaction was used, commit here
                // await transaction.CommitAsync(cancellationToken);
            }
            
            return updateSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming order {OrderId}", orderId);
            
            // TODO: If transaction was used, rollback here
            // await transaction.RollbackAsync(cancellationToken);
            
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

        var orders = await _orderRepository.GetOrdersByAssetAsync(asset);

        //Log.Information("orders:\n" + JsonConvert.SerializeObject(orders, Formatting.Indented));

        var activeOrders = orders.Where(o =>
            o.IsMaker() &&
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
            throw new InvalidOperationException("سفارشات کامل شده یا رد شده قابل کنسل شدن نیستند");
        }

        //if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
        //{
        //    throw new InvalidOperationException("فقط سفارشات در انتظار یا تایید شده قابل لغو هستند");
        //}

        //return await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Cancelled, reason);

        var success = await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Cancelled, reason);

        if (success)
        {
            try
            {
                // «آنچه قفل شد» منهای «آنچه معاملات مصرف کردند» — نه یک بازمحاسبهٔ مستقل.
                //
                // پیش‌تر اینجا RemainingAmount × Price بدون گرد کردن حساب می‌شد، یعنی
                // فرمول سومی جدا از فرمول قفل و فرمول تسویه. سه راه متفاوت برای محاسبهٔ
                // یک کمیت، تضمین می‌کرد که پس از لغوِ یک سفارشِ نیمه‌پرشده باقی‌مانده‌ای
                // جا بماند — و در جهت دیگر، می‌توانست بیش از مقدار قفل‌شده آزاد کند
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
    /// دریافت تمام سفارشات فعال یک کاربر
    /// </summary>
    /// <param name="userId">شناسه کاربر</param>
    /// <returns>لیست سفارشات فعال کاربر</returns>
    /// <remarks>
    /// این متد از repository برای دریافت سفارشات فعال استفاده می‌کند
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
    /// کنسل کردن تمام سفارشات فعال یک کاربر
    /// </summary>
    /// <param name="userId">شناسه کاربر که سفارشاتش باید کنسل شوند</param>
    /// <param name="reason">دلیل کنسل کردن سفارشات (اختیاری)</param>
    /// <returns>تعداد سفارشاتی که با موفقیت کنسل شدند</returns>
    /// <remarks>
    /// این تابع:
    /// 1. تمام سفارشات فعال کاربر را دریافت می‌کند
    /// 2. به صورت یکی یکی آنها را کنسل می‌کند
    /// 3. تعداد سفارشات کنسل شده را برمی‌گرداند
    /// 4. در صورت خطا در کنسل هر سفارش، ادامه می‌دهد و آن را لاگ می‌کند
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
                // Log the error but continue with other orders
                // TODO: Consider adding proper logging here
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
