# SAKIN SOAR - Security Orchestration, Automation and Response

## Overview

The SAKIN SOAR service is a security automation platform that orchestrates and automates response actions triggered by security alerts. Built as a microservice within the SAKIN platform, it enables automatic playbook execution, multi-channel notifications, and agent-based remediation.

## Architecture

```
[Alert Stream (Kafka)]
         │
         ▼
    [SOAR Worker]
         │
    ┌────┴────┬────────┬──────────┐
    │         │        │          │
    ▼         ▼        ▼          ▼
[Notifications] [Agent Dispatch] [Audit]
    │
    ├──▶ Slack
    ├──▶ Email
    └──▶ Jira
```

## Components

### PlaybookExecutor

Orchestrates playbook execution with step-based workflow:

```csharp
public interface IPlaybookExecutor
{
    Task<PlaybookExecutionResult> ExecutePlaybookAsync(
        string playbookId,
        AlertEntity alert,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
}
```

**Features:**
- Sequential step execution
- Conditional execution based on alert properties
- Retry policies and error handling
- Step-level and playbook-level result tracking

### NotificationService

Multi-channel notification delivery:

- **Slack**: Direct channel or webhook-based messages
- **Email**: SMTP-based with HTML templates
- **Jira**: Automatic ticket creation with alert context

### AgentCommandDispatcher

Distributes commands to remote agents for:
- File isolation
- Process termination
- Log collection
- Remediation tasks

### SoarWorker

Background service consuming alert action messages from Kafka and orchestrating responses.

## Playbook Definition

Playbooks are defined in YAML format at `/etc/sakin/playbooks/`:

```yaml
id: phishing-response
name: "Phishing Email Response"
enabled: true
steps:
  - id: slack-notify
    action: notify_slack
    condition: medium_or_higher
    parameters:
      channel: "#security-alerts"
      message: "🚨 Phishing alert: {{ alert.RuleName }}"

  - id: create-ticket
    action: create_jira_ticket
    parameters:
      summary: "Alert: {{ alert.RuleName }}"
      description: "Rule: {{ alert.RuleName }}\nSeverity: {{ alert.Severity }}"

  - id: isolate-host
    action: dispatch_agent_command
    parameters:
      target_agent_id: "agent-001"
      command: "IsolateHost"
      payload: '{"hostname": "{{ context.hostname }}"}'
```

## Configuration

### appsettings.json

```json
{
  "Kafka": {
    "BootstrapServers": "kafka:9092",
    "ConsumerGroup": "sakin-soar-group"
  },
  "KafkaTopics": {
    "AlertActions": "sakin-alerts-actions"
  },
  "SOAR": {
    "PlaybooksPath": "/etc/sakin/playbooks",
    "MaxConcurrentExecutions": 10,
    "ExecutionTimeoutSeconds": 300
  },
  "Notifications": {
    "SlackWebhookUrl": "${SLACK_WEBHOOK_URL}",
    "JiraBaseUrl": "${JIRA_BASE_URL}",
    "JiraApiToken": "${JIRA_API_TOKEN}",
    "SmtpHost": "${SMTP_HOST}",
    "SmtpPort": 587,
    "SmtpUsername": "${SMTP_USERNAME}",
    "SmtpPassword": "${SMTP_PASSWORD}"
  },
  "AuditLogging": {
    "Enabled": true,
    "Topic": "audit-log",
    "IncludePayload": true
  },
  "Telemetry": {
    "ServiceName": "sakin-soar",
    "OtlpEndpoint": "http://jaeger:4317",
    "EnableTracing": true,
    "EnableMetrics": true
  }
}
```

### Environment Variables

```bash
# Notifications
SLACK_WEBHOOK_URL=https://hooks.slack.com/services/YOUR/WEBHOOK/URL
JIRA_BASE_URL=https://jira.example.com
JIRA_API_TOKEN=your_jira_api_token
SMTP_HOST=smtp.example.com
SMTP_PORT=587
SMTP_USERNAME=alerts@example.com
SMTP_PASSWORD=password

# Telemetry
Telemetry__ServiceName=sakin-soar
Telemetry__OtlpEndpoint=http://jaeger:4317
```

## Audit Logging

All playbook executions are logged to the `audit-log` Kafka topic:

```json
{
  "eventId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2024-01-15T10:30:00Z",
  "correlationId": "alert-correlation-123",
  "user": "system",
  "action": "playbook.execution.completed",
  "service": "sakin-soar",
  "details": {
    "executionId": "550e8400-e29b-41d4-a716-446655440001",
    "playbookId": "phishing-response",
    "alertId": "550e8400-e29b-41d4-a716-446655440002",
    "success": true,
    "stepCount": 3,
    "duration": 2500
  }
}
```

## Metrics

Prometheus metrics exposed on `/metrics`:

- `soar_playbook_executions_total` - Total playbook runs
- `soar_playbook_success_total` - Successful executions
- `soar_playbook_duration_seconds` - Execution time histogram
- `soar_step_execution_duration_seconds` - Step-level timing
- `soar_notification_attempts_total` - Notification sends
- `soar_agent_commands_total` - Agent command dispatches

