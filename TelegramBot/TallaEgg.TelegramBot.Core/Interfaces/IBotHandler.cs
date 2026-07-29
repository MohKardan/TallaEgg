using Telegram.Bot.Types;

namespace TallaEgg.TelegramBot.Core.Interfaces;

public interface IBotHandler
{
    Task HandleMessageAsync(Message message);
    
    Task HandleCallbackQueryAsync(CallbackQuery callbackQuery);

    /// <summary>
    /// Starts background work (the conversation sweep and the startup announcement).
    /// Separate from construction so building the handler has no side effects (issue #65).
    /// </summary>
    void Start(CancellationToken cancellationToken = default);
} 