# SAKIN Agent Windows Installation Guide

## Overview

This guide explains how to install the SAKIN Windows Agent on Windows Server and Windows desktop systems. The agent runs as a Windows Service and automatically starts on system boot.

## Quick Start

### GUI Installation

1. Download the installer: `sakin-agent-windows-setup.exe`
2. Run the installer as Administrator
3. Follow the installation wizard
4. Enter your SAKIN endpoint and authentication token
5. Click Install

### Silent Installation

```powershell
# Install with default settings
.\sakin-agent-windows-setup.exe /S

# Install to custom directory
.\sakin-agent-windows-setup.exe /S /D=C:\Program Files\SAKIN\Agent

# Full silent install with parameters
.\sakin-agent-windows-setup.exe /S \
    /endpoint=http://sakin-server:5001 \
    /token=your-auth-token
```

## Installation Options

### GUI Wizard

| Screen | Description |
|--------|-------------|
| Welcome | Installation welcome screen |
| Directory | Choose installation location |
| Configuration | Enter SAKIN endpoint and token |
| Install | Progress bar |
| Finish | Success message and verification |

### Command Line Options

| Option | Description |
|--------|-------------|
| `/S` | Silent installation |
| `/D=<path>` | Installation directory |
| `/endpoint=<url>` | SAKIN ingest endpoint URL |
| `/token=<token>` | Authentication token |

### Examples

```powershell
# Silent installation with endpoint
.\sakin-agent-windows-setup.exe /S /endpoint=http://sakin-server:5001

# Full configuration
.\sakin-agent-windows-setup.exe /S /D=C:\SAKIN\Agent /endpoint=https://sakin.example.com /token=mytoken
```

## Prerequisites

- Windows 10/11 or Windows Server 2019+
- .NET 8.0 Runtime (installed automatically if missing)
- Administrator privileges
- 50 MB free disk space

## Installation Steps

### 1. Download the Installer

Download `sakin-agent-windows-setup.exe` from:
- GitHub Releases: https://github.com/sakin-security/sakin-core/releases
- SAKIN Portal: https://portal.sakin.io/downloads

### 2. Run as Administrator

Right-click the installer and select "Run as administrator"

