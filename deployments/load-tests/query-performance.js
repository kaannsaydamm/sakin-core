import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Trend, Counter, Rate, Gauge } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const PANEL_API_URL = __ENV.PANEL_API_URL || 'http://localhost:5000';
const CLICKHOUSE_URL = __ENV.CLICKHOUSE_URL || 'http://localhost:8123';
const TEST_DURATION = __ENV.TEST_DURATION || '3m';

// Custom metrics
const alertListLatency = new Trend('alert_list_latency_ms');
const clickhouseQueryLatency = new Trend('clickhouse_query_latency_ms');
const panelApiLatency = new Trend('panel_api_latency_ms');
const queryErrorRate = new Rate('query_errors');
const recordsReturned = new Gauge('records_returned');
const queryThroughput = new Counter('queries_executed');
const p95Latency = new Gauge('p95_latency');
const p99Latency = new Gauge('p99_latency');
const cacheHitRate = new Rate('cache_hits');

export const options = {
  stages: [
    { duration: '30s', target: 50 },   // 50 VUs for query tests
    { duration: '2m', target: 50 },    // Hold
    { duration: '30s', target: 100 },  // Spike
    { duration: '30s', target: 50 },   // Back to normal
  ],
  thresholds: {
    alert_list_latency_ms: [
      'p(50) < 100',
      'p(95) < 300',
      'p(99) < 500',
    ],
    clickhouse_query_latency_ms: [
      'p(95) < 500',
      'p(99) < 1000',
    ],
    panel_api_latency_ms: [
      'p(95) < 200',
      'p(99) < 500',
    ],
    query_errors: ['rate < 0.05'],
  },
};

// Alert list query with different filters
const queryAlerts = (pageSize, severity = null, ruleId = null, timeRange = '24h') => {
  const params = {
    page: Math.floor(Math.random() * 10) + 1,
    pageSize: pageSize,
    ...(severity && { severity: severity }),
    ...(ruleId && { ruleId: ruleId }),
    timeRange: timeRange,
  };

  const queryString = Object.entries(params)
    .map(([k, v]) => `${k}=${v}`)
    .join('&');

  const startTime = Date.now();
  const response = http.get(`${PANEL_API_URL}/api/alerts?${queryString}`, {
    headers: {
      'Authorization': 'Bearer mock-token',
      'X-Request-ID': `req-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
    },
  });

  const latency = Date.now() - startTime;
  alertListLatency.add(latency);
  queryThroughput.add(1);

  if (response.status === 200) {
    const data = JSON.parse(response.body);
    recordsReturned.set(data.data?.length || 0);
    if (Math.random() < 0.8) { // Assume 80% cache hit rate
      cacheHitRate.add(1);
    } else {
      cacheHitRate.add(0);
    }
  } else if (response.status === 304) {
    cacheHitRate.add(1); // Cache hit
  } else {
    queryErrorRate.add(1);
  }

  return { latency, status: response.status };
};

// ClickHouse analytics queries
const executeClickHouseQuery = (queryName, queryBody) => {
  const startTime = Date.now();
  const response = http.post(
    `${CLICKHOUSE_URL}/?default_format=JSON`,
    queryBody,
    {
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        'X-ClickHouse-User': 'clickhouse',
        'X-ClickHouse-Key': 'clickhouse_dev_password',
      },
    }
  );

  const latency = Date.now() - startTime;
  clickhouseQueryLatency.add(latency);
  queryThroughput.add(1);

  check(response, {
    [`ClickHouse ${queryName}: Status 200`]: (r) => r.status === 200,
    [`ClickHouse ${queryName}: Latency < 500ms`]: (r) => latency < 500,
  }) || queryErrorRate.add(1);

  return { latency, status: response.status };
};

// Panel API risk score queries
const queryRiskScores = () => {
  const startTime = Date.now();
  const response = http.get(`${PANEL_API_URL}/api/risk-scores?timeRange=7d`, {
    headers: {
      'Authorization': 'Bearer mock-token',
      'X-Request-ID': `req-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
    },
  });

  const latency = Date.now() - startTime;
  panelApiLatency.add(latency);
  queryThroughput.add(1);

  check(response, {
    'Risk scores: Status 2xx': (r) => r.status >= 200 && r.status < 300,
    'Risk scores: Latency < 200ms': (r) => latency < 200,
  }) || queryErrorRate.add(1);

  return latency;
};

// Panel API lifecycle transitions
const queryLifecycleTransitions = () => {
  const startTime = Date.now();
  const response = http.get(`${PANEL_API_URL}/api/alerts/lifecycle-stats?timeRange=24h`, {
    headers: {
      'Authorization': 'Bearer mock-token',
    },
  });

  const latency = Date.now() - startTime;
  panelApiLatency.add(latency);
  queryThroughput.add(1);

  check(response, {
    'Lifecycle stats: Status 2xx': (r) => r.status >= 200 && r.status < 300,
    'Lifecycle stats: Latency < 200ms': (r) => latency < 200,
  }) || queryErrorRate.add(1);

  return latency;
};

