# SAKIN Agent Linux Installation Guide

## Overview

This guide explains how to install the SAKIN Linux Agent on various Linux distributions. The agent runs as a systemd service and automatically starts on system boot.

## Quick Start

### Installation with Default Settings

```bash
# Download and run the installer
curl -sSL https://raw.githubusercontent.com/sakin-security/sakin-core/main/packaging/install-sakin-agent-linux.sh | sudo bash

# Or download and run locally
sudo wget https://raw.githubusercontent.com/sakin-security/sakin-core/main/packaging/install-sakin-agent-linux.sh
sudo chmod +x install-sakin-agent-linux.sh
sudo ./install-sakin-agent-linux.sh
```

### Installation with Custom Endpoint

```bash
sudo ./install-sakin-agent-linux.sh --endpoint http://sakin-server:5001 --token yourtoken123
```

## Installation Options

| Option | Description |
|--------|-------------|
| `--endpoint <url>` | SAKIN ingest endpoint URL (e.g., `http://localhost:5001`) |
| `--token <token>` | Authentication token for the agent |
| `--dry-run` | Run pre-flight checks without installing |
| `--uninstall` | Remove the agent from the system |
| `--help` | Show help message |

## Supported Distributions

| Distribution | Package Manager | Status |
|--------------|-----------------|--------|
| Ubuntu/Debian | apt | ✅ Full Support |
| RHEL/CentOS/Fedora | dnf/yum | ✅ Full Support |
| Arch Linux | pacman | ✅ Full Support |
| Alpine Linux | apk | ✅ Full Support |

## Prerequisites

- .NET 8.0 Runtime
- systemd-based init system
- Root/sudo access
- curl or wget
- jq
- iptables (for network policy features)

## Installation Steps

### 1. Pre-flight Check

Run with `--dry-run` to verify system compatibility:

```bash
sudo ./install-sakin-agent-linux.sh --dry-run
```

This will check:
- Root privileges
- Required tools (curl, tar, jq)
- .NET 8.0 runtime availability
- Supported operating system

### 2. Install the Agent

#### Interactive Installation

```bash
sudo ./install-sakin-agent-linux.sh
```

The installer will:
1. Install system dependencies (.NET 8.0, curl, jq, iptables)
2. Create the `sakin-agent` user
3. Create required directories (`/opt/sakin-agent`, `/var/log/sakin-agent`, `/var/lib/sakin-agent`)
4. Copy agent files
5. Generate configuration
6. Install and enable the systemd service
7. Start the service

#### Non-interactive Installation

```bash
# With endpoint and token
sudo ./install-sakin-agent-linux.sh \
    --endpoint https://sakin.example.com \
    --token your-auth-token
```

### 3. Verify Installation

```bash
# Check service status
sudo systemctl status sakin-agent-linux

# View service logs
sudo journalctl -u sakin-agent-linux -f

# Check if auto-start is enabled
sudo systemctl is-enabled sakin-agent-linux
```

## Post-Installation Verification

The installer performs automatic verification:

1. ✅ Service installed
2. ✅ Service is running
3. ✅ Service is enabled for auto-start on boot
4. ✅ Configuration file exists

### Manual Verification

```bash
# Check service status
sudo systemctl status sakin-agent-linux

# Expected output:
# ● sakin-agent-linux.service - SAKIN Linux Agent
#      Loaded: loaded (/etc/systemd/system/sakin-agent-linux.service; enabled; preset: enabled)
#      Active: active (running) since ...

# Check auto-start is enabled
sudo systemctl is-enabled sakin-agent-linux
# Expected output: enabled

# View recent logs
sudo journalctl -u sakin-agent-linux -n 50

# Verify config file
cat /opt/sakin-agent/appsettings.json
```

## Configuration

### Default Paths

| Path | Description |
|------|-------------|
| `/opt/sakin-agent/` | Agent installation directory |
| `/opt/sakin-agent/Sakin.Agent.Linux.dll` | Main binary |
| `/opt/sakin-agent/appsettings.json` | Configuration file |
| `/var/log/sakin-agent/` | Log files |
| `/var/lib/sakin-agent/` | Data directory |
| `/etc/systemd/system/sakin-agent-linux.service` | Systemd service file |

