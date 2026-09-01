<#
.SYNOPSIS
  Build, launch, and drive the TallaEgg backend (Users/Wallet/Orders APIs) end-to-end,
  without a live Telegram connection.

.DESCRIPTION
  See SKILL.md in this same directory for the full explanation. Short version:

    & driver.ps1 start                                  # build (Release) + launch the 3 APIs, wait for health
    & driver.ps1 status                                 # show whether the 3 ports are listening
    & driver.ps1 smoke                                  # drive the whole stack via the bot Simulator (small run)
    & driver.ps1 smoke --users 20 --trades 50           # same, overriding only the knobs you name
    & driver.ps1 stop                                   # stop whatever is listening on the 3 ports

  This box has Windows PowerShell 5.1 and no PowerShell Core, so invoke the script directly
  with `&` — `pwsh -File driver.ps1 ...` fails with "term not recognized".

  "smoke" is the actual interaction: it runs TallaEgg.TelegramBot.Simulator, which replays
  real bot conversations (registration, admin approval, quote publishing, quote-fill trades,
  menu navigation) through the real IBotHandler and the real Users/Wallet/Orders APIs and
  database — everything except the live Telegram connection, which it fakes.

  "smoke" also waits for settlement before it reports anything. The Simulator's summary line is
  printed when the trading phase ends, but settlement is queued through the Orders outbox and
  drains tens of seconds later, so that line alone is not a result (issue #175). After the run
  the driver polls until no outbox message is Pending, and fails if the number of permanently
  Failed messages went up.

  Two things it changes on the database it runs against:
    * Rows with TelegramId < 0 — created, then wiped by the next run's DataReset phase. A
      real (positive-id) user's data is never touched.
    * The auto-quote enabled flag for MAUA/IRT — the Simulator turns it off in its Phase 2 and
      never turns it back on, because a background publisher replacing the run's quotes breaks
      quote-fill trades. That is per-symbol Orders-DB state, not TelegramId-scoped, so
      DataReset does not restore it. This script reads the flag before the run and puts it
      back afterwards; see Restore-AutoQuote below.
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('start', 'status', 'smoke', 'stop')]
    [string]$Command,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path

$ports = [ordered]@{ users = 5136; wallet = 60933; orders = 5140 }
$projects = @{
    users  = 'src/User/Users.Api/Users.Api.csproj'
    wallet = 'src/Wallet/Wallet.Api/Wallet.Api.csproj'
    orders = 'src/Order/Orders.Api/Orders.Api.csproj'
}
$logDir = Join-Path $repoRoot 'run-logs'
$pidFile = Join-Path $logDir 'launcher.pids'

# The symbol the Simulator trades, and whose auto-quote flag it turns off (Simulation.cs).
$smokeSymbol = 'MAUA/IRT'

function Test-ServicesUp {
    foreach ($name in $ports.Keys) {
        try {
            $r = Invoke-WebRequest -UseBasicParsing -TimeoutSec 2 -Uri "http://localhost:$($ports[$name])/api-docs/index.html"
            if ($r.StatusCode -ne 200) { return $false }
        } catch { return $false }
    }
    return $true
}

function Get-ListeningPorts {
    $busy = @()
    foreach ($name in $ports.Keys) {
        if (Get-NetTCPConnection -LocalPort $ports[$name] -State Listen -ErrorAction SilentlyContinue) {
            $busy += "$name ($($ports[$name]))"
        }
    }
    return $busy
}

function Get-OrdersConnectionString {
    # OutboxMessages lives in the Orders database. Every service reads one shared config file
    # (AGENT.md — Configuration) and ConnectionStrings sits at its root, not under a service
    # section.
    $configPath = Join-Path $repoRoot 'config/appsettings.global.json'
    if (-not (Test-Path $configPath)) {
        throw "config/appsettings.global.json is missing, so the settlement check cannot reach the Orders database. Copy config/appsettings.global.example.json to that path (see SKILL.md Prerequisites)."
    }

    # ReadAllText rather than Get-Content: the file is UTF-8 with a BOM, and in Windows
    # PowerShell 5.1 the BOM survives Get-Content and makes ConvertFrom-Json fail on it.
    $config = [System.IO.File]::ReadAllText($configPath) | ConvertFrom-Json
    $connectionString = $config.ConnectionStrings.OrdersDb
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw "ConnectionStrings:OrdersDb is not set in config/appsettings.global.json."
    }
    return $connectionString
}

