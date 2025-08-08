# TallaEgg - Agent Development Guide

## Build Commands
- **Build entire solution:** `dotnet build TallaEgg.sln`
- **Run tests:** `dotnet test` (uses xUnit with Moq mocking)
- **Run single test project:** `dotnet test TallaEgg.TelegramBot.Tests/TallaEgg.TelegramBot.Tests.csproj`
- **Start TelegramBot:** `cd TelegramBot/TallaEgg.TelegramBot && dotnet run`
- **Start APIs:** Use `run.bat` to start all APIs on ports 5135-5139

## Architecture
- **Clean Architecture** with Core/Application/Infrastructure layers
- **Microservices:** Users (5136), Affiliate (5137), Matching (5138), Wallet (5139), Orders (5135)
- **TelegramBot:** Separate project with Core/Application/Infrastructure layers
- **Database:** SQL Server (see `create_table.sql` for schema)
- **Main API Gateway:** TallaEgg.Api on port 5135

## Bot Configuration
- **Referral Settings:** Configure in `TelegramBot/TallaEgg.TelegramBot/appsettings.json`
- **RequireReferralCode:** true/false to make referral codes mandatory
- **DefaultReferralCode:** used when referral not required (default: "ADMIN2024")
- **Admin Commands:** `/admin_referral_on`, `/admin_referral_off`, `/admin_referral_status`

## Code Style & Conventions
- **Framework:** .NET 9.0 with C# nullable enabled
- **Imports:** Microsoft.Extensions.* for DI/logging, explicit usings for business logic
- **Naming:** PascalCase for classes/methods, camelCase for fields, DTOs suffixed with "Dto"
- **Architecture:** Interfaces in Core, Services in Application, Handlers in Infrastructure
- **Error Handling:** Use `ILogger<T>` for logging, return Result<T> patterns where applicable
- **Testing:** xUnit + Moq + FluentAssertions, AAA pattern, separate Unit/Integration tests
