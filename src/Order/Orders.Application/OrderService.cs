using Microsoft.Extensions.Logging;
using Orders.Core;
using Orders.Infrastructure.Clients;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Requests.Order;
using TallaEgg.Core.Responses.Order;

namespace Orders.Application;

public class OrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IWalletApiClient _walletApiClient;
    private readonly IMatchingEngine _matchingEngine;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IWalletApiClient walletApiClient,
        IMatchingEngine matchingEngine,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _walletApiClient = walletApiClient ?? throw new ArgumentNullException(nameof(walletApiClient));
        _matchingEngine = matchingEngine ?? throw new ArgumentNullException(nameof(matchingEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// ایجاد سفارش واحد با تشخیص خودکار نقش (Maker/Taker)
    /// </summary>
    public async Task<CreateOrderResponse> CreateOrderAsync(OrderDto request)
    {
        try
        {
            _logger.LogInformation("Creating unified order for user {UserId} with symbol {Symbol}, side {Side}, type {Type}",
                request.UserId, request.Symbol, request.Side, request.Side);

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

            var amountToCheck = request.Side == TallaEgg.Core.Enums.Order.OrderSide.Buy
                ? request.Quantity * request.Price
                : request.Quantity;

            _logger.LogInformation("Validating balance for user {UserId}: {Amount} {Asset}",
                userId, amountToCheck, assetToCheck);

            var (balanceCheckSuccess, balanceMessage, hasSufficientBalance) =
                await _walletApiClient.ValidateBalanceAsync(
                userId,
                assetToCheck,
                amountToCheck);

            if (!balanceCheckSuccess)
            {
                _logger.LogWarning("Balance validation failed for user {UserId}: {Message}", userId, balanceMessage);
                throw new InvalidOperationException($"خطا در بررسی موجودی: {balanceMessage}");
            }

            if (!hasSufficientBalance)
            {
                _logger.LogWarning("Insufficient balance for user {UserId}: {Message}", userId, balanceMessage);
                throw new InvalidOperationException($"موجودی ناکافی: {balanceMessage}");
            }

            // 4. For limit orders, lock the balance
            //if (request.Type == OrderTypeEnum.Limit)
            {
                var (lockSuccess, lockMessage, walletDto) = await _walletApiClient.LockBalanceAsync(
                    userId,
                    assetToCheck,
                    amountToCheck);

                if (!lockSuccess)
                {
                    _logger.LogWarning("Failed to lock balance for user {UserId}: {Message}", userId, lockMessage);
                    throw new InvalidOperationException($"خطا در قفل کردن موجودی: {lockMessage}");
                }

                _logger.LogInformation("Successfully locked {Amount} {Asset} for user {UserId}",
                    amountToCheck, assetToCheck, userId);
            }

            // 5. Create appropriate order command based on order type
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

        // Save order to database first
        var createdOrder = await _orderRepository.AddAsync(order);

        // Confirm order first (business validation, balance check already done above)
        var confirmSuccess = await ConfirmOrderIfPendingAsync(createdOrder.Id);
        
        if (confirmSuccess)
        {
            // Only send confirmed orders to matching engine
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
        var orders = await _orderRepository.GetOrdersByAssetAsync(asset);

        var activeOrders = orders.Where(o =>
            o.IsMaker() &&
            o.IsActive() &&
            o.TradingType == tradingType)
            .ToList();

        decimal? bestBid = null;
        decimal? bestAsk = null;

        var buyOrders = activeOrders.Where(o => o.Side == OrderSide.Buy && o.Status == OrderStatus.Confirmed).ToList();
        if (buyOrders.Any())
        {
            bestBid = buyOrders.OrderByDescending(o => o.Price).First().Price;
        }

        var sellOrders = activeOrders.Where(o => o.Side == OrderSide.Sell && o.Status == OrderStatus.Confirmed).ToList();
        if (sellOrders.Any())
        {
            bestAsk = sellOrders.OrderBy(o => o.Price).First().Price;
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

        if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
        {
            throw new InvalidOperationException("فقط سفارشات در انتظار یا تایید شده قابل لغو هستند");
        }

        return await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Cancelled, reason);
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
