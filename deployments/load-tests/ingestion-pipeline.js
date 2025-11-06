import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Trend, Counter, Gauge, Rate } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const KAFKA_BOOTSTRAP = __ENV.KAFKA_BOOTSTRAP || 'kafka:9092';
const TEST_DURATION = __ENV.TEST_DURATION || '5m';
const TARGET_EPS = parseInt(__ENV.TARGET_EPS || '1000');

// Custom metrics
const ingestionLatency = new Trend('ingestion_latency_ms');
const kafkaProducerThroughput = new Counter('kafka_producer_throughput');
const eventProcessed = new Counter('events_processed');
const malformedEventRate = new Rate('malformed_events');
const ingestionErrorRate = new Rate('ingestion_errors');
const activeConnections = new Gauge('active_connections');
const cpuUsagePercent = new Gauge('cpu_usage_percent');
const memoryUsageMB = new Gauge('memory_usage_mb');
const diskIOReadRate = new Gauge('disk_io_read_rate');
const diskIOWriteRate = new Gauge('disk_io_write_rate');

export const options = {
  stages: [
    { duration: '30s', target: Math.round(TARGET_EPS / 100) },      // Ramp-up
    { duration: '2m', target: Math.round(TARGET_EPS / 100) },        // Steady state
    { duration: '1m', target: Math.round((TARGET_EPS * 1.5) / 100) }, // Spike
    { duration: '1m', target: Math.round(TARGET_EPS / 100) },        // Back to normal
    { duration: '30s', target: 0 },                                   // Ramp-down
  ],
  thresholds: {
    ingestion_latency_ms: [
      'p(50) < 50',   // p50 < 50ms
      'p(95) < 100',  // p95 < 100ms
      'p(99) < 150',  // p99 < 150ms
    ],
    ingestion_errors: ['rate < 0.01'], // < 1% error rate
    malformed_events: ['rate < 0.02'],  // < 2% malformed
  },
  ext: {
    loadimpact: {
      projectID: 3356643,
      name: 'S.A.K.I.N. Ingestion Pipeline - 10k EPS Baseline',
    },
  },
};

// Valid Windows EventLog sample
const generateWindowsEventLog = (eventId, sourceIp) => ({
  EventID: eventId,
  Computer: `workstation-${Math.floor(Math.random() * 100)}`,
  TimeCreated: new Date().toISOString(),
  Level: ['Information', 'Warning', 'Error'][Math.floor(Math.random() * 3)],
  Message: `Event generated from ${sourceIp}`,
  SourceIP: sourceIp,
  DestinationIP: `10.0.${Math.floor(Math.random() * 256)}.${Math.floor(Math.random() * 256)}`,
  Port: Math.floor(Math.random() * 65535),
  UserName: `user-${Math.floor(Math.random() * 1000)}`,
  LogonType: [2, 3, 10][Math.floor(Math.random() * 3)],
  Provider: 'Security',
});

// Valid CEF format
const generateCEFSyslog = (eventId, sourceIp) => {
  const timestamp = new Date().toISOString();
  const hostname = `sensor-${Math.floor(Math.random() * 100)}`;
  const cefMessage = `CEF:0|SAKIN|NetworkMonitor|1.0|${eventId}|Security Event|3|src=${sourceIp} dst=10.0.1.1 spt=${Math.floor(Math.random() * 65535)} dpt=443 act=Deny`;
  return `${timestamp} ${hostname} ${cefMessage}`;
};

