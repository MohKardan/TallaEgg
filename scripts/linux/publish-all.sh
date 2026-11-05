#!/usr/bin/env bash
# Publishes the four deployed services into <InstallRoot>/publish/<Service>/, the layout
# install-services.sh expects (issue #349 — the Linux counterpart of
# scripts/windows-services/publish-all.ps1).
#
# Usage:
#   ./publish-all.sh [--install-root /opt/tallaegg] [--runtime linux-x64]
#
# --runtime defaults to linux-x64, framework-dependent: the output runs on any host with the
# ASP.NET Core 9 runtime installed, and publish is safe to run from any OS the .NET SDK
# supports (a Windows or macOS machine can publish straight into a mounted server folder).
#
# Does NOT require config/appsettings.global.json in this repo checkout. Directory.Build.props
# copies it into each output folder when present, but that copy lands at the output root and
# ResolveSharedConfigPath() only ever looks inside a config/ subfolder of each ancestor — so it
# is never the file that gets loaded. The single mechanism that works is the walk up from the
# binary's own folder:
#
#     /opt/tallaegg/publish/<Service>/   <- where the walk starts (AppContext.BaseDirectory,
#                                           which UseSystemd() points the content root at)
#     /opt/tallaegg/publish/              <- checked for config/, absent
#     /opt/tallaegg/                      <- config/appsettings.global.json found here
#
# So <InstallRoot>/config/appsettings.global.json is what every service reads, and
# install-services.sh refuses to install anything until it exists.

set -euo pipefail

INSTALL_ROOT="/opt/tallaegg"
RUNTIME="linux-x64"

while [ $# -gt 0 ]; do
    case "$1" in
        --install-root)
            [ $# -ge 2 ] || { echo "--install-root needs a value" >&2; exit 1; }
            INSTALL_ROOT="$2"; shift 2 ;;
        --runtime)
            [ $# -ge 2 ] || { echo "--runtime needs a value" >&2; exit 1; }
            RUNTIME="$2"; shift 2 ;;
        *)
            echo "Unknown argument: $1" >&2
            echo "Usage: $0 [--install-root /opt/tallaegg] [--runtime linux-x64]" >&2
            exit 1 ;;
    esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

# Same four projects, same publish folder names as the Windows scripts — a fifth deployed
# service must not be something one platform's list forgets.
projects=(
    "src/Wallet/Wallet.Api/Wallet.Api.csproj|Wallet.Api"
    "src/User/Users.Api/Users.Api.csproj|Users.Api"
    "src/Order/Orders.Api/Orders.Api.csproj|Orders.Api"
    "TelegramBot/TallaEgg.TelegramBot.Infrastructure/TallaEgg.TelegramBot.Infrastructure.csproj|Bot"
)

for entry in "${projects[@]}"; do
    csproj="${entry%%|*}"
    folder="${entry##*|}"
    out_dir="$INSTALL_ROOT/publish/$folder"
    echo "Publishing $csproj -> $out_dir"
    dotnet publish "$REPO_ROOT/$csproj" \
        --configuration Release \
        --runtime "$RUNTIME" \
        --self-contained false \
        --output "$out_dir"
done

echo ""
echo "Done. Next: create $INSTALL_ROOT/config/appsettings.global.json"
echo "(from config/appsettings.global.example.json) if it isn't there yet, then run install-services.sh."
