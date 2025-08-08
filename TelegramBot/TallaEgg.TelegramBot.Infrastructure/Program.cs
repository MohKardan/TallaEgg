using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TallaEgg.TelegramBot;
using TallaEgg.TelegramBot.Application.Services;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Infrastructure.Clients;
//using TallaEgg.TelegramBot.Infrastructure.Handlers;
using TallaEgg.TelegramBot.Infrastructure.Repositories;
using TallaEgg.TelegramBot.Infrastructure.Services;
using Telegram.Bot;
//using TallaEgg.TelegramBot;

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
        
        // API Clients - Interface registrations
        services.AddScoped<IUsersApiClient, TallaEgg.TelegramBot.Infrastructure.Clients.UsersApiClient>();
        services.AddScoped<IPriceApiClient, TallaEgg.TelegramBot.Infrastructure.Clients.PriceApiClient>();
        services.AddScoped<IOrderApiClient, TallaEgg.TelegramBot.Infrastructure.Clients.OrderApiClient>();
        
        // API Clients - Concrete class registrations for BotHandler
        services.AddScoped<TallaEgg.TelegramBot.OrderApiClient>(provider => 
            new TallaEgg.TelegramBot.OrderApiClient(configuration["OrderApiUrl"] ?? "http://localhost:5000"));
        services.AddScoped<TallaEgg.TelegramBot.UsersApiClient>(provider => 
            new TallaEgg.TelegramBot.UsersApiClient(configuration["UsersApiUrl"] ?? "http://localhost:5001"));
        services.AddScoped<TallaEgg.TelegramBot.AffiliateApiClient>(provider => 
            new TallaEgg.TelegramBot.AffiliateApiClient(configuration["AffiliateApiUrl"] ?? "http://localhost:5002", 
                provider.GetRequiredService<HttpClient>()));
        services.AddScoped<TallaEgg.TelegramBot.PriceApiClient>(provider => 
            new TallaEgg.TelegramBot.PriceApiClient(configuration["PricesApiUrl"] ?? "http://localhost:5003"));
        services.AddScoped<TallaEgg.TelegramBot.WalletApiClient>(provider => 
            new TallaEgg.TelegramBot.WalletApiClient(configuration["WalletApiUrl"] ?? "http://localhost:5004"));
        
        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPriceRepository, PriceRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        
        // Services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPriceService, PriceService>();
        services.AddScoped<IOrderService, OrderService>();
        
        // Bot Handler
        services.AddScoped<IBotHandler, TallaEgg.TelegramBot.BotHandler>();
        
        // Background Service
        services.AddHostedService<TelegramBotService>();
        
    })
    .Build();

await host.RunAsync(); 