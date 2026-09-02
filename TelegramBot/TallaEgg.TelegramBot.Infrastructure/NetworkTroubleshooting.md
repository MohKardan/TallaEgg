# Network Troubleshooting — reaching Telegram

The bot reaches Telegram by long polling: it dials **out** to `https://api.telegram.org` and
Telegram never dials in. So every connectivity problem here is outbound, and no inbound port or
firewall rule is part of the fix.

## How the bot chooses a connection

`ProxyBotClient.CreateWithProxy` decides, in this order:

1. **`BOT_DIRECT_CONNECTION=1`** in the environment → connect directly, ignoring the system
   proxy entirely. Prints `🔗 Direct connection (proxy bypassed via BOT_DIRECT_CONNECTION=1)`.
2. Otherwise it asks Windows for the system proxy for `api.telegram.org`. If one is configured,
   it connects through it and prints `🔧 Using proxy: …`.
3. If no proxy applies, it connects directly.
4. If any of that throws, it falls back to a plain direct client and says so.

Every path uses a **120-second timeout**, because `getUpdates` is a long poll and the default
100 seconds is too tight for it.

## The failure this exists for

**Symptom**: the bot starts, then dies on `getUpdates` with *"The response ended prematurely"*, or
hangs and never receives an update.

**Cause seen in practice**: the machine can reach Telegram directly — a TUN-mode VPN, say — but a
system HTTP proxy is still configured. The proxy is fine for short requests and unstable on a
long poll, so the connection drops mid-request.

**Fix**: bypass the proxy for this process only.

```powershell
$env:BOT_DIRECT_CONNECTION = "1"
dotnet run --no-build --project TelegramBot/TallaEgg.TelegramBot.Infrastructure/TallaEgg.TelegramBot.Infrastructure.csproj
```

Leave it unset on the server, which relies on the proxy path.

## When nothing connects at all

If the machine genuinely cannot reach Telegram, `BOT_DIRECT_CONNECTION` will not help. Check
outward connectivity first, then the proxy:

```powershell
Invoke-WebRequest -Uri "https://api.telegram.org" -UseBasicParsing   # expect 401/404, not a timeout
netsh winhttp show proxy                                            # what Windows thinks the proxy is
```

A 401 or 404 from `api.telegram.org` is **success** — it means you reached Telegram and it
rejected a request with no token. A timeout or DNS failure is the real problem.

From there:

- **VPN**: disconnect and retry. A split-tunnel VPN that excludes `api.telegram.org` looks
  exactly like an outage.
- **System proxy is wrong or dead**: `netsh winhttp reset proxy` clears it, or set a working one
  with `netsh winhttp set proxy proxy-server="host:port"`. Both are machine-wide — prefer
  `BOT_DIRECT_CONNECTION=1` for a one-off.
- **Firewall or antivirus**: confirm outbound HTTPS from `dotnet.exe` is allowed.

## What is not the problem

- **Inbound ports.** The bot has a `Urls` entry (57546) but runs as a plain generic host with no
  web server, so nothing listens on it. Opening a port fixes nothing.
- **The bot token**, if the failure is a timeout rather than a `401 Unauthorized`. A bad token
  gets a fast, clear rejection; a network problem hangs.
