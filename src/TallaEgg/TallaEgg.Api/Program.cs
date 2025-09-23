using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.Application;
using Orders.Core;
using Orders.Infrastructure;
using System.Text.Json;
using TallaEgg.Core.Models;
using Users.Application;
using Users.Core;
using Serilog;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

const string sharedConfigFileName = "appsettings.global.json";
var sharedConfigPath = ResolveSharedConfigPath(builder.Environment, sharedConfigFileName);
builder.Configuration.AddJsonFile(sharedConfigPath, optional: false, reloadOnChange: true);

var applicationName = builder.Environment.ApplicationName;
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

// تنظیم اتصال به دیتابیس SQL Server (در appsettings.json هم می‌توان قرار داد)
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb") ??
        "Server=localhost;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("TallaEgg.Api")));

// تنظیم اتصال به دیتابیس اصلی TallaEgg
// builder.Services.AddDbContext<TallaEggDbContext>(options =>
//     options.UseSqlServer(builder.Configuration.GetConnectionString("TallaEggDb") ??
//         "Server=localhost;Database=TallaEgg;Trusted_Connection=True;TrustServerCertificate=True;",
//         b => b.MigrationsAssembly("TallaEgg.Api")));

// فقط سرویس‌های مربوط به Orders و Price ثبت شوند
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
//builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<CreateOrderCommandHandler>();
builder.Services.AddScoped<CreateTakerOrderCommandHandler>();

// سرویس‌های مربوط به Symbols
// builder.Services.AddScoped<ISymbolRepository, SymbolRepository>();
// builder.Services.AddScoped<ISymbolService, SymbolService>();

// اضافه کردن CORS
builder.Services.AddCors();

// پیکربندی Serilog برای لاگ‌نویسی روی فایل و کنسول
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/tallaegg-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

// تنظیم CORS
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

static string ResolveSharedConfigPath(Microsoft.Extensions.Hosting.IHostEnvironment environment, string fileName)
{
    var current = new System.IO.DirectoryInfo(environment.ContentRootPath);
    try
    {
        while (current is not null)
        {
            var candidate = System.IO.Path.Combine(current.FullName, "config", fileName);
            if (System.IO.File.Exists(candidate))
            {
                return candidate;
            }
            current = current.Parent;
        }

        var errorMsg = $"Shared configuration '{fileName}' not found relative to '{environment.ContentRootPath}'.";
        Log.Error(errorMsg); // Serilog logs to file as configured
        throw new System.IO.FileNotFoundException(errorMsg, fileName);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error resolving shared config path for file {FileName}", fileName);
        throw;
    }
}

app.Run();