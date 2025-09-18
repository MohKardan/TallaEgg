# TallaEgg Services Stop Script (PowerShell)
# This script stops all TallaEgg services

param(
    [switch]$Force = $false,
    [switch]$Help = $false
)

if ($Help) {
    Write-Host "TallaEgg Services Stop Script" -ForegroundColor Green
    Write-Host "Usage: .\stop-all-services.ps1 [-Force] [-Help]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Parameters:" -ForegroundColor Cyan
    Write-Host "  -Force: Force kill processes without graceful shutdown" -ForegroundColor White
    Write-Host "  -Help: Show this help message" -ForegroundColor White
    exit 0
}

Write-Host ""
Write-Host "🛑 Stopping TallaEgg Services..." -ForegroundColor Red
Write-Host ""

# Define service processes
$ServiceProcesses = @(
    "Users.Api",
    "Affiliate.Api", 
    "Orders.Api",
    "Wallet.Api",
    "TallaEgg.Api",
    "TallaEgg.TelegramBot.Infrastructure"
)

$StoppedCount = 0
$TotalCount = $ServiceProcesses.Count

foreach ($ProcessName in $ServiceProcesses) {
    Write-Host "🔍 Looking for $ProcessName processes..." -ForegroundColor Cyan
    
    $Processes = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue
    
    if ($Processes) {
        foreach ($Process in $Processes) {
            try {
                if ($Force) {
                    Write-Host "⚡ Force killing $($Process.ProcessName) (PID: $($Process.Id))..." -ForegroundColor Yellow
                    Stop-Process -Id $Process.Id -Force
                } else {
                    Write-Host "🛑 Stopping $($Process.ProcessName) (PID: $($Process.Id))..." -ForegroundColor Yellow
                    Stop-Process -Id $Process.Id
                }
                $StoppedCount++
                Write-Host "✅ $($Process.ProcessName) stopped successfully" -ForegroundColor Green
            }
            catch {
                Write-Host "❌ Failed to stop $($Process.ProcessName): $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    } else {
        Write-Host "ℹ️  No $ProcessName processes found" -ForegroundColor Gray
    }
}

# Also look for dotnet processes that might be running our services
Write-Host ""
Write-Host "🔍 Looking for dotnet processes..." -ForegroundColor Cyan

$DotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
    $_.CommandLine -like "*Users.Api*" -or
    $_.CommandLine -like "*Affiliate.Api*" -or
    $_.CommandLine -like "*Orders.Api*" -or
    $_.CommandLine -like "*Wallet.Api*" -or
    $_.CommandLine -like "*TallaEgg.Api*" -or
    $_.CommandLine -like "*TallaEgg.TelegramBot.Infrastructure*"
}

if ($DotnetProcesses) {
    foreach ($Process in $DotnetProcesses) {
        try {
            if ($Force) {
                Write-Host "⚡ Force killing dotnet process (PID: $($Process.Id))..." -ForegroundColor Yellow
                Stop-Process -Id $Process.Id -Force
            } else {
                Write-Host "🛑 Stopping dotnet process (PID: $($Process.Id))..." -ForegroundColor Yellow
                Stop-Process -Id $Process.Id
            }
            $StoppedCount++
            Write-Host "✅ dotnet process stopped successfully" -ForegroundColor Green
        }
        catch {
            Write-Host "❌ Failed to stop dotnet process: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
} else {
    Write-Host "ℹ️  No relevant dotnet processes found" -ForegroundColor Gray
}

Write-Host ""
if ($StoppedCount -gt 0) {
    Write-Host "✅ Stopped $StoppedCount processes" -ForegroundColor Green
} else {
    Write-Host "ℹ️  No TallaEgg services were running" -ForegroundColor Gray
}

Write-Host ""
Write-Host "💡 Tips:" -ForegroundColor Yellow
Write-Host "  - Use -Force parameter to force kill processes" -ForegroundColor White
Write-Host "  - Check Task Manager to verify all processes are stopped" -ForegroundColor White
Write-Host "  - Use start-all-services.ps1 to start services again" -ForegroundColor White

Write-Host ""
Write-Host "Press any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
