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
   `MSSQL$SQLEXPRESS`; for any other instance pass `-SqlServiceName`, and note that **any name
   containing `$` must be in single quotes** — in a double-quoted PowerShell string everything
   after the `$` is read as a variable name and the dependency silently becomes `MSSQL`. A default
   instance (`MSSQLSERVER`) has no `$` and is unaffected. Pass `-SqlServiceName ''` if the database
   is not a service on this machine. It has to be the instance the connection strings in
   `config\appsettings.global.json` actually use; nothing reconciles the two. The script rejects a
   name that does not resolve exactly, and one belonging to a disabled service, rather than leaving
   services that can never start.

## Redeploying a new version

Two ways in. Prefer the first: it installs the same bytes for everyone and keeps `/version`
able to name the commit.

### From a published release

Publishing a release runs the `release-artifacts` workflow, which builds the four services on a
Windows runner and attaches one zip per service to it. The runner builds from a real checkout,
so `/version` reports a commit hash rather than `null` — a source zip downloaded onto the server
has no `.git` for the SDK to read.

```powershell
# Stop first: unzipping over a running service hits locked files.
'TallaEggBot', 'TallaEggOrdersApi', 'TallaEggUsersApi', 'TallaEggWalletApi' |
    ForEach-Object { sc.exe stop $_ }

$tag = 'v1.3.0'
foreach ($service in 'Wallet.Api', 'Users.Api', 'Orders.Api', 'Bot') {
    $zip = "$env:TEMP\$service-$tag.zip"
    Invoke-WebRequest -OutFile $zip `
        "https://github.com/MohKardan/TallaEgg/releases/download/$tag/$service-$tag.zip"
    Expand-Archive $zip -DestinationPath "C:\TallaEgg\publish\$service" -Force
}

