namespace TallaEgg.Core.DTOs.Order
{
    /// <summary>
    /// One participant's position and profit/loss in a single symbol (issue #93).
    /// </summary>
    public class PositionDto
    {
        public string Symbol { get; set; } = "";

        /// <summary>Signed: positive is long (net bought), negative is short (net sold, credit-backed), zero is flat.</summary>
        public decimal Quantity { get; set; }

        /// <summary>FIFO cost basis of <see cref="Quantity"/>. Null when flat -- there is nothing to have a cost basis.</summary>
        public decimal? AverageCost { get; set; }

        /// <summary>The active quote's buy price -- what this participant would receive selling right now. Null if no quote is published.</summary>
        public decimal? MarkPrice { get; set; }

        /// <summary>Realized profit/loss from every closed portion of every trade in this symbol, fees included.</summary>
        public decimal RealizedPnl { get; set; }

        /// <summary>Unrealized profit/loss on <see cref="Quantity"/> at <see cref="MarkPrice"/>. Null when either is unavailable.</summary>
        public decimal? UnrealizedPnl { get; set; }
    }

    /// <summary>
    /// A participant's profit/loss across every symbol they have ever traded, plus the total
    /// (issue #93). Every symbol here quotes against Toman, so summing across symbols is a
    /// meaningful single number, not an apples-to-oranges total.
    /// </summary>
    public class PositionsResponseDto
    {
        public List<PositionDto> Positions { get; set; } = new();
        public decimal TotalRealizedPnl { get; set; }
        public decimal TotalUnrealizedPnl { get; set; }
        public decimal TotalPnl => TotalRealizedPnl + TotalUnrealizedPnl;
    }
}
