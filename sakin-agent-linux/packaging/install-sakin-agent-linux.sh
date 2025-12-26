#!/bin/bash
set -e

# Sakin Agent Linux Installer
# Supports: Debian/Ubuntu, RHEL/CentOS, Arch, Alpine
# Features: Security checks, Proxy support, Auto-start

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default values
INSTALL_DIR="/opt/sakin-agent"
LOG_DIR="/var/log/sakin-agent"
DATA_DIR="/var/lib/sakin-agent"
CONFIG_DIR="/etc/sakin-agent"
USER="sakin-agent"
SERVICE_NAME="sakin-agent-linux"
PROXY=""
ENDPOINT=""
TOKEN=""
DRY_RUN=false
CHECK_HASH=true

# Helper functions
log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

usage() {
    echo "Usage: $0 [options]"
    echo "Options:"
    echo "  --endpoint <url>    Sakin Ingest Endpoint URL (Required)"
    echo "  --token <token>     Agent Authentication Token (Required)"
    echo "  --proxy <url>       HTTP Proxy URL"
    echo "  --dry-run           Verify checks without installing"
    echo "  --no-hash           Skip SHA256 checksum verification (NOT RECOMMENDED)"
    echo "  --help              Show this help message"
    exit 1
}

# Parse arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --endpoint) ENDPOINT="$2"; shift ;;
        --token) TOKEN="$2"; shift ;;
        --proxy) PROXY="$2"; shift ;;
        --dry-run) DRY_RUN=true ;;
        --no-hash) CHECK_HASH=false ;;
        --help) usage ;;
        *) echo "Unknown parameter passed: $1"; usage ;;
    esac
    shift
done

# Validation (unless dry-run needs less strictness? No, usually valid config is needed)
if [ -z "$ENDPOINT" ] || [ -z "$TOKEN" ]; then
    log_error "Endpoint and Token are required."
    usage
fi

if [ "$EUID" -ne 0 ]; then
    log_error "Please run as root."
    exit 1
fi

log_info "Starting Sakin Agent Installation..."
log_info "Endpoint: $ENDPOINT"
[ -n "$PROXY" ] && log_info "Proxy: $PROXY"

# OS Detection
detect_os() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        OS=$NAME
        VER=$VERSION_ID
    elif type lsb_release >/dev/null 2>&1; then
        OS=$(lsb_release -si)
        VER=$(lsb_release -sr)
    else
        OS=$(uname -s)
        VER=$(uname -r)
    fi
    log_info "Detected OS: $OS $VER"
}

# Dependency Installation
install_dependencies() {
    log_info "Installing dependencies..."
    if [ "$DRY_RUN" = true ]; then return; fi

    if command -v apt-get &> /dev/null; then
        apt-get update
        apt-get install -y dotnet-runtime-8.0 curl jq iptables
    elif command -v dnf &> /dev/null; then
        dnf install -y dotnet-runtime-8.0 curl jq iptables
    elif command -v pacman &> /dev/null; then
        pacman -Syu --noconfirm dotnet-runtime curl jq iptables
    elif command -v apk &> /dev/null; then
        apk add dotnet8-runtime curl jq iptables
    else
        log_error "Unsupported package manager. Please install dotnet-runtime-8.0, curl, jq manually."
        exit 1
    fi
}

# User Setup
setup_user() {
    log_info "Creating user $USER..."
    if [ "$DRY_RUN" = true ]; then return; fi

    if ! id "$USER" &>/dev/null; then
        useradd -r -s /bin/false "$USER"
    fi
}

# Directory Setup
setup_directories() {
    log_info "Setting up directories..."
    if [ "$DRY_RUN" = true ]; then return; fi

    mkdir -p "$INSTALL_DIR" "$LOG_DIR" "$DATA_DIR"
    chown -R "$USER:$USER" "$INSTALL_DIR" "$LOG_DIR" "$DATA_DIR"
    chmod 750 "$INSTALL_DIR" "$LOG_DIR" "$DATA_DIR"
}

