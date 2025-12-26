#!/bin/bash
#
# SAKIN Agent Post-Install Verification Script
# Verifies that the SAKIN agent is correctly installed and configured
#
# Usage:
#   ./verify-installation.sh [--endpoint <url>] [--token <token>]
#
set -euo pipefail

AGENT_NAME="sakin-agent-linux"
INSTALL_DIR="/opt/sakin-agent"
CONFIG_FILE="$INSTALL_DIR/appsettings.json"
SERVICE_FILE="/etc/systemd/system/sakin-agent-linux.service"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

passed=0
failed=0

check_pass() {
    echo -e "${GREEN}[PASS]${NC} $*"
    ((passed++))
}

check_fail() {
    echo -e "${RED}[FAIL]${NC} $*"
    ((failed++))
}

check_warn() {
    echo -e "${YELLOW}[WARN]${NC} $*"
}

check_info() {
    echo -e "${BLUE}[INFO]${NC} $*"
}

echo ""
echo "╔══════════════════════════════════════════════════════════╗"
echo "║      SAKIN Agent Post-Install Verification              ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

# Check 1: Service exists
check_info "Checking if service is installed..."
if systemctl list-unit-files | grep -q "$AGENT_NAME"; then
    check_pass "Service '$AGENT_NAME' is installed"
else
    check_fail "Service '$AGENT_NAME' is NOT installed"
fi

# Check 2: Service is running
check_info "Checking if service is running..."
if systemctl is-active --quiet "$AGENT_NAME" 2>/dev/null; then
    check_pass "Service is running"
else
    check_fail "Service is NOT running"
    check_info "Run 'sudo systemctl start $AGENT_NAME' to start the service"
fi

# Check 3: Service is enabled for auto-start
check_info "Checking if service is enabled for auto-start..."
if systemctl is-enabled --quiet "$AGENT_NAME" 2>/dev/null; then
    check_pass "Service is enabled for auto-start on boot"
else
    check_fail "Service is NOT enabled for auto-start"
    check_info "Run 'sudo systemctl enable $AGENT_NAME' to enable auto-start"
fi

# Check 4: Configuration file exists
check_info "Checking configuration file..."
if [[ -f "$CONFIG_FILE" ]]; then
    check_pass "Configuration file exists: $CONFIG_FILE"
else
    check_fail "Configuration file missing: $CONFIG_FILE"
fi

# Check 5: Configuration is valid JSON
if [[ -f "$CONFIG_FILE" ]]; then
    check_info "Validating configuration JSON..."
    if jq empty "$CONFIG_FILE" 2>/dev/null; then
        check_pass "Configuration is valid JSON"
    else
        check_fail "Configuration is NOT valid JSON"
    fi
fi

# Check 6: Configuration has required sections
if [[ -f "$CONFIG_FILE" ]]; then
    check_info "Checking required configuration sections..."
    
    if jq -e '.Sakin' "$CONFIG_FILE" >/dev/null 2>&1; then
        check_pass "Sakin configuration section exists"
    else
        check_fail "Sakin configuration section missing"
    fi
    
    if jq -e '.Agent' "$CONFIG_FILE" >/dev/null 2>&1; then
        check_pass "Agent configuration section exists"
    else
        check_fail "Agent configuration section missing"
    fi
fi

# Check 7: Endpoint is configured
if [[ -f "$CONFIG_FILE" ]]; then
    check_info "Checking endpoint configuration..."
    endpoint=$(jq -r '.Sakin.IngestEndpoint // empty' "$CONFIG_FILE" 2>/dev/null)
    if [[ -n "$endpoint" && "$endpoint" != "http://localhost:5001" ]]; then
        check_pass "Endpoint configured: $endpoint"
    else
        check_warn "Endpoint not configured (using default: http://localhost:5001)"
    fi
fi

# Check 8: Token is configured
if [[ -f "$CONFIG_FILE" ]]; then
    check_info "Checking token configuration..."
    token=$(jq -r '.Sakin.AgentToken // empty' "$CONFIG_FILE" 2>/dev/null)
    if [[ -n "$token" ]]; then
        check_pass "Token is configured (length: ${#token})"
    else
        check_warn "Token is NOT configured"
    fi
fi

# Check 9: Agent binary exists
check_info "Checking agent binary..."
if [[ -f "$INSTALL_DIR/Sakin.Agent.Linux.dll" ]]; then
    check_pass "Agent binary exists: Sakin.Agent.Linux.dll"
else
    check_fail "Agent binary missing: Sakin.Agent.Linux.dll"
fi

# Check 10: Agent user exists
check_info "Checking agent user..."
if id "sakin-agent" &>/dev/null; then
    check_pass "User 'sakin-agent' exists"
else
    check_warn "User 'sakin-agent' does not exist"
fi

# Check 11: Log directory exists
check_info "Checking log directory..."
if [[ -d "/var/log/sakin-agent" ]]; then
    check_pass "Log directory exists: /var/log/sakin-agent"
else
    check_warn "Log directory does not exist: /var/log/sakin-agent"
fi

# Check 12: Data directory exists
check_info "Checking data directory..."
if [[ -d "/var/lib/sakin-agent" ]]; then
    check_pass "Data directory exists: /var/lib/sakin-agent"
else
    check_warn "Data directory does not exist: /var/lib/sakin-agent"
fi

# Check 13: Test endpoint connectivity
endpoint=$(jq -r '.Sakin.IngestEndpoint // empty' "$CONFIG_FILE" 2>/dev/null)
if [[ -n "$endpoint" && "$endpoint" != "http://localhost:5001" ]]; then
    check_info "Testing endpoint connectivity..."
    if curl -sf "${endpoint}/health" >/dev/null 2>&1; then
        check_pass "Endpoint is reachable: $endpoint"
    else
        check_warn "Endpoint is NOT reachable: $endpoint"
        check_info "Verify the endpoint URL or check network connectivity"
    fi
fi

# Check 14: Check recent logs
check_info "Checking recent logs..."
log_count=$(journalctl -u "$AGENT_NAME" -n 10 --no-pager 2>/dev/null | wc -l)
if [[ $log_count -gt 0 ]]; then
    check_pass "Service logs are available ($log_count lines in last 10 entries)"
else
    check_warn "No recent logs found"
fi

# Summary
echo ""
echo "╔══════════════════════════════════════════════════════════╗"
echo "║                   Verification Summary                   ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""
echo -e "Passed: ${GREEN}$passed${NC}"
echo -e "Failed: ${RED}$failed${NC}"
echo ""

if [[ $failed -eq 0 ]]; then
    echo -e "${GREEN}All checks passed! SAKIN Agent is correctly installed.${NC}"
    exit 0
else
    echo -e "${YELLOW}Some checks failed. Review the output above.${NC}"
    echo ""
    echo "Common next steps:"
    echo "  1. If service not running: sudo systemctl start $AGENT_NAME"
    echo "  2. If not enabled: sudo systemctl enable $AGENT_NAME"
    echo "  3. View logs: journalctl -u $AGENT_NAME -f"
    echo "  4. Check status: systemctl status $AGENT_NAME"
    exit 1
fi
