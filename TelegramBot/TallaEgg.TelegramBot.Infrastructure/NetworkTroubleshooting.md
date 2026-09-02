# Network Troubleshooting — reaching Telegram

The bot reaches Telegram by long polling: it dials **out** to `https://api.telegram.org` and
Telegram never dials in. Every connectivity problem here is outbound, so no inbound port or
firewall rule is part of the fix. (`AGENT.md` explains why nothing listens on the bot's
configured port.)

## What the bot actually does

`ProxyBotClient.CreateWithProxy` takes one of four paths:

| Condition | What you get | Timeout |
|---|---|---|
| `BOT_DIRECT_CONNECTION=1` | `HttpClient` on a handler with `UseProxy = false` | 120 s |
| A system proxy applies | `HttpClient` with `Proxy` set explicitly from `WebRequest.GetSystemWebProxy()` | 120 s |
| No system proxy applies | `HttpClient` on the stock handler | 120 s |
| Anything above throws | `HttpClient` on the stock handler | 120 s |

The 120 s matters because `getUpdates` is a long poll and the .NET default of 100 s is too tight
for it. Every row gets it now, the exception fallback included: that row used to supply no
`HttpClient` at all and silently take the 100 s default, which moved the down-alert threshold
with it, because `TelegramBotHostedService` derives its polling-recovery gap from
`_botClient.Timeout` (#199).

### Reading the console output

One line is printed, and it means what it says. Until #199 two of the three did not, so output
from a build older than that fix cannot be read this way.

- **`🔗 Direct connection (proxy bypassed via BOT_DIRECT_CONNECTION=1)`** — the handler has
  `UseProxy = false`, so nothing the bot sends is proxied: not the system proxy, not one
  configured elsewhere in .NET. Before #199 this line printed over a stock handler, which on
  Windows leaves `UseProxy = true` with a null `Proxy` and so falls through to
  `HttpClient.DefaultProxy` — the same WinInet settings the proxy path reads. It proxied, and
  said it did not.
- **`🔧 Using proxy: <uri>`** — a system proxy applies to `api.telegram.org` and the client is
  bound to it. Printed only in that case; it used to be printed unconditionally, *before* the
  check that decides whether a proxy is in play.
- **`🔧 No system proxy applies to api.telegram.org`** — `GetProxy` answered with the destination
  itself, or with null. Both mean no proxy. The first used to surface as
  `🔧 Using proxy: https://api.telegram.org/`, which says *no proxy* and reads as its opposite;
  the second took the proxy branch outright and printed `🔧 Using proxy: ` with nothing after it.

## The failure this file exists for

**Symptom**: the bot starts, then dies on `getUpdates` with *"The response ended prematurely"*, or
hangs and never receives an update.

**Cause seen in practice**: the machine can reach Telegram directly — a TUN-mode VPN, say — while
a system HTTP proxy is still configured. The proxy is fine for short requests and unstable on a
long poll, so the connection drops mid-request.

**First thing to try** (scoped to one process; leave it unset on the server, which relies on the
proxy path):

```powershell
dotnet build TallaEgg.sln
$env:BOT_DIRECT_CONNECTION = "1"
dotnet run --no-build --project TelegramBot/TallaEgg.TelegramBot.Infrastructure/TallaEgg.TelegramBot.Infrastructure.csproj
```

The build is not optional — `dotnet run --no-build` against a stale `bin` runs code you have
already changed, which here would mean running a binary that predates the flag and concluding it
does not work.

Two outcomes, and they mean different things:

- **The drops continue.** The proxy is not what was dropping them — with the flag set the bot's
  client does not use one. Look at the VPN and the direct route instead. Note that the flag covers
  the bot's Telegram client only; the startup diagnostics build their own `HttpClient`, and
  everything else on the machine still follows the system proxy.
- **The bot stops connecting entirely**, with `No connection could be made because the target machine
  actively refused it. (api.telegram.org:443)` or a timeout. The machine cannot reach Telegram
  directly after all, so the proxy was doing real work. Unset the flag; the fix is elsewhere.

## When nothing connects at all

Check that you can reach Telegram at all. `Invoke-WebRequest` **throws** on any non-2xx in
Windows PowerShell 5.1, so a success and a failure both print red; use `curl.exe` and read the
bare status code instead:

```powershell
curl.exe -s -o NUL -w "%{http_code}`n" https://api.telegram.org/bot0:0/getMe
```

- **`401`** — you reached Telegram. It rejected an invalid token, which is the expected answer.
  Connectivity is fine and the problem is elsewhere.
- **A timeout, DNS failure, or `000`** — the real thing this file is about.
- **`404`, an HTML body, or a redirect** — do **not** read this as success. A transparent proxy,
  corporate blocker or captive portal answers that way for a host it is intercepting. Only the
  `401`, with a body of `{"ok":false,"error_code":401,...}`, proves you are talking to Telegram.

### Finding the proxy .NET is actually using

**`netsh winhttp show proxy` is the wrong store.** `HttpClient` and
`WebRequest.GetSystemWebProxy()` read **WinInet** (per-user Internet Settings); `netsh winhttp`
reads WinHTTP. A machine whose VPN client set a local proxy typically reports *"Direct access (no
proxy server)"* from `netsh winhttp` while .NET proxies every request. Read WinInet instead:

```powershell
Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' |
    Select-Object ProxyEnable, ProxyServer, AutoConfigURL
```

That is also the setting to clear — through the Windows proxy settings UI, or by setting
`ProxyEnable` to `0` — if you want the machine off the proxy entirely. `netsh winhttp reset
proxy` will not clear it.

Beyond that: disconnect the VPN and retry (a split-tunnel VPN excluding `api.telegram.org` looks
exactly like an outage), and confirm outbound HTTPS from `dotnet.exe` is not blocked by a
firewall or antivirus.

## What is not the problem

- **Inbound ports.** The bot runs as a plain generic host with no web server — see `AGENT.md`.
  Opening a port fixes nothing.
- **The bot token**, when the failure is a timeout rather than a `401`. A bad token is rejected
  fast and clearly; a network problem hangs.