## Tracing

All playbook executions are traced with OpenTelemetry:

- Execution spans with step children
- Notification spans
- Agent dispatch spans
- Error tracking and exception recording

View traces in Jaeger: http://localhost:16686

## Deployment

### Docker Compose

```yaml
soar:
  build:
    context: ../
    dockerfile: sakin-soar/Dockerfile
  depends_on:
    - kafka
    - postgres
    - redis
  environment:
    - Kafka__BootstrapServers=kafka:9092
    - Telemetry__OtlpEndpoint=http://jaeger:4317
  volumes:
    - ../configs/playbooks:/etc/sakin/playbooks
```

### Kubernetes

```bash
kubectl apply -f k8s/helm/soar-chart/
```

## API Reference

### Playbook Execution

**POST /api/soar/playbooks/{playbookId}/execute**

```json
{
  "alertId": "550e8400-e29b-41d4-a716-446655440000",
  "parameters": {
    "hostname": "host-01",
    "username": "user@domain.com"
  }
}
```

**Response:**
```json
{
  "executionId": "550e8400-e29b-41d4-a716-446655440001",
  "playbookId": "phishing-response",
  "success": true,
  "stepResults": [
    {
      "stepId": "slack-notify",
      "success": true,
      "output": "Message sent to #security-alerts",
      "duration": 1200
    }
  ]
}
```

## Troubleshooting

### Playbooks not executing

1. Check SOAR Worker logs
2. Verify Kafka connectivity
3. Validate playbook YAML syntax
4. Check playbook file permissions

### Notifications failing

1. Verify credentials (Slack webhook, Jira token, SMTP password)
2. Check network connectivity to notification services
3. Review Alertmanager logs for failures
4. Test connectivity manually:
   ```bash
   curl -X POST $SLACK_WEBHOOK_URL -d '{"text":"test"}'
   ```

### Agent commands not executing

1. Verify agent connectivity
2. Check agent command handler logs
3. Validate command payload format
4. Review Jaeger traces for dispatch failures

## Best Practices

1. **Conditional Execution**: Use conditions to prevent unnecessary actions
   ```yaml
   condition: high_severity
   ```

2. **Error Handling**: Set retry policies for critical steps
   ```yaml
   retryCount: 3
   retryDelay: 5
   ```

3. **Notifications**: Always notify on critical findings
4. **Auditing**: Enable full payload logging for compliance
5. **Testing**: Test playbooks in staging before production

## Advanced Usage

### Custom Step Actions

Extend PlaybookExecutor to support custom actions:

```csharp
case "custom_action":
    return await ExecuteCustomActionAsync(step, alert, parameters, cancellationToken);
```

### Dynamic Playbook Loading

Load playbooks from database or remote source:

```csharp
public interface IPlaybookRepository
{
    Task<PlaybookDefinition?> GetPlaybookAsync(string playbookId);
}
```

### Multi-Step Orchestration

Chain multiple playbooks:

```yaml
triggers:
  - on: playbook-completed
    execute_playbook: next-step
```

## Performance Tuning

- Increase `MaxConcurrentExecutions` for parallel playbook runs
- Set `ExecutionTimeoutSeconds` based on slowest step
- Use caching for frequently accessed playbooks
- Monitor Kafka consumer lag

## Security Considerations

- Validate all input parameters before execution
- Use secrets management for credentials (not env vars in prod)
- Audit all agent commands
- Rate-limit playbook execution to prevent abuse
- Restrict playbook access by role/team

## Integration Examples

### Slack Notification with Alert Context

```yaml
- id: notify-slack
  action: notify_slack
  parameters:
    channel: "#{{ alert.Severity }}-alerts"
    message: |
      🚨 Alert: {{ alert.RuleName }}
      Severity: {{ alert.Severity }}
      Source: {{ alert.Source }}
      Rule ID: {{ alert.RuleId }}
```

### Jira Ticket with Alert Details

```yaml
- id: create-ticket
  action: create_jira_ticket
  parameters:
    summary: "[{{ alert.Severity | upper }}] {{ alert.RuleName }}"
    description: |
      h2. Alert Details
      * Rule: {{ alert.RuleName }}
      * Severity: {{ alert.Severity }}
      * Source: {{ alert.Source }}
      * Time: {{ alert.TriggeredAt }}
      
      h2. Correlation Context
      {{ alert.Context | tojson }}
```

### Remote Host Remediation

```yaml
- id: isolate-host
  action: dispatch_agent_command
  condition: critical
  parameters:
    target_agent_id: "{{ context.agent_id }}"
    command: "IsolateHost"
    payload: |
      {
        "hostname": "{{ context.hostname }}",
        "duration_minutes": 60,
        "reason": "{{ alert.RuleName }}"
      }
```

## References

- [SAKIN Architecture](./docs/README.md)
- [Alert Lifecycle Management](./alert-lifecycle.md)
- [Audit Logging](./configuration.md)
- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
- [Jaeger Tracing](https://www.jaegertracing.io/docs/)
