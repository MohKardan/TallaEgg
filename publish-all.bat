@echo off
REM TallaEgg Publishing Script (Batch Version)
REM This script publishes all projects to the publishes folder

setlocal enabledelayedexpansion

REM Default configuration
set CONFIGURATION=Release
set CLEAN=false

REM Parse command line arguments
:parse_args
if "%1"=="" goto :start_publishing
if "%1"=="-Configuration" (
    set CONFIGURATION=%2
    shift
    shift
    goto :parse_args
)
if "%1"=="-Clean" (
    set CLEAN=true
    shift
    goto :parse_args
)
if "%1"=="-Help" (
    echo TallaEgg Publishing Script (Batch Version)
    echo Usage: publish-all.bat [-Configuration Release^|Debug] [-Clean] [-Help]
    echo.
    echo Parameters:
    echo   -Configuration: Build configuration (Release or Debug). Default: Release
    echo   -Clean: Clean solution before publishing. Default: false
    echo   -Help: Show this help message
    exit /b 0
)
shift
goto :parse_args

:start_publishing
echo.
echo 🚀 Starting TallaEgg Publishing Process...
echo Configuration: %CONFIGURATION%

REM Clean solution if requested
if "%CLEAN%"=="true" (
    echo 🧹 Cleaning solution...
    dotnet clean --configuration %CONFIGURATION%
    if errorlevel 1 (
        echo ❌ Clean failed!
        exit /b 1
    )
)

REM Build solution first
echo 🔨 Building solution...
dotnet build --configuration %CONFIGURATION% --no-restore
if errorlevel 1 (
    echo ❌ Build failed!
    exit /b 1
)

REM Publish Users API
echo.
echo 📦 Publishing Users API...
if exist "src\User\Users.Api\Users.Api.csproj" (
    dotnet publish "src\User\Users.Api\Users.Api.csproj" --configuration %CONFIGURATION% --output "publishes\APIs\Users" --self-contained false --no-build --verbosity minimal
    if errorlevel 1 (
        echo ❌ Failed to publish Users API
    ) else (
        echo ✅ Users API published successfully!
    )
) else (
    echo ⚠️  Project file not found: src\User\Users.Api\Users.Api.csproj
)

REM Publish Affiliate API
echo.
echo 📦 Publishing Affiliate API...
if exist "src\Affiliate\Affiliate.Api\Affiliate.Api.csproj" (
    dotnet publish "src\Affiliate\Affiliate.Api\Affiliate.Api.csproj" --configuration %CONFIGURATION% --output "publishes\APIs\Affiliate" --self-contained false --no-build --verbosity minimal
    if errorlevel 1 (
        echo ❌ Failed to publish Affiliate API
    ) else (
        echo ✅ Affiliate API published successfully!
    )
) else (
    echo ⚠️  Project file not found: src\Affiliate\Affiliate.Api\Affiliate.Api.csproj
)

REM Publish Orders API
echo.
echo 📦 Publishing Orders API...
if exist "src\Order\Orders.Api\Orders.Api.csproj" (
    dotnet publish "src\Order\Orders.Api\Orders.Api.csproj" --configuration %CONFIGURATION% --output "publishes\APIs\Orders" --self-contained false --no-build --verbosity minimal
    if errorlevel 1 (
        echo ❌ Failed to publish Orders API
    ) else (
        echo ✅ Orders API published successfully!
    )
) else (
    echo ⚠️  Project file not found: src\Order\Orders.Api\Orders.Api.csproj
)

REM Publish Wallet API
echo.
echo 📦 Publishing Wallet API...
if exist "src\Wallet\Wallet.Api\Wallet.Api.csproj" (
    dotnet publish "src\Wallet\Wallet.Api\Wallet.Api.csproj" --configuration %CONFIGURATION% --output "publishes\APIs\Wallet" --self-contained false --no-build --verbosity minimal
    if errorlevel 1 (
        echo ❌ Failed to publish Wallet API
    ) else (
        echo ✅ Wallet API published successfully!
    )
) else (
    echo ⚠️  Project file not found: src\Wallet\Wallet.Api\Wallet.Api.csproj
)

