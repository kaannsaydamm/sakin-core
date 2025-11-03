-- Sakin Security Platform - ClickHouse Database Initialization
-- This script creates the required tables and views for analytics

-- Create database if not exists
CREATE DATABASE IF NOT EXISTS sakin_analytics;

-- Switch to the analytics database
USE sakin_analytics;

-- Network Events Table - For real-time network traffic analysis
CREATE TABLE IF NOT EXISTS network_events (
    event_time DateTime64(3) DEFAULT now64(),
    event_date Date DEFAULT toDate(event_time),
    event_id String,
    event_type LowCardinality(String),
    severity LowCardinality(String),
    src_ip IPv4,
    dst_ip IPv4,
    protocol LowCardinality(String),
    src_port UInt16,
    dst_port UInt16,
    bytes_sent UInt64,
    bytes_received UInt64,
    packet_count UInt32,
    session_duration UInt32,
    metadata String
) ENGINE = MergeTree()
PARTITION BY toYYYYMM(event_date)
ORDER BY (event_date, event_time, src_ip, dst_ip)
TTL event_date + INTERVAL 90 DAY
SETTINGS index_granularity = 8192;

-- Security Alerts Table - For threat detection and correlation
CREATE TABLE IF NOT EXISTS security_alerts (
    alert_time DateTime64(3) DEFAULT now64(),
    alert_date Date DEFAULT toDate(alert_time),
    alert_id String,
    alert_type LowCardinality(String),
    severity LowCardinality(String),
    status LowCardinality(String),
    title String,
    description String,
    src_ip IPv4,
    dst_ip IPv4,
    indicators Array(String),
    mitre_tactics Array(String),
    mitre_techniques Array(String),
    risk_score UInt8,
    false_positive UInt8 DEFAULT 0
) ENGINE = MergeTree()
PARTITION BY toYYYYMM(alert_date)
ORDER BY (alert_date, alert_time, severity, alert_type)
TTL alert_date + INTERVAL 180 DAY
SETTINGS index_granularity = 8192;

-- DNS Queries Table - For DNS analysis and threat hunting
CREATE TABLE IF NOT EXISTS dns_queries (
    query_time DateTime64(3) DEFAULT now64(),
    query_date Date DEFAULT toDate(query_time),
    src_ip IPv4,
    dns_server IPv4,
    query_name String,
    query_type LowCardinality(String),
    response_code LowCardinality(String),
    response_ips Array(IPv4),
    ttl UInt32
) ENGINE = MergeTree()
PARTITION BY toYYYYMM(query_date)
ORDER BY (query_date, query_time, src_ip)
TTL query_date + INTERVAL 30 DAY
SETTINGS index_granularity = 8192;

-- TLS/SNI Data Table - For HTTPS traffic analysis
CREATE TABLE IF NOT EXISTS tls_sessions (
    session_time DateTime64(3) DEFAULT now64(),
    session_date Date DEFAULT toDate(session_time),
    src_ip IPv4,
    dst_ip IPv4,
    sni String,
    tls_version LowCardinality(String),
    cipher_suite String,
    certificate_subject String,
    certificate_issuer String,
    certificate_valid UInt8
) ENGINE = MergeTree()
PARTITION BY toYYYYMM(session_date)
ORDER BY (session_date, session_time, sni)
TTL session_date + INTERVAL 30 DAY
SETTINGS index_granularity = 8192;

-- Application Metrics Table - For service health and performance
CREATE TABLE IF NOT EXISTS application_metrics (
    metric_time DateTime64(3) DEFAULT now64(),
    metric_date Date DEFAULT toDate(metric_time),
    service_name LowCardinality(String),
    metric_name LowCardinality(String),
    metric_value Float64,
    tags Map(String, String)
) ENGINE = MergeTree()
PARTITION BY toYYYYMM(metric_date)
ORDER BY (metric_date, metric_time, service_name, metric_name)
TTL metric_date + INTERVAL 30 DAY
SETTINGS index_granularity = 8192;

-- Create materialized views for common queries