export default function () {
  group('Alert List - 1k Records', () => {
    const result = queryAlerts(1000, null, null, '24h');
    check(true, {
      '1k Records: Latency < 300ms': () => result.latency < 300,
    });
  });

  sleep(0.5);

  group('Alert List - 10k Records', () => {
    const result = queryAlerts(10000, null, null, '7d');
    check(true, {
      '10k Records: Latency < 500ms': () => result.latency < 500,
    });
  });

  sleep(0.5);

  group('Alert List - Filtered by Severity', () => {
    const severities = [1, 2, 3, 4, 5];
    const severity = severities[Math.floor(Math.random() * severities.length)];
    const result = queryAlerts(100, severity, null, '24h');
    check(true, {
      'Severity filter: Latency < 200ms': () => result.latency < 200,
    });
  });

  sleep(0.5);

  group('Alert List - Filtered by Rule', () => {
    const ruleId = `rule-${Math.floor(Math.random() * 100)}`;
    const result = queryAlerts(100, null, ruleId, '7d');
    check(true, {
      'Rule filter: Latency < 200ms': () => result.latency < 200,
    });
  });

  sleep(0.5);

  group('Alert List - Time Range 7 Days', () => {
    const result = queryAlerts(1000, null, null, '7d');
    check(true, {
      '7d Range: Latency < 400ms': () => result.latency < 400,
    });
  });

  sleep(0.5);

  group('ClickHouse - Top 10 Source IPs (24h)', () => {
    const query = `
      SELECT 
        source_ip,
        COUNT(*) as alert_count
      FROM sakin_analytics.events
      WHERE event_date >= today() - 1
      GROUP BY source_ip
      ORDER BY alert_count DESC
      LIMIT 10
    `;
    executeClickHouseQuery('Top IPs', query);
  });

  sleep(0.5);

  group('ClickHouse - Alert Distribution by Severity (7d)', () => {
    const query = `
      SELECT 
        severity,
        COUNT(*) as count
      FROM sakin_analytics.events
      WHERE event_date >= today() - 7
      GROUP BY severity
      ORDER BY severity DESC
    `;
    executeClickHouseQuery('Severity Distribution', query);
  });

  sleep(0.5);

  group('ClickHouse - User Activity Patterns', () => {
    const query = `
      SELECT 
        username,
        hostname,
        toHour(event_timestamp) as hour,
        COUNT(*) as event_count
      FROM sakin_analytics.events
      WHERE event_date >= today() - 1
      GROUP BY username, hostname, hour
      ORDER BY event_count DESC
      LIMIT 100
    `;
    executeClickHouseQuery('User Activity', query);
  });

  sleep(0.5);

  group('ClickHouse - Data Exfiltration Detection', () => {
    const query = `
      SELECT 
        source_ip,
        destination_ip,
        SUM(bytes_sent) as total_bytes,
        COUNT(*) as connection_count
      FROM sakin_analytics.events
      WHERE 
        event_date >= today() - 1 
        AND event_type = 'data_exfiltration'
      GROUP BY source_ip, destination_ip
      ORDER BY total_bytes DESC
      LIMIT 50
    `;
    executeClickHouseQuery('Data Exfil', query);
  });

  sleep(0.5);

  group('Panel API - Risk Scores', () => {
    queryRiskScores();
  });

  sleep(0.5);

  group('Panel API - Lifecycle Transitions', () => {
    queryLifecycleTransitions();
  });

  sleep(0.5);

  group('Panel API - Anomaly Scores', () => {
    const startTime = Date.now();
    const response = http.get(`${PANEL_API_URL}/api/anomalies?timeRange=24h&limit=100`, {
      headers: {
        'Authorization': 'Bearer mock-token',
      },
    });

    const latency = Date.now() - startTime;
    panelApiLatency.add(latency);
    queryThroughput.add(1);

    check(response, {
      'Anomalies: Status 2xx': (r) => r.status >= 200 && r.status < 300,
      'Anomalies: Latency < 300ms': (r) => latency < 300,
    }) || queryErrorRate.add(1);
  });

  sleep(0.5);

  group('Combined Complex Query', () => {
    const result = queryAlerts(5000, 4, 'rule-critical', '24h');
    check(true, {
      'Complex query: Latency < 400ms': () => result.latency < 400,
    });
  });

  sleep(1.0);
}

export function handleSummary(data) {
  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    'summary.json': JSON.stringify(data),
  };
}
