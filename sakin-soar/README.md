# Sakin SOAR — Security Orchestration, Automation, and Response

## Overview

The SOAR service enables automated incident response workflows by executing security playbooks in response to alerts. It orchestrates actions across security tools, sends notifications, executes agent commands, and provides comprehensive audit logging for compliance.

## Key Features

### Playbook Execution
- **Workflow Orchestration**: Sequential step execution with conditional logic
- **Event-Driven**: Triggered by security alerts from the correlation engine
- **Retry Policies**: Configurable retry logic for failed steps
- **State Management**: Track playbook execution state and step results
- **Conditional Execution**: Skip/branch logic based on conditions

### Notification Channels
- **Slack Integration**: Send alerts and notifications to Slack channels
- **Email Notifications**: SMTP-based email alerts and reports
- **Jira Integration**: Create and update tickets automatically
- **Webhooks**: HTTP webhooks for custom integrations

### Agent Command Execution
- **Distributed Task Execution**: Execute commands on remote agents
- **Command Types**: Block IP, isolate host, quarantine file, gather logs
- **Async Execution**: Non-blocking command dispatch with completion tracking
- **Result Aggregation**: Collect results from multiple agents
- **Timeout Handling**: Configurable command timeouts

### Audit Logging (Sprint 7)
- **Action Tracking**: Log all playbook executions and steps
- **User Attribution**: Track which user triggered the playbook
- **Step Results**: Detailed logs of each step's execution
- **Compliance**: Full audit trail for regulatory requirements

## Architecture

```
[Alerts] ──▶ Kafka (alerts topic)
     │
     ▼
[SOAR Worker]
     │
     ├─▶ [Playbook Router]
     │   │
     │   ▼
     │ [Load Playbook Definition]
     │
     ├─▶ [Step Executor]
     │   ├─ [Notification Handler] ──▶ Slack/Email/Jira
     │   └─ [Agent Command Dispatcher] ──▶ Remote Agents
     │
     ├─▶ [Audit Logger] ──▶ Kafka (audit-events topic)
     │
     └─▶ [Result Aggregator]
         │
         ▼
     [PostgreSQL]
```

## Playbook Development

### Playbook Structure

```json
{
  "Id": "playbook-block-attacker",
  "Name": "Block Attacker IP",
  "Description": "Automatically block source IP after brute force detection",
  "Enabled": true,
  "TriggerRules": ["rule-brute-force-detection"],
  "Steps": [
    {
      "Id": "step-1",
      "Name": "Notify Security Team",
      "Type": "Notification",
      "Channel": "slack",
      "Message": "Brute force attack detected from {alert.SourceIp}",
      "OnFailure": "Continue"
    },
    {
      "Id": "step-2",
      "Name": "Create Jira Ticket",
      "Type": "Notification",
      "Channel": "jira",
      "Project": "SEC",
      "IssueType": "Incident",
      "Summary": "Brute Force: {alert.SourceIp}",
      "OnFailure": "Continue"
    },
    {
      "Id": "step-3",
      "Name": "Block IP on Firewall",
      "Type": "AgentCommand",
      "Agent": "firewall-agent-01",
      "Command": "block_ip",
      "Parameters": {
        "ip": "{alert.SourceIp}",
        "duration": 3600
      },
      "OnFailure": "Retry"
    },
    {
      "Id": "step-4",
      "Name": "Quarantine Host",
      "Type": "AgentCommand",
      "Agent": "endpoint-agent-01",
      "Command": "isolate_host",
      "Parameters": {
        "hostname": "{alert.Hostname}"
      },
      "OnFailure": "Continue"
    }
  ]
}
```

### Step Types

#### Notification Steps
```json
{
  "Type": "Notification",
  "Channel": "slack|email|jira",
  "Message": "Alert message with {field} substitution",
  "To": "channel or email",
  "Template": "optional-template-name"
}
```

