#!/bin/bash
#
# SAKIN Linux Agent Installer
# Installs SAKIN security agent as a systemd service with auto-start on boot
#
# Usage:
#   ./install-sakin-agent-linux.sh [--endpoint <url>] [--token <token>] [--dry-run] [--uninstall]
#
# Options:
#   --endpoint    SAKIN ingest endpoint URL (e.g., http://localhost:5001)
#   --token       Authentication token for the agent
#   --dry-run     Run checks without making any changes
#   --uninstall   Remove the agent from the system
#   --help        Show this help message
#

set -euo pipefail

# Configuration
AGENT_NAME="sakin-agent-linux"
AGENT_USER="sakin-agent"
AGENT_GROUP="sakin-agent"
INSTALL_DIR="/opt/sakin-agent"
LOG_DIR="/var/log/sakin-agent"
DATA_DIR="/var/lib/sakin-agent"
SYSTEMD_DIR="/etc/systemd/system"
CONFIG_FILE="appsettings.json"
SERVICE_FILE="sakin-agent-linux.service"
RELEASE_TARBALL="${AGENT_NAME}-release.tar.gz"
GITHUB_REPO="sakin-security/sakin-core"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color
BOLD='\033[1m'

# Script arguments
DRY_RUN=false
UNINSTALL=false
ENDPOINT=""
TOKEN=""
AGENT_ID=""

# Parse arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --endpoint)
                ENDPOINT="$2"
                shift 2
                ;;
            --token)
                TOKEN="$2"
                shift 2
                ;;
            --dry-run)
                DRY_RUN=true
                shift
                ;;
            --uninstall)
                UNINSTALL=true
                shift
                ;;
            --help|-h)
                show_help
                exit 0
                ;;
            *)
                error "Unknown option: $1"
                show_help
                exit 1
                ;;
        esac
    done
}

show_help() {
    cat << EOF
${BOLD}SAKIN Linux Agent Installer${NC}

${BOLD}USAGE:${NC}
    $(basename "$0") [OPTIONS]

${BOLD}OPTIONS:${NC}
    --endpoint URL    SAKIN ingest endpoint URL (e.g., http://localhost:5001)
    --token TOKEN     Authentication token for the agent
    --dry-run         Run checks without making any changes
    --uninstall       Remove the agent from the system
    --help, -h        Show this help message

${BOLD}EXAMPLES:${NC}
    # Install with default settings
    sudo ./install-sakin-agent-linux.sh

    # Install with custom endpoint
    sudo ./install-sakin-agent-linux.sh --endpoint http://sakin-server:5001

    # Install with endpoint and token
    sudo ./install-sakin-agent-linux.sh --endpoint https://sakin.example.com --token mytoken123

    # Dry run to check system compatibility
    sudo ./install-sakin-agent-linux.sh --dry-run

    # Uninstall the agent
    sudo ./install-sakin-agent-linux.sh --uninstall

${BOLD}SUPPORTED DISTRIBUTIONS:${NC}
    - Ubuntu/Debian (apt)
    - RHEL/CentOS/Fedora (dnf/yum)
    - Arch Linux (pacman)
    - Alpine (apk)

${BOLD}DOCUMENTATION:${NC}
    https://github.com/${GITHUB_REPO}/blob/main/packaging/install-linux-README.md
EOF
}

# Output functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $*"
}

log_success() {
    echo -e "${GREEN}[OK]${NC} $*"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $*" >&2
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $*" >&2
}

log_step() {
    echo -e "${BOLD}[STEP]${NC} $*"
}

run_cmd() {
    if [[ "$DRY_RUN" == "true" ]]; then
        echo -e "${YELLOW}[DRY-RUN]${NC} Would execute: $*"
        return 0
    fi
    "$@"
}

run_cmd_sudo() {
    if [[ "$DRY_RUN" == "true" ]]; then
        echo -e "${YELLOW}[DRY-RUN]${NC} Would execute (sudo): $*"
        return 0
    fi
    sudo "$@"
}

