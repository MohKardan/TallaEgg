namespace TallaEgg.Core.DTOs.Order
{
    /// <summary>Auto-quote settings for one symbol, for transfer between the Orders service and the bot (issue #90).</summary>
    public class AutoQuoteSettingsDto
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal SpreadPercent { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public record UpdateAutoQuoteSpreadRequest(decimal SpreadPercent, Guid UpdatedByUserId);

    public record SetAutoQuoteEnabledRequest(bool IsEnabled, Guid UpdatedByUserId);
}