#### Agent Command Steps
```json
{
  "Type": "AgentCommand",
  "Agent": "agent-name",
  "Command": "block_ip|isolate_host|quarantine_file|gather_logs",
  "Parameters": {
    "key": "value",
    "reference": "{alert.field}"
  },
  "Timeout": 30000
}
```

#### Conditional Steps
```json
{
  "Type": "Condition",
  "Condition": "alert.Severity >= 8",
  "ThenSteps": [...],
  "ElseSteps": [...]
}
```

### Example Playbooks

**Rapid Response to Ransomware**
```json
{
  "Id": "playbook-ransomware-response",
  "Name": "Ransomware Rapid Response",
  "TriggerRules": ["rule-ransomware-detection"],
  "Steps": [
    {
      "Type": "Notification",
      "Channel": "slack",
      "Message": "🚨 RANSOMWARE DETECTED: {alert.Hostname}"
    },
    {
      "Type": "AgentCommand",
      "Agent": "edr-agent",
      "Command": "isolate_host",
      "Parameters": {"hostname": "{alert.Hostname}"}
    },
    {
      "Type": "Notification",
      "Channel": "jira",
      "Project": "SEC",
      "IssueType": "Incident",
      "Summary": "Ransomware: {alert.Hostname}",
      "Priority": "Highest"
    }
  ]
}
```

**Data Exfiltration Response**
```json
{
  "Id": "playbook-data-exfil-response",
  "Name": "Data Exfiltration Response",
  "TriggerRules": ["rule-data-exfiltration"],
  "Steps": [
    {
      "Type": "AgentCommand",
      "Agent": "firewall-agent",
      "Command": "block_ip",
      "Parameters": {
        "ip": "{alert.DestinationIp}",
        "duration": 86400
      }
    },
    {
      "Type": "Notification",
      "Channel": "email",
      "To": "security-team@company.com",
      "Message": "Data exfiltration blocked: {alert.DestinationIp}"
    }
  ]
}
```

## Configuration

### appsettings.json

```json
{
  "Kafka": {
    "BootstrapServers": "kafka:9092",
    "AlertsTopic": "alerts",
    "AuditEventsTopic": "audit-events",
    "ConsumerGroup": "soar-service"
  },
  "Database": {
    "ConnectionString": "Server=postgres;Database=sakin;User Id=sakin;Password=password;"
  },
  "Playbooks": {
    "PlaybooksPath": "./playbooks",
    "ReloadInterval": 300
  },
  "Notifications": {
    "Slack": {
      "WebhookUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL",
      "Enabled": true
    },
    "Email": {
      "SmtpServer": "smtp.gmail.com",
      "SmtpPort": 587,
      "From": "soar@company.com",
      "Username": "your-email@gmail.com",
      "Password": "your-app-password",
      "Enabled": true
    },
    "Jira": {
      "Url": "https://jira.company.com",
      "Username": "soar-bot",
      "ApiToken": "your-api-token",
      "Enabled": true
    }
  },
  "Agents": {
    "Timeout": 30000,
    "RetryCount": 3,
    "RetryDelayMs": 1000
  },
  "OpenTelemetry": {
    "Enabled": true,
    "OtlpEndpoint": "http://localhost:4318"
  }
}
```

### Environment Variables

- `Kafka__BootstrapServers`: Kafka broker connection
- `Kafka__AlertsTopic`: Input topic for alerts
- `Database__ConnectionString`: PostgreSQL connection
- `SLACK_WEBHOOK_URL`: Slack webhook for notifications
- `SMTP_SERVER`: Email SMTP server
- `SMTP_PORT`: Email SMTP port
- `JIRA_URL`: Jira instance URL
- `JIRA_USERNAME`: Jira username
- `JIRA_API_TOKEN`: Jira API token
- `ASPNETCORE_ENVIRONMENT`: Runtime environment
- `OTEL_EXPORTER_OTLP_ENDPOINT`: OpenTelemetry endpoint

## Running Locally

### Prerequisites
- Docker (for Kafka, PostgreSQL)
- .NET 8 SDK
- Slack workspace (for testing notifications)