# Pre-flight checks
preflight_checks() {
    log_step "Running pre-flight checks..."

    # Check if running as root
    if [[ $EUID -ne 0 ]]; then
        log_error "This script must be run as root or with sudo"
        log_info "Usage: sudo $0"
        exit 1
    fi

    # Check for required tools
    local missing_tools=()
    for tool in curl tar jq; do
        if ! command -v "$tool" &> /dev/null; then
            missing_tools+=("$tool")
        fi
    done

    if [[ ${#missing_tools[@]} -gt 0 ]]; then
        log_warn "Missing tools will be installed: ${missing_tools[*]}"
    fi

    # Detect OS
    detect_os
}

detect_os() {
    log_info "Detecting operating system..."

    if [[ -f /etc/os-release ]]; then
        source /etc/os-release
        OS_ID="$ID"
        OS_VERSION="$VERSION_ID"
        OS_NAME="$NAME"
    elif [[ -f /etc/alpine-release ]]; then
        OS_ID="alpine"
        OS_VERSION=$(cat /etc/alpine-release)
        OS_NAME="Alpine Linux"
    else
        OS_ID="unknown"
        OS_NAME="Unknown Linux"
        log_warn "Could not detect OS, assuming generic Linux"
    fi

    log_info "Detected: ${OS_NAME} (${OS_ID} ${OS_VERSION})"

    # Detect package manager
    case "$OS_ID" in
        ubuntu|debian|linuxmint|pop)
            PKG_MGR="apt"
            PKG_UPDATE="apt-get update -qq"
            PKG_INSTALL="apt-get install -y -qq"
            ;;
        rhel|centos|fedora|rocky|alma)
            PKG_MGR="dnf"
            PKG_UPDATE="dnf makecache -qq"
            PKG_INSTALL="dnf install -y -q"
            ;;
        opensuse*|suse)
            PKG_MGR="zypper"
            PKG_UPDATE="zypper refresh -q"
            PKG_INSTALL="zypper install -y -q"
            ;;
        arch|manjaro|archarm)
            PKG_MGR="pacman"
            PKG_UPDATE="pacman -Sy --noconfirm"
            PKG_INSTALL="pacman -S --noconfirm"
            ;;
        alpine)
            PKG_MGR="apk"
            PKG_UPDATE="apk update -q"
            PKG_INSTALL="apk add -q"
            ;;
        *)
            PKG_MGR="unknown"
            log_warn "Unknown package manager, will skip package installation"
            ;;
    esac

    log_info "Package manager: ${PKG_MGR}"
}

# Install system dependencies
install_dependencies() {
    log_step "Installing system dependencies..."

    if [[ "$PKG_MGR" == "unknown" ]]; then
        log_warn "Skipping package installation for unknown distribution"
        return 0
    fi

    if [[ "$DRY_RUN" == "true" ]]; then
        log_info "[DRY-RUN] Would install: dotnet-runtime-8.0 curl jq wget iptables"
        return 0
    fi

    # Update package lists
    if ! eval "$PKG_UPDATE" 2>/dev/null; then
        log_warn "Failed to update package lists, continuing..."
    fi

    # Common packages to install
    local packages="curl jq wget iptables"

    case "$PKG_MGR" in
        apt)
            packages="$packages apt-transport-https ca-certificates"
            # Install .NET 8.0
            if ! dpkg -l | grep -q "dotnet-runtime-8.0"; then
                log_info "Installing .NET 8.0 runtime..."
                if ! command -v dotnet &> /dev/null; then
                    wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
                    chmod +x /tmp/dotnet-install.sh
                    /tmp/dotnet-install.sh --channel 8.0 --runtime dotnet
                    export PATH="$PATH:$HOME/.dotnet"
                fi
            fi
            ;;
        dnf)
            packages="$packages dnf-plugins-core ca-certificates"
            # Install .NET 8.0
            if ! rpm -q dotnet-runtime-8.0 &>/dev/null; then
                log_info "Installing .NET 8.0 runtime..."
                if ! command -v dotnet &> /dev/null; then
                    wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
                    chmod +x /tmp/dotnet-install.sh
                    /tmp/dotnet-install.sh --channel 8.0 --runtime dotnet
                    export PATH="$PATH:$HOME/.dotnet"
                fi
            fi
            ;;
        pacman)
            # .NET is available in AUR or community
            packages="$packages base-devel"
            if ! command -v dotnet &> /dev/null; then
                log_warn ".NET 8.0 not found. Please install dotnet-runtime-8.0 manually."
            fi
            ;;
        apk)
            packages="$packages ca-certificates"
            if ! command -v dotnet &> /dev/null; then
                log_warn ".NET 8.0 not found. Please install dotnet-runtime-8.0 manually."
            fi
            ;;
    esac

    # Install common packages
    log_info "Installing packages: $packages"
    eval "$PKG_INSTALL $packages" 2>/dev/null || {
        log_warn "Some packages failed to install, continuing..."
    }
}

