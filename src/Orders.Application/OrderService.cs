using Microsoft.Extensions.Logging;
using Orders.Core;
using Orders.Infrastructure.Clients;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Requests.Order;
using TallaEgg.Core.Responses.Order;

namespace Orders.Application;

public class BestBidAskResult
{
    public string Asset { get; set; } = string.Empty;
    public TradingType TradingType { get; set; }
    public decimal? BestBid { get; set; }
    public decimal? BestAsk { get; set; }
    public decimal? Spread { get; set; }
    public Guid? MatchingOrderId { get; set; }
}

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
    public async Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request)
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
            var orderType = request.Side == TallaEgg.Core.Enums.Order.OrderType.Buy ? TallaEgg.Core.Enums.Order.OrderType.Buy : TallaEgg.Core.Enums.Order.OrderType.Sell;
            var tradingType = request.TradingType;

            // 3. Create appropriate order command based on order type
            Order order;
            OrderRole determinedRole;
            List<TradeDto> executedTrades = new();

            if (request.Type == OrderTypeEnum.Market)
            {
                // Market orders are always Takers
                var marketCommand = new CreateMarketOrderRequest
                {
                    Asset = request.Symbol,
                    Amount = request.Quantity,
                    UserId = Guid.Parse(request.UserId),
                    Type = orderType,
                    TradingType = tradingType,
                    Notes = request.Notes
                };

                order = await CreateMarketOrderAsync(marketCommand);
                determinedRole = OrderRole.Taker;
            }
            else // Limit order
            {
                if (request.Price == null)
                {
                    throw new ArgumentException("قیمت برای سفارش محدود الزامی است");
                }

                // Limit orders start as Makers
                var limitCommand = new CreateOrderCommand(
                    request.Symbol,
                    request.Quantity,
                    request.Price.Value,
                    Guid.Parse(request.UserId),
                    orderType,
                    tradingType,
                    request.Notes
                );

                order = await CreateOrderAsync(limitCommand);
                
                // Determine role based on order status
                determinedRole = order.Status == OrderStatus.Completed || order.Status == OrderStatus.Partially 
                    ? OrderRole.Mixed 
                    : OrderRole.Maker;
            }

            // 4. Build response
            var response = new CreateOrderResponse
            {
                Order = new OrderHistoryDto
                {
                    Id = order.Id,
                    Asset = order.Asset,
                    Amount = order.Amount,
                    Price = order.Price,
                    Type = orderType,
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
        // Simple implementation - in real scenario would include balance validation, etc.
        var order = Order.CreateMakerOrder(
            command.Asset,
            command.Amount,
            command.Price,
            command.UserId,
            command.Type,
            command.TradingType,
            command.Notes
        );

        var createdOrder = await _orderRepository.AddAsync(order);
        await _matchingEngine.ProcessOrderAsync(createdOrder);
        
        return createdOrder;
    }

    public async Task<Order> CreateMarketOrderAsync(CreateMarketOrderRequest command)
    {
        // Get estimated price for market order
        var estimatedPrice = 50000m; // Simplified - would calculate from order book
        
        var marketOrder = Order.CreateMarketOrder(
            command.Asset,
            command.Amount,
            estimatedPrice,
            command.UserId,
            command.Type,
            command.TradingType,
            command.Notes
        );

        var createdOrder = await _orderRepository.AddAsync(marketOrder);
        await _matchingEngine.ProcessOrderAsync(createdOrder);
        
        return createdOrder;
    }

    public async Task<Order?> GetOrderByIdAsync(Guid orderId)
    {
        return await _orderRepository.GetByIdAsync(orderId);
    }

    public async Task<PagedResult<OrderHistoryDto>> GetOrdersByUserIdAsync(Guid userId, int pageNumber, int pageSize)
    {
        return await _orderRepository.GetOrdersByUserIdAsync(userId, pageNumber, pageSize);
    }

    public async Task<BestBidAskResult> GetBestBidAskAsync(string asset, TradingType tradingType)
    {
        var orders = await _orderRepository.GetOrdersByAssetAsync(asset);
        
        var activeOrders = orders.Where(o => 
            o.IsMaker() && 
            o.IsActive() && 
            o.TradingType == tradingType)
            .ToList();

        decimal? bestBid = null;
        decimal? bestAsk = null;

        var buyOrders = activeOrders.Where(o => o.Type == OrderType.Buy && o.Status == OrderStatus.Pending).ToList();
        if (buyOrders.Any())
        {
            bestBid = buyOrders.OrderByDescending(o => o.Price).First().Price;
        }

        var sellOrders = activeOrders.Where(o => o.Type == OrderType.Sell && o.Status == OrderStatus.Pending).ToList();
        if (sellOrders.Any())
        {
            bestAsk = sellOrders.OrderBy(o => o.Price).First().Price;
        }

        return new BestBidAskResult
        {
            Asset = asset,
            TradingType = tradingType,
            BestBid = bestBid,
            BestAsk = bestAsk,
            Spread = bestBid.HasValue && bestAsk.HasValue ? bestAsk.Value - bestBid.Value : null,
            MatchingOrderId = null
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
