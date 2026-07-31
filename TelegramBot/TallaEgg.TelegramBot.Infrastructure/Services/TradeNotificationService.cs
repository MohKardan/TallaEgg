using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Utilties;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace TallaEgg.TelegramBot.Infrastructure.Services;

/// <summary>
/// سرویس اطلاع‌رسانی تطبیق معاملات
/// </summary>
/// <remarks>
/// این سرویس مسئول ارسال اطلاعیه‌های تطبیق معاملات به کاربران تلگرام است
/// </remarks>
public class TradeNotificationService
{
    private readonly ITelegramBotClient _botClient;
    private readonly UsersApiClient _usersApiClient;

    public TradeNotificationService(ITelegramBotClient botClient, UsersApiClient usersApiClient)
    {
        _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
        _usersApiClient = usersApiClient ?? throw new ArgumentNullException(nameof(usersApiClient));
    }

    /// <summary>
    /// ارسال اطلاعیه تطبیق معامله به کاربران مربوطه
    /// </summary>
    /// <param name="notification">اطلاعات کامل معامله تطبیق یافته</param>
    /// <returns>نتیجه عملیات ارسال اطلاعیه شامل وضعیت ارسال به هر دو طرف</returns>
    /// <remarks>
    /// این متد:
    /// 1. اطلاعات کاربران خریدار و فروشنده را از API دریافت می‌کند
    /// 2. پیام‌های مناسب برای هر کاربر تولید می‌کند
    /// 3. اطلاعیه‌ها را به تلگرام ارسال می‌کند
    /// 4. نتیجه عملیات را برمی‌گرداند
    /// </remarks>
    public async Task<TradeNotificationResult> SendTradeMatchNotificationAsync(TradeMatchNotificationDto notification)
    {
        var result = new TradeNotificationResult
        {
            TradeId = notification.TradeId,
            NotificationDateTime = DateTime.UtcNow
        };

        // ارسال اطلاعیه به خریدار
        result.BuyerNotificationSent = await SendTradeNotificationToBuyerAsync(notification);
        
        // ارسال اطلاعیه به فروشنده  
        result.SellerNotificationSent = await SendTradeNotificationToSellerAsync(notification);

        return result;
    }

