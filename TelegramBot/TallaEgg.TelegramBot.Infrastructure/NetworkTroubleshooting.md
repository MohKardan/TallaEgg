# Network Troubleshooting — reaching Telegram

The bot reaches Telegram by long polling: it dials **out** to `https://api.telegram.org` and
Telegram never dials in. Every connectivity problem here is outbound, so no inbound port or
firewall rule is part of the fix. (`AGENT.md` explains why nothing listens on the bot's
configured port.)

## What the bot actually does

`ProxyBotClient.CreateWithProxy` takes one of four paths:

| Condition | What you get | Timeout |
|---|---|---|
| `BOT_DIRECT_CONNECTION=1` | `HttpClient` with the **default** handler | 120 s |
| A system proxy applies | `HttpClient` with `Proxy` set explicitly from `WebRequest.GetSystemWebProxy()` | 120 s |
| No system proxy applies | `HttpClient` with the default handler | 120 s |
| Anything above throws | Bare `TelegramBotClient(token)`, no `HttpClient` supplied | **100 s** |

The 120 s matters because `getUpdates` is a long poll and the .NET default of 100 s is too tight
for it. Note the last row: the exception fallback drops to that 100 s default, and
`TelegramBotHostedService` derives its polling-recovery gap from `_botClient.Timeout`, so a
silent fall onto that path shifts the down-alert threshold too.

### Two things the console output will not tell you

**`🔧 Using proxy: …` is printed unconditionally**, on line 29, *before* the check on line 31
that decides whether a proxy is actually in play. When no proxy applies, `GetProxy` returns the
destination itself and you get `🔧 Using proxy: https://api.telegram.org/` — which means *no
proxy*. Do not read that line as evidence of one.

**`BOT_DIRECT_CONNECTION=1` does not disable proxying.** It prints
`🔗 Direct connection (proxy bypassed via BOT_DIRECT_CONNECTION=1)` and constructs
`new HttpClient { Timeout = … }` — the default `HttpClientHandler`, which on Windows has
`UseProxy = true` and `Proxy = null`, so it falls through to `HttpClient.DefaultProxy` and reads
the same WinInet settings. Nothing sets `UseProxy = false`.

What the flag really changes is *how* the proxy is resolved — `DefaultProxy` instead of an
explicit `WebRequest.GetSystemWebProxy()` handler — which is sometimes enough to shake off a
misbehaving handler, and sometimes not. **Treat it as worth trying, not as a guaranteed bypass.**
A real bypass needs `new HttpClient(new HttpClientHandler { UseProxy = false })`; that gap is
tracked as an issue, not fixed here.

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

If the drops continue, the proxy is still in the path — see the note above — and the next step is
to clear it machine-wide or disconnect the VPN.

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
