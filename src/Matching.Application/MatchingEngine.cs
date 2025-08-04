using Matching.Core;
using Wallet.Core;

namespace Matching.Application;

public class MatchingEngine
{
    private readonly IMatchingRepository _matchingRepository;
    private readonly IWalletService _walletService;
    private readonly decimal _tradingFee = 0.001m; // 0.1% trading fee

    public MatchingEngine(IMatchingRepository matchingRepository, IWalletService walletService)
    {
        _matchingRepository = matchingRepository;
        _walletService = walletService;
    }

    public async Task<(bool success, string message, Order? order)> PlaceOrderAsync(
        Guid userId, string asset, decimal amount, decimal price, OrderType type)
    {
        try
        {
            // Validate order
            if (amount <= 0 || price <= 0)
                return (false, "مقدار و قیمت باید بزرگتر از صفر باشد.", null);

            // Check wallet balance
            var balanceCheck = await CheckWalletBalanceAsync(userId, asset, amount, price, type);
            if (!balanceCheck.success)
                return (false, balanceCheck.message, null);

            // Create order
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Asset = asset,
                Amount = amount,
                Price = price,
                Type = type,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                OrderId = GenerateOrderId()
            };

            await _matchingRepository.CreateOrderAsync(order);

            // Try to match the order
            await MatchOrderAsync(order);

            return (true, "سفارش با موفقیت ثبت شد.", order);
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ثبت سفارش: {ex.Message}", null);
        }
    }

    private async Task MatchOrderAsync(Order newOrder)
    {
        var oppositeType = newOrder.Type == OrderType.Buy ? OrderType.Sell : OrderType.Buy;
        var pendingOrders = await _matchingRepository.GetPendingOrdersAsync(newOrder.Asset, oppositeType);

        var remainingAmount = newOrder.Amount;
        var executedAmount = 0m;
        var executedPrice = 0m;

        foreach (var existingOrder in pendingOrders)
        {
            if (remainingAmount <= 0) break;

            // Check if orders can match
            if (newOrder.Type == OrderType.Buy && existingOrder.Price > newOrder.Price) break;
            if (newOrder.Type == OrderType.Sell && existingOrder.Price < newOrder.Price) break;

            // Calculate trade amount
            var tradeAmount = Math.Min(remainingAmount, existingOrder.Amount);
            var tradePrice = existingOrder.Price; // Price of the existing order

            // Execute trade
            var trade = new Trade
            {
                Id = Guid.NewGuid(),
                BuyOrderId = newOrder.Type == OrderType.Buy ? newOrder.Id : existingOrder.Id,
                SellOrderId = newOrder.Type == OrderType.Sell ? newOrder.Id : existingOrder.Id,
                BuyerUserId = newOrder.Type == OrderType.Buy ? newOrder.UserId : existingOrder.UserId,
                SellerUserId = newOrder.Type == OrderType.Sell ? newOrder.UserId : existingOrder.UserId,
                Asset = newOrder.Asset,
                Amount = tradeAmount,
                Price = tradePrice,
                ExecutedAt = DateTime.UtcNow,
                Fee = tradeAmount * tradePrice * _tradingFee,
                TradeId = GenerateTradeId()
            };

            await _matchingRepository.CreateTradeAsync(trade);

            // Update wallet balances
            await UpdateWalletBalancesAsync(trade);

            // Update orders
            remainingAmount -= tradeAmount;
            executedAmount += tradeAmount;
            executedPrice = tradePrice;

            // Update existing order
            existingOrder.Amount -= tradeAmount;
            if (existingOrder.Amount <= 0)
            {
                existingOrder.Status = OrderStatus.Filled;
                existingOrder.ExecutedAt = DateTime.UtcNow;
            }
            else
            {
                existingOrder.Status = OrderStatus.Partial;
                existingOrder.ExecutedAmount = (existingOrder.ExecutedAmount ?? 0) + tradeAmount;
            }
            await _matchingRepository.UpdateOrderAsync(existingOrder);
        }

        // Update new order
        if (executedAmount > 0)
        {
            newOrder.ExecutedAmount = executedAmount;
            newOrder.ExecutedPrice = executedPrice;
            
            if (remainingAmount <= 0)
            {
                newOrder.Status = OrderStatus.Filled;
                newOrder.ExecutedAt = DateTime.UtcNow;
            }
            else
            {
                newOrder.Status = OrderStatus.Partial;
                newOrder.Amount = remainingAmount;
            }
            await _matchingRepository.UpdateOrderAsync(newOrder);
        }
    }

    private async Task<(bool success, string message)> CheckWalletBalanceAsync(
        Guid userId, string asset, decimal amount, decimal price, OrderType type)
    {
        if (type == OrderType.Buy)
        {
            // Check if user has enough fiat currency (assuming USD as base currency)
            var requiredFiat = amount * price;
            var fiatBalance = await _walletService.GetBalanceAsync(userId, "USD");
            if (fiatBalance < requiredFiat)
                return (false, "موجودی ناکافی برای خرید.");
        }
        else // Sell
        {
            // Check if user has enough asset
            var assetBalance = await _walletService.GetBalanceAsync(userId, asset);
            if (assetBalance < amount)
                return (false, "موجودی ناکافی برای فروش.");
        }

        return (true, "موجودی کافی است.");
    }

    private async Task UpdateWalletBalancesAsync(Trade trade)
    {
        var tradeValue = trade.Amount * trade.Price;
        var buyerFee = trade.Fee / 2;
        var sellerFee = trade.Fee / 2;

        // Update buyer wallet
        await _walletService.DebitAsync(trade.BuyerUserId, "USD", tradeValue + buyerFee);
        await _walletService.CreditAsync(trade.BuyerUserId, trade.Asset, trade.Amount);

        // Update seller wallet
        await _walletService.DebitAsync(trade.SellerUserId, trade.Asset, trade.Amount);
        await _walletService.CreditAsync(trade.SellerUserId, "USD", tradeValue - sellerFee);
    }

    public async Task<(bool success, string message)> CancelOrderAsync(Guid orderId, Guid userId)
    {
        var order = await _matchingRepository.GetOrderAsync(orderId);
        if (order == null)
            return (false, "سفارش یافت نشد.");

        if (order.UserId != userId)
            return (false, "شما مجاز به لغو این سفارش نیستید.");

        if (order.Status != OrderStatus.Pending)
            return (false, "فقط سفارش‌های در انتظار قابل لغو هستند.");

        var success = await _matchingRepository.CancelOrderAsync(orderId);
        return success ? (true, "سفارش با موفقیت لغو شد.") : (false, "خطا در لغو سفارش.");
    }

    public async Task<IEnumerable<Order>> GetUserOrdersAsync(Guid userId, string? asset = null)
    {
        return await _matchingRepository.GetUserOrdersAsync(userId, asset);
    }

    public async Task<IEnumerable<Trade>> GetUserTradesAsync(Guid userId, string? asset = null)
    {
        return await _matchingRepository.GetUserTradesAsync(userId, asset);
    }

    public async Task<IEnumerable<Order>> GetOrderBookAsync(string asset, int depth = 10)
    {
        return await _matchingRepository.GetOrderBookAsync(asset, depth);
    }

    public async Task<IEnumerable<Trade>> GetRecentTradesAsync(string asset, int count = 50)
    {
        return await _matchingRepository.GetRecentTradesAsync(asset, count);
    }

    private string GenerateOrderId()
    {
        return $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
    }

    private string GenerateTradeId()
    {
        return $"TRD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
    }
}

// Interface for wallet service (will be implemented in Wallet service)
//public interface IWalletService
//{
//    Task<decimal> GetBalanceAsync(Guid userId, string asset);
//    Task<bool> CreditAsync(Guid userId, string asset, decimal amount);
//    Task<bool> DebitAsync(Guid userId, string asset, decimal amount);
//} 