### Steps

```bash
# 1. Start infrastructure
cd deployments
docker compose -f docker-compose.dev.yml up -d

# 2. Verify services
./scripts/verify-services.sh

# 3. Initialize database
psql -f scripts/postgres/01-init-database.sql

# 4. Load playbooks
mkdir -p sakin-soar/playbooks
cp docs/example-playbooks/*.json sakin-soar/playbooks/

# 5. Configure environment
cd sakin-soar
cp .env.example .env
# Edit .env with your Slack webhook URL and credentials

# 6. Run SOAR service
cd Sakin.SOAR
dotnet run
```

## Development

### Building

```bash
dotnet build sakin-soar/Sakin.SOAR/Sakin.SOAR.csproj
```

### Testing

```bash
dotnet test tests/Sakin.SOAR.Tests/Sakin.SOAR.Tests.csproj
```

### Adding Custom Playbooks

1. Create JSON playbook file in `sakin-soar/playbooks/`
2. Define steps with notification and/or agent commands
3. Set trigger rules
4. Restart SOAR service or reload via API
5. Monitor execution in audit logs

## Integration

### Input
- **Kafka Topic**: `alerts`
- **Format**: AlertEntity with correlation rule details and risk score

### Output
- **Kafka Topic**: `audit-events`
- **Database**: PostgreSQL playbook execution history
- **External**: Slack messages, Jira tickets, email, agent commands

### Dependencies
- **PostgreSQL**: Playbook definitions, execution history
- **Kafka**: Alert triggering, audit event publishing
- **Slack API**: Notification delivery
- **Jira API**: Ticket creation
- **SMTP Server**: Email delivery
- **Agent Network**: Remote command execution

## Monitoring

### Metrics
- `soar_playbook_executions_total`: Total playbook executions
- `soar_playbook_success_total`: Successful playbook runs
- `soar_playbook_failures_total`: Failed playbook runs
- `soar_step_duration_seconds`: Duration per step
- `soar_notification_sent_total`: Notifications sent
- `soar_command_executed_total`: Agent commands executed

### Health Checks
- `GET /healthz`: Service health status
- Kafka connectivity
- Database connectivity
- External service connectivity (Slack, Jira, SMTP)

### Audit Logs
Structured JSON logs via Serilog:
- Playbook execution start/end
- Step execution details
- Notification delivery
- Agent command results
- Error handling and exceptions

## Notification Testing

### Test Slack Integration
```bash
# Manually send test message
curl -X POST -H 'Content-type: application/json' \
  --data '{"text":"Test message from SOAR"}' \
  $SLACK_WEBHOOK_URL
```

### Test Email Integration
```bash
# Check SMTP connectivity
telnet smtp.gmail.com 587
```

### Test Jira Integration
```bash
# Verify API token
curl -u soar-bot:$JIRA_API_TOKEN \
  https://jira.company.com/rest/api/3/myself
```

## Troubleshooting

### Playbook Not Triggering
1. Verify alert matches trigger rule
2. Check playbook is enabled: `GET /api/playbooks`
3. Review alert processing logs
4. Verify Kafka topic connectivity

### Notifications Not Sending
1. Verify credentials in appsettings.json
2. Test external service connectivity (curl)
3. Check firewall rules allow outbound connections
4. Review notification service logs

### Agent Commands Failing
1. Verify agent is registered: `GET /api/agents`
2. Check agent connectivity
3. Verify command syntax matches agent capabilities
4. Review agent logs for error details

### Audit Logs Missing
1. Verify Kafka audit topic: `kafka-topics.sh --list`
2. Check audit logger is enabled
3. Verify database persistence
4. Review Serilog configuration

## Further Reading

- [SOAR Playbook Examples](../docs/sprint7-soar.md)
- [Alert Lifecycle Management](../docs/alert-lifecycle.md)
- [Monitoring Guide](../docs/monitoring.md)
- [Playbook Best Practices](../docs/playbook-guide.md)
