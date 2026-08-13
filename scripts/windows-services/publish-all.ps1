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
    Requires config\appsettings.global.json to exist in this repo checkout at publish time —
    Directory.Build.props copies it into each output folder automatically when present. Without
    it, ResolveSharedConfigPath() falls back to walking up from <InstallRoot>\config\, which
    install-services.ps1 checks for separately before creating any service.
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