# Create required directories and user
setup_directories() {
    log_step "Setting up directories and user..."

    # Create user if it doesn't exist
    if ! id "$AGENT_USER" &>/dev/null; then
        log_info "Creating user: $AGENT_USER"
        run_cmd_sudo useradd --system --no-create-home --shell /usr/sbin/nologin "$AGENT_USER" || {
            log_error "Failed to create user"
            exit 1
        }
    else
        log_info "User $AGENT_USER already exists"
    fi

    # Create directories
    local dirs=("$INSTALL_DIR" "$LOG_DIR" "$DATA_DIR")

    for dir in "${dirs[@]}"; do
        if [[ ! -d "$dir" ]]; then
            log_info "Creating directory: $dir"
            run_cmd_sudo mkdir -p "$dir"
        else
            log_info "Directory exists: $dir"
        fi
        run_cmd_sudo chown "$AGENT_USER:$AGENT_GROUP" "$dir"
        run_cmd_sudo chmod 750 "$dir"
    done

    log_success "Directories and user setup complete"
}

# Generate configuration file
generate_config() {
    log_step "Generating configuration..."

    local config_template="${SCRIPT_DIR:-$(dirname "$0")}/configs/linux/${CONFIG_FILE}"

    # If template doesn't exist, create it
    if [[ ! -f "$config_template" ]]; then
        config_template="$INSTALL_DIR/${CONFIG_FILE}.template"
        create_config_template "$config_template"
    fi

    # Generate config with substitutions
    local config_file="$INSTALL_DIR/$CONFIG_FILE"

    log_info "Generating config file: $config_file"

    # Read template and substitute values
    local config_content
    config_content=$(cat "$config_template")

    # Agent ID (default to hostname)
    local agent_id="${AGENT_ID:-$(hostname)}"

    # Apply substitutions
    config_content="${config_content//\{\{AGENT_ID\}\}/$agent_id}"
    config_content="${config_content//\{\{ENDPOINT\}\}/$ENDPOINT}"
    config_content="${config_content//\{\{TOKEN\}\}/$TOKEN}"
    config_content="${config_content//\{\{HOSTNAME\}\}/$(hostname)}"

    # Write config
    if [[ "$DRY_RUN" == "true" ]]; then
        echo -e "${YELLOW}[DRY-RUN]${NC} Would create config with:"
        echo "  Endpoint: $ENDPOINT"
        echo "  Agent ID: $agent_id"
    else
        echo "$config_content" > "$config_file"
        chown "$AGENT_USER:$AGENT_GROUP" "$config_file"
        chmod 640 "$config_file"
    fi

    log_success "Configuration generated"
}

