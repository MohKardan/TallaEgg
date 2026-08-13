# Windows Deployment (issue #70)

How to run Users.Api, Wallet.Api, Orders.Api, and the Telegram bot as native Windows services on
the production server, so they restart automatically after a crash or a reboot — the actual
target of KR1's 7-consecutive-days uptime measurement.

`Affiliate.Api` and `TallaEgg.Api` are not part of this: nothing calls `TallaEgg.Api`, and
`Affiliate.Api` starts but has no migrations (see the README's Database Setup section), so
neither belongs in a deployment. Decision recorded on
[#69](https://github.com/MohKardan/TallaEgg/issues/69).

## Why native Windows services, not NSSM or Docker

- **No third-party binary.** Every deployed project now calls
  `builder.Host.UseWindowsService()` (the APIs) or `.UseWindowsService()` on the bot's
  `IHostBuilder` chain — a single line from the official
  `Microsoft.Extensions.Hosting.WindowsServices` package. That's a no-op outside a real Service
  Control Manager session, so it never affects `dotnet run` locally. Once present, plain
  `sc.exe` — already on every Windows box — can create, start, and configure crash-recovery for
  the process directly. NSSM would be one more thing to download, trust, and keep updated for no
  extra capability here.
- **Not Docker.** Same reasoning the issue itself gives: `ResolveSharedConfigPath()` walks *up*
  the directory tree from the content root looking for `config\appsettings.global.json`, which
  doesn't exist in a from-scratch container image without a volume mount recreating it. That's
  packaging work with no payoff for four processes on one host, this close to a review.

## One-time setup

1. On the server, pick a deployment root — these scripts default to `C:\TallaEgg`, unrelated to
   where (or whether) a git checkout lives.
2. Create `C:\TallaEgg\config\appsettings.global.json` from
   `config\appsettings.global.example.json` (see the main README's Configuration section) with
   production values. Bind addresses stay as `http://localhost:<port>` — see the README's Ports
   and bind addresses section (#69); nothing here should listen on a public interface.
3. From a dev machine or the server itself, with this repo checked out:
   ```powershell
   .\scripts\windows-services\publish-all.ps1 -InstallRoot C:\TallaEgg
   ```
4. From an elevated PowerShell session on the server:
   ```powershell
   .\scripts\windows-services\install-services.ps1 -InstallRoot C:\TallaEgg -TallaEggApiKey (Read-Host -AsSecureString "TallaEgg API key")
   ```
   This creates four services (`TallaEggWalletApi`, `TallaEggUsersApi`, `TallaEggOrdersApi`,
   `TallaEggBot`), each set to start automatically at boot and restart on crash (10s, then 30s,
   then 60s backoff, resetting after 24h of stability), with `ASPNETCORE_ENVIRONMENT=Production`
   and `TALLAEGG_API_KEY` set directly in the service's registry environment — sc.exe has no
   flag for this, so there is no other native way to hand a Windows service its own environment
   variables.

## Redeploying a new version

```powershell
.\scripts\windows-services\publish-all.ps1 -InstallRoot C:\TallaEgg
.\scripts\windows-services\install-services.ps1 -InstallRoot C:\TallaEgg -TallaEggApiKey (Read-Host -AsSecureString "TallaEgg API key")
```

`install-services.ps1` stops and recreates each service, so re-running it is the redeploy step —
there's no separate update path to remember.

## Start order — what's covered and what isn't

`Orders.Api` and `Users.Api` call `Wallet.Api` on startup paths, and all three run
`Database.MigrateAsync()` as the first thing they do. The service dependency graph
(`Wallet.Api` before `Users.Api`/`Orders.Api`, all three before the bot) makes Windows wait for
each dependency to reach the *Running* state first, which absorbs most of the simultaneous-boot
noise the issue warns about.

What it does **not** guarantee: "Running" means the process started, not that its own migration
or first outbound call has finished. A dependent service can still log a handful of connection
retries in the first few seconds after boot. That's expected — restart-on-crash absorbs it — but
if it's ever not just noise, tighten this with an explicit `Start-Sleep` between dependency tiers
in `install-services.ps1` rather than assuming SCM ordering alone is sufficient.

## Logs

Each service writes its own `logs\<service>-.log` (bot: `logs\telegrambot-.log`) via Serilog,
rolling daily, independent of anything the SCM does. Retention is capped at 30 files
(`retainedFileCountLimit: 30` — issue #70's log-rotation item); before that fix these grew
without bound. `Get-Content -Wait` on the relevant file is the fastest way to watch a service
live; nothing here writes to the Windows Event Log.

## Removing the services

```powershell
.\scripts\windows-services\uninstall-services.ps1
```

Stops and deletes all four. Published files, logs, and the database are untouched.

## Not covered here

A liveness signal (the running bot proactively reporting a restart, e.g. through the existing
`TelegramLoggerService`) was noted in #70 as worth doing but not required — it's still open.
