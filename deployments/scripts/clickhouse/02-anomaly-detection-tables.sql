-- Sakin Security Platform - Anomaly Detection Tables
-- This script creates tables for normalized events and baseline features

-- Create database if not exists
CREATE DATABASE IF NOT EXISTS sakin;

-- Switch to the sakin database
USE sakin;

-- Normalized Events Table - For storing all normalized events from Kafka
CREATE TABLE IF NOT EXISTS events (
    event_id UUID,
    received_at DateTime64(3),
    event_timestamp DateTime64(3),
    event_type LowCardinality(String),
    severity LowCardinality(String),
    source_ip String,
    destination_ip String,
    source_port UInt16,
    destination_port UInt16,
    protocol LowCardinality(String),
    username String,
    hostname String,
    device_name String,
    source String,
    source_type LowCardinality(String),
    event_date Date MATERIALIZED toDate(event_timestamp)
) ENGINE = MergeTree()
PARTITION BY toYYYYMM(event_date)
ORDER BY (event_date, username, hostname, event_timestamp)
TTL event_date + INTERVAL 30 DAY
SETTINGS index_granularity = 8192;

-- Baseline Features Table - For storing calculated baselines (optional, Redis is primary)
CREATE TABLE IF NOT EXISTS baseline_features (
    calculated_at DateTime64(3) DEFAULT now64(),
    metric_type LowCardinality(String),
    username String,
    hostname String,
    hour_of_day UInt8,
    mean Float64,
    stddev Float64,
    count UInt64,
    min_value Float64,
    max_value Float64,
    window_days UInt8,
    calc_date Date MATERIALIZED toDate(calculated_at)
) ENGINE = ReplacingMergeTree(calculated_at)
PARTITION BY toYYYYMM(calc_date)
ORDER BY (metric_type, username, hostname, hour_of_day, calc_date)
TTL calc_date + INTERVAL 7 DAY
SETTINGS index_granularity = 8192;

-- Create indexes for fast lookups
CREATE INDEX IF NOT EXISTS idx_events_username ON events (username) TYPE bloom_filter GRANULARITY 4;
CREATE INDEX IF NOT EXISTS idx_events_hostname ON events (hostname) TYPE bloom_filter GRANULARITY 4;

-- Create materialized views for common baseline queries

-- Hourly activity count by user
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_user_hourly_activity
ENGINE = SummingMergeTree()
PARTITION BY toYYYYMM(event_date)
ORDER BY (event_date, username, hour_of_day)
AS
SELECT
    event_date,
    username,
    toHour(event_timestamp) as hour_of_day,
    count() as event_count
FROM events
WHERE username != ''
GROUP BY event_date, username, hour_of_day;

-- Connection count by user and host
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_user_host_connections
ENGINE = SummingMergeTree()
PARTITION BY toYYYYMM(event_date)
ORDER BY (event_date, username, hostname)
AS
SELECT
    event_date,
    username,
    hostname,
    count() as connection_count,
    uniq(destination_port) as unique_ports
FROM events
WHERE username != '' AND hostname != ''
GROUP BY event_date, username, hostname;

-- Sample queries for baseline calculation

-- Calculate hourly activity baseline (mean, stddev) for a user
-- SELECT 
--     username,
--     toHour(event_timestamp) as hour_of_day,
--     count() as event_count,
--     avg(event_count) OVER (PARTITION BY username, hour_of_day) as mean,
--     stddevPop(event_count) OVER (PARTITION BY username, hour_of_day) as stddev
-- FROM events
-- WHERE event_timestamp >= now() - INTERVAL 7 DAY
--   AND username = 'admin'
-- GROUP BY username, hour_of_day, toStartOfHour(event_timestamp)
-- ORDER BY hour_of_day;

-- Calculate connection count baseline for user@host pairs
-- SELECT 
--     username,
--     hostname,
--     avg(conn_count) as mean,
--     stddevPop(conn_count) as stddev,
--     min(conn_count) as min_val,
--     max(conn_count) as max_val,
--     count() as sample_count
-- FROM (
--     SELECT 
--         username,
--         hostname,
--         count() as conn_count
--     FROM events
--     WHERE event_timestamp >= now() - INTERVAL 7 DAY
--       AND username != ''
--       AND hostname != ''
--     GROUP BY username, hostname, toStartOfHour(event_timestamp)
-- )
-- GROUP BY username, hostname
-- HAVING sample_count >= 3;
