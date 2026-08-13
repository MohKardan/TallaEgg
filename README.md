# TallaEgg Trading Platform

## Overview

TallaEgg lets a gold shop trade with its own customers over Telegram.

The shop publishes a two-sided quote — the price it will buy at and the price it will sell at, entered as a single `71000000-80000000` pair. Approved customers see that quote and take either side of it. Every trade has the shop as its counterparty; customers never trade with each other.

A new user who starts the bot registers, shares their phone number, and waits for the shop to approve them. Until then they have no access. To trade they need either a balance or credit, which the shop grants manually — credit is a ceiling their gold balance may go negative against, so ten grams of credit is what lets a customer sell ten grams they do not yet hold.

Approved customers can trade at the published quote, review their trade history, and settle physically with the shop.

https://private-user-images.githubusercontent.com/45781438/530709572-98e34f7f-e778-45ec-8516-4a3372e6764b.mp4

https://private-user-images.githubusercontent.com/45781438/530709883-c2a1096b-5c32-4a05-9fe5-81720a5f2567.mp4

## Key Capabilities

- **Dealer quote model** — the shop publishes a quote; orders are created only at fill time and consumed immediately, so no collateral sits locked in a resting order book.
- Atomic trade settlement over a transactional outbox, idempotent on the trade id, with collateral locked before an order becomes matchable.
- Wallet domain with deposits, withdrawals, balance locking, gold-denominated credit, transaction history, and default wallet provisioning.
- RESTful minimal APIs for users, wallets, and orders returning a unified `ApiResponse<T>` envelope.
- Telegram bot that consumes the platform APIs and exposes a small notification API for other services.
- Centralised configuration (`config/appsettings.global.json`), Serilog logging, and typed HTTP clients across services.
- A matching engine for peer-to-peer order books remains in the codebase but **does not run for dealer symbols** — see [Trading model](#trading-model).

## Repository Layout

| Path | Description |
| --- | --- |
| `src/User` | Users service — onboarding, phone/role/status, default wallets |
| `src/Wallet` | Wallet service — balances, locking, settlement, transaction history |
| `src/Order` | Orders service — quotes, quote fills, trades, matching engine |
| `src/Affiliate` | Affiliate service — invitation codes (**not currently functional**, see below) |
| `src/TallaEgg` | Shared core/application/infrastructure libraries plus a legacy orchestration API |
| `src/Wallet/Wallet.Tests` | The test suite for the whole platform (see [Testing](#testing)) |
| `TelegramBot` | Telegram bot host, handlers, and typed API clients |
| `config/appsettings.global.json` | Shared configuration consumed by every service — git-ignored ([#33](https://github.com/MohKardan/TallaEgg/issues/33)); copy it from `config/appsettings.global.example.json` |
| `docs/` | Architecture, operations, process, OKRs, and the business proposal |
| `governance/` | Charter, bylaws, meeting notes, and `P-XXXX` proposals |
| `scripts/`, `publishes/` | Helper scripts and deployment artefacts |
| `SoftwareArchitecture/` | Diagrams |

## Trading model

The platform supports two market modes per symbol, set in configuration:

| Mode | Behaviour |
| --- | --- |
| **`Dealer`** (current) | The shop publishes a quote. A customer accepting it creates both orders and matches them in one operation. The background matching engine **skips these symbols entirely** — it would otherwise reach the same order pair a fill is already matching and produce two trades from one. |
| `OrderBook` | Classic maker/taker matching through the background engine. Not used in production today. |

`MAUA/IRT` (gold / toman) runs in `Dealer` mode. The counterparty of a fill is whoever published the quote, so nothing needs to name the shop in configuration.

## Tech Stack

- .NET 9.0 with C# 12, minimal APIs, and background services.
- Entity Framework Core 9 with the SQL Server provider.
- Serilog for structured logging to console and rolling files.
- Telegram.Bot over long polling — **no inbound ports are required**.
- Typed `HttpClient` wrappers for inter-service calls.

## Prerequisites

- .NET SDK 9.0.
- SQL Server reachable from the host. `(localdb)\MSSQLLocalDB` works for development; see [#68](https://github.com/MohKardan/TallaEgg/issues/68) for the move to a deployable instance.
- A Telegram bot token from [@BotFather](https://t.me/BotFather).
- Your own Telegram numeric user id (ask [@userinfobot](https://t.me/userinfobot)) — this is what makes you the shop operator on a fresh database.

## Configuration

Every service loads `config/appsettings.global.json`, then flattens the section under `Services:` matching its own assembly name. There is no per-service `appsettings.json` to maintain.

> ⚠️ **This file used to be tracked in git and its old contents (a bot token and the shared API
> key) are still readable in this public repo's history.** Treat both as compromised until they
> are rotated — see [#33](https://github.com/MohKardan/TallaEgg/issues/33). The file itself is
> now git-ignored; do not re-add it or any other secret to source control.

Create your own copy from the template below.

```json
{
  "ConnectionStrings": {
    "UsersDb": "Server=(localdb)\\MSSQLLocalDB;Database=TallaEggUsers;Trusted_Connection=True;TrustServerCertificate=True;",
    "WalletDb": "Server=(localdb)\\MSSQLLocalDB;Database=TallaEggWallet;Trusted_Connection=True;TrustServerCertificate=True;",
    "OrdersDb": "Server=(localdb)\\MSSQLLocalDB;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;",
    "AffiliateDb": "Server=(localdb)\\MSSQLLocalDB;Database=TallaEggAffiliate;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Services": {
    "Users.Api": {
      "Urls": [ "http://localhost:5136" ],
      "WalletApiUrl": "http://localhost:60933/"
    },
    "Wallet.Api": {
      "Urls": [ "http://localhost:60933" ]
    },
    "Orders.Api": {
      "Urls": [ "http://localhost:5140" ],
      "WalletApiUrl": "http://localhost:60933/api",
      "Matching": {
        "RequireMarketMakerCounterparty": true,
        "MarketModes": { "MAUA/IRT": "Dealer" }
      }
    },
    "Affiliate.Api": {
      "Urls": [ "http://localhost:60812" ]
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
        "DefaultReferralCode": "admin",
        "OwnerTelegramIds": [ 123456789 ]
      },
      "TelegramBotToken": "<token from BotFather>"
    }
  }
}
```

### The settings that matter

| Setting | Why it matters |
| --- | --- |
| `BotSettings:OwnerTelegramIds` | The only thing that lets anyone in on an empty database. A configured owner is approved and given the `Admin` role automatically when they register. Put **your own** Telegram id here. |
| `BotSettings:DefaultReferralCode` | Must be `admin` — the code carried by the administrator row `Users.Api` seeds. Registration rejects any code that belongs to no user, so a mismatch here means **nobody can register at all**. |
| `Matching:MarketModes` | Without `"MAUA/IRT": "Dealer"` the symbol falls back to `OrderBook` and every quote fill is refused. |
| `TelegramBotToken` | Read from this file. `TELEGRAM_BOT_TOKEN` is only a fallback for the standalone notification API, **not** for the bot itself. |

### Ports and bind addresses

Every `Urls` entry above is plain HTTP on loopback (`localhost`), on purpose — see
[#69](https://github.com/MohKardan/TallaEgg/issues/69):

- The bot reaches Telegram by **long polling**; it dials out, Telegram never dials in.
- The four real APIs are only ever called by the bot, on the same host.
- So **no inbound port needs to be open**, and no HTTPS bind address is needed either — a
  `https://localhost:...` entry requires a dev certificate that will not exist on a server, and
  the only thing between an internet-facing port and the database would be the shared API key
  above. Binding to loopback is the actual security control here, not a placeholder to replace.

| Service | Port | Purpose |
| --- | --- | --- |
| Users.Api | 5136 | Registration, roles, wallet provisioning |
| Wallet.Api | 60933 | Balances, settlement |
| Orders.Api | 5140 | Quotes, trades, matching |
| Affiliate.Api | 60812 | Not deployed — see [Database Setup](#database-setup) |
| TallaEgg.TelegramBot.Infrastructure | 57546 | Bot's own notification endpoint |
| TallaEgg.Api | 5135 | Not deployed — legacy, nothing calls it |

On a real server, confirm none of these show up in `netstat`/`ss` on a public interface — only on
`127.0.0.1`.

### Shared API key

Wallet.Api, Users.Api, Orders.Api, and Affiliate.Api authenticate inter-service calls with a
shared key sent via the `X-API-Key` header (`TallaEgg.Core.APIKeyConstant`). It is read from the
`TALLAEGG_API_KEY` environment variable, not from any tracked file.

- **Development**: none of the four services enforce this — API-key authentication is only wired
  up when `ASPNETCORE_ENVIRONMENT=Production`. Leave the variable unset locally.
- **Production**: set `TALLAEGG_API_KEY` before starting any service; each one throws at startup
  if it is missing.

> **`ASPNETCORE_ENVIRONMENT` must be set explicitly on a server.** Locally `dotnet run` always
> reports `Development` because of `launchSettings.json` — that file is not used by a published
> deployment, so a server defaults to `Production` the moment it's set (or left implicit) outside
> `dotnet run`. Until [#69](https://github.com/MohKardan/TallaEgg/issues/69), the `Production`
> code path — API-key auth, no Swagger redirect exemption — had never actually been exercised.

## Database Setup

Every API calls `Database.MigrateAsync()` at startup, so the schema is created on first run. No manual step is needed for Users, Wallet, or Orders.

To apply migrations ahead of time:

```
dotnet restore
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/User/Users.Api/Users.Api.csproj
dotnet ef database update --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet ef database update --project src/Order/Orders.Api/Orders.Api.csproj
```

`Users.Api` seeds one administrator row (`5564f136-b9fb-4719-b4dc-b0833fa24761`) whose only purpose is to own the bootstrap invitation code. It has no Telegram id and cannot be signed in as.

> **Affiliate has no migrations.** `Affiliate.Api` calls `MigrateAsync()` but ships zero migration files, so it starts cleanly and then fails every request with `Invalid object name 'Invitations'`. Nothing currently calls it — the bot's only invitation call is commented out — so it can be left out of a deployment.

## First run

On an empty database, this is the whole setup. There is no SQL to run and no id to copy between files.

1. Put your Telegram id in `BotSettings:OwnerTelegramIds` and start the services.
2. Send `/start` to the bot, then share your phone number when prompted.
   → You are approved and given the `Admin` role automatically.
3. Publish a quote by sending the buy and sell price as one pair, e.g. `79000000-79500000`.
4. Have a customer `/start` and share their number, then approve them: `ت <their phone>`.
5. Grant them credit: `ش <their phone> 10 طلا`.
6. They can now trade at the published quote.

### Operator commands

Prices are per mesghal; gold amounts are in grams. Numbers may be typed in Persian or Latin digits.

| Command | Effect |
| --- | --- |
| `<buy>-<sell>` | Publish a quote, e.g. `79000000-79500000` |
| `ت <phone>` | Approve an account |
| `ر <phone>` | Reject an account |
| `ن <phone> <role>` | Change a role — `کاربر عادی`, `حسابدار`, `مدیر`, `مدیر ارشد` |
| `ش <phone> <amount> <asset>` | Credit an account, e.g. `ش 09121234567 500000 تومان` |
| `د <phone> <amount> <asset>` | Debit an account |
| `م <phone>` | Show balances |
| `س <phone>` | Show a user's open orders |
| `ک [search]` | List users |

## Running Locally

Build once, then start each service in its own terminal. Building while services are running fails on locked DLLs.

```
dotnet build TallaEgg.sln

dotnet run --no-build --project src/User/Users.Api/Users.Api.csproj
dotnet run --no-build --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet run --no-build --project src/Order/Orders.Api/Orders.Api.csproj
dotnet run --no-build --project TelegramBot/TallaEgg.TelegramBot.Infrastructure/TallaEgg.TelegramBot.Infrastructure.csproj
```

`Affiliate.Api` is not needed (see above). `src/TallaEgg/TallaEgg.Api` is a legacy orchestration API that nothing calls.

Swagger UI is at `/api-docs` on each service — for example `http://localhost:5136/api-docs`. The bot host also exposes `/api/telegram/notifications/trade-match`.

## Production Deployment

On a server, the four deployed services (not Affiliate.Api or TallaEgg.Api — see above) run as
native Windows services instead of a terminal per process, so they restart automatically after a
crash or reboot. See
[`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md) — issue #70 —
for the one-time setup and the `scripts/windows-services/` install scripts.

## Service Highlights

- **Users.Api** — registration with invitation codes, phone updates, role and status management, default wallet provisioning, lookups by Telegram id, phone, or role.
- **Wallet.Api** — balances, deposits, withdrawals, lock/unlock, atomic trade settlement, transaction history.
- **Orders.Api** — quote publishing and history, quote fills, trade history, best bid/ask, and the (dormant for dealer symbols) matching engine.
- **Telegram Bot** — long-polling host, typed API clients, trade notifications, and the operator command surface above.
- **Affiliate.Api** — invitation codes. Present but non-functional; see the note under Database Setup.

## Testing

```
dotnet test TallaEgg.sln
```

**`src/Wallet/Wallet.Tests` is the only test project in the solution** and holds the suite for the whole platform — wallet, orders, matching, quote fills, bot handlers, and formatting. Its name is historical; new tests belong here regardless of which service they cover.

Two older projects under `TelegramBot/TallaEgg.TelegramBot.Tests` are **not** in the solution and do not currently compile, so `dotnet test` never runs them.

## Logging

Each service writes to the console and to rolling files under its own `logs/` directory — for example `src/Order/Orders.Api/logs/orders-api-<date>.log`.

## Deployment

Long polling means the bot needs **no inbound ports**. The remaining work to make a deployment repeatable is tracked in [#68](https://github.com/MohKardan/TallaEgg/issues/68) (move off LocalDB), [#69](https://github.com/MohKardan/TallaEgg/issues/69) (production URLs), [#70](https://github.com/MohKardan/TallaEgg/issues/70) (process supervision), and [#71](https://github.com/MohKardan/TallaEgg/issues/71) (CI).

Publishing scripts live under `publishes/` and `publish-all.ps1`. They assume local build outputs and do not handle secrets.

> **Production is a code path this project has never executed.** `launchSettings.json` forces the Development environment locally and is not used by a published deployment, so the `Production` branches — including `UseAuthentication` and the API-key check — have never run. That is what makes [#71](https://github.com/MohKardan/TallaEgg/issues/71) (build and smoke-test in Release under CI) worth more than it looks.
