import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Trend, Counter, Rate, Gauge } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const SOAR_API_URL = __ENV.SOAR_API_URL || 'http://localhost:8080';
const TEST_DURATION = __ENV.TEST_DURATION || '2m';

// Custom metrics
const playbookExecutionLatency = new Trend('playbook_execution_latency_ms');
const endToEndLatency = new Trend('e2e_latency_ms');
const notificationDispatchLatency = new Trend('notification_dispatch_latency_ms');
const playbookSuccessRate = new Rate('playbook_success');
const notificationSuccessRate = new Rate('notification_success');
const agentCommandLatency = new Trend('agent_command_latency_ms');
const playbookExecutionThroughput = new Counter('playbook_executions');
const externalApiCallLatency = new Trend('external_api_call_latency_ms');
const playbookErrorRate = new Rate('playbook_errors');
const activePlaybookExecutions = new Gauge('active_executions');

export const options = {
  stages: [
    { duration: '30s', target: 20 },   // Ramp up
    { duration: '1m', target: 20 },    // Steady state
    { duration: '30s', target: 0 },    // Ramp down
  ],
  thresholds: {
    playbook_execution_latency_ms: [
      'p(50) < 500',
      'p(95) < 2000',
      'p(99) < 5000',
    ],
    e2e_latency_ms: [
      'p(95) < 3000',
      'p(99) < 5000',
    ],
    notification_dispatch_latency_ms: [
      'p(95) < 1000',
    ],
    playbook_success: ['rate > 0.95'],
    notification_success: ['rate > 0.90'],
    playbook_errors: ['rate < 0.05'],
  },
};