create_config_template() {
    local template_file="$1"
    cat > "$template_file" << 'EOF'
{
  "Agent": {
    "AgentId": "{{AGENT_ID}}",
    "Hostname": "{{HOSTNAME}}",
    "DryRun": false
  },
  "Sakin": {
    "IngestEndpoint": "{{ENDPOINT}}",
    "AgentToken": "{{TOKEN}}"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "ClientId": "{{AGENT_ID}}"
  },
  "KafkaProducer": {
    "DefaultTopic": "agent-events"
  },
  "KafkaTopics": {
    "AgentCommand": "sakin-agent-command",
    "AgentResult": "sakin-agent-result",
    "AuditLog": "sakin-audit-log"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
EOF
}

# Copy agent files
copy_agent_files() {
    log_step "Copying agent files..."

    local script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

    # Determine source directory
    local source_dir="$script_dir"

    # Check if release tarball exists
    if [[ -f "$script_dir/$RELEASE_TARBALL" ]]; then
        log_info "Extracting release tarball: $RELEASE_TARBALL"
        if [[ "$DRY_RUN" != "true" ]]; then
            tar -xzf "$script_dir/$RELEASE_TARBALL" -C "$INSTALL_DIR" --strip-components=1
        fi
    elif [[ -d "$script_dir/../../sakin-agent-linux" ]]; then
        # Use source directory
        source_dir="$script_dir/../../sakin-agent-linux"
        log_info "Using source directory: $source_dir"
        if [[ "$DRY_RUN" != "true" ]]; then
            cp -r "$source_dir/Sakin.Agent.Linux/publish/"* "$INSTALL_DIR/" 2>/dev/null || {
                # Try to build if publish doesn't exist
                log_info "Publish directory not found, attempting to build..."
            }
        fi
    else
        log_warn "Agent binary not found. Please provide $RELEASE_TARBALL or build the agent first."
        log_info "Downloading agent from GitHub releases..."
        download_agent
    fi

    # Copy systemd service file
    if [[ -f "$script_dir/systemd/$SERVICE_FILE" ]]; then
        log_info "Installing systemd service file..."
        run_cmd_sudo cp "$script_dir/systemd/$SERVICE_FILE" "$SYSTEMD_DIR/"
        run_cmd_sudo chmod 644 "$SYSTEMD_DIR/$SERVICE_FILE"
    fi

    log_success "Agent files copied"
}

download_agent() {
    log_info "Downloading agent from GitHub..."

    local latest_release
    latest_release=$(curl -sL "https://api.github.com/repos/${GITHUB_REPO}/releases/latest" | jq -r '.tag_name // "v1.0.0"')

    local download_url="https://github.com/${GITHUB_REPO}/releases/download/${latest_release}/${RELEASE_TARBALL}"

    if [[ "$DRY_RUN" != "true" ]]; then
        if command -v curl &>/dev/null; then
            curl -sSL "$download_url" -o "/tmp/$RELEASE_TARBALL"
            tar -xzf "/tmp/$RELEASE_TARBALL" -C "$INSTALL_DIR" --strip-components=1
        else
            log_error "curl not available and agent binary not found"
            exit 1
        fi
    fi
}

# Install and enable service
install_service() {
    log_step "Installing systemd service..."

    # Reload systemd daemon
    run_cmd_sudo systemctl daemon-reload

    # Enable service (auto-start on boot)
    log_info "Enabling service for auto-start on boot..."
    run_cmd_sudo systemctl enable "$AGENT_NAME"

    # Start service
    log_info "Starting service..."
    run_cmd_sudo systemctl start "$AGENT_NAME"

    # Wait a moment and check status
    sleep 2

    if [[ "$DRY_RUN" != "true" ]]; then
        if systemctl is-active --quiet "$AGENT_NAME"; then
            log_success "Service is running"
        else
            log_warn "Service may not have started correctly"
            log_info "Check logs with: journalctl -u $AGENT_NAME -f"
        fi

        # Verify auto-start is enabled
        if systemctl is-enabled --quiet "$AGENT_NAME"; then
            log_success "Service is enabled for auto-start on boot"
        else
            log_error "Service is NOT enabled for auto-start"
        fi
    fi
}

# Post-installation verification
verify_installation() {
    log_step "Verifying installation..."

    local checks_passed=0
    local checks_total=4

    # Check 1: Service is installed
    if systemctl list-unit-files | grep -q "$AGENT_NAME"; then
        log_success "[1/4] Service installed"
        ((checks_passed++))
    else
        log_error "[1/4] Service not installed"
    fi

    # Check 2: Service is running
    if systemctl is-active --quiet "$AGENT_NAME"; then
        log_success "[2/4] Service is running"
        ((checks_passed++))
    else
        log_error "[2/4] Service is not running"
    fi

    # Check 3: Service is enabled for auto-start
    if systemctl is-enabled --quiet "$AGENT_NAME"; then
        log_success "[3/4] Service is enabled for auto-start on boot"
        ((checks_passed++))
    else
        log_error "[3/4] Service is NOT enabled for auto-start"
    fi

    # Check 4: Configuration is valid
    if [[ -f "$INSTALL_DIR/$CONFIG_FILE" ]]; then
        log_success "[4/4] Configuration file exists"
        ((checks_passed++))
    else
        log_error "[4/4] Configuration file missing"
    fi

    echo ""
    if [[ $checks_passed -eq $checks_total ]]; then
        log_success "Installation verification passed ($checks_passed/$checks_total)"
        return 0
    else
        log_error "Installation verification failed ($checks_passed/$checks_total)"
        return 1
    fi
}

# Uninstall function
uninstall() {
    log_step "Uninstalling SAKIN Linux Agent..."

    # Stop service
    log_info "Stopping service..."
    run_cmd_sudo systemctl stop "$AGENT_NAME" 2>/dev/null || true
    run_cmd_sudo systemctl disable "$AGENT_NAME" 2>/dev/null || true

    # Remove service file
    log_info "Removing service file..."
    run_cmd_sudo rm -f "$SYSTEMD_DIR/$SERVICE_FILE"
    run_cmd_sudo systemctl daemon-reload

    # Remove files
    log_info "Removing agent files..."
    run_cmd_sudo rm -rf "$INSTALL_DIR"
    run_cmd_sudo rm -rf "$LOG_DIR"
    run_cmd_sudo rm -rf "$DATA_DIR"

    # Remove user
    log_info "Removing user..."
    run_cmd_sudo userdel "$AGENT_USER" 2>/dev/null || true
    run_cmd_sudo groupdel "$AGENT_GROUP" 2>/dev/null || true

    log_success "SAKIN Linux Agent has been uninstalled"
}

# Main function
main() {
    parse_args "$@"

    # Header
    echo ""
    echo -e "${BOLD}╔══════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BOLD}║          SAKIN Linux Agent Installer                     ║${NC}"
    echo -e "${BOLD}║          Siber Analiz ve Kontrol İstihbarat Noktası      ║${NC}"
    echo -e "${BOLD}╚══════════════════════════════════════════════════════════╝${NC}"
    echo ""

    if [[ "$DRY_RUN" == "true" ]]; then
        log_warn "DRY RUN MODE - No changes will be made"
        echo ""
    fi

    if [[ "$UNINSTALL" == "true" ]]; then
        uninstall
        exit 0
    fi

    # Run installation steps
    preflight_checks
    install_dependencies
    setup_directories
    copy_agent_files
    generate_config
    install_service
    verify_installation

    echo ""
    log_success "SAKIN Linux Agent installation complete!"
    echo ""
    log_info "Useful commands:"
    echo "  Check status:  systemctl status $AGENT_NAME"
    echo "  View logs:     journalctl -u $AGENT_NAME -f"
    echo "  Restart:       sudo systemctl restart $AGENT_NAME"
    echo "  Stop:          sudo systemctl stop $AGENT_NAME"
    echo "  Disable auto:  sudo systemctl disable $AGENT_NAME"
    echo ""
    log_info "Documentation: https://github.com/${GITHUB_REPO}/blob/main/packaging/install-linux-README.md"
}

# Run main function
main "$@"
