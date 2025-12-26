# Sakin Agent - Linux Installation Guide

## Overview

The Sakin Linux Agent is a plug-n-play service that collects and forwards telemetry to the Sakin Ingest endpoint. This installer handles dependencies, configuration, and service management automatically.

## Prerequisites

- **Supported OS**: Ubuntu/Debian, CentOS/RHEL, Arch Linux, Alpine Linux.
- **Privileges**: Root access (`sudo`) is required.
- **Connectivity**: Access to the Sakin Ingest Endpoint.

## Installation

### 1. Download the Installer

Download `install-sakin-agent-linux.sh` and the agent binary to your target machine.

### 2. Run the Installer

Make sure the script is executable:

```bash
chmod +x install-sakin-agent-linux.sh
```

Run the installer with your configuration:

```bash
sudo ./install-sakin-agent-linux.sh \
  --endpoint "http://your-sakin-ingest:5001" \
  --token "YOUR_AGENT_TOKEN"
```

### Options

| Flag | Description |
|------|-------------|
| `--endpoint` | **Required.** The full URL of the Sakin Ingest service. |
| `--token` | **Required.** Authentication token for the agent. |
| `--proxy` | Optional HTTP Proxy URL (e.g., `http://proxy:8080`). |
| `--dry-run` | Check dependencies and configuration without installing. |
| `--no-hash` | Skip SHA256 checksum verification (Not recommended for prod). |

## Post-Installation

The service `sakin-agent-linux` will start automatically.

- **Check Status**: `systemctl status sakin-agent-linux`
- **View Logs**: `journalctl -u sakin-agent-linux -f`
- **Diagnostics**: Run the agent binary with health check:
  ```bash
  /opt/sakin-agent/Sakin.Agent.Linux --check-health
  ```

## Troubleshooting

- **Service fails to start**: Check if `appsettings.json` has valid JSON.
- **Permission denied**: Ensure `sakin-agent` user owns `/opt/sakin-agent`.
- **Connectivity issues**: Verify `--proxy` settings if behind a corporate firewall.
