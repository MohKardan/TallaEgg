namespace TallaEgg.Core.DTOs.Order
{
    /// <summary>Whether one symbol is currently tradable, for transfer between the Orders service and the bot.</summary>
    public class SymbolSettingsDto
    {
        public string Symbol { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public record SetSymbolActiveRequest(bool IsActive, Guid UpdatedByUserId);
}
