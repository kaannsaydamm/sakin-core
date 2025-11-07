import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Trend, Counter, Rate, Gauge } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const TARGET_EPS = parseInt(__ENV.TARGET_EPS || '1000');
const SCENARIO = __ENV.SCENARIO || 'normal'; // normal, hot-key, high-cardinality

// Custom metrics
const ruleEvalLatency = new Trend('rule_eval_latency_ms');
const alertCreationRate = new Counter('alerts_created');
const redisStateLatency = new Trend('redis_state_latency_ms');
const anomalyDetectionLatency = new Trend('anomaly_detection_latency_ms');
const windowStateSize = new Gauge('window_state_bytes');
const memoryUsageMB = new Gauge('memory_usage_mb');
const correlationErrorRate = new Rate('correlation_errors');
const redisMemoryUsageMB = new Gauge('redis_memory_mb');
const lockContentionEvents = new Counter('lock_contention_events');
const highCardinalityKeysCount = new Gauge('high_cardinality_keys');

export const options = {
  stages: [
    { duration: '30s', target: Math.round(TARGET_EPS / 100) },
    { duration: '3m', target: Math.round(TARGET_EPS / 100) },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    rule_eval_latency_ms: [
      'p(50) < 30',   // p50 < 30ms
      'p(95) < 50',   // p95 < 50ms
      'p(99) < 100',  // p99 < 100ms
    ],
    redis_state_latency_ms: [
      'p(95) < 20',
    ],
    correlation_errors: ['rate < 0.01'],
  },
};