function Get-OutboxCount {
    # Status values are OutboxMessageStatus in src/Order/Orders.Core/OutboxMessage.cs:
    # 0 Pending, 1 Completed, 2 Failed, 3 Abandoned.
    param(
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][int]$Status
    )

    $conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = 'SELECT COUNT(*) FROM OutboxMessages WHERE Status = @status'
        $null = $cmd.Parameters.AddWithValue('@status', $Status)
        return [int]$cmd.ExecuteScalar()
    }
    finally {
        $conn.Dispose()
    }
}

function Wait-OutboxDrained {
    # Waits until nothing is queued or mid-dispatch. A message stays Pending for its whole
    # working life — while an instance holds its lease and the wallet call is in flight, and
    # while it sits between retries — so zero Pending is the point at which the run really is
    # over. 300 x 2s = 10 minutes, the same bound DataReset's own wait uses.
    param([Parameter(Mandatory = $true)][string]$ConnectionString)

    $pending = Get-OutboxCount -ConnectionString $ConnectionString -Status 0
    for ($i = 0; $i -lt 300 -and $pending -gt 0; $i++) {
        if ($i % 5 -eq 0) {
            Write-Host "Waiting for settlement: $pending outbox message(s) still pending..."
        }
        Start-Sleep -Seconds 2
        $pending = Get-OutboxCount -ConnectionString $ConnectionString -Status 0
    }

    if ($pending -gt 0) {
        throw "Settlement did not finish: $pending outbox message(s) are still Pending after 10 minutes. Inspect them at http://localhost:$($ports['orders'])/api/outbox/unsettled."
    }
}

function Get-AutoQuoteEnabled {
    # Returns $true/$false, or $null if the setting could not be read (caller then skips restore
    # rather than guessing — writing the wrong value back is worse than leaving it alone).
    try {
        $r = Invoke-RestMethod -TimeoutSec 10 -Uri "http://localhost:$($ports['orders'])/api/autoquote-settings/$smokeSymbol"
        return [bool]$r.Data.IsEnabled
    } catch {
        Write-Warning "Could not read auto-quote setting for ${smokeSymbol}: $($_.Exception.Message)"
        return $null
    }
}

