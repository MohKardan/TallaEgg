<#
.SYNOPSIS
    Installs Users.Api, Wallet.Api, Orders.Api, and the Telegram bot as native Windows services
    (issue #70), so they survive a crash or a reboot without an operator starting them by hand.

.DESCRIPTION
    Uses only sc.exe and the registry — no third-party supervisor (NSSM, etc.) — because every
    service already calls builder.Host.UseWindowsService() / Host.UseWindowsService(), which
    makes the process a proper Windows Service under the SCM. That call is a no-op outside a
    real service session, so it never affects `dotnet run`.

    Affiliate.Api and TallaEgg.Api are deliberately not installed here — see #69: nothing calls
    TallaEgg.Api, and Affiliate.Api starts but has no migrations, so its endpoints 500. Neither
    is part of a deployment.

.PARAMETER InstallRoot
    Root folder containing `config\appsettings.global.json` and a `publish\<Service>\` folder
    per service (see publish-all.ps1 in this same directory). Defaults to C:\TallaEgg.

.PARAMETER TallaEggApiKey
    The shared inter-service API key (see README's "Shared API key" section). Required — every
    service throws at startup in Production if this is missing, by design (issue #33).

.PARAMETER SqlServiceName
    Name of the local SQL Server service the three APIs migrate against, added to their SCM
    dependency list so they cannot start before the database (issue #228). Defaults to SQL Server
    Express's own default instance service. Pass an empty string when the database is not a
    service on this machine.

.EXAMPLE
    .\install-services.ps1 -InstallRoot C:\TallaEgg -TallaEggApiKey (Read-Host -AsSecureString)

.EXAMPLE
    Against a named instance. Any service name containing a $ must be in SINGLE quotes: in a
    double-quoted PowerShell string, $TALLAEGG below is read as an undefined variable and the
    dependency silently becomes "MSSQL". A default instance ("MSSQLSERVER") has no $ and is not
    affected, but single quotes are harmless there and worth the habit.

    .\install-services.ps1 -TallaEggApiKey (Read-Host -AsSecureString) -SqlServiceName 'MSSQL$TALLAEGG'

.NOTES
    Run as Administrator. Re-running is safe: existing services are stopped and deleted first,
    then recreated with the current configuration.
#>
[CmdletBinding()]
param(
    [string]$InstallRoot = "C:\TallaEgg",

    [Parameter(Mandatory = $true)]
    [Security.SecureString]$TallaEggApiKey,

    # Single-quoted on purpose: in a double-quoted string PowerShell reads $SQLEXPRESS as an
    # undefined variable and silently hands sc.exe the name "MSSQL".
    [string]$SqlServiceName = 'MSSQL$SQLEXPRESS'
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated (Administrator) PowerShell session."
}

$configPath = Join-Path $InstallRoot "config\appsettings.global.json"
if (-not (Test-Path $configPath)) {
    throw "Missing $configPath. Create it from config\appsettings.global.example.json with production values before installing services."
}

# Resolve the database dependency before anything is created, and before the API key is
# decrypted below — sc.exe accepts a dependency on a service that cannot start and only fails at
# start time, as a bare error 1075 or 1058 on the next boot, with nobody watching.
$sqlDependency = @()
if ($SqlServiceName) {
    # Get-Service -Name matches wildcards, so a value like 'MSSQL*' would satisfy a bare existence
    # test and then be written verbatim as a dependency on a service literally named "MSSQL*".
    # Require an exact name.
    $sqlService = Get-Service -Name $SqlServiceName -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq $SqlServiceName }
    if (-not $sqlService) {
        throw ("No Windows service named exactly '$SqlServiceName' on this machine. " +
               "List the local SQL Server services with: Get-Service -Name 'MSSQL*' " +
               "then pass one of the names it prints as -SqlServiceName " +
               "(a wildcard is not a service name), " +
               "or pass -SqlServiceName '' if the database is not hosted on this machine.")
    }
    # A disabled instance still resolves. Depending on one would stop all three APIs from ever
    # starting, which is a worse outage than the one this dependency exists to fix. SQL Express
    # gets installed and disabled by other products, so this is not hypothetical.
    if ($sqlService.StartType -eq 'Disabled') {
        throw ("The '$SqlServiceName' service is disabled, so nothing that depends on it can " +
               "start. Enable it, or pass the instance the connection strings actually use as " +
               "-SqlServiceName, or -SqlServiceName '' if the database is not hosted here.")
    }
    $sqlDependency = @($SqlServiceName)
}

$plainApiKey = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToGlobalAllocUnicode($TallaEggApiKey))

# Two orderings decide whether this deployment survives a reboot, and only the second was
# declared before issue #228.
#
# Against the database: Wallet.Api, Users.Api and Orders.Api each run MigrateAsync() before they
# report Running, and SQL Server Express installs itself as *delayed* auto-start, roughly two
# minutes after boot. Ordinary auto-start services therefore ran first, blocked in MigrateAsync()
# against a database that was not listening, and the SCM killed them at its 45-second start
# timeout. Naming the SQL service here fixes the order: an auto-start service is allowed to depend
# on a delayed auto-start one, and the SCM then has to start the delayed service at boot instead
# of after the delay (SERVICE_DELAYED_AUTO_START_INFO, Remarks).
#
# Against each other: Orders.Api and Users.Api call Wallet.Api on startup paths, so Wallet.Api
# goes first and the bot, which opens no database of its own, goes last.
#
# Neither ordering waits for readiness. "Running" means the dependency's process started and
# reported ready — for SQL Server, not that the instance accepts connections yet; for an API, not
# that its own migration or first HTTP call has finished. This narrows the window rather than
# closing it. See the runbook for the residual case.
$services = @(
    @{ Name = "TallaEggWalletApi"; Publish = "Wallet.Api";  Exe = "Wallet.Api.exe";                          DependsOn = $sqlDependency }
    @{ Name = "TallaEggUsersApi";  Publish = "Users.Api";   Exe = "Users.Api.exe";                           DependsOn = $sqlDependency + @("TallaEggWalletApi") }
    @{ Name = "TallaEggOrdersApi"; Publish = "Orders.Api";  Exe = "Orders.Api.exe";                          DependsOn = $sqlDependency + @("TallaEggWalletApi") }
    @{ Name = "TallaEggBot";       Publish = "Bot";         Exe = "TallaEgg.TelegramBot.Infrastructure.exe"; DependsOn = @("TallaEggWalletApi", "TallaEggUsersApi", "TallaEggOrdersApi") }
)

foreach ($svc in $services) {
    $exePath = Join-Path $InstallRoot "publish\$($svc.Publish)\$($svc.Exe)"
    if (-not (Test-Path $exePath)) {
        throw "Missing $exePath. Run publish-all.ps1 first (or dotnet publish that project into $InstallRoot\publish\$($svc.Publish))."
    }

    $existing = Get-Service -Name $svc.Name -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "Stopping and removing existing service $($svc.Name)..."
        Stop-Service -Name $svc.Name -Force -ErrorAction SilentlyContinue
        sc.exe delete $svc.Name | Out-Null
        Start-Sleep -Seconds 1
    }

    Write-Host "Creating service $($svc.Name)..."
    sc.exe create $svc.Name binPath= "`"$exePath`"" start= auto obj= "LocalSystem" | Out-Null
    sc.exe description $svc.Name "TallaEgg $($svc.Publish) (issue #70)" | Out-Null

    # Restart on crash: after 10s, then 30s, then 60s for any subsequent crash within the same
    # 24h window (reset= 86400). A tight, unthrottled restart loop would mask the first real
    # failure in noise — the exact risk the issue warns about for simultaneous startup.
    sc.exe failure $svc.Name reset= 86400 actions= restart/10000/restart/30000/restart/60000 | Out-Null
    sc.exe failureflag $svc.Name 1 | Out-Null

    if ($svc.DependsOn.Count -gt 0) {
        $dependString = ($svc.DependsOn -join "/") + "/"
        # $ErrorActionPreference does not cover a native exit code, and this one write is the
        # whole of issue #228's fix: swallowing a failure here means rebooting straight back into
        # the outage while the script reports success.
        $configOutput = sc.exe config $svc.Name depend= $dependString
        if ($LASTEXITCODE -ne 0) {
            throw ("Could not set the dependency list for $($svc.Name) " +
                   "(sc.exe exit $LASTEXITCODE): $configOutput")
        }
    }

    # Native services have no ASPNETCORE_ENVIRONMENT/TALLAEGG_API_KEY unless set here — sc.exe
    # has no flag for this, so it goes directly into the per-service registry key the SCM reads
    # at process launch.
    $envKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$($svc.Name)"
    Set-ItemProperty -Path $envKey -Name "Environment" -Value @(
        "ASPNETCORE_ENVIRONMENT=Production",
        "TALLAEGG_API_KEY=$plainApiKey"
    ) -Type MultiString

    Write-Host "Starting $($svc.Name)..."
    Start-Service -Name $svc.Name
}

$plainApiKey = $null
[GC]::Collect()

Write-Host ""
Write-Host "All services installed and started. Check status with:"
Write-Host "  Get-Service TallaEgg*"
Write-Host "Logs are under each publish folder's logs\ directory (Serilog), independent of these services."
