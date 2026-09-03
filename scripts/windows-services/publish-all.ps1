<#
.SYNOPSIS
    Publishes the four deployed services into <InstallRoot>\publish\<Service>\, the layout
    install-services.ps1 expects.

.PARAMETER InstallRoot
    Root folder to publish into. Defaults to C:\TallaEgg. The repository itself does not need
    to live here — this script can be run from a normal dev clone with -InstallRoot pointed at
    the server's deployment folder (e.g. a mapped drive during setup, or run locally on the
    server after a `git pull`).

.NOTES
    Does NOT require config\appsettings.global.json in this repo checkout. Directory.Build.props
    copies it into each output folder when present, but that copy lands at the output root and
    ResolveSharedConfigPath() only ever looks inside a config\ subfolder of each ancestor — so it
    is never the file that gets loaded. The single mechanism that works is the walk up from the
    binary's own folder:

        C:\TallaEgg\publish\<Service>\   <- where the walk starts (AppContext.BaseDirectory,
                                            which UseWindowsService() sets ContentRootPath to)
        C:\TallaEgg\publish\             <- checked for config\, absent
        C:\TallaEgg\                     <- config\appsettings.global.json found here

    So <InstallRoot>\config\appsettings.global.json is what every service reads, and
    install-services.ps1 refuses to create a service until it exists. Before #212 the bot walked
    up from the working directory instead, which the SCM sets to C:\Windows\System32 — it could
    not start as a service at all.
#>
[CmdletBinding()]
param(
    [string]$InstallRoot = "C:\TallaEgg"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path "$PSScriptRoot\..\.."

$projects = @(
    @{ Path = "src\Wallet\Wallet.Api\Wallet.Api.csproj"; Publish = "Wallet.Api" }
    @{ Path = "src\User\Users.Api\Users.Api.csproj"; Publish = "Users.Api" }
    @{ Path = "src\Order\Orders.Api\Orders.Api.csproj"; Publish = "Orders.Api" }
    @{ Path = "TelegramBot\TallaEgg.TelegramBot.Infrastructure\TallaEgg.TelegramBot.Infrastructure.csproj"; Publish = "Bot" }
)

foreach ($proj in $projects) {
    $csproj = Join-Path $repoRoot $proj.Path
    $outDir = Join-Path $InstallRoot "publish\$($proj.Publish)"
    Write-Host "Publishing $($proj.Path) -> $outDir"
    dotnet publish $csproj --configuration Release --output $outDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $($proj.Path)"
    }
}

Write-Host ""
Write-Host "Done. Next: create $InstallRoot\config\appsettings.global.json (from config\appsettings.global.example.json) if it isn't there yet, then run install-services.ps1."