    /// <summary>
    /// ارسال اطلاعیه تطبیق معامله به کاربر خریدار
    /// </summary>
    /// <param name="notification">اطلاعات معامله</param>
    /// <returns>true اگر ارسال موفق باشد، در غیر این صورت false</returns>
    /// <remarks>
    /// این متد:
    /// 1. اطلاعات کاربر خریدار را از UsersApiClient دریافت می‌کند
    /// 2. بررسی می‌کند که آیا کاربر TelegramUserId دارد یا نه
    /// 3. پیام مخصوص خریدار را تولید و ارسال می‌کند
    /// </remarks>
    private async Task<bool> SendTradeNotificationToBuyerAsync(TradeMatchNotificationDto notification)
    {
        try
        {
            // دریافت اطلاعات کاربر خریدار
            var buyerUser = await _usersApiClient.GetUserByIdAsync(notification.BuyerUserId);
            
            if (buyerUser == null)
            {
                Console.WriteLine($"کاربر خریدار با شناسه {notification.BuyerUserId} یافت نشد");
                return false;
            }

            // بررسی اینکه آیا کاربر در تلگرام ثبت‌نام کرده یا نه
            if (buyerUser.TelegramId == 0)
            {
                Console.WriteLine($"کاربر خریدار {buyerUser.PhoneNumber} در تلگرام ثبت‌نام نکرده است");
                return false;
            }

            var buyerMessage = FormatBuyerTradeMessage(notification);
            
            // ارسال پیام به تلگرام کاربر خریدار
            await _botClient.SendMessage(
                chatId: buyerUser.TelegramId,
                text: buyerMessage,
                parseMode: ParseMode.Markdown
            );
            
            Console.WriteLine($"اطلاعیه تطبیق معامله با موفقیت به خریدار {buyerUser.PhoneNumber} ارسال شد");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"خطا در ارسال اطلاعیه به خریدار: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// ارسال اطلاعیه تطبیق معامله به کاربر فروشنده
    /// </summary>
    /// <param name="notification">اطلاعات معامله</param>
    /// <returns>true اگر ارسال موفق باشد، در غیر این صورت false</returns>
    /// <remarks>
    /// این متد:
    /// 1. اطلاعات کاربر فروشنده را از UsersApiClient دریافت می‌کند
    /// 2. بررسی می‌کند که آیا کاربر TelegramUserId دارد یا نه
    /// 3. پیام مخصوص فروشنده را تولید و ارسال می‌کند
    /// </remarks>
    private async Task<bool> SendTradeNotificationToSellerAsync(TradeMatchNotificationDto notification)
    {
        try
        {
            // دریافت اطلاعات کاربر فروشنده
            var sellerUser = await _usersApiClient.GetUserByIdAsync(notification.SellerUserId);
            
            if (sellerUser == null)
            {
                Console.WriteLine($"کاربر فروشنده با شناسه {notification.SellerUserId} یافت نشد");
                return false;
            }

            // بررسی اینکه آیا کاربر در تلگرام ثبت‌نام کرده یا نه
            if (sellerUser.TelegramId == 0)
            {
                Console.WriteLine($"کاربر فروشنده {sellerUser.PhoneNumber} در تلگرام ثبت‌نام نکرده است");
                return false;
            }

            var sellerMessage = FormatSellerTradeMessage(notification);
            
            // ارسال پیام به تلگرام کاربر فروشنده
            await _botClient.SendMessage(
                chatId: sellerUser.TelegramId,
                text: sellerMessage,
                parseMode: ParseMode.Markdown
            );
            
            Console.WriteLine($"اطلاعیه تطبیق معامله با موفقیت به فروشنده {sellerUser.PhoneNumber} ارسال شد");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"خطا در ارسال اطلاعیه به فروشنده: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// تولید متن پیام اطلاعیه برای کاربر خریدار
    /// </summary>
    /// <param name="notification">اطلاعات معامله</param>
    /// <returns>متن فارسی پیام برای خریدار با فرمت Markdown</returns>
    /// <remarks>
    /// این متد پیام کاربرپسند و مفصل برای خریدار تولید می‌کند شامل:
    /// - ایموجی و فرمت‌بندی مناسب
    /// - اطلاعات کامل معامله (دارایی، قیمت، حجم، زمان)
    /// - وضعیت سفارش (درصد تکمیل و باقی‌مانده)
    /// - شناسه‌های مرجع برای پیگیری
    /// </remarks>
    private string FormatBuyerTradeMessage(TradeMatchNotificationDto notification)
    {
        // The variable was already called "shamsi" and had never been anything of the sort —
        // it formatted a Gregorian date in UTC. Now it is what its name claims.
        var shamsiDateTime = PersianFormat.DateTimeText(notification.TradeDateTime);
        
        return $"🎉 **خرید موفق انجام شد!**\n\n" +
               $"💰 **جزئیات معامله:**\n" +
               $"📊 دارایی: `{notification.Asset}`\n" +
               $"📈 قیمت: `{notification.Price:N0}`\n" +
               $"📦 حجم خریداری شده: `{notification.MatchedVolume:N8}`\n" +
               $"🗓️ تاریخ: `{shamsiDateTime}`\n\n" +
               $"📋 **وضعیت سفارش خرید شما:**\n" +
               $"✅ تکمیل شده: `{notification.BuyOrderCompletionPercentage:F2}%`\n" +
               $"⏳ باقی‌مانده: `{notification.BuyOrderRemainingPercentage:F2}%`\n" +
               $"📊 حجم کل سفارش: `{notification.BuyOrderTotalVolume:N8}`\n" +
               $"📉 حجم باقی‌مانده: `{notification.BuyOrderRemainingVolume:N8}`\n\n" +
               $"🔖 شناسه معامله: `{notification.TradeId}`\n" +
               $"🎯 شناسه سفارش: `{notification.BuyOrderId}`";
    }

    /// <summary>
    /// تولید متن پیام اطلاعیه برای کاربر فروشنده
    /// </summary>
    /// <param name="notification">اطلاعات معامله</param>
    /// <returns>متن فارسی پیام برای فروشنده با فرمت Markdown</returns>
    /// <remarks>
    /// این متد پیام کاربرپسند و مفصل برای فروشنده تولید می‌کند شامل:
    /// - ایموجی و فرمت‌بندی مناسب
    /// - اطلاعات کامل معامله (دارایی، قیمت، حجم، زمان)
    /// - وضعیت سفارش (درصد تکمیل و باقی‌مانده)
    /// - شناسه‌های مرجع برای پیگیری
    /// </remarks>
    private string FormatSellerTradeMessage(TradeMatchNotificationDto notification)
    {
        // The variable was already called "shamsi" and had never been anything of the sort —
        // it formatted a Gregorian date in UTC. Now it is what its name claims.
        var shamsiDateTime = PersianFormat.DateTimeText(notification.TradeDateTime);
        
        return $"💸 **فروش موفق انجام شد!**\n\n" +
               $"💰 **جزئیات معامله:**\n" +
               $"📊 دارایی: `{notification.Asset}`\n" +
               $"📈 قیمت: `{notification.Price:N0}`\n" +
               $"📦 حجم فروخته شده: `{notification.MatchedVolume:N8}`\n" +
               $"🗓️ تاریخ: `{shamsiDateTime}`\n\n" +
               $"📋 **وضعیت سفارش فروش شما:**\n" +
               $"✅ تکمیل شده: `{notification.SellOrderCompletionPercentage:F2}%`\n" +
               $"⏳ باقی‌مانده: `{notification.SellOrderRemainingPercentage:F2}%`\n" +
               $"📊 حجم کل سفارش: `{notification.SellOrderTotalVolume:N8}`\n" +
               $"📉 حجم باقی‌مانده: `{notification.SellOrderRemainingVolume:N8}`\n\n" +
               $"🔖 شناسه معامله: `{notification.TradeId}`\n" +
               $"🎯 شناسه سفارش: `{notification.SellOrderId}`";
    }
}

/// <summary>
/// DTO برای نتیجه عملیات ارسال اطلاعیه تطبیق معامله
/// </summary>
/// <remarks>
/// این کلاس وضعیت ارسال اطلاعیه به هر دو طرف معامله را نگهداری می‌کند
/// و برای بازگشت نتیجه از متدهای اطلاع‌رسانی استفاده می‌شود
/// </remarks>
public class TradeNotificationResult
{
    /// <summary>
    /// شناسه یکتای معامله
    /// </summary>
    public Guid TradeId { get; set; }

    /// <summary>
    /// آیا اطلاعیه به خریدار ارسال شد؟
    /// </summary>
    public bool BuyerNotificationSent { get; set; }

    /// <summary>
    /// آیا اطلاعیه به فروشنده ارسال شد؟
    /// </summary>
    public bool SellerNotificationSent { get; set; }

    /// <summary>
    /// تاریخ و زمان ارسال اطلاعیه
    /// </summary>
    public DateTime NotificationDateTime { get; set; }

    /// <summary>
    /// آیا اطلاعیه با موفقیت کامل ارسال شد؟ (هر دو طرف)
    /// </summary>
    public bool IsFullySuccessful => BuyerNotificationSent && SellerNotificationSent;

    /// <summary>
    /// آیا اطلاعیه حداقل به یکی از طرف‌ها ارسال شد؟
    /// </summary>
    public bool IsPartiallySuccessful => BuyerNotificationSent || SellerNotificationSent;
}
