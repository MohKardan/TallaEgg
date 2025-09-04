using Telegram.Bot.Types;

namespace TallaEgg.TelegramBot.Core.Interfaces;

public interface IBotHandler
{
    Task HandleUpdateAsync(object update);
    
    Task HandleCallbackQueryAsync(CallbackQuery callbackQuery);
} 