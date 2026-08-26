using Orders.Core;
using TallaEgg.Core.DTOs.Order;

namespace Orders.Application.Services;

/// <summary>
/// A participant's position and profit/loss across every symbol they have ever traded
/// (issue #93). Runs identically for a customer or for the SuperAdmin/house account — the
/// house is just another party on every dealer trade, so its P&amp;L needs no separate logic,
/// only calling this with its own user id.
/// </summary>
public class PositionService
{
    private readonly ITradeRepository _tradeRepository;
    private readonly IQuoteRepository _quoteRepository;

    public PositionService(ITradeRepository tradeRepository, IQuoteRepository quoteRepository)
    {
        _tradeRepository = tradeRepository;
        _quoteRepository = quoteRepository;
    }

    public async Task<PositionsResponseDto> GetPositionsAsync(Guid userId)
    {
        var buyTrades = await _tradeRepository.GetTradesByBuyerUserIdAsync(userId);
        var sellTrades = await _tradeRepository.GetTradesBySellerUserIdAsync(userId);

        // Buyer/seller are mutually exclusive per trade (self-matching is refused elsewhere),
        // so this concat cannot double-count a trade.
        var bySymbol = buyTrades.Select(t => (Trade: t, IsBuyer: true))
            .Concat(sellTrades.Select(t => (Trade: t, IsBuyer: false)))
            .GroupBy(x => x.Trade.Symbol, StringComparer.OrdinalIgnoreCase);

        var response = new PositionsResponseDto();

        foreach (var group in bySymbol)
        {
            var legs = group.Select(x => new PositionTradeLeg(
                x.Trade.CreatedAt,
                x.IsBuyer ? x.Trade.Quantity : -x.Trade.Quantity,
                x.Trade.Price,
                x.IsBuyer ? x.Trade.FeeBuyer : x.Trade.FeeSeller));

            var result = PositionCalculator.Calculate(legs);

            // The buy price is the honest mark: it is what this participant would actually
            // receive selling right now. Using the sell price would flatter every long
            // position (and understate every short) by the spread.
            var quote = await _quoteRepository.GetActiveAsync(group.Key);
            var markPrice = quote?.BuyPrice;

            decimal? unrealizedPnl = result.RemainingQuantity == 0
                ? 0m
                : markPrice.HasValue && result.AverageCost.HasValue
                    ? result.RemainingQuantity * (markPrice.Value - result.AverageCost.Value)
                    : null; // flat's the only case a missing quote doesn't block an answer

            response.Positions.Add(new PositionDto
            {
                Symbol = group.Key,
                Quantity = result.RemainingQuantity,
                AverageCost = result.AverageCost,
                MarkPrice = markPrice,
                RealizedPnl = result.RealizedPnl,
                UnrealizedPnl = unrealizedPnl
            });

            response.TotalRealizedPnl += result.RealizedPnl;
            response.TotalUnrealizedPnl += unrealizedPnl ?? 0m;
        }

        response.Positions = response.Positions.OrderBy(p => p.Symbol, StringComparer.OrdinalIgnoreCase).ToList();
        return response;
    }
}
