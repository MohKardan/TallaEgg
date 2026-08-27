using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Infrastructure.Clients;

namespace Orders.Application.Services;

/// <summary>
/// When an order is fully filled, releases whatever collateral is still locked against it.
///
/// Why a residue exists (issue #52): the lock is computed once for the whole order and rounds up,
/// while each trade's consumption is computed separately and rounds down. Those opposite directions
/// guarantee that total consumption never exceeds the lock, but they leave a small difference that
/// belongs to the user and has to go back.
///
/// <b>Timing matters.</b> The first version of this ran immediately after matching and failed in
/// practice: the balance lock is created <b>after</b> the match (audit finding C-5) and consumed
/// <b>later still</b> by outbox settlement. The release therefore raced both, and sometimes ran
/// against a wallet that had nothing locked in it yet.
///
/// It is now called from the outbox processor after a successful settlement. That point is
/// inherently after the lock exists — settlement cannot succeed without it — and after it is used.
/// </summary>
public class OrderCollateralReconciler
{
    private readonly IOrderRepository _orderRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IWalletApiClient _walletApiClient;
    private readonly ILogger<OrderCollateralReconciler> _logger;

    public OrderCollateralReconciler(
        IOrderRepository orderRepository,
        ITradeRepository tradeRepository,
        IWalletApiClient walletApiClient,
        ILogger<OrderCollateralReconciler> logger)
    {
        _orderRepository = orderRepository;
        _tradeRepository = tradeRepository;
        _walletApiClient = walletApiClient;
        _logger = logger;
    }

    /// <summary>
    /// Releases an order's residual collateral once it is fully filled. Does nothing for an order
    /// still open, because the remaining collateral still backs the unfilled part.
    /// </summary>
    public async Task ReleaseResidualLockIfCompletedAsync(Guid orderId)
    {
        try
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order is null || order.Status != OrderStatus.Completed)
                return;

            var (asset, residual) = await ComputeResidualLockAsync(order);
            if (residual <= 0)
                return;

            var (success, message) = await _walletApiClient.UnlockBalanceAsync(order.UserId, asset, residual);

            if (success)
                _logger.LogInformation(
                    "Released residual lock of {Residual} {Asset} for completed order {OrderId}.",
                    residual, asset, orderId);
            else
                // Harmless: the residue stays where it is and reconciliation (#39) can pick it up
                // later. No money is lost, it simply is not released.
                _logger.LogWarning(
                    "Could not release residual lock of {Residual} {Asset} for completed order {OrderId}: {Message}",
                    residual, asset, orderId, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing residual lock for order {OrderId}", orderId);
        }
    }

    /// <summary>
    /// Residue = "what was locked" minus "what this order's trades consumed".
    ///
    /// "What was locked" is recomputed with the same formula and the same rounding direction used
    /// when the order was placed. Rounding here and ceiling there would recreate the very difference
    /// this code exists to remove.
    ///
    /// Recomputation is only possible because the price is rounded to the column's precision when
    /// the order is placed. Before that, the lock was computed from an unrounded price and could not
    /// be reconstructed from the stored row.
    /// </summary>
    public async Task<(string Asset, decimal Residual)> ComputeResidualLockAsync(Order order)
    {
        var parts = order.Asset.Split('/');
        var baseAsset = parts[0];
        var quoteAsset = parts.Length > 1 ? parts[1] : parts[0];

        if (order.Side == OrderSide.Buy)
        {
            var locked = CurrenciesConstant.CeilingToCurrencyPrecision(order.Amount * order.Price, quoteAsset);
            var trades = await _tradeRepository.GetTradesByBuyOrderIdAsync(order.Id);
            return (quoteAsset, locked - trades.Sum(t => t.QuoteQuantity));
        }

        // Sell side: the collateral is the base asset and each trade consumes exactly Quantity with
        // no rounding, so normally there is no residue. The calculation is kept so that if that
        // assumption ever changes, this is covered automatically.
        var lockedBase = CurrenciesConstant.RoundToCurrencyPrecision(order.Amount, baseAsset);
        var sellTrades = await _tradeRepository.GetTradesBySellOrderIdAsync(order.Id);
        return (baseAsset, lockedBase - sellTrades.Sum(t => t.Quantity));
    }
}
