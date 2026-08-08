namespace TallaEgg.TelegramBot.Simulator;

/// <summary>One simulated Telegram customer, driven through the real BotHandler.</summary>
public sealed class VirtualUser
{
    public required long TelegramId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Phone { get; init; }
    public required string Username { get; init; }

    /// <summary>Set once registration + phone-share complete.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Set once an admin approves or rejects the account.</summary>
    public bool Approved { get; set; }
}
