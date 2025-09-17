using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Services;
using Telegram.Bot;
using System.IO;

namespace TallaEgg.TelegramBot.Infrastructure;

/// <summary>
/// Minimal API برای دریافت اطلاعیه‌های تطبیق معاملات
/// </summary>
/// <remarks>
/// این برنامه endpoint هایی برای دریافت اطلاعیه‌ها از سرویس تطبیق معاملات فراهم می‌کند
/// و آنها را به کاربران مربوطه در تلگرام ارسال می‌کند
/// </remarks>
public class TelegramNotificationApi
{

        private static string ResolveSharedConfigPath(Microsoft.Extensions.Hosting.IHostEnvironment environment, string fileName)
        {
            var current = new DirectoryInfo(environment.ContentRootPath);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "config", fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                current = current.Parent;
            }

            throw new FileNotFoundException($"Shared configuration '{fileName}' not found relative to '{environment.ContentRootPath}'.", fileName);
        }


    /// <summary>
    /// راه‌اندازی و اجرای Minimal API برای اطلاعیه‌های تلگرام
    /// </summary>
    /// <param name="args">آرگومان‌های خط فرمان</param>
    /// <remarks>
    /// این متد:
    /// 1. سرویس‌های مورد نیاز را پیکربندی می‌کند
    /// 2. endpoint های API را تعریف می‌کند
    /// 3. برنامه را اجرا می‌کند
    /// </remarks>
    public static void RunNotificationApi(string[] args)
    {        var builder = WebApplication.CreateBuilder(args);


        const string sharedConfigFileName = "appsettings.global.json";
        var sharedConfigPath = ResolveSharedConfigPath(builder.Environment, sharedConfigFileName);
        builder.Configuration.AddJsonFile(sharedConfigPath, optional: false, reloadOnChange: true);

        var applicationName = builder.Environment.ApplicationName ?? "TallaEgg.TelegramBot.Infrastructure";
        var serviceSection = builder.Configuration.GetSection($"Services:{applicationName}");
        if (!serviceSection.Exists())
        {
            throw new InvalidOperationException($"Missing configuration section 'Services:{applicationName}' in {sharedConfigFileName}.");
        }

        var prefix = $"Services:{applicationName}:";
        var flattened = serviceSection.AsEnumerable(true)
            .Where(pair => pair.Value is not null)
            .Select(pair => new KeyValuePair<string, string>(
                pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? pair.Key[prefix.Length..]
                    : pair.Key,
                pair.Value!))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        builder.Configuration.AddInMemoryCollection(flattened);

        var urls = serviceSection.GetSection("Urls").Get<string[]>();
        if (urls is { Length: > 0 })
        {
            builder.WebHost.UseUrls(urls);
        }

        // اضافه کردن سرویس‌های مورد نیاز
        builder.Services.AddHttpClient();

        // خواندن تنظیمات
        var configuration = builder.Configuration;
        var botToken = configuration["TelegramBotToken"] ?? Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var usersApiUrl = configuration["UsersApiUrl"];

        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new InvalidOperationException($"TelegramBotToken is not configured in {sharedConfigFileName} or environment variable TELEGRAM_BOT_TOKEN.");
        }

        if (string.IsNullOrWhiteSpace(usersApiUrl))
        {
            throw new InvalidOperationException($"UsersApiUrl is not configured in {sharedConfigFileName}.");
        }

        builder.Services.AddSingleton<ITelegramBotClient>(provider => 
        {
            return new TelegramBotClient(botToken);
        });

        builder.Services.AddSingleton<UsersApiClient>(provider =>
        {
            var httpClient = provider.GetRequiredService<HttpClient>();
            var logger = provider.GetRequiredService<ILogger<UsersApiClient>>();
            return new UsersApiClient(httpClient, configuration, logger);
        });

        builder.Services.AddSingleton<TradeNotificationService>();

        var app = builder.Build();

        // تعریف endpoint برای دریافت اطلاعیه تطبیق معامله
        app.MapPost("/api/telegram/notifications/trade-match", 
            /// <summary>
            /// دریافت اطلاعیه تطبیق معامله و ارسال به کاربران
            /// </summary>
            /// <param name="notification">اطلاعات کامل معامله تطبیق یافته</param>
            /// <param name="notificationService">سرویس اطلاع‌رسانی</param>
            /// <returns>نتیجه عملیات ارسال اطلاعیه</returns>
            /// <remarks>
            /// این endpoint توسط سرویس تطبیق معاملات فراخوانی می‌شود و شامل:
            /// - اعتبارسنجی داده‌های ورودی
            /// - ارسال اطلاعیه به خریدار و فروشنده
            /// - برگشت نتیجه عملیات با فرمت ApiResponse
            /// 
            /// TODO: اضافه کردن authentication و authorization
            /// TODO: اضافه کردن rate limiting
            /// TODO: اضافه کردن logging مفصل
            /// </remarks>
            async (TradeMatchNotificationDto notification, TradeNotificationService notificationService) =>
        {
            try
            {
        // اعتبارسنجی ورودی
                if (notification == null)
                {
                    return Results.BadRequest(ApiResponse<object>.Fail("اطلاعات اطلاعیه نمی‌تواند خالی باشد"));
                }

                if (notification.BuyerUserId == Guid.Empty || notification.SellerUserId == Guid.Empty)
                {
                    return Results.BadRequest(ApiResponse<object>.Fail("شناسه کاربران نامعتبر است"));
                }

                if (notification.MatchedVolume <= 0 || notification.Price <= 0)
                {
                    return Results.BadRequest(ApiResponse<object>.Fail("حجم یا قیمت معامله نامعتبر است"));
                }

                if (string.IsNullOrEmpty(notification.Asset))
                {
                    return Results.BadRequest(ApiResponse<object>.Fail("نماد دارایی نمی‌تواند خالی باشد"));
                }

        // ارسال اطلاعیه‌ها
                var result = await notificationService.SendTradeMatchNotificationAsync(notification);

        // تعیین نوع پاسخ بر اساس نتیجه
                if (result.IsFullySuccessful)
                {
                    return Results.Ok(ApiResponse<TradeNotificationResult>.Ok(result, 
                        "اطلاعیه تطبیق معامله با موفقیت به هر دو طرف ارسال شد"));
                }
                else if (result.IsPartiallySuccessful)
                {
                    return Results.Ok(ApiResponse<TradeNotificationResult>.Ok(result, 
                        "اطلاعیه فقط به یکی از طرفین ارسال شد"));
                }
                else
                {
                    return Results.Ok(ApiResponse<object>.Fail(
                        "ارسال اطلاعیه به هیچ یک از طرفین موفق نبود"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در پردازش اطلاعیه تطبیق معامله: {ex.Message}");
                return Results.Json(ApiResponse<object>.Fail($"خطای داخلی سرور: {ex.Message}"), 
                    statusCode: 500);
            }
        })
        .WithName("NotifyTradeMatch")
        .WithSummary("ارسال اطلاعیه تطبیق معامله")
        .WithDescription("این endpoint توسط سرویس تطبیق معاملات برای اطلاع‌رسانی به کاربران فراخوانی می‌شود");

        // تعریف endpoint سلامت برای بررسی وضعیت سرویس
        app.MapGet("/health", 
            /// <summary>
            /// بررسی وضعیت سلامت سرویس اطلاع‌رسانی
            /// </summary>
            /// <returns>وضعیت سلامت سرویس</returns>
            () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
        .WithName("HealthCheck")
        .WithSummary("بررسی سلامت سرویس");

        Console.WriteLine("🚀 Telegram Notification API در حال راه‌اندازی...");
        Console.WriteLine($"🌐 Base URL: http://localhost:5000");
        Console.WriteLine("📡 Endpoints موجود:");
        Console.WriteLine("   POST /api/telegram/notifications/trade-match - دریافت اطلاعیه تطبیق معامله");
        Console.WriteLine("   GET  /health - بررسی سلامت سرویس");

        app.Run();
    }
}



