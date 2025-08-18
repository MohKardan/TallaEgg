using TallaEgg.Core.Enums.Order;

namespace Orders.Application;

public record CreateMarketOrderCommand(
    string Asset,
    decimal Amount,
    Guid UserId,
    OrderType Type,
    TradingType TradingType,
    string? Notes = null);
