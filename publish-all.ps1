# TallaEgg Publishing Script
# This script publishes all projects to the publishes folder

param(
    [string]$Configuration = "Release",
    [switch]$Clean = $false,
    [switch]$Help = $false
)

if ($Help) {
    Write-Host "TallaEgg Publishing Script" -ForegroundColor Green
    Write-Host "Usage: .\publish-all.ps1 [-Configuration Release|Debug] [-Clean] [-Help]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Parameters:" -ForegroundColor Cyan
    Write-Host "  -Configuration: Build configuration (Release or Debug). Default: Release" -ForegroundColor White
    Write-Host "  -Clean: Clean solution before publishing. Default: false" -ForegroundColor White
    Write-Host "  -Help: Show this help message" -ForegroundColor White
    exit 0
}

Write-Host "🚀 Starting TallaEgg Publishing Process..." -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow

# Set error action preference
$ErrorActionPreference = "Stop"

# Get the script directory (solution root)
$SolutionRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $SolutionRoot

# Clean solution if requested
if ($Clean) {
    Write-Host "🧹 Cleaning solution..." -ForegroundColor Blue
    dotnet clean --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Clean failed!" -ForegroundColor Red
        exit 1
    }
}

# Build solution first
Write-Host "🔨 Building solution..." -ForegroundColor Blue
dotnet build --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

# Define projects to publish
$Projects = @(
    @{
        Name = "Users API"
        Path = "src\User\Users.Api\Users.Api.csproj"
        OutputPath = "publishes\APIs\Users"
    },
    @{
        Name = "Affiliate API"
        Path = "src\Affiliate\Affiliate.Api\Affiliate.Api.csproj"
        OutputPath = "publishes\APIs\Affiliate"
    },
    @{
        Name = "Orders API"
        Path = "src\Order\Orders.Api\Orders.Api.csproj"
        OutputPath = "publishes\APIs\Orders"
    },
    @{
        Name = "Wallet API"
        Path = "src\Wallet\Wallet.Api\Wallet.Api.csproj"
        OutputPath = "publishes\APIs\Wallet"
    },
    @{
        Name = "TallaEgg API"
        Path = "src\TallaEgg\TallaEgg.Api\TallaEgg.Api.csproj"
        OutputPath = "publishes\APIs\TallaEgg"
    },
    @{
        Name = "Telegram Bot"
        Path = "TelegramBot\TallaEgg.TelegramBot.Infrastructure\TallaEgg.TelegramBot.Infrastructure.csproj"
        OutputPath = "publishes\TelegramBot"
    }
)

# Publish each project
foreach ($Project in $Projects) {
    Write-Host ""
    Write-Host "📦 Publishing $($Project.Name)..." -ForegroundColor Cyan
    
    # Check if project file exists
    if (-not (Test-Path $Project.Path)) {
        Write-Host "⚠️  Project file not found: $($Project.Path)" -ForegroundColor Yellow
        continue
    }
    
    # Create output directory if it doesn't exist
    if (-not (Test-Path $Project.OutputPath)) {
        New-Item -ItemType Directory -Path $Project.OutputPath -Force | Out-Null
    }
    
    # Publish the project
    try {
        dotnet publish $Project.Path `
            --configuration $Configuration `
            --output $Project.OutputPath `
            --self-contained false `
            --no-build `
            --verbosity minimal
            
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ $($Project.Name) published successfully!" -ForegroundColor Green
        } else {
            Write-Host "❌ Failed to publish $($Project.Name)" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "❌ Error publishing $($Project.Name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Copy configuration files
Write-Host ""
Write-Host "📋 Copying configuration files..." -ForegroundColor Blue

# Copy global configuration to each API
$ConfigSource = "config\appsettings.global.json"
if (Test-Path $ConfigSource) {
    $ApiFolders = @(
        "publishes\APIs\Users",
        "publishes\APIs\Affiliate", 
        "publishes\APIs\Orders",
        "publishes\APIs\Wallet",
        "publishes\APIs\TallaEgg",
        "publishes\TelegramBot"
    )
    
    foreach ($Folder in $ApiFolders) {
        if (Test-Path $Folder) {
            Copy-Item $ConfigSource "$Folder\appsettings.global.json" -Force
            Write-Host "  ✅ Copied config to $Folder" -ForegroundColor Green
        }
    }
} else {
    Write-Host "⚠️  Configuration file not found: $ConfigSource" -ForegroundColor Yellow
}

# Create deployment info file
$DeploymentInfo = @{
    PublishedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Configuration = $Configuration
    Projects = $Projects | ForEach-Object { @{
        Name = $_.Name
        Path = $_.Path
        OutputPath = $_.OutputPath
        Published = Test-Path $_.OutputPath
    }}
}

$DeploymentInfo | ConvertTo-Json -Depth 3 | Out-File "publishes\deployment-info.json" -Encoding UTF8

Write-Host ""
Write-Host "🎉 Publishing completed!" -ForegroundColor Green
Write-Host "📁 Published files are in the 'publishes' folder" -ForegroundColor Yellow
Write-Host "📄 Deployment info saved to 'publishes\deployment-info.json'" -ForegroundColor Yellow

# Show summary
Write-Host ""
Write-Host "📊 Publishing Summary:" -ForegroundColor Cyan
foreach ($Project in $Projects) {
    $Status = if (Test-Path $Project.OutputPath) { "✅ Published" } else { "❌ Failed" }
    Write-Host "  $($Project.Name): $Status" -ForegroundColor White
}
