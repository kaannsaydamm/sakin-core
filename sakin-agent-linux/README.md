# SAKIN Linux Agent

A secure, containerized Linux agent for the SAKIN SIEM+SOAR platform that provides active response capabilities.

## Features

- **Secure Command Execution**: Enumerated command types prevent arbitrary RCE
- **IP Blocking**: iptables-based network blocking with persistence
- **Host Isolation**: Network isolation capabilities
- **Script Execution**: Allowlisted script execution with security controls
- **Dry Run Mode**: Safe testing without actual execution
- **Audit Logging**: Complete audit trail of all operations
- **Heartbeat Monitoring**: Regular status reporting

## Security Design

The agent implements a security-first approach:

1. **Enumerated Commands**: Only predefined command types are accepted
2. **Parameter Validation**: All inputs are validated and sanitized
3. **Allowlist Control**: Only pre-approved scripts can be executed
4. **Non-Root Execution**: Runs as non-privileged user
5. **Audit Trail**: Every action is logged to the audit topic

## Installation

### Systemd Service

1. Copy the binary to `/opt/sakin-agent/`
2. Copy the systemd unit file:
   ```bash
   sudo cp packaging/systemd/sakin-agent-linux.service /etc/systemd/system/
   sudo systemctl daemon-reload
   sudo systemctl enable sakin-agent-linux
   ```
3. Create configuration file at `/opt/sakin-agent/appsettings.json`
4. Start the service:
   ```bash
   sudo systemctl start sakin-agent-linux
   ```

### Docker

```bash
docker build -t sakin-agent-linux .
docker run -d \
  --name sakin-agent \
  --net host \
  --cap-add NET_ADMIN \
  -v /opt/sakin-agent/appsettings.json:/app/appsettings.json \
  -v /opt/sakin-agent/scripts:/app/scripts \
  sakin-agent-linux
```

## Configuration

Key configuration options in `appsettings.json`:

```json
{
  "Agent": {
    "AgentId": "linux-agent-server01",
    "DryRun": false,
    "AllowlistedScripts": ["security-check.sh"],
    "ScriptsDirectory": "scripts",
    "CommandExpireTime": "00:05:00"
  },
  "Kafka": {
    "BootstrapServers": "kafka:9092"
  }
}
```

## Supported Commands

| Command Type | Description | Linux Implementation |
|--------------|-------------|---------------------|
| `BlockIp` | Block IP address | `iptables -I INPUT -s <IP> -j DROP` |
| `UnblockIp` | Unblock IP address | `iptables -D INPUT -s <IP> -j DROP` |
| `IsolateHost` | Isolate from network | `iptables -P INPUT DROP` |
| `RunAllowlistedScript` | Execute approved script | `/bin/bash <script>` |

## Development

### Building

```bash
dotnet build Sakin.Agent.Linux.csproj -c Release
```

### Testing

```bash
dotnet test
```

### Local Development

```bash
dotnet run --project Sakin.Agent.Linux.csproj
```

## Monitoring

The agent publishes heartbeat messages to the audit topic every 5 minutes and command results to the agent-result topic.

## Troubleshooting

Check logs with:
```bash
# Systemd
sudo journalctl -u sakin-agent-linux -f

# Docker
docker logs -f sakin-agent
```