<#
.SYNOPSIS
  Build, launch, and drive the TallaEgg backend (Users/Wallet/Orders APIs) end-to-end,
  without a live Telegram connection.

.DESCRIPTION
  See SKILL.md in this same directory for the full explanation. Short version:

    pwsh driver.ps1 start                                  # build (Release) + launch the 3 APIs, wait for health
    pwsh driver.ps1 status                                 # show whether the 3 ports are listening
    pwsh driver.ps1 smoke                                  # drive the whole stack via the bot Simulator (small run)
    pwsh driver.ps1 smoke --users 20 --quotes 20 --trades 50 --seed 7   # bigger/custom run
    pwsh driver.ps1 stop                                   # stop whatever is listening on the 3 ports

  "smoke" is the actual interaction: it runs TallaEgg.TelegramBot.Simulator, which replays
  real bot conversations (registration, admin approval, quote publishing, quote-fill trades,
  menu navigation) through the real IBotHandler and the real Users/Wallet/Orders APIs and
  database — everything except the live Telegram connection, which it fakes. It only ever
  touches rows with TelegramId < 0, so it is safe to run against a real local dev database.
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
Set-Location $repoRoot

$ports = [ordered]@{ users = 5136; wallet = 60933; orders = 5140 }
$projects = @{
    users  = 'src/User/Users.Api/Users.Api.csproj'
    wallet = 'src/Wallet/Wallet.Api/Wallet.Api.csproj'
    orders = 'src/Order/Orders.Api/Orders.Api.csproj'
}
$logDir = Join-Path $repoRoot 'run-logs'

function Test-ServicesUp {
    foreach ($name in $ports.Keys) {
        try {
            $r = Invoke-WebRequest -UseBasicParsing -TimeoutSec 2 -Uri "http://localhost:$($ports[$name])/api-docs/index.html"
            if ($r.StatusCode -ne 200) { return $false }
        } catch { return $false }
    }
    return $true
}

switch ($Command) {
    'start' {
        $configPath = Join-Path $repoRoot 'config/appsettings.global.json'
        if (-not (Test-Path $configPath)) {
            throw "config/appsettings.global.json is missing. Copy config/appsettings.global.example.json to that path and point ConnectionStrings at a local SQL Server (see SKILL.md Prerequisites)."
        }

        New-Item -ItemType Directory -Force -Path $logDir | Out-Null

        Write-Host "Building (Release)..."
        dotnet build TallaEgg.sln --configuration Release | Out-Host

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
        $procIds | Set-Content (Join-Path $logDir 'launcher.pids')

        Write-Host "Waiting for services to come up..."
        for ($i = 0; $i -lt 30; $i++) {
            if (Test-ServicesUp) {
                Write-Host "All services up after $($i * 2)s."
                return
            }
            Start-Sleep -Seconds 2
        }

        Write-Host "Services did not come up in time. Recent stderr:"
        Get-ChildItem $logDir -Filter '*.err.log' | ForEach-Object {
            Write-Host "--- $($_.Name) ---"
            Get-Content $_.FullName -Tail 40
        }
        throw "Timed out waiting for Users/Wallet/Orders APIs to report healthy."
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
        $simArgs = if ($Rest -and $Rest.Count -gt 0) { $Rest } else { @('--users', '5', '--quotes', '5', '--trades', '10', '--seed', '1') }
        Write-Host "Running simulator with args: $simArgs"
        dotnet run --no-build --configuration Release `
            --project TelegramBot/TallaEgg.TelegramBot.Simulator/TallaEgg.TelegramBot.Simulator.csproj -- @simArgs
    }

    'stop' {
        $stopped = 0
        Get-NetTCPConnection -LocalPort ([int[]]$ports.Values) -State Listen -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess -Unique |
            ForEach-Object {
                Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
                $stopped++
            }
        "Stopped $stopped process(es)."
    }
}
