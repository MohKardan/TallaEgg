using TallaEgg.Api.Modules.Matching.Core;
using TallaEgg.Api.Modules.Wallet.Core;

namespace TallaEgg.Api.Modules.Matching.Application;

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

    public async Task<(bool success, string message)> CancelOrderAsync(Guid orderId, Guid userId)
    {
        try
        {
            var order = await _matchingRepository.GetOrderAsync(orderId);
            if (order == null)
                return (false, "سفارش یافت نشد.");

            if (order.UserId != userId)
                return (false, "شما مجاز به لغو این سفارش نیستید.");

            if (order.Status != OrderStatus.Pending)
                return (false, "فقط سفارشات در انتظار قابل لغو هستند.");

            order.Status = OrderStatus.Cancelled;
            await _matchingRepository.UpdateOrderAsync(order);

            return (true, "سفارش با موفقیت لغو شد.");
        }
        catch (Exception ex)
        {
            return (false, $"خطا در لغو سفارش: {ex.Message}");
        }
    }

    public async Task<IEnumerable<Order>> GetUserOrdersAsync(Guid userId, string? asset = null)
    {
        return await _matchingRepository.GetUserOrdersAsync(userId, asset);
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

            // Update order amounts
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
            }
            await _matchingRepository.UpdateOrderAsync(existingOrder);
        }

        // Update new order
        if (remainingAmount <= 0)
        {
            newOrder.Status = OrderStatus.Filled;
            newOrder.ExecutedAt = DateTime.UtcNow;
        }
        else if (executedAmount > 0)
        {
            newOrder.Status = OrderStatus.Partial;
        }
        newOrder.ExecutedAmount = executedAmount;
        newOrder.ExecutedPrice = executedPrice;
        await _matchingRepository.UpdateOrderAsync(newOrder);
    }

    private async Task<(bool success, string message)> CheckWalletBalanceAsync(Guid userId, string asset, decimal amount, decimal price, OrderType type)
    {
        try
        {
            if (type == OrderType.Buy)
            {
                // Check if user has enough money to buy
                var balance = await _walletService.GetBalanceAsync(userId, "USDT"); // Assuming USDT as base currency
                var requiredAmount = amount * price;
                if (balance < requiredAmount)
                    return (false, "موجودی ناکافی برای خرید.");
            }
            else
            {
                // Check if user has enough asset to sell
                var balance = await _walletService.GetBalanceAsync(userId, asset);
                if (balance < amount)
                    return (false, "موجودی ناکافی برای فروش.");
            }

            return (true, "موجودی کافی است.");
        }
        catch (Exception ex)
        {
            return (false, $"خطا در بررسی موجودی: {ex.Message}");
        }
    }

    private async Task UpdateWalletBalancesAsync(Trade trade)
    {
        try
        {
            var tradeValue = trade.Amount * trade.Price;
            var fee = trade.Fee;

            // Update buyer's wallet
            await _walletService.WithdrawAsync(trade.BuyerUserId, "USDT", tradeValue + fee, trade.Id.ToString());
            await _walletService.DepositAsync(trade.BuyerUserId, trade.Asset, trade.Amount, trade.Id.ToString());

            // Update seller's wallet
            await _walletService.WithdrawAsync(trade.SellerUserId, trade.Asset, trade.Amount, trade.Id.ToString());
            await _walletService.DepositAsync(trade.SellerUserId, "USDT", tradeValue - fee, trade.Id.ToString());
        }
        catch (Exception ex)
        {
            // Log error and handle wallet update failure
            throw new InvalidOperationException($"خطا در به‌روزرسانی موجودی کیف پول: {ex.Message}");
        }
    }

    private string GenerateOrderId()
    {
        return $"ORD_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
    }

    private string GenerateTradeId()
    {
        return $"TRD_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
    }
}

