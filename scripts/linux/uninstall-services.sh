#!/usr/bin/env bash
# Stops and removes the four TallaEgg systemd services installed by install-services.sh
# (issue #349 — the Linux counterpart of scripts/windows-services/uninstall-services.ps1).
#
# Removes the unit files, the environment file holding the API key (part of the service
# definition, the same thing the Windows uninstall deletes with the service), and the
# environment file's path is the only secret on disk this script is responsible for.
#
# Does not touch published files, logs, the deployment root's config folder, or the database.
# The 'tallaegg' service account is left in place; a reinstall reuses it.

set -euo pipefail

ENV_FILE="/opt/tallaegg/config/tallaegg.env"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$EUID" -ne 0 ]; then
    echo "Run this script as root (sudo)." >&2
    exit 1
fi

# Stop in reverse dependency order: bot first, Wallet last — the same order the Windows
# uninstaller uses, and the same one the redeploy runbook needs.
units=(
    "tallaegg-bot.service"
    "tallaegg-orders.service"
    "tallaegg-users.service"
    "tallaegg-wallet.service"
)

for unit in "${units[@]}"; do
    if systemctl list-unit-files "$unit" --no-legend 2>/dev/null | grep -q .; then
        echo "Stopping and disabling $unit..."
        systemctl stop "$unit" 2>/dev/null || true
        systemctl disable "$unit" 2>/dev/null || true
    else
        echo "$unit is not installed, skipping."
    fi
done

for unit in "${units[@]}"; do
    if [ -f "/etc/systemd/system/$unit" ]; then
        rm -f "/etc/systemd/system/$unit"
    fi
done

systemctl daemon-reload

if [ -f "$ENV_FILE" ]; then
    rm -f "$ENV_FILE"
    echo "Removed $ENV_FILE (the API key enters it again on the next install)."
fi

echo "Done. Published files, the config folder's appsettings.global.json, the logs and the"
echo "database are untouched."