-- Top talkers by bytes (last 24 hours)
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_top_talkers_24h
ENGINE = SummingMergeTree()
PARTITION BY toYYYYMMDD(event_date)
ORDER BY (event_date, src_ip, dst_ip)
POPULATE AS
SELECT
    event_date,
    src_ip,
    dst_ip,
    count() as connection_count,
    sum(bytes_sent) as total_bytes_sent,
    sum(bytes_received) as total_bytes_received
FROM network_events
WHERE event_time >= now() - INTERVAL 24 HOUR
GROUP BY event_date, src_ip, dst_ip;

-- Alert statistics by severity
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_alert_stats_hourly
ENGINE = SummingMergeTree()
PARTITION BY toYYYYMMDD(alert_date)
ORDER BY (alert_date, alert_hour, severity)
POPULATE AS
SELECT
    alert_date,
    toStartOfHour(alert_time) as alert_hour,
    severity,
    count() as alert_count,
    avg(risk_score) as avg_risk_score
FROM security_alerts
GROUP BY alert_date, alert_hour, severity;

-- Insert sample data for testing
INSERT INTO network_events (event_id, event_type, severity, src_ip, dst_ip, protocol, src_port, dst_port, bytes_sent, bytes_received, packet_count, session_duration, metadata) VALUES
    ('evt-001', 'connection', 'info', '192.168.1.100', '8.8.8.8', 'UDP', 54321, 53, 128, 512, 2, 1, '{"query": "example.com"}'),
    ('evt-002', 'connection', 'info', '192.168.1.100', '1.1.1.1', 'UDP', 54322, 53, 128, 512, 2, 1, '{"query": "google.com"}'),
    ('evt-003', 'connection', 'info', '192.168.1.101', '172.217.14.206', 'TCP', 54323, 443, 2048, 8192, 24, 15, '{"sni": "www.google.com"}'),
    ('evt-004', 'connection', 'warning', '192.168.1.102', '203.0.113.42', 'TCP', 54324, 8080, 1024, 4096, 12, 30, '{"suspicious": true}');

INSERT INTO security_alerts (alert_id, alert_type, severity, status, title, description, src_ip, dst_ip, indicators, mitre_tactics, mitre_techniques, risk_score) VALUES
    ('alert-001', 'suspicious_connection', 'medium', 'open', 'Suspicious Outbound Connection', 'Connection to known suspicious IP', '192.168.1.102', '203.0.113.42', ['suspicious-ip', 'unusual-port'], ['command-and-control'], ['T1071'], 65),
    ('alert-002', 'dns_tunneling', 'high', 'open', 'Potential DNS Tunneling Detected', 'Abnormal DNS query pattern detected', '192.168.1.105', '8.8.8.8', ['dns-tunneling', 'high-entropy'], ['exfiltration'], ['T1048'], 85);

INSERT INTO dns_queries (src_ip, dns_server, query_name, query_type, response_code, response_ips, ttl) VALUES
    ('192.168.1.100', '8.8.8.8', 'example.com', 'A', 'NOERROR', ['93.184.216.34'], 3600),
    ('192.168.1.100', '8.8.8.8', 'google.com', 'A', 'NOERROR', ['172.217.14.206'], 300),
    ('192.168.1.101', '1.1.1.1', 'github.com', 'A', 'NOERROR', ['140.82.121.4'], 60);

INSERT INTO tls_sessions (src_ip, dst_ip, sni, tls_version, cipher_suite, certificate_subject, certificate_issuer, certificate_valid) VALUES
    ('192.168.1.101', '172.217.14.206', 'www.google.com', 'TLSv1.3', 'TLS_AES_256_GCM_SHA384', 'CN=www.google.com', 'CN=GTS CA 1C3', 1),
    ('192.168.1.101', '142.250.185.46', 'www.youtube.com', 'TLSv1.3', 'TLS_AES_256_GCM_SHA384', 'CN=*.youtube.com', 'CN=GTS CA 1C3', 1);

INSERT INTO application_metrics (service_name, metric_name, metric_value, tags) VALUES
    ('network-sensor', 'cpu_usage', 45.2, {'host': 'sensor-01'}),
    ('network-sensor', 'memory_usage', 512.5, {'host': 'sensor-01', 'unit': 'MB'}),
    ('ingest', 'events_processed', 1523, {'host': 'ingest-01'}),
    ('correlation', 'alerts_generated', 5, {'host': 'correlation-01'});
