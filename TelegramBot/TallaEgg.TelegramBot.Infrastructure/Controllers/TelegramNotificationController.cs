using Microsoft.AspNetCore.Mvc;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using Telegram.Bot;

namespace TallaEgg.TelegramBot.Infrastructure.Controllers;

/// <summary>
/// کنترلر برای مدیریت اطلاع‌رسانی‌های مربوط به معاملات
/// </summary>
[ApiController]
[Route("api/telegram/notifications")]
public class TelegramNotificationController : ControllerBase
{
    private readonly ITelegramBotClient _botClient;
    private readonly UsersApiClient _usersApiClient;

    public TelegramNotificationController(ITelegramBotClient botClient, UsersApiClient usersApiClient)
    {
        _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
        _usersApiClient = usersApiClient ?? throw new ArgumentNullException(nameof(usersApiClient));
    }

    /// <summary>
    /// ارسال اطلاعیه تطبیق معامله به کاربران مربوطه
    /// </summary>
    /// <param name="notification">اطلاعات کامل معامله تطبیق یافته</param>
    /// <returns>نتیجه عملیات ارسال اطلاعیه</returns>
    /// <remarks>
    /// این endpoint توسط سرویس تطبیق معاملات فراخوانی می‌شود
    /// و پیام‌های مربوط به موفقیت تطبیق سفارشات را به خریدار و فروشنده ارسال می‌کند.
    /// 
    /// اطلاعات ارسالی شامل:
    /// - جزئیات کامل معامله (حجم، قیمت، زمان)
    /// - درصد تکمیل و باقی‌مانده هر سفارش
    /// - اطلاعات مربوط به دارایی معامله شده
    /// </remarks>
    /// <response code="200">اطلاعیه با موفقیت ارسال شد</response>
    /// <response code="400">خطا در اعتبارسنجی داده‌های ورودی</response>
    /// <response code="500">خطا در ارسال اطلاعیه</response>
    [HttpPost("trade-match")]
    public async Task<IActionResult> NotifyTradeMatch([FromBody] TradeMatchNotificationDto notification)
    {
        try
        {
            // TODO: اعتبارسنجی ورودی
            if (notification == null)
            {
                return BadRequest(ApiResponse<object>.Fail("اطلاعات اطلاعیه نمی‌تواند خالی باشد"));
            }

            // TODO: بررسی صحت شناسه‌های کاربران
            // نیاز است تا از UsersApiClient برای بررسی وجود کاربران استفاده شود

            // ارسال اطلاعیه به خریدار
            var buyerNotificationSent = await SendTradeNotificationToBuyer(notification);
            
            // ارسال اطلاعیه به فروشنده
            var sellerNotificationSent = await SendTradeNotificationToSeller(notification);

            var result = new TradeNotificationResult
            {
                TradeId = notification.TradeId,
                BuyerNotificationSent = buyerNotificationSent,
                SellerNotificationSent = sellerNotificationSent,
                NotificationDateTime = DateTime.UtcNow
            };

            if (buyerNotificationSent && sellerNotificationSent)
            {
                return Ok(ApiResponse<TradeNotificationResult>.Ok(result, "اطلاعیه تطبیق معامله با موفقیت به هر دو طرف ارسال شد"));
            }
            else if (buyerNotificationSent || sellerNotificationSent)
            {
                return Ok(ApiResponse<TradeNotificationResult>.Ok(result, "اطلاعیه فقط به یکی از طرفین ارسال شد"));
            }
            else
            {
                return Ok(ApiResponse<TradeNotificationResult>.Fail("ارسال اطلاعیه به هیچ یک از طرفین موفق نبود"));
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail($"خطای داخلی سرور: {ex.Message}"));
        }
    }