function Restore-AutoQuote {
    param([bool]$Enabled)

    try {
        # Guid.Empty as the updater: this is a dev-tooling restore, not an admin action, and
        # AutoQuoteSettings.CreateDefault already uses Guid.Empty as its no-real-user sentinel.
        $body = @{ IsEnabled = $Enabled; UpdatedByUserId = [guid]::Empty.ToString() } | ConvertTo-Json
        Invoke-RestMethod -Method Post -TimeoutSec 10 -ContentType 'application/json' -Body $body `
            -Uri "http://localhost:$($ports['orders'])/api/autoquote-settings/$smokeSymbol/enabled" | Out-Null
        Write-Host "Restored auto-quote for $smokeSymbol to IsEnabled=$Enabled."
    } catch {
        Write-Warning "Failed to restore auto-quote for ${smokeSymbol} to IsEnabled=${Enabled}: $($_.Exception.Message)"
        Write-Warning "Turn it back on from the bot with the admin command 'اتومات روشن' if it was on before."
    }
}

Push-Location $repoRoot
try {

switch ($Command) {
    'start' {
        $configPath = Join-Path $repoRoot 'config/appsettings.global.json'
        if (-not (Test-Path $configPath)) {
            throw "config/appsettings.global.json is missing. Copy config/appsettings.global.example.json to that path and point ConnectionStrings at a local SQL Server (see SKILL.md Prerequisites)."
        }

        # Without this, a second 'start' launches three processes that each die on "address
        # already in use" while Test-ServicesUp answers 200 from the processes already running
        # — reporting success for a stack that is not running the build we just made.
        $busy = Get-ListeningPorts
        if ($busy.Count -gt 0) {
            throw "Already listening: $($busy -join ', '). Run 'driver.ps1 stop' first, or 'driver.ps1 status' to see what owns them."
        }

        New-Item -ItemType Directory -Force -Path $logDir | Out-Null

        Write-Host "Building (Release)..."
        dotnet build TallaEgg.sln --configuration Release | Out-Host
        # A native command's nonzero exit does not throw in Windows PowerShell 5.1, not even
        # under $ErrorActionPreference = 'Stop'. Unchecked, a failed build falls straight
        # through to 'dotnet run --no-build' and silently serves the previous build.
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed (exit $LASTEXITCODE). Fix the build before starting the services."
        }

        $procIds = @()
        foreach ($name in @('users', 'wallet', 'orders')) {
            Write-Host "Starting $name ($($projects[$name]))..."
            $p = Start-Process dotnet -ArgumentList @('run', '--no-build', '--configuration', 'Release', '--project', $projects[$name]) `
                -RedirectStandardOutput (Join-Path $logDir "$name.out.log") `
                -RedirectStandardError (Join-Path $logDir "$name.err.log") `
                -PassThru -WindowStyle Hidden
            $procIds += $p.Id
            if ($name -eq 'users') {
                # Orders.Api's own startup calls Wallet/Users; giving Users a head start avoids
                # a burst of connection-refused warnings in its log (harmless, but noisy).
                Start-Sleep -Seconds 3
            }
        }
        $procIds | Set-Content $pidFile

        Write-Host "Waiting for services to come up..."
        for ($i = 0; $i -lt 30; $i++) {
            if (Test-ServicesUp) {
                Write-Host "All services up after $($i * 2)s."
                return
            }
            Start-Sleep -Seconds 2
        }

        # Both streams: all three APIs configure Serilog .WriteTo.Console() with no
        # standardErrorFromLevel, so errors land in *.out.log, not *.err.log.
        Write-Host "Services did not come up in time. Recent output:"
        Get-ChildItem $logDir -Filter '*.log' |
            Where-Object { $_.Name -ne 'smoke.log' } |
            Sort-Object Name |
            ForEach-Object {
                Write-Host "--- $($_.Name) ---"
                Get-Content $_.FullName -Tail 40
            }
        Write-Host "The services launched above are still running; 'driver.ps1 stop' cleans them up."
        throw "Timed out waiting for Users/Wallet/Orders APIs to report healthy. If all three are in fact serving, check that ASPNETCORE_ENVIRONMENT is Development — the /api-docs probe only exists there."
    }

    'status' {
        foreach ($name in $ports.Keys) {
            $conn = Get-NetTCPConnection -LocalPort $ports[$name] -State Listen -ErrorAction SilentlyContinue
            if ($conn) {
                $ownerPid = ($conn | Select-Object -First 1 -ExpandProperty OwningProcess)
                "{0,-8} port {1,-6} LISTENING (pid $ownerPid)" -f $name, $ports[$name]
            }
            else {
                "{0,-8} port {1,-6} down" -f $name, $ports[$name]
            }
        }
    }

    'smoke' {
        if (-not (Test-ServicesUp)) {
            throw "Users/Wallet/Orders are not all up. Run 'driver.ps1 start' first."
        }

        # Merge over the defaults rather than replacing them: passing $Rest straight through
        # would drop every knob the caller did not name, and SimulationOptions.FromArgs then
        # falls back to its own compiled defaults (100 users / 120 quotes / 1000 trades) —
        # so 'smoke --seed 7' would quietly become a hundred-fold bigger run.
        $simOptions = [ordered]@{ '--users' = '5'; '--quotes' = '5'; '--trades' = '10'; '--seed' = '1' }
        for ($i = 0; $i -lt $Rest.Count; $i++) {
            $key = $Rest[$i]
            if (-not $simOptions.Contains($key)) {
                throw "Unknown simulator argument '$key'. Supported: $($simOptions.Keys -join ', ')."
            }
            if ($i + 1 -ge $Rest.Count) {
                throw "Simulator argument '$key' has no value."
            }
            $simOptions[$key] = $Rest[++$i]
        }
        if ([int]$simOptions['--users'] -lt 2) {
            # User #0 is promoted to admin and is the counterparty to every fill, so it can
            # never trade with itself; with fewer than two users the trade phase has nobody
            # left and records "No approved users to trade with."
            throw "--users must be at least 2 (user #0 becomes the admin/market maker and cannot be its own counterparty)."
        }

        $simArgs = @()
        foreach ($k in $simOptions.Keys) { $simArgs += $k; $simArgs += $simOptions[$k] }

        New-Item -ItemType Directory -Force -Path $logDir | Out-Null
        $smokeLog = Join-Path $logDir 'smoke.log'

        # Counted before the run and compared as a delta afterwards, never as an absolute:
        # these databases already carry failures from earlier work that belong to nobody's
        # current change, and a run that adds none of its own is a pass (issue #175).
        $ordersConnectionString = Get-OrdersConnectionString
        $failedBefore = Get-OutboxCount -ConnectionString $ordersConnectionString -Status 2

        $autoQuoteWas = Get-AutoQuoteEnabled
        Write-Host "Running simulator with args: $simArgs"
        try {
            dotnet run --no-build --configuration Release `
                --project TelegramBot/TallaEgg.TelegramBot.Simulator/TallaEgg.TelegramBot.Simulator.csproj -- @simArgs |
                Tee-Object -FilePath $smokeLog
            $simExit = $LASTEXITCODE
        }
        finally {
            # The Simulator disables auto-quote for the symbol and never re-enables it; put the
            # flag back however we found it, including when the run threw part-way through.
            if ($null -ne $autoQuoteWas -and $autoQuoteWas) {
                Restore-AutoQuote -Enabled $true
            }
        }

        if ($simExit -ne 0) {
            throw "Simulator exited with code $simExit. Full output: $smokeLog"
        }

        # The Simulator returns 0 whatever happens — it only *logs* its error count — so the
        # summary line is the real result and has to be parsed for it.
        $summary = Select-String -Path $smokeLog -Pattern 'trades attempted \d+, errors (\d+)' | Select-Object -Last 1
        if (-not $summary) {
            throw "Simulator produced no summary line; it did not finish. Full output: $smokeLog"
        }
        $errorCount = [int]$summary.Matches[0].Groups[1].Value
        if ($errorCount -ne 0) {
            throw "Simulation finished with $errorCount error(s). Full output: $smokeLog"
        }

        # Everything above only checks the trading phase. Settlement is queued and happens after
        # the summary line, so without the wait below the driver reports green on a run whose
        # settlements are still in flight — or already doomed (issue #175). Nearly every
        # settlement regression is post-summary, which is exactly what this catches.
        Wait-OutboxDrained -ConnectionString $ordersConnectionString
        $failedAfter = Get-OutboxCount -ConnectionString $ordersConnectionString -Status 2
        $newFailures = $failedAfter - $failedBefore
        if ($newFailures -gt 0) {
            throw "Simulation traded cleanly but $newFailures settlement(s) failed permanently (Failed outbox messages went from $failedBefore to $failedAfter). The trades are recorded but NOT settled and their collateral is still locked. Inspect them at http://localhost:$($ports['orders'])/api/outbox/unsettled. Full output: $smokeLog"
        }

        Write-Host "Simulation completed with errors 0; outbox drained with no new failed settlements."
    }

    'stop' {
        # Port owners first, then anything still alive from launcher.pids: a service that died
        # before Kestrel bound (unreachable SQL Server, say) owns no port but is still running,
        # and that is exactly the case 'start' throws on.
        $targets = @()
        $targets += Get-NetTCPConnection -LocalPort ([int[]]$ports.Values) -State Listen -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess
        if (Test-Path $pidFile) {
            $targets += Get-Content $pidFile | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ }
        }

        $stopped = 0
        $failed = @()
        foreach ($target in ($targets | Select-Object -Unique)) {
            $proc = Get-Process -Id $target -ErrorAction SilentlyContinue
            if (-not $proc) { continue }
            try {
                Stop-Process -Id $target -Force -ErrorAction Stop
                $stopped++
            } catch {
                # Counting a kill that did not happen is how 'stop' comes to report a stack
                # down while it is still listening, which the next 'start' then trips over.
                $failed += "$target ($($proc.ProcessName)): $($_.Exception.Message)"
            }
        }

        if (Test-Path $pidFile) { Remove-Item $pidFile -Force }

        "Stopped $stopped process(es)."
        if ($failed.Count -gt 0) {
            Write-Warning "Could not stop $($failed.Count) process(es):"
            $failed | ForEach-Object { Write-Warning "  $_" }
        }
    }
}

}
finally {
    Pop-Location
}
