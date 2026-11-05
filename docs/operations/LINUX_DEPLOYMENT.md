# Linux Deployment (issue #349)

How to run Users.Api, Wallet.Api, Orders.Api, and the Telegram bot as systemd services on
Ubuntu or any other systemd distribution, so they restart automatically after a crash or a
reboot. This is the counterpart of [`WINDOWS_DEPLOYMENT.md`](WINDOWS_DEPLOYMENT.md) — the same
four services, the same layout, the same failure modes, on a Linux host.

`Affiliate.Api` and `TallaEgg.Api` are not part of this: nothing calls `TallaEgg.Api`, and
`Affiliate.Api` starts but has no migrations (see the README's Database Setup section), so
neither belongs in a deployment. Decision recorded on
[#69](https://github.com/MohKardan/TallaEgg/issues/69).

## What was verified, and what was not

The application code's Linux readiness is already measured, not assumed:

- The full test suite (1,218 tests) runs on `ubuntu-latest` in every CI build
  (`build-and-test.yml`).
- The `manual-test-run` workflow boots all four services and a live Telegram bot on
  `ubuntu-latest` against a containerized SQL Server — so the four hosts demonstrably start,
  resolve their configuration and long-poll Telegram on Linux with `UseWindowsService()` in
  them (it is a no-op outside a Windows SCM session).
- `PersianFormat` uses a fixed +03:30 offset and the built-in `PersianCalendar`; nothing
  depends on the OS time zone database.

**Not yet verified, because no Ubuntu host was available to the author of this document: the
systemd units and the two installer scripts have not been through a real `systemd` boot.** The
[verification checklist](#verification-checklist-first-deployment-on-a-real-host) below is the
procedure to run on the first deployment; treat the first real install as the test of these
files, and expect to adjust hardening settings there.

## Why systemd, not Docker

- **No third-party supervisor.** Every deployed host calls
  `builder.Host.UseSystemd()` (the APIs) or `.UseSystemd()` on the bot's `IHostBuilder` chain —
  a single line from the official `Microsoft.Extensions.Hosting.Systemd` package, chained after
  the existing `UseWindowsService()`. Each call is a no-op on the other platform, and both are
  enforced by `ServiceHostPathAnchorTests`. Once present, plain systemd units — already on every
  distribution — supervise the processes directly. No NSSM-equivalent to download, trust or
  update.
- **Not Docker.** Same reasoning the Windows runbook gives: `ResolveSharedConfigPath()` walks
  *up* the directory tree from the content root looking for `config/appsettings.global.json`,
  which does not exist in a from-scratch container image without a volume mount recreating it.
  That is packaging work with no payoff for four processes on one host.
- **`Type=notify`, not the default `Type=simple`.** systemd only sets `NOTIFY_SOCKET` for
  notify-type units, and `UseSystemd()` only detects systemd through that variable — so a
  `simple` unit would leave both the readiness signal and the content-root anchoring inactive.
  With `Type=notify` the host reports `READY=1` as soon as it has started, and a process that
  dies during construction is seen as failed rather than "active".

## Layout

The deployment root is `/opt/tallaegg`, hard-coded in the unit files exactly the way
`C:\TallaEgg` is hard-coded in the Windows scripts:

```
/opt/tallaegg/
  config/
    appsettings.global.json     <- every service reads this (walks up to it from its folder)
    tallaegg.env                <- the inter-service API key; root:root, mode 600,
                                   written by install-services.sh
  publish/
    Wallet.Api/                 <- framework-dependent output of publish-all.sh;
    Users.Api/                     each folder holds its binary and its own logs/ directory
    Orders.Api/
    Bot/
```

The walk that finds the shared config starts at the binary's folder
(`AppContext.BaseDirectory`, which `UseSystemd()` points the content root at under systemd,
backed up by each unit's `WorkingDirectory=`) and goes up: `publish/<Service>/config/` —
absent; `publish/config/` — absent; `/opt/tallaegg/config/appsettings.global.json` — found
(issues [#212](https://github.com/MohKardan/TallaEgg/issues/212) and
[#211](https://github.com/MohKardan/TallaEgg/issues/211)). Replacing a service folder never
touches the configuration, which is the reason the layout is shaped this way.

## One-time setup

1. **Install the ASP.NET Core 9 runtime.** On Ubuntu 24.04 it is in the distribution's own
   feed:
   ```bash
   sudo apt-get update && sudo apt-get install -y aspnetcore-runtime-9.0
   ```
   On older LTS releases, follow Microsoft's [Linux install
   documentation](https://learn.microsoft.com/en-us/dotnet/core/install/linux) for the package
   feed to add. What matters is that `/usr/bin/dotnet` exists — that is the path every unit
   file's `ExecStart` names. A runtime installed elsewhere (the `dotnet-install.sh` default of
   `$HOME/.dotnet`, say) needs either a symlink at `/usr/bin/dotnet` or a drop-in overriding
   `ExecStart` in each unit. To build on the server rather than only run release artifacts,
   install `dotnet-sdk-9.0` instead.

2. **Install SQL Server, or point at a remote one.** The connection strings live in
   `config/appsettings.global.json` either way — SQL authentication
   (`User Id=sa;Password=...`), port 1433, the instance nothing in the units reconciles with
   them (the same pairing rule the Windows runbook states). For a local instance:
   ```bash
   sudo apt-get install -y mssql-server
   sudo /opt/mssql/bin/mssql-conf setup
   ```
   The package installs a unit named `mssql-server.service`. The three API units declare
   `Wants=/After=` on it, so it starts before them; on a host whose database is remote the
   unit does not exist and the directives are ignored with a warning at `daemon-reload` —
   which is exactly why they are soft dependencies and not `Requires=`: a requirement on a
   unit that is not there refuses every start.

3. **Create the deployment root and the configuration.**
   ```bash
   sudo mkdir -p /opt/tallaegg/config
   sudo cp config/appsettings.global.example.json /opt/tallaegg/config/appsettings.global.json
   sudoedit /opt/tallaegg/config/appsettings.global.json
   ```
   Bind addresses stay `http://localhost:<port>` (see the README's Ports and bind addresses
   section, #69); nothing here should listen on a public interface.

4. **Publish the four services** — from a release (preferred, see
   [Redeploying](#redeploying-a-new-version)) or by building on the server:
   ```bash
   sudo ./scripts/linux/publish-all.sh                  # -> /opt/tallaegg/publish/<Service>
   ```

5. **Install the services:**
   ```bash
   sudo ./scripts/linux/install-services.sh
   ```
   This verifies the layout (config file, all four entry assemblies), prompts for the shared
   API key, then:
   - creates the dedicated unprivileged account `tallaegg` (system account, no shell, no
     sudo, home at the deployment root) and takes ownership of `/opt/tallaegg/publish` — the
     services write their logs beside their binaries, so the output must belong to them;
   - makes the shared configuration group-readable by the service group alone (root-owned,
     mode 640 — it carries the SQL credentials and the bot token);
   - writes the API key into `/opt/tallaegg/config/tallaegg.env`, root-owned and mode 600.
     **The key never appears in a unit file**: units in `/etc/systemd/system` are
     world-readable, and systemd reads `EnvironmentFile=` itself before dropping privileges,
     so the key is readable by no service account;
   - copies the four units into `/etc/systemd/system/`, enables them for boot, and starts
     them in dependency order — Wallet first, the bot last.

   Re-running the installer is safe and is the Linux equivalent of the Windows redeploy step:
   it overwrites the units, reloads systemd, and restarts every service that is already
   running so unit-file changes take effect.

## Which build is running (issue #218)

Same as on Windows:

- **In the log**, as the first line each service writes at startup — the only answer
  available for a service that crashes before it can serve anything:
  ```bash
  journalctl -u tallaegg-users -n 20
  # Starting Users.Api, version 1.6.0, built from commit ff95e00a327536efa53e2af247b661ba9be5f744.
  ```
- **Over HTTP**, from the three APIs. `GET /version` carries no exemption from the Production
  authorization policy, so it needs the API key like every other endpoint:
  ```bash
  curl -s http://localhost:60933/version -H "X-API-Key: $TALLAEGG_API_KEY"
  # -> {"data":{"version":"1.6.0","commitHash":"ff95e00..."}}
  ```
  Ports are in the README's Ports and bind addresses section. The bot serves nothing, so its
  log line is the whole of its answer.

The hash names the commit checked out on the machine that published the build — for release
artifacts, the GitHub runner — and says nothing about the server. `commitHash` is `null` when
that build could not see a `.git` directory.

## Redeploying a new version

Two ways in. Prefer the first: it installs the same bytes for everyone and keeps `/version`
able to name the commit.

### From a published release

Publishing a release runs the `release-artifacts` workflow, whose `publish-linux` job builds
the four services on an Ubuntu runner through the same `publish-all.sh` an operator runs by
hand, verifies the assemblies carry the tagged version, refuses to package an
`appsettings.global.json`, and attaches one tarball per service:
`<Service>-<tag>-linux-x64.tar.gz`.

**Stop bot-first, start bot-last.** Unlike the Windows services, these units do not declare
`Requires=` on each other, so `systemctl stop` of one service does not cascade and systemd
will not refuse a stop the way `sc.exe` does with error 1051 — the order below is discipline
for a clean shutdown, not something the service manager enforces. Alphabetical order is not
dependency order.

```bash
tag=v1.6.1
order=(Wallet.Api Users.Api Orders.Api Bot)   # dependency order: starting reads down

# Download everything before stopping anything, so a network failure costs no downtime.
for svc in "${order[@]}"; do
    curl -fL -o "/tmp/$svc-$tag-linux-x64.tar.gz" \
        "https://github.com/MohKardan/TallaEgg/releases/download/$tag/$svc-$tag-linux-x64.tar.gz"
done

sudo systemctl stop tallaegg-bot tallaegg-orders tallaegg-users tallaegg-wallet
# Wait for all four to report inactive before continuing: a running process holds its own DLLs.
systemctl list-units 'tallaegg-*'

for svc in "${order[@]}"; do
    live="/opt/tallaegg/publish/$svc"
    sudo rm -rf "$live.bak"
    sudo mv "$live" "$live.bak"                 # the old build, kept to roll back to
    sudo mkdir -p "$live"
    sudo tar -xzf "/tmp/$svc-$tag-linux-x64.tar.gz" -C "$live"
    sudo chown -R tallaegg:tallaegg "$live"     # untarring as root leaves root-owned files,
                                                # and the service writes logs beside its binary
done

sudo systemctl start tallaegg-wallet tallaegg-users tallaegg-orders tallaegg-bot
```

Expanding into a fresh folder rather than over the old one is what stops a file deleted
between releases from lingering; the renamed `.bak` beside it is the rollback — swap the two
folders back and restart.

**`install-services.sh` does not need to run for this.** It exists to create or reconfigure
the units, and a redeploy does neither: stopping a service, replacing its files and starting
it again is what picks up new binaries. Run it when a unit definition changes — a new
dependency, a rotated API key — which is also the only time the key is needed.

### Building on the server

```bash
sudo ./scripts/linux/publish-all.sh
sudo ./scripts/linux/install-services.sh       # re-runs restart each running service
```

## Start order — what's covered and what isn't

Two orderings decide whether this deployment comes back after a reboot, and both are declared
in the unit files themselves (the Windows installer had to set them per service with
`sc.exe depend=`; systemd takes them from the units).

### Against the database

`tallaegg-wallet`, `tallaegg-users` and `tallaegg-orders` each declare
`Wants=mssql-server.service` and `After=mssql-server.service`. `After=` orders their start
behind SQL Server's when both are being started together — at boot, and whenever an operator
starts them on a stopped host. It is an ordering dependency, not a readiness promise:
"started" for `mssql-server.service` means its process launched, and SQL Server can take well
over a minute after that before it accepts connections. The bot names no database unit at
all — it opens no database of its own.

### Against each other

`tallaegg-users` and `tallaegg-orders` call `Wallet.Api` on startup paths, so both order
themselves after `tallaegg-wallet`, and the bot orders itself after all three. Every one of
these is `Wants=/After=` rather than `Requires=` — see the
[unit dependencies](#why-wantsafter-everywhere-and-not-requires) note below.

### What neither ordering buys, and what waits it out anyway

*Running* means the dependency's process started. Nothing more — for SQL Server it does not
mean the instance accepts connections yet, and for an API it does not mean its own migration
has finished. The dependency narrows the window; it does not close it. What closes it is
application code, the same on both platforms: since
[#230](https://github.com/MohKardan/TallaEgg/issues/230) the three APIs migrate from a hosted
service *after* the host has started, so they report started promptly and retry a database
that is not answering yet — ten attempts, backoff doubling from five seconds to a minute,
every request answered `503` by the readiness gate until it succeeds, `GET /version` the one
exception. See the Windows runbook's section on this for the log lines; they are identical
here. **A `503` from these services during the first minute after a boot is the expected
shape of a slow database, not a fault.**

`Type=notify` deserves a word here: the host reports `READY=1` when the host has *started*,
not when the migration has finished — the migration deliberately yields first (issue #230's
whole point was to leave the pre-start window). So systemd's view of "ready" is the same
honest statement the SCM's view of "Running" is on Windows, and the same application-level
retry is what actually waits for the database.

### Why `Wants=`/`After=` everywhere, and not `Requires=`

A `Requires=` on a unit that does not exist refuses every start of the requiring unit — which
is what a remote-database host would hit. `Wants=`/`After=` against a missing unit are
ignored with a warning. The second reason is operational: stopping a unit that others merely
*want* stops only that unit, so a targeted `systemctl restart tallaegg-wallet` does not take
the rest of the stack with it. The Windows SCM enforces its stop ordering from the other side
(error 1051 on stopping a service others depend on); here the runbook's stop-bot-first
discipline does that job instead.

### Reboot survival and crash recovery

All four units are enabled (`WantedBy=multi-user.target`), so boot starts them, ordered as
above. Each unit carries:

```ini
Restart=always
RestartSec=10s
```

`always`, not `on-failure`, for a reason specific to this application: a service whose
migration gave up after ten attempts logs the failure at `Fatal` and **stops itself with a
clean exit** (`DatabaseMigrationHostedService`). `on-failure` would leave it down; `always`
brings it back after ten seconds to try again — which is the behaviour you want when the
database was rebooting and came back late. An operator's explicit `systemctl stop` is not a
crash and is never fought by the restart.

## Logs

Two independent places, like on Windows:

- **journald**, from each service's console sink:
  ```bash
  journalctl -u tallaegg-wallet -f          # follow one service
  journalctl -u 'tallaegg-*' -f             # follow the whole stack
  ```
- **The Serilog rolling file** each service writes into a `logs/` folder *beside its own
  binary*:
  ```
  /opt/tallaegg/publish/Wallet.Api/logs/wallet-api-<date>.log
  /opt/tallaegg/publish/Users.Api/logs/users-api-<date>.log
  /opt/tallaegg/publish/Orders.Api/logs/orders-api-<date>.log
  /opt/tallaegg/publish/Bot/logs/telegrambot-<date>.log
  ```

Retention is capped at 30 files (`retainedFileCountLimit: 30` — issue #70's log-rotation
item). The file sink's path is anchored at `AppContext.BaseDirectory` by
`StartupLogging.LogFilePath` (issue #211's fix), so it follows the binary rather than the
working directory — which is why a unit's `WorkingDirectory=` is belt-and-suspenders rather
than load-bearing for logs. `tail -f` on the relevant file is the fastest way to watch a
service live.

The file sink is also the reason `/opt/tallaegg/publish` must belong to the `tallaegg`
account: a service that cannot create its `logs/` directory loses its file sink, and
everything it would have written exists only in journald — diagnostics go to `ILogger` and
nowhere else, so the journal would still hold the lines, but the 30-day rolling history the
retention cap promises would not.

## Removing the services

```bash
sudo ./scripts/linux/uninstall-services.sh
```

Stops and disables all four, deletes the unit files and the API-key environment file (the
same thing the Windows uninstaller deletes with the service). Published files, the shared
configuration, the logs, and the database are untouched. The `tallaegg` account is left in
place; a reinstall reuses it.

## Verification checklist (first deployment on a real host)

The units and scripts in this repository have not yet been through a real systemd boot — the
first deployment is their test. Run, in order:

1. **Before starting anything**, ask systemd whether the units parse:
   ```bash
   systemd-analyze verify /etc/systemd/system/tallaegg-*.service
   ```
   Silence is pass. A typo in a directive name is reported here and nowhere else.
2. Install and start: `sudo ./scripts/linux/install-services.sh`, then
   `systemctl status tallaegg-*` — all four `active (running)`, Wallet first in the log
   ordering.
3. **Read the startup sequence** for one API:
   ```bash
   journalctl -u tallaegg-wallet -n 50
   ```
   Expect the version line, `Application started.`, and `Database migration succeeded on
   attempt 1 of 10.` A `503` for the first seconds is the readiness gate, not a fault.
4. **Configuration walk**: confirm the service found the shared file by stopping it and
   moving the config — `sudo mv /opt/tallaegg/config/appsettings.global.json /opt/tallaegg/config/appsettings.global.json.off`
   — starting Wallet, and reading the `FileNotFoundException` naming the walk in
   `journalctl -u tallaegg-wallet`. Then restore the file. Doing this once on purpose is
   cheaper than discovering it during an outage.
5. **`GET /version`** for each API with the API key (see above).
6. **Reboot survival**: `sudo reboot`, log back in, `systemctl status tallaegg-*` — all four
   active. Expect `503`s while SQL Server warms up; that is the documented shape.
7. **Crash recovery**:
   ```bash
   sudo systemctl kill --signal=SIGKILL tallaegg-orders
   sleep 12 && systemctl is-active tallaegg-orders    # -> active, restarted after 10s
   ```
8. **Logs**: confirm `/opt/tallaegg/publish/<Service>/logs/` exists and is growing, owned by
   `tallaegg`.
9. **Exposure review**: `systemd-analyze security tallaegg-*`. The shipped hardening is
   deliberately conservative (see each unit's comment — and note that
   `MemoryDenyWriteExecute=true` is *not* set, because the .NET JIT needs W^X and dies under
   it); tighten further only with a measurement on this host.

## Not covered here

- A liveness signal (the running bot proactively reporting a restart) was noted in #70 as
  worth doing but not required — it is still open, and the same note applies to this
  deployment.
- The translated READMEs still describe the Windows-only deployment; only the English README
  points at this document.
- Container images, SELinux/AppArmor profiles beyond the units' built-in hardening, and
  orchestration beyond one host: none of this is built, for the same reason the Windows
  deployment never reached for Docker.