    /// <summary>
    /// ارسال اطلاعیه تطبیق معامله به کاربر خریدار
    /// </summary>
    /// <param name="notification">اطلاعات معامله</param>
    /// <returns>true اگر ارسال موفق باشد، در غیر این صورت false</returns>
    /// <remarks>
    /// این متد پیام مخصوص خریدار را تولید و ارسال می‌کند شامل:
    /// - اطلاعات خرید انجام شده
    /// - وضعیت تکمیل سفارش خرید
    /// - مبلغ و حجم دریافتی
    /// </remarks>
    private async Task<bool> SendTradeNotificationToBuyer(TradeMatchNotificationDto notification)
    {
        try
        {
            // TODO: دریافت اطلاعات کاربر خریدار از UsersApiClient
            // var buyerUser = await _usersApiClient.GetUserAsync(notification.BuyerUserId);
            
            // TODO: بررسی اینکه آیا کاربر در تلگرام ثبت‌نام کرده یا نه
            // اگر TelegramUserId نداشته باشد، اطلاعیه ارسال نمی‌شود

            var buyerMessage = FormatBuyerTradeMessage(notification);
            
            // TODO: ارسال پیام به تلگرام کاربر خریدار
            // await _botClient.SendMessage(buyerUser.TelegramUserId, buyerMessage);
            
            // فعلاً برای تست true برمی‌گردانیم
            // بعداً که UsersApiClient پیاده‌سازی شد، این قسمت تکمیل می‌شود
            
            return true; // موقت
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
    /// این متد پیام مخصوص فروشنده را تولید و ارسال می‌کند شامل:
    /// - اطلاعات فروش انجام شده
    /// - وضعیت تکمیل سفارش فروش
    /// - مبلغ و حجم فروخته شده
    /// </remarks>
    private async Task<bool> SendTradeNotificationToSeller(TradeMatchNotificationDto notification)
    {
        try
        {
            // TODO: دریافت اطلاعات کاربر فروشنده از UsersApiClient
            // var sellerUser = await _usersApiClient.GetUserAsync(notification.SellerUserId);
            
            // TODO: بررسی اینکه آیا کاربر در تلگرام ثبت‌نام کرده یا نه
            
            var sellerMessage = FormatSellerTradeMessage(notification);
            
            // TODO: ارسال پیام به تلگرام کاربر فروشنده
            // await _botClient.SendMessage(sellerUser.TelegramUserId, sellerMessage);
            
            return true; // موقت
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
    /// <returns>متن فارسی پیام برای خریدار</returns>
    /// <remarks>
    /// این متد پیام کاربرپسند و مفصل برای خریدار تولید می‌کند
    /// شامل ایموجی، فرمت‌بندی مناسب و اطلاعات کامل معامله
    /// </remarks>
    private string FormatBuyerTradeMessage(TradeMatchNotificationDto notification)
    {
        var shamsiDateTime = notification.TradeDateTime.ToString("yyyy/MM/dd HH:mm:ss");
        
        return $"🎉 **خرید موفق انجام شد!**\n\n" +
               $"💰 **جزئیات معامله:**\n" +
               $"📊 دارایی: {notification.Asset}\n" +
               $"📈 قیمت: {notification.Price:N0}\n" +
               $"📦 حجم خریداری شده: {notification.MatchedVolume:N8}\n" +
               $"🗓️ تاریخ: {shamsiDateTime}\n\n" +
               $"📋 **وضعیت سفارش خرید شما:**\n" +
               $"✅ تکمیل شده: {notification.BuyOrderCompletionPercentage:F2}%\n" +
               $"⏳ باقی‌مانده: {notification.BuyOrderRemainingPercentage:F2}%\n" +
               $"📊 حجم کل سفارش: {notification.BuyOrderTotalVolume:N8}\n" +
               $"📉 حجم باقی‌مانده: {notification.BuyOrderRemainingVolume:N8}\n\n" +
               $"🔖 شناسه معامله: `{notification.TradeId}`\n" +
               $"🎯 شناسه سفارش: `{notification.BuyOrderId}`";
    }

    /// <summary>
    /// تولید متن پیام اطلاعیه برای کاربر فروشنده
    /// </summary>
    /// <param name="notification">اطلاعات معامله</param>
    /// <returns>متن فارسی پیام برای فروشنده</returns>
    /// <remarks>
    /// این متد پیام کاربرپسند و مفصل برای فروشنده تولید می‌کند
    /// شامل ایموجی، فرمت‌بندی مناسب و اطلاعات کامل معامله
    /// </remarks>
    private string FormatSellerTradeMessage(TradeMatchNotificationDto notification)
    {
        var shamsiDateTime = notification.TradeDateTime.ToString("yyyy/MM/dd HH:mm:ss");
        
        return $"💸 **فروش موفق انجام شد!**\n\n" +
               $"💰 **جزئیات معامله:**\n" +
               $"📊 دارایی: {notification.Asset}\n" +
               $"📈 قیمت: {notification.Price:N0}\n" +
               $"📦 حجم فروخته شده: {notification.MatchedVolume:N8}\n" +
               $"🗓️ تاریخ: {shamsiDateTime}\n\n" +
               $"📋 **وضعیت سفارش فروش شما:**\n" +
               $"✅ تکمیل شده: {notification.SellOrderCompletionPercentage:F2}%\n" +
               $"⏳ باقی‌مانده: {notification.SellOrderRemainingPercentage:F2}%\n" +
               $"📊 حجم کل سفارش: {notification.SellOrderTotalVolume:N8}\n" +
               $"📉 حجم باقی‌مانده: {notification.SellOrderRemainingVolume:N8}\n\n" +
               $"🔖 شناسه معامله: `{notification.TradeId}`\n" +
               $"🎯 شناسه سفارش: `{notification.SellOrderId}`";
    }
}

/// <summary>
/// DTO برای نتیجه عملیات ارسال اطلاعیه تطبیق معامله
/// </summary>
/// <remarks>
/// این کلاس وضعیت ارسال اطلاعیه به هر دو طرف معامله را نگهداری می‌کند
/// </remarks>
public class TradeNotificationResult
{
    /// <summary>
    /// شناسه معامله
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
}