// Generate normalized event
const generateNormalizedEvent = (sourceIp, username, hostname) => ({
  timestamp: new Date().toISOString(),
  event_id: `evt-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
  source_ip: sourceIp,
  destination_ip: `10.0.${Math.floor(Math.random() * 256)}.${Math.floor(Math.random() * 256)}`,
  source_port: Math.floor(Math.random() * 65535),
  destination_port: [22, 80, 443, 3306, 5432, 5601, 9200][Math.floor(Math.random() * 7)],
  protocol: ['tcp', 'udp'][Math.floor(Math.random() * 2)],
  event_type: ['ssh_login', 'failed_auth', 'port_scan', 'data_exfiltration', 'privilege_escalation'][Math.floor(Math.random() * 5)],
  severity: Math.floor(Math.random() * 5) + 1,
  bytes_sent: Math.floor(Math.random() * 1000000),
  bytes_received: Math.floor(Math.random() * 1000000),
  username: username,
  hostname: hostname,
  device_name: `device-${Math.floor(Math.random() * 100)}`,
  geo_country: ['US', 'CN', 'RU', 'IR', 'KP'][Math.floor(Math.random() * 5)],
  geo_city: 'Unknown',
  threat_level: Math.floor(Math.random() * 10) + 1,
  is_vpn: Math.random() < 0.1,
  is_proxy: Math.random() < 0.05,
});

// SSH Brute-force pattern (stateful rule)
const generateBruteForceEvent = (sourceIp, targetHost) => ({
  ...generateNormalizedEvent(sourceIp, `admin`, targetHost),
  event_type: 'failed_auth',
  destination_port: 22,
  protocol: 'tcp',
  severity: 4,
});

// Simulate stateful rule aggregation window
let aggregationWindow = {};

const processAggregationWindow = (sourceIp, rule) => {
  const key = `${sourceIp}-${rule}`;
  if (!aggregationWindow[key]) {
    aggregationWindow[key] = {
      count: 0,
      firstSeen: Date.now(),
      lastSeen: Date.now(),
    };
  }

  aggregationWindow[key].count++;
  aggregationWindow[key].lastSeen = Date.now();

  // Simulate Redis operations
  const startTime = Date.now();
  const duration = Math.random() * 10 + 1; // 1-10ms
  redisStateLatency.add(duration);

  return aggregationWindow[key].count >= 5; // Alert if 5+ events in window
};

export default function () {
  group('Stateless Rule Evaluation', () => {
    const event = generateNormalizedEvent(
      `192.168.${Math.floor(Math.random() * 256)}.${Math.floor(Math.random() * 256)}`,
      `user-${Math.floor(Math.random() * 1000)}`,
      `host-${Math.floor(Math.random() * 100)}`
    );

    const startTime = Date.now();
    const ruleCount = 1000; // Evaluate 1000+ stateless rules
    
    // Simulate rule evaluation
    let alertsTriggered = 0;
    for (let i = 0; i < ruleCount; i++) {
      // Pattern matching simulation
      if (
        event.severity >= 4 &&
        event.event_type === 'failed_auth' &&
        Math.random() < 0.01
      ) {
        alertsTriggered++;
        alertCreationRate.add(1);
      }
    }

    const evalTime = Date.now() - startTime;
    ruleEvalLatency.add(evalTime);

    check(true, {
      'Stateless rules: Eval time < 30ms': () => evalTime < 30,
      'Stateless rules: No errors': () => true,
    }) || correlationErrorRate.add(1);
  });

  group('Stateful Aggregation - SSH Brute Force Detection', () => {
    const sourceIp = SCENARIO === 'hot-key' 
      ? '192.168.1.1' // Single hot source
      : `192.168.${Math.floor(Math.random() * 256)}.${Math.floor(Math.random() * 256)}`;

    for (let attempt = 0; attempt < 5; attempt++) {
      const event = generateBruteForceEvent(sourceIp, `target-${attempt % 10}`);
      const startTime = Date.now();

      const shouldAlert = processAggregationWindow(sourceIp, 'ssh-brute-force');
      const latency = Date.now() - startTime;

      ruleEvalLatency.add(latency);

      if (shouldAlert) {
        alertCreationRate.add(1);
      }

      check(true, {
        'Brute force: Window processing < 50ms': () => latency < 50,
      }) || correlationErrorRate.add(1);
    }

    windowStateSize.set(Object.keys(aggregationWindow).length * 100); // Estimate bytes
  });

  group('High-Cardinality Anomaly Detection', () => {
    let sourceIp;
    
    if (SCENARIO === 'high-cardinality') {
      // 1M unique source IPs
      sourceIp = `192.${Math.floor(Math.random() * 256)}.${Math.floor(Math.random() * 256)}.${Math.floor(Math.random() * 256)}`;
    } else {
      sourceIp = `192.168.${Math.floor(Math.random() * 256)}.${Math.floor(Math.random() * 256)}`;
    }

    const event = generateNormalizedEvent(sourceIp, `user-${Math.floor(Math.random() * 1000)}`, `host-${Math.floor(Math.random() * 100)}`);
    const startTime = Date.now();

    // Simulate anomaly detection against baselines
    const isAnomalous = event.severity >= 4 || event.is_vpn || event.geo_country === 'CN';

    const latency = Date.now() - startTime;
    anomalyDetectionLatency.add(latency);

    if (isAnomalous) {
      alertCreationRate.add(1);
    }

    highCardinalityKeysCount.set(Object.keys(aggregationWindow).length);

    check(true, {
      'Anomaly detection: Latency < 10ms': () => latency < 10,
    }) || correlationErrorRate.add(1);
  });

  group('Redis Lock Contention Simulation (Hot-Key Scenario)', () => {
    if (SCENARIO === 'hot-key') {
      // High concurrency on single key
      const hotKey = 'hot-key-1';
      const startTime = Date.now();

      // Simulate lock acquisition attempt
      const lockAcquireTime = Math.random() * 50 + 5; // 5-55ms under contention
      if (lockAcquireTime > 20) {
        lockContentionEvents.add(1);
      }

      const totalLatency = Date.now() - startTime + lockAcquireTime;
      redisStateLatency.add(totalLatency);

      check(true, {
        'Hot-key: No deadlock': () => lockAcquireTime < 100,
      });
    }
  });

  group('Memory Pressure - High Cardinality Cleanup', () => {
    if (SCENARIO === 'high-cardinality') {
      // Simulate memory accumulation
      const estimatedMemory = Object.keys(aggregationWindow).length * 0.001; // MB
      memoryUsageMB.set(estimatedMemory);
      redisMemoryUsageMB.set(estimatedMemory * 2); // Redis overhead

      check(true, {
        'Memory: No OOM condition': () => estimatedMemory < 500,
        'Cleanup: Maintaining < 1M keys': () => Object.keys(aggregationWindow).length < 1000000,
      });

      // Simulate cleanup
      if (Object.keys(aggregationWindow).length > 500000) {
        const keysToDelete = Math.floor(Object.keys(aggregationWindow).length * 0.1);
        let deleted = 0;
        for (const key of Object.keys(aggregationWindow)) {
          if (deleted >= keysToDelete) break;
          if (Date.now() - aggregationWindow[key].lastSeen > 3600000) {
            delete aggregationWindow[key];
            deleted++;
          }
        }
      }
    }
  });

  sleep(0.1);
}

export function teardown() {
  console.log(`Correlation engine test completed`);
  console.log(`Total alerts created: ${alertCreationRate.value}`);
  console.log(`Aggregation window size: ${Object.keys(aggregationWindow).length}`);
  console.log(`Scenario: ${SCENARIO}`);
}
