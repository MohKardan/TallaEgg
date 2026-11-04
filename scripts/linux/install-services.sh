#!/usr/bin/env bash
# Installs Users.Api, Wallet.Api, Orders.Api, and the Telegram bot as systemd services
# (issue #349 — the Linux counterpart of scripts/windows-services/install-services.ps1), so
# they survive a crash or a reboot without an operator starting them by hand.
#
# What it does:
#   1. Verifies the deployment layout (shared config + the four publish folders) exists.
#   2. Prompts for the inter-service API key and writes it into a root-owned, mode-600
#      environment file systemd loads before dropping privileges — a unit file in
#      /etc/systemd/system is world-readable, so the key must never be inline in one.
#   3. Creates the dedicated unprivileged 'tallaegg' service account (no shell, no sudo).
#   4. Copies the unit files from scripts/linux/systemd/ into /etc/systemd/system/, enables
#      them and starts them in dependency order (Wallet first, the bot last).
#
# Affiliate.Api and TallaEgg.Api are deliberately not installed here — see #69: nothing calls
# TallaEgg.Api, and Affiliate.Api starts but has no migrations, so its endpoints 500. Neither
# is part of a deployment.
#
# Re-running is safe: existing units are overwritten, reloaded and restarted with the current
# configuration, which makes re-running this script the Linux redeploy step — the same role
# install-services.ps1 plays on Windows.

set -euo pipefail

INSTALL_ROOT="/opt/tallaegg"
SERVICE_USER="tallaegg"
ENV_FILE="$INSTALL_ROOT/config/tallaegg.env"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$EUID" -ne 0 ]; then
    echo "Run this script as root (sudo)." >&2
    exit 1
fi

# Verify the layout before touching systemd — the same rule as the Windows installer, which
# resolves its SQL dependency and the service executable before creating anything.
if [ ! -f "$INSTALL_ROOT/config/appsettings.global.json" ]; then
    echo "Missing $INSTALL_ROOT/config/appsettings.global.json." >&2
    echo "Create it from config/appsettings.global.example.json with production values" >&2
    echo "before installing services." >&2
    exit 1
fi

# unit|publish folder|entry assembly — in dependency order, Wallet first, bot last.
services=(
    "tallaegg-wallet.service|Wallet.Api|Wallet.Api.dll"
    "tallaegg-users.service|Users.Api|Users.Api.dll"
    "tallaegg-orders.service|Orders.Api|Orders.Api.dll"
    "tallaegg-bot.service|Bot|TallaEgg.TelegramBot.Infrastructure.dll"
)

for entry in "${services[@]}"; do
    IFS='|' read -r unit folder dll <<< "$entry"
    if [ ! -f "$INSTALL_ROOT/publish/$folder/$dll" ]; then
        echo "Missing $INSTALL_ROOT/publish/$folder/$dll." >&2
        echo "Run publish-all.sh first (or dotnet publish that project into" >&2
        echo "$INSTALL_ROOT/publish/$folder)." >&2
        exit 1
    fi
done

printf 'Enter the TallaEgg API key (input hidden): '
read -rs API_KEY
echo ""

if [ -z "$API_KEY" ]; then
    echo "No API key entered — every service throws at startup in Production if this is" >&2
    echo "missing, by design (issue #214)." >&2
    exit 1
fi

# The service account. Home is the deployment root (systemd sets HOME from passwd; the .NET
# runtime writes nothing there for a published app, but an unset HOME has surprised hosts
# before), shell is nologin, and --no-create-home because the directory already exists.
if ! id -u "$SERVICE_USER" >/dev/null 2>&1; then
    useradd --system --home-dir "$INSTALL_ROOT" --no-create-home --shell /usr/sbin/nologin "$SERVICE_USER"
    echo "Created service account $SERVICE_USER."
fi

# The services run as $SERVICE_USER and each writes its own rolling log beside its binary, so
# the publish output has to belong to that account — whoever ran publish-all.sh owned it first.
# Re-applied on every install, which also covers the files a manual redeploy untarred as root.
chown -R "$SERVICE_USER:$SERVICE_USER" "$INSTALL_ROOT/publish"

# The shared configuration carries the SQL credentials and the bot token. The services only
# read it, so it stays root-owned but becomes group-readable by the service group alone
# (640) rather than world-readable (644) — the same audience the Windows layout gives it
# behind LocalSystem.
chgrp "$SERVICE_USER" "$INSTALL_ROOT/config/appsettings.global.json"
chmod 640 "$INSTALL_ROOT/config/appsettings.global.json"

# Write the environment file: root-owned, mode 600, so only systemd (reading it as root before
# dropping to the service user) and an administrator can read the key.
umask 077
printf 'TALLAEGG_API_KEY=%s\n' "$API_KEY" > "$ENV_FILE"
unset API_KEY

for entry in "${services[@]}"; do
    IFS='|' read -r unit _folder _dll <<< "$entry"
    install -m 644 "$SCRIPT_DIR/systemd/$unit" "/etc/systemd/system/$unit"
done

systemctl daemon-reload

for entry in "${services[@]}"; do
    IFS='|' read -r unit _folder _dll <<< "$entry"
    systemctl enable "$unit" >/dev/null 2>&1
done

# Start (or restart on a re-install, so unit-file changes take effect) in dependency order:
# Wallet, then the two APIs that call it, then the bot. The units' After=/Wants= make the
# order explicit to systemd as well, so boot-time activation follows the same sequence.
# "Started" means the process is up — not that its migration has finished. What waits out a
# slow database is the application's own retry (issue #230), not this order.
for entry in "${services[@]}"; do
    IFS='|' read -r unit _folder _dll <<< "$entry"
    if systemctl is-active --quiet "$unit"; then
        echo "Restarting $unit (picks up the updated unit file)..."
        systemctl restart "$unit"
    else
        echo "Starting $unit..."
        systemctl start "$unit"
    fi
done

echo ""
echo "All services installed and started. Check status with:"
echo "  systemctl status tallaegg-*"
echo "Logs: journalctl -u tallaegg-<unit-short>  (each service also writes its own rolling"
echo "log under $INSTALL_ROOT/publish/<Service>/logs/, independent of journald)."