.\install-services.ps1 -InstallRoot C:\TallaEgg -TallaEggApiKey (Read-Host -AsSecureString "TallaEgg API key")
```

The shared configuration lives at `C:\TallaEgg\config\appsettings.global.json`, one level above
`publish\`, so unzipping over the service folders never touches it. That separation is the reason
the layout is shaped this way.

The workflow refuses to package an `appsettings.global.json`, so a release asset never carries
credentials, and it fails rather than attaching anything if the built assemblies do not report
the tagged version.

### Building on the server

The only option for a commit that has no release, and the fallback if a release has no assets.

```powershell
.\scripts\windows-services\publish-all.ps1 -InstallRoot C:\TallaEgg
.\scripts\windows-services\install-services.ps1 -InstallRoot C:\TallaEgg -TallaEggApiKey (Read-Host -AsSecureString "TallaEgg API key")
```

### Either way

`install-services.ps1` stops and recreates each service, so re-running it is the redeploy step —
there's no separate update path to remember.

Add `-SqlServiceName` if this host's SQL Server service is not Express's default
`MSSQL$SQLEXPRESS` (see the one-time setup above). Since #228 the script resolves that name before
it creates anything, so on such a host the redeploy now stops with an error rather than installing
services that cannot start.

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

### What neither ordering buys, and what waits it out anyway

*Running* means the dependency's process started and reported ready. Nothing more. For SQL Server
it does **not** mean the instance is accepting connections yet; for an API it does not mean its own
migration has finished.

So the dependency narrows the window, it does not close it. What closes it is the services waiting
for their database instead of trusting Windows' start order, and since
[#230](https://github.com/MohKardan/TallaEgg/issues/230) they do.

Until then they could not. `Wallet.Api`, `Users.Api` and `Orders.Api` each ran `MigrateAsync()`
between `builder.Build()` and `app.Run()`, and `WindowsServiceLifetime` does not connect to the SCM
dispatcher until `app.Run()` — so every second spent migrating was a second the SCM counted against
its 45-second start timeout with nothing connected yet. No `DbContext` here configures
`EnableRetryOnFailure`, so a database that was not answering did not fail fast either: the call
blocked, the process was killed, and the service never reported anything. Restart-on-crash did not
rescue the ones behind it, and still does not — the `sc.exe failure` actions run when a service's
own process terminates, so a service the SCM never launched, because its declared dependency
failed, takes no recovery action at all and simply stays `Stopped`.

### How a service waits for its database now (issue #230)

Each of the three APIs registers its migration as a hosted service instead, so the host starts and
the service reports `Running` within a second or two whether or not the database is up. What
follows is visible in the service's own log under `C:\TallaEgg\publish\<Service>\logs\`:

- **The database is up.** One line, and the service is serving:
  ```
  [18:53:20 INF] Application started. Press Ctrl+C to shut down.
  [18:53:21 INF] Database migration succeeded on attempt 1 of 10. The service is now answering requests.
  ```
- **The database arrives late.** Each attempt is logged and the backoff doubles from five seconds
  to a minute. No restart is needed — the same process recovers:
  ```
  [18:53:35 WRN] Database migration attempt 1 of 10 failed. Retrying in 5s; requests are answered 503 until it succeeds.
  [18:53:55 WRN] Database migration attempt 2 of 10 failed. Retrying in 10s; requests are answered 503 until it succeeds.
  [18:54:40 INF] Database migration succeeded on attempt 4 of 10. The service is now answering requests.
  ```
- **The database never comes.** After ten attempts — roughly six minutes of waiting plus each
  attempt's own connection timeout — the service logs the last exception at `Fatal` and **stops
  itself** rather than staying `Running` and useless. Expect it in `Get-Service` as `Stopped`, with
  this in its log:
  ```
  [FTL] Database migration failed on all 10 attempts. The service cannot serve a request without
        its schema, so it is stopping rather than staying up and answering 503 forever.
  ```

**While the migration has not succeeded, every request is answered `503 Service Unavailable`** with
a Persian message saying the service is still starting. `GET /version` is the one exception, so
"which build is this?" still has an answer while a service is degraded. A `503` from these services
during the first minute after a boot is the expected shape of a slow database, not a fault.

The bot is unchanged: it opens no database of its own.

### Applying this to a machine installed before #228

`install-services.ps1` only writes the dependency list when it creates a service, so an existing
install keeps whatever it was given at install time. Either re-run the installer (it stops,
deletes and recreates all four — the redeploy step above), or, with no downtime at all, set the
three dependency lists directly.

**First, confirm what the SQL Server service on this host is actually called.** sc.exe accepts a
dependency on a service that does not exist and only fails at the *next boot*, so a wrong name here
converts an intermittent outage into a permanent one. From an elevated PowerShell session:

```powershell
Get-Service -Name 'MSSQL*' | Format-Table Name, Status, StartType -AutoSize
```

`MSSQL$SQLEXPRESS` is SQL Server Express's default and is what these services expect; a default
full instance is `MSSQLSERVER`, a named one `MSSQL$<INSTANCE>`. It has to be the instance the
connection strings in `config\appsettings.global.json` actually point at — the two are configured
separately and nothing reconciles them. If the database is not a service on this machine, stop
here: the ordering cannot be expressed as an SCM dependency at all.

Then, substituting that name for `MSSQL$SQLEXPRESS` if it differs:

```powershell
sc.exe config TallaEggWalletApi depend= 'MSSQL$SQLEXPRESS/'
sc.exe config TallaEggUsersApi  depend= 'MSSQL$SQLEXPRESS/TallaEggWalletApi/'
sc.exe config TallaEggOrdersApi depend= 'MSSQL$SQLEXPRESS/TallaEggWalletApi/'
```

Four things to get right:

- **A name containing `$` must be in single quotes.** Double-quoted or unquoted, PowerShell reads
  `$SQLEXPRESS` as an undefined variable and drops it: sc.exe receives `MSSQL/` from the first line
  and `MSSQL/TallaEggWalletApi/` from the other two. All three services then fail at the next boot
  with error 1075 — *the dependency service does not exist or has been marked for deletion* —
  which is a worse outage than the one being fixed. (`MSSQLSERVER` has no `$` and is unaffected,
  but quoting it costs nothing.)
- **`depend=` replaces the whole list**, it does not append. That is why `TallaEggWalletApi` is
  repeated in the second and third lines.
- **The space after `depend=` is part of sc.exe's syntax**, not a typo.
- **Read each command's output.** sc.exe reports failure through its exit code, which PowerShell
  will not raise as an error for you.

`TallaEggBot` is unchanged: it opens no database and already depends on all three APIs.

Nothing needs restarting for this to take effect — a dependency list is read when a service is next
started, and the point of the change is what happens at the next boot. Confirm it landed on all
three:

```powershell
'TallaEggWalletApi','TallaEggUsersApi','TallaEggOrdersApi' | ForEach-Object { sc.exe qc $_ }
```

Each should show a `DEPENDENCIES` line naming the SQL service in full. A line reading `MSSQL` on
its own is the quoting mistake above — fix it before rebooting.

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
