#!/usr/bin/env bash
#
# AxarDB Automatic Update Script for Debian
# Checks GitHub Releases, downloads latest binaries, safely updates the instance
# and guarantees that existing database data and collections are never deleted.
#

set -euo pipefail

# Configuration defaults
REPO_OWNER="metin-yakar"
REPO_NAME="AxarDB"
SERVICE_NAME="axardb"
CHECK_ONLY=0
FORCE_UPDATE=0
INSTALL_DIR=""

log() {
    local level="$1"
    shift
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] [$level] $*"
}

usage() {
    cat <<EOF
Usage: $0 [OPTIONS]

Options:
  -d, --dir DIR          Set AxarDB installation directory (default: /opt/axardb or detected)
  -s, --service NAME     Set systemd service name (default: axardb)
  -c, --check            Check for update availability without installing
  -f, --force            Force update even if version is already up-to-date
  -h, --help             Display this help message
EOF
    exit 1
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -d|--dir)
            INSTALL_DIR="$2"
            shift 2
            ;;
        -s|--service)
            SERVICE_NAME="$2"
            shift 2
            ;;
        -c|--check)
            CHECK_ONLY=1
            shift
            ;;
        -f|--force)
            FORCE_UPDATE=1
            shift
            ;;
        -h|--help)
            usage
            ;;
        *)
            log "ERROR" "Unknown argument: $1"
            usage
            ;;
    esac
done

# Resolve installation directory
if [[ -z "$INSTALL_DIR" ]]; then
    if [[ -f "./AxarDB.dll" || -f "./AxarDB" ]]; then
        INSTALL_DIR="$(pwd)"
    elif [[ -f "../AxarDB.dll" || -f "../AxarDB" ]]; then
        INSTALL_DIR="$(cd .. && pwd)"
    elif [[ -d "/opt/axardb" ]]; then
        INSTALL_DIR="/opt/axardb"
    else
        INSTALL_DIR="$(pwd)"
    fi
fi

mkdir -p "$INSTALL_DIR"
INSTALL_DIR="$(cd "$INSTALL_DIR" && pwd)"
log "INFO" "Target installation directory: $INSTALL_DIR"

# Check dependencies
for cmd in curl unzip jq; do
    if ! command -v "$cmd" &>/dev/null; then
        log "WARN" "Command '$cmd' not found. Installing via apt-get..."
        if command -v apt-get &>/dev/null; then
            apt-get update -qq && apt-get install -y -qq "$cmd"
        else
            log "ERROR" "Missing prerequisite '$cmd'. Please install it first."
            exit 1
        fi
    fi
done

# Read current version
VERSION_FILE="$INSTALL_DIR/version.txt"
CURRENT_VERSION="none"
if [[ -f "$VERSION_FILE" ]]; then
    CURRENT_VERSION="$(cat "$VERSION_FILE" | tr -d '[:space:]')"
fi
log "INFO" "Current installed version: $CURRENT_VERSION"

# Fetch latest release info from GitHub API
API_URL="https://api.github.com/repos/${REPO_OWNER}/${REPO_NAME}/releases/latest"
log "INFO" "Checking latest release from: $API_URL"

RELEASE_JSON=$(curl -sSL -H "User-Agent: AxarDB-Updater-Debian" -H "Accept: application/vnd.github.v3+json" "$API_URL")

if [[ -z "$RELEASE_JSON" ]] || echo "$RELEASE_JSON" | grep -q '"message": "Not Found"'; then
    log "ERROR" "Could not fetch release metadata from GitHub repository."
    exit 1
fi

LATEST_TAG=$(echo "$RELEASE_JSON" | jq -r '.tag_name // empty')
if [[ -z "$LATEST_TAG" ]]; then
    log "ERROR" "Failed to parse latest tag from GitHub response."
    exit 1
fi

log "INFO" "Latest available release: $LATEST_TAG"

# Check if update is required
if [[ "$CURRENT_VERSION" == "$LATEST_TAG" && $FORCE_UPDATE -eq 0 ]]; then
    log "INFO" "AxarDB is already up-to-date ($CURRENT_VERSION). No update needed."
    exit 0
fi

if [[ $CHECK_ONLY -eq 1 ]]; then
    log "INFO" "Update is available: $CURRENT_VERSION -> $LATEST_TAG"
    exit 0
fi

log "INFO" "Starting update procedure: $CURRENT_VERSION -> $LATEST_TAG"