### Configuration File

The agent uses `appsettings.json` with the following structure:

```json
{
  "Agent": {
    "AgentId": "linux-agent-server01",
    "Hostname": "server01",
    "DryRun": false
  },
  "Sakin": {
    "IngestEndpoint": "http://localhost:5001",
    "AgentToken": "your-token"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "ClientId": "linux-agent-server01"
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
```

### Environment Variables

The agent supports environment variable overrides:

| Variable | Description |
|----------|-------------|
| `SAKIN_ENDPOINT` | Override ingest endpoint URL |
| `SAKIN_TOKEN` | Override authentication token |
| `SAKIN_AGENT_NAME` | Override agent name |
| `SAKIN_LOG_LEVEL` | Override log level (Debug, Information, Warning, Error) |

Example:
```bash
sudo systemctl edit sakin-agent-linux
```

Add:
```ini
[Service]
Environment="SAKIN_ENDPOINT=http://new-server:5001"
Environment="SAKIN_TOKEN=new-token"
```

Then reload:
```bash
sudo systemctl daemon-reload
sudo systemctl restart sakin-agent-linux
```

## Service Management

### Start the Service

```bash
sudo systemctl start sakin-agent-linux
```

### Stop the Service

```bash
sudo systemctl stop sakin-agent-linux
```

### Restart the Service

```bash
sudo systemctl restart sakin-agent-linux
```

### View Logs

```bash
# Follow logs in real-time
sudo journalctl -u sakin-agent-linux -f

# View last 100 log entries
sudo journalctl -u sakin-agent-linux -n 100

# View logs since last boot
sudo journalctl -u sakin-agent-linux -b
```

### Disable Auto-Start

```bash
# Disable (prevent auto-start on boot)
sudo systemctl disable sakin-agent-linux

# Re-enable
sudo systemctl enable sakin-agent-linux
```

## Uninstallation

```bash
sudo ./install-sakin-agent-linux.sh --uninstall
```

This will:
1. Stop and disable the service
2. Remove the systemd service file
3. Delete agent files and directories
4. Remove the `sakin-agent` user

## Troubleshooting

### Service Fails to Start

```bash
# Check detailed error
sudo systemctl status sakin-agent-linux
sudo journalctl -u sakin-agent-linux -n 50

# Common issues:
# - .NET 8.0 not installed
# - Invalid configuration
# - Port already in use
```

### Check .NET Installation

```bash
dotnet --list-runtimes
# Expected: Microsoft.NETCore.App 8.0.x
```

### Verify Configuration

```bash
# Check if config is valid JSON
cat /opt/sakin-agent/appsettings.json | jq .

# Test configuration
cd /opt/sakin-agent
dotnet Sakin.Agent.Linux.dll --check-config
```

### Connectivity Test

```bash
# Test connection to SAKIN endpoint
curl -v http://your-sakin-server:5001/health
```

### Service Logs

```bash
# Enable debug logging
sudo systemctl edit sakin-agent-linux

[Service]
Environment="SAKIN_LOG_LEVEL=Debug"

sudo systemctl daemon-reload
sudo systemctl restart sakin-agent-linux
```

### Check Disk Space

```bash
df -h /opt/sakin-agent /var/log/sakin-agent
```

### Reset Installation

```bash
# Uninstall and reinstall
sudo ./install-sakin-agent-linux.sh --uninstall
sudo ./install-sakin-agent-linux.sh --endpoint http://your-server:5001
```

## Security Considerations

1. **Run as non-root user**: The agent runs as `sakin-agent` user
2. **Protected filesystem**: systemd ProtectSystem=true limits filesystem access
3. **No new privileges**: NoNewPrivileges=true prevents privilege escalation
4. **PrivateTmp**: Isolated temporary files
5. **Auto-restart**: Service restarts automatically on failure

## Support

- **Documentation**: https://github.com/sakin-security/sakin-core/tree/main/packaging
- **Issues**: https://github.com/sakin-security/sakin-core/issues
- **Discord**: https://discord.gg/sakin-security
