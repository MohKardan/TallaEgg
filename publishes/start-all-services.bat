@echo off
REM TallaEgg Services Startup Script
REM This script starts all TallaEgg services

echo.
echo 🚀 Starting TallaEgg Services...
echo.

REM Get the script directory
set SCRIPT_DIR=%~dp0

REM Start Users API
echo 📦 Starting Users API...
start "TallaEgg Users API" cmd /k "cd /d "%SCRIPT_DIR%APIs\Users" && dotnet Users.Api.dll"
timeout /t 3 /nobreak >nul

REM Start Affiliate API
echo 📦 Starting Affiliate API...
start "TallaEgg Affiliate API" cmd /k "cd /d "%SCRIPT_DIR%APIs\Affiliate" && dotnet Affiliate.Api.dll"
timeout /t 3 /nobreak >nul

REM Start Orders API
echo 📦 Starting Orders API...
start "TallaEgg Orders API" cmd /k "cd /d "%SCRIPT_DIR%APIs\Orders" && dotnet Orders.Api.dll"
timeout /t 3 /nobreak >nul

REM Start Wallet API
echo 📦 Starting Wallet API...
start "TallaEgg Wallet API" cmd /k "cd /d "%SCRIPT_DIR%APIs\Wallet" && dotnet Wallet.Api.dll"
timeout /t 3 /nobreak >nul

REM Start TallaEgg API
echo 📦 Starting TallaEgg API...
start "TallaEgg Main API" cmd /k "cd /d "%SCRIPT_DIR%APIs\TallaEgg" && dotnet TallaEgg.Api.dll"
timeout /t 3 /nobreak >nul

REM Start Telegram Bot
echo 📦 Starting Telegram Bot...
start "TallaEgg Telegram Bot" cmd /k "cd /d "%SCRIPT_DIR%TelegramBot" && dotnet TallaEgg.TelegramBot.Infrastructure.dll"

echo.
echo ✅ All services started!
echo 📊 Check the opened windows for service status
echo.
echo Services running on:
echo   Users API: http://localhost:5136
echo   Affiliate API: http://localhost:60812
echo   Orders API: http://localhost:5140
echo   Wallet API: http://localhost:60933
echo   TallaEgg API: http://localhost:5135
echo   Telegram Bot: Running in background
echo.
pause
