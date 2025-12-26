#!/bin/bash
#
# SAKIN Linux Agent Build Script
# Builds the agent and creates a release tarball for distribution
#
# Usage:
#   ./build-linux-agent.sh [--version <version>] [--output <dir>]
#
set -euo pipefail

# Configuration
VERSION="${VERSION:-$(git describe --tags --always 2>/dev/null || echo '1.0.0')}"
AGENT_NAME="sakin-agent-linux"
BUILD_DIR="sakin-agent-linux/Sakin.Agent.Linux/bin/Release/net8.0/publish"
OUTPUT_DIR="${OUTPUT_DIR:-.}"
SOURCE_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SOURCE_DIR/.." && pwd)"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info() {
    echo -e "${GREEN}[INFO]${NC} $*"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $*" >&2
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $*" >&2
}

# Parse arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --version)
                VERSION="$2"
                shift 2
                ;;
            --output)
                OUTPUT_DIR="$2"
                shift 2
                ;;
            --help|-h)
                echo "Usage: $0 [--version <version>] [--output <dir>]"
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                exit 1
                ;;
        esac
    done
}

# Build the agent
build_agent() {
    log_info "Building SAKIN Linux Agent v${VERSION}..."

    cd "$ROOT_DIR"

    # Restore dependencies
    log_info "Restoring dependencies..."
    dotnet restore sakin-agent-linux/Sakin.Agent.Linux/Sakin.Agent.Linux.csproj

    # Build in Release mode
    log_info "Building agent (Release)..."
    dotnet publish sakin-agent-linux/Sakin.Agent.Linux/Sakin.Agent.Linux.csproj \
        -c Release \
        -o "$BUILD_DIR" \
        --self-contained false \
        -p:Version="$VERSION"

    log_info "Build complete!"
}

# Create tarball
create_tarball() {
    log_info "Creating release tarball..."

    local tarball_name="${AGENT_NAME}-v${VERSION}.tar.gz"
    local output_path="$OUTPUT_DIR/$tarball_name"

    # Verify build directory exists
    if [[ ! -d "$BUILD_DIR" ]]; then
        log_error "Build directory not found: $BUILD_DIR"
        log_error "Please run build first"
        exit 1
    fi

    # Create tarball
    cd "$BUILD_DIR"
    tar -czvf "$output_path" .

    # Also copy config files
    cd "$ROOT_DIR"
    local config_dir="$OUTPUT_DIR/configs"
    mkdir -p "$config_dir"
    cp packaging/configs/linux/*.json "$config_dir/" 2>/dev/null || true

    log_info "Release tarball created: $output_path"
    log_info "Config files copied to: $config_dir"
}

# Print summary
print_summary() {
    echo ""
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}Build Complete!${NC}"
    echo -e "${GREEN}========================================${NC}"
    echo ""
    echo "Version: $VERSION"
    echo "Output: $OUTPUT_DIR"
    echo ""
    echo "Files created:"
    echo "  - ${AGENT_NAME}-v${VERSION}.tar.gz"
    echo "  - configs/ (configuration templates)"
    echo ""
    echo "To create the installer package:"
    echo "  1. Extract tarball on target system"
    echo "  2. Run: sudo ./packaging/install-sakin-agent-linux.sh --endpoint <url> --token <token>"
    echo ""
}

# Main function
main() {
    parse_args "$@"

    echo ""
    echo "========================================"
    echo "SAKIN Linux Agent Build Script"
    echo "========================================"
    echo ""

    build_agent
    create_tarball
    print_summary
}

main "$@"
