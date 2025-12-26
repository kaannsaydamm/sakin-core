# Sakin Agent - Windows Installation Guide

## Overview

The Sakin Windows Agent (Collector) runs as a background Windows Service to collect logs and metrics.

## Prerequisites

- **OS**: Windows 10/11 or Windows Server 2016+.
- **Privileges**: Administrator rights are required.

## Installation

### 1. Run the Setup

Double-click `SakinAgentSetup.exe`. You will be prompted for Administrator approval.

### 2. Configuration Wizard

Follow the on-screen instructions:
1.  **Welcome**: Click Next.
2.  **Install Location**: Choose where to install (Default: `Program Files\Sakin Agent`).
3.  **Configuration**:
    *   **Endpoint**: Enter the Sakin Ingest URL (e.g., `http://localhost:5001`).
    *   **Token**: Enter your Agent Token.
    *   **Proxy**: (Optional) Enter proxy URL.
    *   **Uninstall Password**: (Optional) Set a password required to uninstall the agent.
4.  **Install**: The installer will copy files and register the service.

### 3. Verification

The service starts automatically.
- Open **Services.msc** and look for **Sakin Security Agent**. It should be `Running`.
- Open **Event Viewer** -> **Application** to see "Sakin Agent" logs.

## Uninstallation

To remove the agent:
1.  Go to **Control Panel -> Programs and Features**.
2.  Select **Sakin Agent** and click **Uninstall**.
3.  If a password was set, you must provide it to proceed.

## Troubleshooting

- **Service not starting**: Check `%ProgramData%\Sakin\Agent\logs` for error logs.
- **Connectivity**: Verify firewall rules allow outbound traffic to the Endpoint on port 5001 (or configured port).