![Run as Administrator](https://via.placeholder.com/400x200?text=Run+as+Administrator)

### 3. Follow Installation Wizard

#### Welcome Screen
Click "Next" to begin

#### Installation Directory
Default: `C:\Program Files\SAKIN\Agent`

#### Configuration
Enter your SAKIN configuration:
- **SAKIN Endpoint**: `http://your-sakin-server:5001`
- **Authentication Token**: Your agent token

#### Complete Installation
Click "Install" to begin

### 4. Verify Installation

After installation, the installer verifies:

1. ✅ Service installed
2. ✅ Service is running
3. ✅ Service is enabled for auto-start on boot
4. ✅ Configuration is valid

### 5. Post-Installation

Check the service:

```powershell
# Using PowerShell
Get-Service sakin-agent-windows

# Using Services Manager
services.msc
```

Expected status: **Running**
Startup type: **Automatic**

## Post-Installation Verification

### Check Service Status

```powershell
# PowerShell
Get-Service sakin-agent-windows | Format-List

# Output:
# Name                : sakin-agent-windows
# DisplayName         : SAKIN Agent
# Status              : Running
# StartType           : Automatic
```

### Verify Auto-Start

```powershell
Get-Service sakin-agent-windows | Select-Object Name, StartType, Status

# Expected:
# Name                    StartType  Status
# ----                    ---------  ------
# sakin-agent-windows     Automatic  Running
```

### Check Event Logs

Open Event Viewer and check for SAKIN Agent entries:

```powershell
# PowerShell - view SAKIN Agent events
Get-EventLog -LogName Application -Source "SAKIN Agent" -Newest 10
```

### Test Configuration

```powershell
# Check if agent can connect to endpoint
Invoke-WebRequest -Uri http://your-sakin-server:5001/health -UseBasicParsing
```

## Configuration

### Default Paths

| Path | Description |
|------|-------------|
| `C:\Program Files\SAKIN\Agent\` | Installation directory |
| `C:\Program Files\SAKIN\Agent\appsettings.json` | Configuration file |
| `C:\Program Files\SAKIN\Agent\logs\` | Log files |
| `%APPDATA%\SAKIN\Agent\` | Data directory |

### Configuration File

Located at: `C:\Program Files\SAKIN\Agent\appsettings.json`

```json
{
  "Agent": {
    "Name": "eventlog-collector-01",
    "Hostname": "server01",
    "AgentId": "windows-agent-server01",
    "DryRun": false
  },
  "Sakin": {
    "IngestEndpoint": "http://your-sakin-server:5001",
    "AgentToken": "your-auth-token"
  },
  "EventLogs": {
    "Enabled": true,
    "Mode": "RealTime",
    "PollInterval": 5000,
    "BatchSize": 100,
    "LogNames": ["Security", "System", "Application"]
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  }
}
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `SAKIN_ENDPOINT` | Override ingest endpoint URL |
| `SAKIN_TOKEN` | Override authentication token |
| `SAKIN_LOG_LEVEL` | Override log level |

Set via system environment variables or service registry:

```powershell
# Set environment variable for service
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\sakin-agent-windows" `
    -Name Environment -Value "SAKIN_ENDPOINT=http://new-server:5001"

# Restart service
Restart-Service sakin-agent-windows
```

## Service Management

### Start the Service

```powershell
# PowerShell
Start-Service sakin-agent-windows

# Command Prompt (as admin)
sc start sakin-agent-windows
```

### Stop the Service

```powershell
# PowerShell
Stop-Service sakin-agent-windows

# Command Prompt (as admin)
sc stop sakin-agent-windows
```

### Restart the Service

```powershell
# PowerShell
Restart-Service sakin-agent-windows

# Command Prompt (as admin)
sc stop sakin-agent-windows
sc start sakin-agent-windows
```

### Change Startup Type

```powershell
# Set to Manual
Set-Service sakin-agent-windows -StartupType Manual

# Set to Automatic (Delayed Start)
Set-Service sakin-agent-windows -StartupType AutomaticDelayedStart

# Set to Disabled
Set-Service sakin-agent-windows -StartupType Disabled
```

### View Logs

```powershell
# Event Viewer (GUI)
eventvwr.msc

# PowerShell - view application logs
Get-EventLog -LogName Application -Source "SAKIN Agent" -Newest 50

# PowerShell - view last 24 hours
Get-EventLog -LogName Application -Source "SAKIN Agent" -After (Get-Date).AddHours(-24)
```

### Text Logs

Logs are also written to:
```
C:\Program Files\SAKIN\Agent\logs\agent.log
```

## Uninstallation

### Using Control Panel

1. Open Control Panel > Programs > Programs and Features
2. Find "SAKIN Agent" in the list
3. Click Uninstall and follow the wizard

### Using PowerShell

```powershell
# Get uninstall command
Get-ItemProperty -Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SAKINAgent"

# Run uninstaller
& "C:\Program Files\SAKIN\Agent\uninstall.exe"

# Silent uninstall
& "C:\Program Files\SAKIN\Agent\uninstall.exe" /S
```

### Using Command Line

```cmd
# Silent uninstall
"C:\Program Files\SAKIN\Agent\uninstall.exe" /S
```

### Manual Uninstallation

```powershell
# Stop and remove service (as admin)
sc stop sakin-agent-windows
sc delete sakin-agent-windows

# Remove installation directory
Remove-Item -Recurse -Force "C:\Program Files\SAKIN\Agent"

# Remove registry entries
Remove-Item -Recurse -Path "HKLM:\Software\SAKIN"
Remove-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application\SAKIN Agent"

# Remove shortcuts
Remove-Item -Force "$env:USERPROFILE\Desktop\SAKIN Agent - Logs.lnk"
Remove-Item -Force "$env:USERPROFILE\Desktop\SAKIN Agent - Uninstall.lnk"
```

## Troubleshooting

### Service Fails to Start

```powershell
# Check detailed error
Get-EventLog -LogName Application -Source "SAKIN Agent" -Newest 20

# Check service dependencies
sc qc sakin-agent-windows

# Common issues:
# - .NET 8.0 not installed
# - Invalid configuration (check appsettings.json)
# - Kafka endpoint not reachable
```

### Check .NET Installation

```powershell
dotnet --list-runtimes

# Expected output should include:
# Microsoft.NETCore.App 8.0.x
# Microsoft.WindowsDesktop.App 8.0.x (optional)
```

### Verify Configuration

```powershell
# Validate JSON syntax
Get-Content "C:\Program Files\SAKIN\Agent\appsettings.json" | ConvertFrom-Json
```

### Connectivity Test

```powershell
# Test SAKIN endpoint
Invoke-WebRequest -Uri http://your-sakin-server:5001/health -UseBasicParsing

# Test Kafka connectivity
Test-NetConnection your-kafka-server -Port 9092
```

### Enable Debug Logging

Edit `appsettings.json` and change:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

Then restart the service:

```powershell
Restart-Service sakin-agent-windows
```

### Check Disk Space

```powershell
Get-PSDrive C | Select-Object Name, Used, Free
```

### Reset Installation

```powershell
# Uninstall
& "C:\Program Files\SAKIN\Agent\uninstall.exe" /S

# Reinstall with new settings
.\sakin-agent-windows-setup.exe /S /endpoint=http://new-server:5001 /token=new-token
```

### Event Log Permissions

If event log collection fails:

```powershell
# Check event log permissions
wevtutil gl security

# Grant permissions (as admin)
wevtutil sl security /ca:O:BAG:SYD:(A;;0xf0007;;;SY)(A;;0x7;;;BA)
```

## Security Considerations

1. **Run as SYSTEM**: The service runs with Local System privileges
2. **Event Log Access**: Requires read access to Security, System, Application logs
3. **Network Communication**: Outbound HTTPS/HTTP to SAKIN server
4. **No Inbound Ports**: Agent doesn't open inbound ports
5. **Audit Logging**: All actions are logged to Windows Event Log

## Firewall Configuration

The installer automatically creates firewall rules. Manual configuration:

```powershell
# Allow outbound to SAKIN endpoint
New-NetFirewallRule -DisplayName "SAKIN Agent - Outbound" `
    -Direction Outbound `
    -Protocol TCP `
    -RemotePort 5001 `
    -Action Allow

# Allow outbound to Kafka
New-NetFirewallRule -DisplayName "SAKIN Agent - Kafka" `
    -Direction Outbound `
    -Protocol TCP `
    -RemotePort 9092 `
    -Action Allow
```

## Support

- **Documentation**: https://github.com/sakin-security/sakin-core/tree/main/packaging
- **Issues**: https://github.com/sakin-security/sakin-core/issues
- **Discord**: https://discord.gg/sakin-security
- **Portal**: https://portal.sakin.io
