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

   The three APIs are also made dependent on the local SQL Server service, so Windows cannot start
   them before the database (see Start order below). The default is SQL Server Express's
   `MSSQL$SQLEXPRESS`; for any other instance pass `-SqlServiceName`, **in single quotes** — in a
   double-quoted PowerShell string everything after the `$` is read as a variable name and the
   dependency silently becomes `MSSQL`. Pass `-SqlServiceName ''` if the database is not a service
   on this machine. The script fails up front if the name does not resolve, rather than leaving a
   service that cannot start.

## Redeploying a new version

```powershell
.\scripts\windows-services\publish-all.ps1 -InstallRoot C:\TallaEgg
.\scripts\windows-services\install-services.ps1 -InstallRoot C:\TallaEgg -TallaEggApiKey (Read-Host -AsSecureString "TallaEgg API key")
```

`install-services.ps1` stops and recreates each service, so re-running it is the redeploy step —
there's no separate update path to remember.

## Which build is running (issue #218)

Each service reports its own version and the commit it was built from, so a redeploy can be
confirmed without a file-properties dialog over RDP:

- **In the log**, as the first line each service writes at startup — the only answer available
  for a service that crashes before it can serve anything:
  ```
  [08:21:31 INF] Starting Users.Api, version 1.1.0, built from commit ff95e00a327536efa53e2af247b661ba9be5f744.
  ```
- **Over HTTP**, from the three APIs. `GET /version` carries no exemption from the Production
  authorization policy, so it needs the API key like every other endpoint:
  ```powershell
  Invoke-RestMethod http://localhost:<port>/version -Headers @{ 'X-API-Key' = $env:TALLAEGG_API_KEY }
  # -> data.version 1.1.0, data.commitHash ff95e00a327536efa53e2af247b661ba9be5f744
  ```
  Ports are in the README's Ports and bind addresses section. The bot serves nothing, so its
  log line is the whole of its answer.

The hash names the commit checked out **on the machine that ran `publish-all.ps1`**, and says
nothing about the server. `commitHash` is `null` when that build could not see a `.git`
directory — publishing from a source archive rather than a checkout looks like this.
Uncommitted changes on the build machine do not move it either: it is the last commit, not the
working tree.

## Start order — what's covered and what isn't