# Locate Debian asset URL (AxarDB-debian.zip or linux-x64)
DOWNLOAD_URL=$(echo "$RELEASE_JSON" | jq -r '.assets[] | select(.name | test("debian|linux"; "i")) | .browser_download_url' | head -n 1)

if [[ -z "$DOWNLOAD_URL" ]]; then
    DOWNLOAD_URL=$(echo "$RELEASE_JSON" | jq -r '.assets[] | select(.name | test("\\.zip$")) | .browser_download_url' | head -n 1)
fi

if [[ -z "$DOWNLOAD_URL" ]]; then
    log "ERROR" "No matching Debian/Linux release zip asset found in release $LATEST_TAG"
    exit 1
fi

ASSET_NAME=$(basename "$DOWNLOAD_URL")
log "INFO" "Downloading release asset: $ASSET_NAME"

# Temporary workspace
STAGING_DIR="$(mktemp -d /tmp/axardb_update_XXXXXX)"
ZIP_PATH="$STAGING_DIR/$ASSET_NAME"
EXTRACT_DIR="$STAGING_DIR/extracted"
mkdir -p "$EXTRACT_DIR"

cleanup() {
    rm -rf "$STAGING_DIR"
}
trap cleanup EXIT

# Download package
curl -sSL -H "User-Agent: AxarDB-Updater-Debian" -o "$ZIP_PATH" "$DOWNLOAD_URL"

# Extract package
log "INFO" "Extracting release package..."
unzip -q -o "$ZIP_PATH" -d "$EXTRACT_DIR"

# Resolve nested subfolder if present (e.g. 'debian/')
SOURCE_DIR="$EXTRACT_DIR"
if [[ -d "$EXTRACT_DIR/debian" && -f "$EXTRACT_DIR/debian/AxarDB.dll" ]]; then
    SOURCE_DIR="$EXTRACT_DIR/debian"
fi

# Stop systemd service if running
SERVICE_WAS_RUNNING=0
if command -v systemctl &>/dev/null; then
    if systemctl is-active --quiet "$SERVICE_NAME" 2>/dev/null; then
        log "INFO" "Stopping systemd service '$SERVICE_NAME'..."
        systemctl stop "$SERVICE_NAME"
        SERVICE_WAS_RUNNING=1
    fi
fi

# CRITICAL: Safe Copy Routine (Strict Data Preservation)
# Protected folders that MUST NOT be deleted or overwritten with empty directories:
PROTECTED_ITEMS=("Data" "Bulk" "Views" "Triggers" "backup_queries" "request_logs" "error_logs" "debug_logs" "view_logs" "trigger_logs")

is_protected() {
    local item="$1"
    for p in "${PROTECTED_ITEMS[@]}"; do
        if [[ "$item" == "$p" ]]; then
            return 0
        fi
    done
    return 1
}

log "INFO" "Updating binaries and web assets while preserving database data..."

for item in "$SOURCE_DIR"/*; do
    [ -e "$item" ] || continue
    name=$(basename "$item")

    if [[ -d "$item" ]]; then
        if is_protected "$name" && [[ -d "$INSTALL_DIR/$name" ]]; then
            log "INFO" "Preserving existing data directory: $name"
            continue
        fi
        cp -rf "$item" "$INSTALL_DIR/"
    else
        # If user customized appsettings.json, preserve it
        if [[ "$name" == "appsettings.json" && -f "$INSTALL_DIR/$name" ]]; then
            log "INFO" "Preserving existing appsettings.json configuration file."
            continue
        fi
        cp -f "$item" "$INSTALL_DIR/"
    fi
done

# Ensure execute permissions on binary
if [[ -f "$INSTALL_DIR/AxarDB" ]]; then
    chmod +x "$INSTALL_DIR/AxarDB"
fi

# Update version file
echo "$LATEST_TAG" > "$VERSION_FILE"
log "INFO" "Updated version file to $LATEST_TAG."

# Restart systemd service if it was running
if [[ $SERVICE_WAS_RUNNING -eq 1 ]]; then
    log "INFO" "Restarting systemd service '$SERVICE_NAME'..."
    systemctl start "$SERVICE_NAME"
    if systemctl is-active --quiet "$SERVICE_NAME"; then
        log "INFO" "Service '$SERVICE_NAME' started successfully."
    else
        log "WARN" "Service '$SERVICE_NAME' may have failed to start. Check 'systemctl status $SERVICE_NAME'."
    fi
fi

log "INFO" "AxarDB update to $LATEST_TAG completed successfully without data loss."
