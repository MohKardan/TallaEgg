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

.EXAMPLE
    .\install-services.ps1 -InstallRoot C:\TallaEgg -TallaEggApiKey (Read-Host -AsSecureString)

.NOTES
    Run as Administrator. Re-running is safe: existing services are stopped and deleted first,
    then recreated with the current configuration.
#>
[CmdletBinding()]
param(
    [string]$InstallRoot = "C:\TallaEgg",

    [Parameter(Mandatory = $true)]
    [Security.SecureString]$TallaEggApiKey
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated (Administrator) PowerShell session."
}

$configPath = Join-Path $InstallRoot "config\appsettings.global.json"
if (-not (Test-Path $configPath)) {
    throw "Missing $configPath. Create it from config\appsettings.global.example.json with production values before installing services."
}

$plainApiKey = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToGlobalAllocUnicode($TallaEggApiKey))

# Order matters for the -DependsOn column: Orders.Api and Users.Api call Wallet.Api on startup
# paths, and all three run MigrateAsync() as the first thing they do. sc.exe's service
# dependency only guarantees the dependency reached "Running" before this one starts — not that
# its own startup work (migration, first HTTP call) has finished — so this absorbs most of the
# ordering risk the issue calls out, not all of it. See the runbook for the residual case.
$services = @(
    @{ Name = "TallaEggWalletApi"; Publish = "Wallet.Api";  Exe = "Wallet.Api.exe";                          DependsOn = @() }
    @{ Name = "TallaEggUsersApi";  Publish = "Users.Api";   Exe = "Users.Api.exe";                           DependsOn = @("TallaEggWalletApi") }
    @{ Name = "TallaEggOrdersApi"; Publish = "Orders.Api";  Exe = "Orders.Api.exe";                          DependsOn = @("TallaEggWalletApi") }
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
        sc.exe config $svc.Name depend= $dependString | Out-Null
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
