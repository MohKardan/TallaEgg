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
/// Minimal API that receives trade-match notifications.
/// </summary>
/// <remarks>
/// It exposes endpoints for the matching service to post notifications to, and forwards them to the
/// relevant users on Telegram.
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
    /// Configures and runs the notification Minimal API.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <remarks>
    /// Registers the required services, defines the API endpoints, and runs the application.
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

        // Register the required services.
        builder.Services.AddHttpClient();

        // Read configuration.
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

        // The endpoint that receives a trade-match notification.
        app.MapPost("/api/telegram/notifications/trade-match", 
            /// <summary>
            /// Receives a trade-match notification and sends it on to the users.
            /// </summary>
            /// <param name="notification">The matched trade in full.</param>
            /// <param name="notificationService">Notification service.</param>
            /// <returns>Whether the notifications were sent.</returns>
            /// <remarks>
            /// Called by the matching service. It validates the input, notifies both the buyer and
            /// the seller, and returns the outcome as an ApiResponse.
            /// 
            /// TODO: add authentication and authorization.
            /// TODO: add rate limiting.
            /// TODO: add detailed logging.
            /// </remarks>
            async (TradeMatchNotificationDto notification, TradeNotificationService notificationService) =>
        {
            try
            {
        // Validate the input.
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

        // Send the notifications.
                var result = await notificationService.SendTradeMatchNotificationAsync(notification);

        // Choose the response based on the outcome.
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

        // Health endpoint.
        app.MapGet("/health", 
            /// <summary>
            /// Reports the notification service's health.
            /// </summary>
            /// <returns>The service's health status.</returns>
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



