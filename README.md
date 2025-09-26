# TallaEgg Trading Platform

## Overview
TallaEgg is a modular .NET 9 trading platform that models the core workflows of a centralised exchange. The solution is split into focused minimal API services and background workers that cooperate through shared DTOs, HTTP clients, and SQL Server databases.

## Key Capabilities
- RESTful minimal APIs for users, wallets, orders, and affiliate programs that return a unified `ApiResponse<T>` envelope.
- Matching engine background service with maker/taker logic, database-level locking, and scheduled order book processing.
- Wallet domain with deposit, withdrawal, balance locking, trade settlement, transaction history, and default wallet provisioning.
- Invitation and affiliate tracking including code creation, validation, usage counting, and per-user reporting.
- Telegram bot infrastructure that consumes the platform APIs, streams trade notifications, and exposes a lightweight notification API for other services.
- Centralised configuration (`config/appsettings.global.json`), Serilog logging, and typed HTTP clients across services.

## Repository Layout
| Path | Description |
| --- | --- |
| src/User | Users service (API, Application, Core, Infrastructure) for onboarding, profile updates, roles, and default wallets |
| src/Wallet | Wallet service with EF Core persistence, wallet operations, and transaction endpoints |
| src/Order | Orders service, application layer, and matching engine background service |
| src/Affiliate | Affiliate microservice for invitation codes and referral tracking |
| src/TallaEgg | Shared core/application/infrastructure libraries plus orchestration API |
| TelegramBot | Telegram bot core, infrastructure host, clients, and automated tests |
| config/appsettings.global.json | Shared configuration consumed by all services |
| tools, publish, publishes | Helper scripts and deployment artifacts |

## Tech Stack
- .NET 9.0 with C# 12, minimal APIs, and background services.
- Entity Framework Core 9 with SQL Server providers.
- Serilog for structured logging to console and rolling files.
- Telegram.Bot client plus proxy-aware wrappers for bot connectivity.
- Hosted services and typed `HttpClient` wrappers for inter-service calls.

## Prerequisites
- .NET SDK 9.0 (preview channel as of this repository).
- Local or network-accessible SQL Server (Express/localdb works for development).
- Telegram bot token (store in an environment variable such as `TELEGRAM_BOT_TOKEN`).
- Optional: PowerShell 7+ or Bash for running the helper scripts.

## Configuration
All services load shared settings from `config/appsettings.global.json` and then flatten the service-specific section that matches the hosting assembly. Copy the template below, replace connection strings, ports, and secrets with values that match your environment, and do **not** commit real credentials:

```json
{
  "ConnectionStrings": {
    "UsersDb": "Server=localhost;Database=TallaEggUsers;Trusted_Connection=True;TrustServerCertificate=True;",
    "WalletDb": "Server=localhost;Database=TallaEggWallet;Trusted_Connection=True;TrustServerCertificate=True;",
    "OrdersDb": "Server=localhost;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;",
    "AffiliateDb": "Server=localhost;Database=TallaEggAffiliate;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Services": {
    "Users.Api": {
      "Urls": [ "http://localhost:5136" ],
      "WalletApiUrl": "http://localhost:60933/api"
    },
    "Wallet.Api": {
      "Urls": [ "https://localhost:60932", "http://localhost:60933" ]
    },
    "Orders.Api": {
      "Urls": [ "https://localhost:7140", "http://localhost:5140" ],
      "WalletApiUrl": "http://localhost:60933/api"
    },
    "Affiliate.Api": {
      "Urls": [ "https://localhost:60811", "http://localhost:60812" ]
    },
    "TallaEgg.TelegramBot.Infrastructure": {
      "Urls": [ "http://localhost:57546" ],
      "OrderApiUrl": "http://localhost:5140/api",
      "UsersApiUrl": "http://localhost:5136/api",
      "AffiliateApiUrl": "http://localhost:60812/api",
      "PricesApiUrl": "http://localhost:5140/api",
      "WalletApiUrl": "http://localhost:60933/api",
      "BotSettings": {
        "RequireReferralCode": false,
        "DefaultReferralCode": "ADMIN2024"
      },
      "TelegramBotToken": "set-with-env-or-user-secrets"
    }
  }
}
```

Override `TelegramBotToken` and other sensitive values via environment variables or `dotnet user-secrets` in development. The services also honour environment variables used by `Host.CreateDefaultBuilder`.

## Database Setup
Every API calls `Database.MigrateAsync()` on startup, so running each service will create or update its database automatically once migrations are present. To initialise them ahead of time you can execute:

```
dotnet restore
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/User/Users.Api/Users.Api.csproj
dotnet ef database update --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet ef database update --project src/Order/Orders.Api/Orders.Api.csproj
dotnet ef database update --project src/Affiliate/Affiliate.Api/Affiliate.Api.csproj
```

The Users service seeds a super admin account (`Id = 5564f136-b9fb-4719-b4dc-b0833fa24761`). Update or disable this seed before going beyond development.

## Running Locally
From the repository root you can start each component using the following commands in separate terminals:

```
dotnet run --project src/User/Users.Api/Users.Api.csproj
dotnet run --project src/Affiliate/Affiliate.Api/Affiliate.Api.csproj
dotnet run --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet run --project src/Order/Orders.Api/Orders.Api.csproj
dotnet run --project src/TallaEgg/TallaEgg.Api/TallaEgg.Api.csproj
dotnet run --project TelegramBot/TallaEgg.TelegramBot.Infrastructure/TallaEgg.TelegramBot.Infrastructure.csproj
```

Swagger UI is available at `/api-docs` (for example `http://localhost:5136/api-docs` for the Users API). The Telegram infrastructure host also spins up a minimal API at `/api/telegram/notifications/trade-match` for receiving trade match notifications.

## Service Highlights
- **Users.Api**: registration with invitation codes, phone updates, role/status management, default wallet provisioning, and lookups by Telegram ID, phone, or role.
- **Wallet.Api**: balance queries, deposits, withdrawals, lock/unlock operations, trade settlement endpoint, transaction history, and default wallet creation.
- **Orders.Api**: order creation, confirmation, cancellation, active order listing, trade history, best bid/ask computation, and maker/taker aware matching engine.
- **Affiliate.Api**: create, validate, and redeem invitation codes plus per-user invitation reports.
- **Telegram Bot**: long-polling bot hosted in `TallaEgg.TelegramBot.Infrastructure`, typed API clients, trade notification service, and proxy-aware bot client factory.

## Testing
Run the full test suite with:

```
dotnet test TallaEgg.sln
```

`TallaEgg.TelegramBot.Tests` covers bot command handlers and client integrations; add more domain-specific tests alongside the corresponding projects.

## Logging
Each service writes structured logs to the console and to rolling files under its local `logs/` directory (for example `src/Order/Orders.Api/logs`). Ensure the directories exist or adjust the Serilog sinks before deploying.

## Deployment
The repository includes publishing scripts under `publish/` and `publish-all.*`. Review and adapt them for your environment; they assume local build outputs and do not handle secrets.