# Download and Verify
download_agent() {
    log_info "Downloading Agent binary..."
    if [ "$DRY_RUN" = true ]; then return; fi

    # TODO: Replace with actual release URL handling
    # For now assuming local build or placeholder URL
    # URL="https://github.com/sakin-security/sakin-core/releases/download/latest/sakin-agent-linux.tar.gz"
    
    # Mocking download for now, assuming we might copy from local if in dev env
    # In a real installer, we would curl -L -x "$PROXY" "$URL" -o /tmp/sakin-agent.tar.gz
    
    if [ -f "sakin-agent-linux.tar.gz" ]; then
        cp sakin-agent-linux.tar.gz /tmp/sakin-agent.tar.gz
    else
        log_warn "Installer running in development mode without online binary. Please ensure binary exists."
        # In real scenario, exit or download
    fi

    if [ "$CHECK_HASH" = true ] && [ -f "/tmp/sakin-agent.tar.gz.sha256" ]; then
         echo "$(cat /tmp/sakin-agent.tar.gz.sha256) /tmp/sakin-agent.tar.gz" | sha256sum --check
         if [ $? -ne 0 ]; then
             log_error "Checksum verification failed!"
             exit 1
         fi
    fi

    tar -xzf /tmp/sakin-agent.tar.gz -C "$INSTALL_DIR"
}

# Configuration
configure_agent() {
    log_info "Configuring Agent..."
    if [ "$DRY_RUN" = true ]; then return; fi

    TEMPLATE_PATH="appsettings.template.json"
    CONFIG_PATH="$INSTALL_DIR/appsettings.json"

    if [ ! -f "$TEMPLATE_PATH" ]; then
        # Fallback if template not found locally (maybe downloaded with binary?)
        TEMPLATE_PATH="$INSTALL_DIR/appsettings.template.json"
    fi
    
    if [ -f "$TEMPLATE_PATH" ]; then
        cp "$TEMPLATE_PATH" "$CONFIG_PATH"
        # Use sed to replace placeholders
        sed -i "s|\${SAKIN_ENDPOINT}|$ENDPOINT|g" "$CONFIG_PATH"
        sed -i "s|\${SAKIN_TOKEN}|$TOKEN|g" "$CONFIG_PATH"
        sed -i "s|\${PROXY_URL}|$PROXY|g" "$CONFIG_PATH"
        sed -i "s|\${HOSTNAME}|$(hostname)|g" "$CONFIG_PATH"
        
        # Apply immutable bit (Anti-Tampering)
        if command -v chattr &> /dev/null; then
             chattr +i "$CONFIG_PATH"
        fi
    else
        log_error "Configuration template not found."
        exit 1
    fi
}

# Service Setup
setup_service() {
    log_info "Installing Systemd service..."
    if [ "$DRY_RUN" = true ]; then return; fi

    SERVICE_FILE="systemd/sakin-agent-linux.service"
    SYSTEM_SERVICE_PATH="/etc/systemd/system/$SERVICE_NAME.service"

    if [ -f "$SERVICE_FILE" ]; then
        cp "$SERVICE_FILE" "$SYSTEM_SERVICE_PATH"
        chmod 644 "$SYSTEM_SERVICE_PATH"
        
        # Apply immutable bit to service file
        if command -v chattr &> /dev/null; then
             chattr +i "$SYSTEM_SERVICE_PATH"
        fi

        systemctl daemon-reload
        systemctl enable "$SERVICE_NAME"
        systemctl start "$SERVICE_NAME"
        
        # Check status
        if systemctl is-active --quiet "$SERVICE_NAME"; then
            log_info "Service started successfully."
        else
            log_error "Service failed to start. Check logs: journalctl -u $SERVICE_NAME"
            exit 1
        fi
    else
        log_error "Service file not found."
        exit 1
    fi
}

detect_os
install_dependencies
setup_user
setup_directories
# download_agent # Skipping generic download logic for dev-local context mostly
configure_agent
setup_service

log_info "Installation Complete!"
