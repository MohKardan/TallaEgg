# TallaEgg Services Startup Script (PowerShell)
# This script starts all TallaEgg services

param(
    [switch]$Background = $false,
    [switch]$Help = $false
)

if ($Help) {
    Write-Host "TallaEgg Services Startup Script" -ForegroundColor Green
    Write-Host "Usage: .\start-all-services.ps1 [-Background] [-Help]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Parameters:" -ForegroundColor Cyan
    Write-Host "  -Background: Start services in background (no console windows)" -ForegroundColor White
    Write-Host "  -Help: Show this help message" -ForegroundColor White
    exit 0
}

Write-Host ""
Write-Host "🚀 Starting TallaEgg Services..." -ForegroundColor Green
Write-Host ""

# Get the script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Define services
$Services = @(
    @{
        Name = "Users API"
        Path = "$ScriptDir\APIs\Users"
        Executable = "Users.Api.dll"
        Port = "5136"
    },
    @{
        Name = "Affiliate API"
        Path = "$ScriptDir\APIs\Affiliate"
        Executable = "Affiliate.Api.dll"
        Port = "60812"
    },
    @{
        Name = "Orders API"
        Path = "$ScriptDir\APIs\Orders"
        Executable = "Orders.Api.dll"
        Port = "5140"
    },
    @{
        Name = "Wallet API"
        Path = "$ScriptDir\APIs\Wallet"
        Executable = "Wallet.Api.dll"
        Port = "60933"
    },
    @{
        Name = "TallaEgg API"
        Path = "$ScriptDir\APIs\TallaEgg"
        Executable = "TallaEgg.Api.dll"
        Port = "5135"
    },
    @{
        Name = "Telegram Bot"
        Path = "$ScriptDir\TelegramBot"
        Executable = "TallaEgg.TelegramBot.Infrastructure.dll"
        Port = "Background"
    }
)

# Start each service
foreach ($Service in $Services) {
    Write-Host "📦 Starting $($Service.Name)..." -ForegroundColor Cyan
    
    if (-not (Test-Path $Service.Path)) {
        Write-Host "⚠️  Service path not found: $($Service.Path)" -ForegroundColor Yellow
        continue
    }
    
    if (-not (Test-Path "$($Service.Path)\$($Service.Executable)")) {
        Write-Host "⚠️  Executable not found: $($Service.Path)\$($Service.Executable)" -ForegroundColor Yellow
        continue
    }
    
    try {
        if ($Background) {
            # Start in background
            Start-Process -FilePath "dotnet" -ArgumentList $Service.Executable -WorkingDirectory $Service.Path -WindowStyle Hidden
            Write-Host "✅ $($Service.Name) started in background" -ForegroundColor Green
        } else {
            # Start with console window
            Start-Process -FilePath "dotnet" -ArgumentList $Service.Executable -WorkingDirectory $Service.Path
            Write-Host "✅ $($Service.Name) started" -ForegroundColor Green
        }
        
        # Wait a bit between services
        Start-Sleep -Seconds 2
    }
    catch {
        Write-Host "❌ Failed to start $($Service.Name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "✅ All services started!" -ForegroundColor Green
Write-Host "📊 Service Status:" -ForegroundColor Cyan

foreach ($Service in $Services) {
    Write-Host "  $($Service.Name): Running on port $($Service.Port)" -ForegroundColor White
}

Write-Host ""
Write-Host "🌐 API Endpoints:" -ForegroundColor Cyan
Write-Host "  Users API: http://localhost:5136" -ForegroundColor White
Write-Host "  Affiliate API: http://localhost:60812" -ForegroundColor White
Write-Host "  Orders API: http://localhost:5140" -ForegroundColor White
Write-Host "  Wallet API: http://localhost:60933" -ForegroundColor White
Write-Host "  TallaEgg API: http://localhost:5135" -ForegroundColor White
Write-Host "  Telegram Bot: Running in background" -ForegroundColor White

Write-Host ""
Write-Host "💡 Tips:" -ForegroundColor Yellow
Write-Host "  - Use -Background parameter to start services without console windows" -ForegroundColor White
Write-Host "  - Check Task Manager to see running processes" -ForegroundColor White
Write-Host "  - Use stop-all-services.ps1 to stop all services" -ForegroundColor White

if (-not $Background) {
    Write-Host ""
    Write-Host "Press any key to continue..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