// Alert generator
const generateAlert = () => ({
  alert_id: `alert-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
  rule_id: `rule-${Math.floor(Math.random() * 100)}`,
  severity: Math.floor(Math.random() * 5) + 1,
  source_ip: `192.168.${Math.floor(Math.random() * 256)}.${Math.floor(Math.random() * 256)}`,
  destination_ip: `10.0.${Math.floor(Math.random() * 256)}.${Math.floor(Math.random() * 256)}`,
  event_type: ['ssh_login', 'failed_auth', 'port_scan', 'data_exfiltration'][Math.floor(Math.random() * 4)],
  username: `user-${Math.floor(Math.random() * 1000)}`,
  hostname: `host-${Math.floor(Math.random() * 100)}`,
  timestamp: new Date().toISOString(),
  description: 'Security event triggered by correlation rule',
});

// Execute playbook
const executePlaybook = (playbookId, alert) => {
  const payload = JSON.stringify({
    alert_id: alert.alert_id,
    playbook_id: playbookId,
    parameters: {
      block_ip: alert.source_ip,
      ticket_title: `Security Alert: ${alert.event_type}`,
      ticket_severity: alert.severity,
      alert_details: alert,
    },
  });

  const startTime = Date.now();
  const response = http.post(
    `${SOAR_API_URL}/api/playbooks/${playbookId}/execute`,
    payload,
    {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer mock-token',
        'X-Request-ID': `req-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
        'X-Trace-ID': `trace-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
      },
      timeout: '10s',
    }
  );

  const latency = Date.now() - startTime;
  playbookExecutionLatency.add(latency);
  playbookExecutionThroughput.add(1);

  if (response.status === 200 || response.status === 202) {
    playbookSuccessRate.add(1);
  } else {
    playbookSuccessRate.add(0);
    playbookErrorRate.add(1);
  }

  return {
    execution_id: response.json('execution_id'),
    latency: latency,
    status: response.status,
  };
};

// Simulate external API call (e.g., Firewall block)
const callExternalApi = (action, target) => {
  // Simulate network latency to external firewall/SOAR system
  const simulatedLatency = Math.random() * 1000 + 200; // 200-1200ms
  externalApiCallLatency.add(simulatedLatency);

  return {
    success: Math.random() < 0.95, // 95% success rate
    latency: simulatedLatency,
    action: action,
    target: target,
  };
};

// Send notification
const sendNotification = (channel, message) => {
  const payload = JSON.stringify({
    channel: channel,
    message: message,
    timestamp: new Date().toISOString(),
  });

  const startTime = Date.now();
  const response = http.post(
    `${SOAR_API_URL}/api/notifications/${channel}/send`,
    payload,
    {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer mock-token',
      },
      timeout: '5s',
    }
  );

  const latency = Date.now() - startTime;
  notificationDispatchLatency.add(latency);

  if (response.status === 200 || response.status === 202) {
    notificationSuccessRate.add(1);
  } else {
    notificationSuccessRate.add(0);
  }

  return {
    latency: latency,
    status: response.status,
  };
};

// Send agent command
const sendAgentCommand = (agentId, command) => {
  const payload = JSON.stringify({
    agent_id: agentId,
    command: command,
    timeout: 30,
  });

  const startTime = Date.now();
  const response = http.post(
    `${SOAR_API_URL}/api/agents/${agentId}/command`,
    payload,
    {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer mock-token',
      },
      timeout: '30s',
    }
  );

  const latency = Date.now() - startTime;
  agentCommandLatency.add(latency);

  return {
    latency: latency,
    status: response.status,
    result: response.json('result'),
  };
};

export default function () {
  const alert = generateAlert();
  const overallStart = Date.now();

  group('Block IP Playbook Execution', () => {
    const playbook = executePlaybook('block-ip', alert);

    if (playbook.status === 200 || playbook.status === 202) {
      group('External API - Firewall Block', () => {
        const apiResult = callExternalApi('block', alert.source_ip);
        check(true, {
          'Firewall block: Success': () => apiResult.success,
          'Firewall block: Latency < 2s': () => apiResult.latency < 2000,
        });
      });

      sleep(0.5);

      group('Notification - Slack', () => {
        const slackMsg = `🚨 Security Alert: ${alert.event_type} from ${alert.source_ip}`;
        const notification = sendNotification('slack', slackMsg);
        check(true, {
          'Slack notification: Status 2xx': () => notification.status >= 200 && notification.status < 300,
          'Slack notification: Latency < 1s': () => notification.latency < 1000,
        });
      });

      sleep(0.5);

      group('Notification - Email', () => {
        const emailMsg = `Security alert: ${alert.event_type} from ${alert.source_ip} on ${alert.hostname}`;
        const notification = sendNotification('email', emailMsg);
        check(true, {
          'Email notification: Status 2xx': () => notification.status >= 200 && notification.status < 300,
          'Email notification: Latency < 2s': () => notification.latency < 2000,
        });
      });

      sleep(0.5);

      group('Agent Command - Isolate Host', () => {
        const command = sendAgentCommand(alert.hostname, 'isolate-network');
        check(true, {
          'Agent command: Executed': () => command.status >= 200 && command.status < 300,
          'Agent command: Latency < 10s': () => command.latency < 10000,
        });
      });
    }
  });

  sleep(0.5);

  group('Create Ticket Playbook Execution', () => {
    const playbook = executePlaybook('create-jira-ticket', alert);

    if (playbook.status === 200 || playbook.status === 202) {
      group('External API - Jira Create Ticket', () => {
        const payload = JSON.stringify({
          project: 'SEC',
          issue_type: 'Security Incident',
          summary: `${alert.event_type} - ${alert.source_ip}`,
          description: `Alert: ${alert.alert_id}`,
          priority: alert.severity,
        });

        const startTime = Date.now();
        const response = http.post(
          `${SOAR_API_URL}/api/integrations/jira/issue`,
          payload,
          {
            headers: {
              'Content-Type': 'application/json',
              'Authorization': 'Bearer mock-token',
            },
            timeout: '5s',
          }
        );

        const latency = Date.now() - startTime;
        externalApiCallLatency.add(latency);

        check(response, {
          'Jira ticket: Status 2xx': (r) => r.status >= 200 && r.status < 300,
          'Jira ticket: Latency < 3s': (r) => latency < 3000,
        });
      });

      sleep(0.5);

      group('Notification - Jira Ticket Created', () => {
        const jiraMsg = `Jira ticket created for alert ${alert.alert_id}`;
        const notification = sendNotification('jira', jiraMsg);
        check(true, {
          'Jira notification: Status 2xx': () => notification.status >= 200 && notification.status < 300,
        });
      });
    }
  });

  sleep(0.5);

  group('Collect Evidence Playbook', () => {
    const evidencePayload = JSON.stringify({
      alert_id: alert.alert_id,
      collect_network_logs: true,
      collect_system_logs: true,
      collect_memory_dump: false,
      hosts: [alert.hostname],
    });

    const startTime = Date.now();
    const response = http.post(
      `${SOAR_API_URL}/api/playbooks/collect-evidence/execute`,
      evidencePayload,
      {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': 'Bearer mock-token',
        },
        timeout: '30s',
      }
    );

    const latency = Date.now() - startTime;
    playbookExecutionLatency.add(latency);

    check(response, {
      'Evidence collection: Status 2xx': (r) => r.status >= 200 && r.status < 300,
      'Evidence collection: Latency < 20s': (r) => latency < 20000,
    });

    if (response.status === 200 || response.status === 202) {
      playbookSuccessRate.add(1);
    } else {
      playbookSuccessRate.add(0);
    }
  });

  const e2eLatency = Date.now() - overallStart;
  endToEndLatency.add(e2eLatency);

  check(true, {
    'End-to-end: Latency < 5s': () => e2eLatency < 5000,
  });

  activePlaybookExecutions.set(__VU);
  sleep(1);
}

export function teardown() {
  console.log(`SOAR playbook tests completed`);
  console.log(`Total playbook executions: ${playbookExecutionThroughput.value}`);
}