Two orderings decide whether this deployment comes back after a reboot. Before
[#228](https://github.com/MohKardan/TallaEgg/issues/228) only the second one was declared, and the
first is the one that mattered.

### Against the database

`Wallet.Api`, `Users.Api` and `Orders.Api` each run `Database.MigrateAsync()` before their host
starts, so none of them can get anywhere before SQL Server is up. **SQL Server Express installs
itself as *delayed* auto-start** — its own default, roughly two minutes after boot. The four
TallaEgg services were ordinary auto-start and declared no dependency on it, so at every
unattended boot they ran first, blocked in `MigrateAsync()` against a database that was not
listening, and the SCM killed `Wallet.Api` at its 45-second start timeout. The other three then
failed on the dependency they declare on `Wallet.Api`, and nothing was running until a person
logged in. That is #228, and it made every reboot an outage.

`install-services.ps1` now names the SQL Server service in the dependency list of all three APIs
(`-SqlServiceName`, default `MSSQL$SQLEXPRESS`). Windows permits an auto-start service to depend
on a delayed auto-start one, and the SCM then has to start the delayed service at boot rather than
after the delay — so declaring the dependency both orders the start *and* pulls SQL Server forward
into the boot-time pass. From
[SERVICE_DELAYED_AUTO_START_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winsvc/ns-winsvc-service_delayed_auto_start_info),
Remarks:

> An auto-start service can depend on a delayed auto-start service, but this is not generally
> desirable as the SCM must start the dependent delayed auto-start service at boot.

"Not generally desirable" there is about boot-time performance, which is the trade this machine
wants: it exists to run these four services, and two minutes of downtime per reboot buys nothing.

The bot is not given the dependency — it opens no database of its own, and it already waits on all
three APIs.

### Against each other

`Orders.Api` and `Users.Api` call `Wallet.Api` on startup paths, so `Wallet.Api` starts first and
the bot starts last.

### What neither ordering buys

*Running* means the dependency's process started and reported ready. Nothing more. For SQL Server
it does **not** mean the instance is accepting connections yet; for an API it does not mean its own
migration or first outbound call has finished. A dependent can still log a handful of connection
retries in the first seconds after boot — that's expected, and restart-on-crash absorbs it.

So the dependency narrows the window, it does not close it. Closing it means the services waiting
for their database instead of trusting Windows' start order, which is application code and is
tracked separately in [#230](https://github.com/MohKardan/TallaEgg/issues/230). Note the trap
recorded there: retrying around `MigrateAsync()` where it currently stands would make this *worse*,
because that code runs before the host connects to the SCM and a longer wait there is a longer
start timeout, not a recovery.

### Applying this to a machine installed before #228

`install-services.ps1` only writes the dependency list when it creates a service, so an existing
install keeps whatever it was given at install time. Either re-run the installer (it stops,
deletes and recreates all four — the redeploy step above), or, with no downtime at all, set the
three dependency lists directly from an elevated PowerShell session:

```powershell
sc.exe config TallaEggWalletApi depend= 'MSSQL$SQLEXPRESS/'
sc.exe config TallaEggUsersApi  depend= 'MSSQL$SQLEXPRESS/TallaEggWalletApi/'
sc.exe config TallaEggOrdersApi depend= 'MSSQL$SQLEXPRESS/TallaEggWalletApi/'
```

Three things to get right:

- **The single quotes are mandatory.** Unquoted or double-quoted, PowerShell expands
  `$SQLEXPRESS` as an undefined variable, sc.exe receives `MSSQL/`, and the services then fail to
  start with error 1075 (dependent service does not exist) — a worse outage than the one being
  fixed.
- **`depend=` replaces the whole list**, it does not append. That is why `TallaEggWalletApi` is
  repeated in the second and third lines.
- **The space after `depend=` is part of sc.exe's syntax**, not a typo.

`TallaEggBot` is unchanged: it opens no database and already depends on all three APIs.

Nothing needs restarting for this to take effect — a dependency list is read when a service is
next started, and the point of the change is what happens at the next boot. Confirm it landed with
`sc.exe qc TallaEggWalletApi`, which should list `DEPENDENCIES : MSSQL$SQLEXPRESS`.

## Logs

Each service writes its own log via Serilog into a `logs\` folder **beside its own binary**,
rolling daily, independent of anything the SCM does:

```
C:\TallaEgg\publish\Wallet.Api\logs\wallet-api-<date>.log
C:\TallaEgg\publish\Users.Api\logs\users-api-<date>.log
C:\TallaEgg\publish\Orders.Api\logs\orders-api-<date>.log
C:\TallaEgg\publish\Bot\logs\telegrambot-<date>.log
```

Retention is capped at 30 files (`retainedFileCountLimit: 30` — issue #70's log-rotation item);
before that fix these grew without bound. `Get-Content -Wait` on the relevant file is the fastest
way to watch a service live; nothing here writes to the Windows Event Log.

Until #211 the sink path was relative, and Serilog resolves a relative path against the process
working directory. `sc.exe create` has no option to set one, so the SCM started every service in
`C:\Windows\System32` and all four logs were written there. **On a machine deployed before that
fix, that is where the older files still are** — the fix changes where new ones go, it does not
move what is already written.

## Removing the services

```powershell
.\scripts\windows-services\uninstall-services.ps1
```

Stops and deletes all four. Published files, logs, and the database are untouched.

## Not covered here

A liveness signal (the running bot proactively reporting a restart, e.g. through the existing
`TelegramLoggerService`) was noted in #70 as worth doing but not required — it's still open.
