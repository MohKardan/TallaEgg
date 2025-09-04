using Telegram.Bot.Types;

namespace TallaEgg.TelegramBot.Core.Interfaces;

public interface IBotHandler
{
    Task HandleMessageAsync(Message message);
    
    Task HandleCallbackQueryAsync(CallbackQuery callbackQuery);
} 