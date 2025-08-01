using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using TallaEgg.TelegramBot.Application.Services;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Handlers;
using TallaEgg.TelegramBot.Infrastructure.Repositories;
using TallaEgg.TelegramBot.Infrastructure.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Configuration
        var configuration = context.Configuration;
        
        // Telegram Bot Client
        var botToken = configuration["TelegramBotToken"];
        if (string.IsNullOrEmpty(botToken) || botToken == "YOUR_BOT_TOKEN_HERE")
        {
            Console.WriteLine("⚠️  Warning: TelegramBotToken is not configured. Bot will not function properly.");
            Console.WriteLine("   Please set a valid bot token in appsettings.json");
            // Use a dummy token for development
            services.AddSingleton<ITelegramBotClient>(provider => new TelegramBotClient("dummy_token"));
        }
        else
        {
            services.AddSingleton<ITelegramBotClient>(provider => new TelegramBotClient(botToken));
        }
        
        // HTTP Client
        services.AddHttpClient();
        
        // API Clients
        services.AddScoped<IUsersApiClient, UsersApiClient>();
        services.AddScoped<IPriceApiClient, PriceApiClient>();
        services.AddScoped<IOrderApiClient, OrderApiClient>();
        
        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPriceRepository, PriceRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        
        // Services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPriceService, PriceService>();
        services.AddScoped<IOrderService, OrderService>();
        
        // Bot Handler
        services.AddScoped<IBotHandler, BotHandler>();
        
        // Background Service
        services.AddHostedService<TelegramBotService>();
    })
    .Build();

await host.RunAsync(); 