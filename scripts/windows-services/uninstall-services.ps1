<#
.SYNOPSIS
    Stops and removes the four TallaEgg Windows services installed by install-services.ps1.
    Does not touch published files, logs, or the database.

.NOTES
    Run as Administrator.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated (Administrator) PowerShell session."
}

$serviceNames = @("TallaEggBot", "TallaEggOrdersApi", "TallaEggUsersApi", "TallaEggWalletApi")

foreach ($name in $serviceNames) {
    $existing = Get-Service -Name $name -ErrorAction SilentlyContinue
    if (-not $existing) {
        Write-Host "$name is not installed, skipping."
        continue
    }

    Write-Host "Stopping and removing $name..."
    Stop-Service -Name $name -Force -ErrorAction SilentlyContinue
    sc.exe delete $name | Out-Null
}

Write-Host "Done."