// Valid HTTP CEF payload
const generateHttpCefPayload = (eventId, sourceIp) => ({
  timestamp: new Date().toISOString(),
  source: sourceIp,
  destination: `10.0.${Math.floor(Math.random() * 256)}.${Math.floor(Math.random() * 256)}`,
  event_type: ['ssh_login', 'failed_auth', 'port_scan', 'data_exfiltration'][Math.floor(Math.random() * 4)],
  severity: [1, 2, 3, 4, 5][Math.floor(Math.random() * 5)],
  protocol: ['tcp', 'udp'][Math.floor(Math.random() * 2)],
  source_port: Math.floor(Math.random() * 65535),
  destination_port: [22, 80, 443, 3306, 5432, 5601][Math.floor(Math.random() * 6)],
  bytes_sent: Math.floor(Math.random() * 1000000),
  bytes_received: Math.floor(Math.random() * 1000000),
  username: `user-${Math.floor(Math.random() * 1000)}`,
  hostname: `host-${Math.floor(Math.random() * 100)}`,
  rule_triggered: `rule-${Math.floor(Math.random() * 100)}`,
  alert_id: `alert-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
});

// Malformed/invalid data (for chaos scenario)
const generateMalformedData = () => {
  const types = [
    'invalid json {broken',
    'not a valid syslog format at all',
    Buffer.from([0xFF, 0xFE, 0xFD, 0xFC]).toString(), // Invalid UTF-8
    '',
  ];
  return types[Math.floor(Math.random() * types.length)];
};

export function setup() {
  console.log(`Starting ingestion pipeline test targeting ${TARGET_EPS} EPS`);
  return { startTime: Date.now() };
}

export default function (data) {
  const sourceIp = `192.168.${Math.floor(Math.random() * 256)}.${Math.floor(Math.random() * 256)}`;
  const eventId = Math.floor(Math.random() * 10000);

  group('Windows EventLog Ingestion', () => {
    const startTime = Date.now();
    const isMalformed = Math.random() < 0.01; // 1% malformed for chaos scenario

    let payload;
    let response;

    if (isMalformed) {
      payload = generateMalformedData();
    } else {
      payload = JSON.stringify(generateWindowsEventLog(eventId, sourceIp));
    }

    try {
      response = http.post(`${BASE_URL}/api/ingest/windows-eventlog`, payload, {
        headers: {
          'Content-Type': isMalformed ? 'application/text' : 'application/json',
          'X-Request-ID': `req-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
          'X-Trace-ID': `trace-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
          'User-Agent': `K6LoadTester/${__ENV.VERSION || '1.0'}`,
        },
      });

      const latency = Date.now() - startTime;
      ingestionLatency.add(latency);
      kafkaProducerThroughput.add(1);
      eventProcessed.add(1);
      activeConnections.set(__VU);

      if (isMalformed) {
        malformedEventRate.add(1);
      } else {
        malformedEventRate.add(0);
      }

      check(response, {
        'Windows EventLog: Status 2xx': (r) => r.status >= 200 && r.status < 300,
        'Windows EventLog: Latency < 100ms': (r) => latency < 100,
      }) || ingestionErrorRate.add(1);
    } catch (error) {
      ingestionErrorRate.add(1);
      console.error(`Windows EventLog ingestion error: ${error}`);
    }
  });

  group('CEF Syslog Ingestion', () => {
    const startTime = Date.now();
    const payload = generateCEFSyslog(eventId, sourceIp);

    try {
      const response = http.post(
        `${BASE_URL}/api/ingest/syslog`,
        payload,
        {
          headers: {
            'Content-Type': 'text/plain',
            'X-Request-ID': `req-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
            'X-Trace-ID': `trace-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
          },
        }
      );

      const latency = Date.now() - startTime;
      ingestionLatency.add(latency);
      kafkaProducerThroughput.add(1);
      eventProcessed.add(1);

      check(response, {
        'CEF Syslog: Status 2xx': (r) => r.status >= 200 && r.status < 300,
        'CEF Syslog: Latency < 100ms': (r) => latency < 100,
      }) || ingestionErrorRate.add(1);
    } catch (error) {
      ingestionErrorRate.add(1);
      console.error(`CEF Syslog ingestion error: ${error}`);
    }
  });

  group('HTTP CEF Collector Ingestion', () => {
    const startTime = Date.now();
    const payload = JSON.stringify(generateHttpCefPayload(eventId, sourceIp));

    try {
      const response = http.post(
        `${BASE_URL}/api/ingest/http-cef`,
        payload,
        {
          headers: {
            'Content-Type': 'application/json',
            'X-Request-ID': `req-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
            'X-Trace-ID': `trace-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
            'Authorization': 'Bearer mock-token',
          },
        }
      );

      const latency = Date.now() - startTime;
      ingestionLatency.add(latency);
      kafkaProducerThroughput.add(1);
      eventProcessed.add(1);

      check(response, {
        'HTTP CEF: Status 2xx': (r) => r.status >= 200 && r.status < 300,
        'HTTP CEF: Latency < 100ms': (r) => latency < 100,
      }) || ingestionErrorRate.add(1);
    } catch (error) {
      ingestionErrorRate.add(1);
      console.error(`HTTP CEF ingestion error: ${error}`);
    }
  });

  sleep(0.1); // 100ms between events
}

export function teardown(data) {
  const duration = Date.now() - data.startTime;
  console.log(`Test completed in ${duration}ms`);
  console.log(`Total events processed: ${eventProcessed.value}`);
  console.log(`Estimated EPS: ${Math.round((eventProcessed.value / duration) * 1000)}`);
}