REM Publish TallaEgg API
echo.
echo 📦 Publishing TallaEgg API...
if exist "src\TallaEgg\TallaEgg.Api\TallaEgg.Api.csproj" (
    dotnet publish "src\TallaEgg\TallaEgg.Api\TallaEgg.Api.csproj" --configuration %CONFIGURATION% --output "publishes\APIs\TallaEgg" --self-contained false --no-build --verbosity minimal
    if errorlevel 1 (
        echo ❌ Failed to publish TallaEgg API
    ) else (
        echo ✅ TallaEgg API published successfully!
    )
) else (
    echo ⚠️  Project file not found: src\TallaEgg\TallaEgg.Api\TallaEgg.Api.csproj
)

REM Publish Telegram Bot
echo.
echo 📦 Publishing Telegram Bot...
if exist "TelegramBot\TallaEgg.TelegramBot.Infrastructure\TallaEgg.TelegramBot.Infrastructure.csproj" (
    dotnet publish "TelegramBot\TallaEgg.TelegramBot.Infrastructure\TallaEgg.TelegramBot.Infrastructure.csproj" --configuration %CONFIGURATION% --output "publishes\TelegramBot" --self-contained false --no-build --verbosity minimal
    if errorlevel 1 (
        echo ❌ Failed to publish Telegram Bot
    ) else (
        echo ✅ Telegram Bot published successfully!
    )
) else (
    echo ⚠️  Project file not found: TelegramBot\TallaEgg.TelegramBot.Infrastructure\TallaEgg.TelegramBot.Infrastructure.csproj
)

REM Copy configuration files
echo.
echo 📋 Copying configuration files...
if exist "config\appsettings.global.json" (
    copy "config\appsettings.global.json" "publishes\APIs\Users\appsettings.global.json" >nul 2>&1
    copy "config\appsettings.global.json" "publishes\APIs\Affiliate\appsettings.global.json" >nul 2>&1
    copy "config\appsettings.global.json" "publishes\APIs\Orders\appsettings.global.json" >nul 2>&1
    copy "config\appsettings.global.json" "publishes\APIs\Wallet\appsettings.global.json" >nul 2>&1
    copy "config\appsettings.global.json" "publishes\APIs\TallaEgg\appsettings.global.json" >nul 2>&1
    copy "config\appsettings.global.json" "publishes\TelegramBot\appsettings.global.json" >nul 2>&1
    echo ✅ Configuration files copied successfully!
) else (
    echo ⚠️  Configuration file not found: config\appsettings.global.json
)

echo.
echo 🎉 Publishing completed!
echo 📁 Published files are in the 'publishes' folder

REM Show summary
echo.
echo 📊 Publishing Summary:
if exist "publishes\APIs\Users" (
    echo   Users API: ✅ Published
) else (
    echo   Users API: ❌ Failed
)
if exist "publishes\APIs\Affiliate" (
    echo   Affiliate API: ✅ Published
) else (
    echo   Affiliate API: ❌ Failed
)
if exist "publishes\APIs\Orders" (
    echo   Orders API: ✅ Published
) else (
    echo   Orders API: ❌ Failed
)
if exist "publishes\APIs\Wallet" (
    echo   Wallet API: ✅ Published
) else (
    echo   Wallet API: ❌ Failed
)
if exist "publishes\APIs\TallaEgg" (
    echo   TallaEgg API: ✅ Published
) else (
    echo   TallaEgg API: ❌ Failed
)
if exist "publishes\TelegramBot" (
    echo   Telegram Bot: ✅ Published
) else (
    echo   Telegram Bot: ❌ Failed
)

echo.
pause
