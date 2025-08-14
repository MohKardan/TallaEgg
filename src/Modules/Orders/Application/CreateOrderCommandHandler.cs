using TallaEgg.Api.Modules.Orders.Core;
using TallaEgg.Api.Modules.Matching.Application;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Api.Modules.Orders.Application;

public class CreateOrderCommandHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly MatchingEngine _matchingEngine;

    public CreateOrderCommandHandler(IOrderRepository orderRepository, MatchingEngine matchingEngine)
    {
        _orderRepository = orderRepository;
        _matchingEngine = matchingEngine;
    }

    public async Task<(bool success, string message, Order? order)> HandleAsync(CreateOrderCommand command)
    {
        try
        {
            // Validate command
            if (command.Amount <= 0 || command.Price <= 0)
                return (false, "مقدار و قیمت باید بزرگتر از صفر باشد.", null);

            if (string.IsNullOrWhiteSpace(command.Asset))
                return (false, "نام ارز نمی‌تواند خالی باشد.", null);

            // Create order entity
            var order = new Order
            {
                Id = Guid.NewGuid(),
                Asset = command.Asset.Trim().ToUpperInvariant(),
                Amount = command.Amount,
                Price = command.Price,
                UserId = command.UserId,
                Type = command.Type,
                Status = OrderStatus.Pending,
                TradingType = command.TradingType,
                Role = OrderRole.Maker,
                CreatedAt = DateTime.UtcNow,
                Notes = command.Notes,
                OrderId = GenerateOrderId()
            };

            // Save order to database
            var savedOrder = await _orderRepository.CreateAsync(order);

            // Try to match the order using matching engine
            var matchingResult = await _matchingEngine.PlaceOrderAsync(
                command.UserId, 
                order.Asset, 
                order.Amount, 
                order.Price, 
                order.Type);

            if (matchingResult.success)
            {
                // Update order status based on matching result
                if (matchingResult.order != null)
                {
                    savedOrder.Status = matchingResult.order.Status;
                    savedOrder.ExecutedAmount = matchingResult.order.ExecutedAmount;
                    savedOrder.ExecutedPrice = matchingResult.order.ExecutedPrice;
                    savedOrder.ExecutedAt = matchingResult.order.ExecutedAt;
                    await _orderRepository.UpdateAsync(savedOrder);
                }
            }

            return (true, "سفارش با موفقیت ثبت شد.", savedOrder);
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ثبت سفارش: {ex.Message}", null);
        }
    }

    public async Task<(bool success, string message, Order? order)> HandleAsync(CreateTakerOrderCommand command)
    {
        try
        {
            // Get parent order
            var parentOrder = await _orderRepository.GetByIdAsync(command.ParentOrderId);
            if (parentOrder == null)
                return (false, "سفارش اصلی یافت نشد.", null);

            if (parentOrder.Status != OrderStatus.Pending)
                return (false, "سفارش اصلی در وضعیت قابل قبول نیست.", null);

            if (command.Amount > parentOrder.Amount)
                return (false, "مقدار سفارش نمی‌تواند بیشتر از سفارش اصلی باشد.", null);

            // Create taker order
            var takerOrder = new Order
            {
                Id = Guid.NewGuid(),
                Asset = parentOrder.Asset,
                Amount = command.Amount,
                Price = parentOrder.Price,
                UserId = command.UserId,
                Type = parentOrder.Type == OrderType.Buy ? OrderType.Sell : OrderType.Buy, // Opposite type
                Status = OrderStatus.Pending,
                TradingType = parentOrder.TradingType,
                Role = OrderRole.Taker,
                CreatedAt = DateTime.UtcNow,
                ParentOrderId = parentOrder.Id,
                Notes = command.Notes,
                OrderId = GenerateOrderId()
            };

            // Save taker order
            var savedTakerOrder = await _orderRepository.CreateAsync(takerOrder);

            // Update parent order
            parentOrder.Amount -= command.Amount;
            if (parentOrder.Amount <= 0)
            {
                parentOrder.Status = OrderStatus.Filled;
                parentOrder.ExecutedAt = DateTime.UtcNow;
            }
            await _orderRepository.UpdateAsync(parentOrder);

            return (true, "سفارش با موفقیت ثبت شد.", savedTakerOrder);
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ثبت سفارش: {ex.Message}", null);
        }
    }

    private string GenerateOrderId()
    {
        return $"ORD_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
    }
}
