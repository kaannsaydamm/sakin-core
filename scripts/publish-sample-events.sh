#!/bin/bash
set -e

KAFKA_BOOTSTRAP="${KAFKA_BOOTSTRAP:-localhost:9093}"
TOPIC="${TOPIC:-normalized-events}"

echo "=== Publishing Sample Events to Kafka ==="
echo "Bootstrap servers: $KAFKA_BOOTSTRAP"
echo "Topic: $TOPIC"
echo ""

# Function to publish an event
publish_event() {
    local event="$1"
    echo "$event" | docker run --rm -i --network host confluentinc/cp-kafka:7.5.0 \
        kafka-console-producer \
        --broker-list "$KAFKA_BOOTSTRAP" \
        --topic "$TOPIC"
}

# Sample 1: Stateless authentication failure event
echo "Publishing stateless authentication failure event..."
AUTH_EVENT=$(cat <<'EOF'
{
  "EventId": "evt-001",
  "Source": "windows-server-01",
  "SourceType": "windows-eventlog",
  "ReceivedAt": "2024-01-15T10:30:00Z",
  "Normalized": {
    "Id": "norm-001",
    "Timestamp": "2024-01-15T10:30:00Z",
    "EventType": "authentication_failure",
    "Severity": "medium",
    "SourceIp": "192.168.1.100",
    "DestinationIp": "10.0.0.5",
    "Protocol": "rdp",
    "Metadata": {
      "username": "admin",
      "event_code": "4625",
      "action": "login_failed"
    }
  },
  "Raw": {
    "EventCode": 4625,
    "Message": "An account failed to log on"
  },
  "Enrichment": {}
}
EOF
)
publish_event "$AUTH_EVENT"
echo "✓ Published authentication failure event"

# Sample 2-6: Multiple failed RDP login attempts (for aggregation rule)
echo ""
echo "Publishing 5 failed RDP login attempts from same IP (for aggregation)..."
for i in {1..5}; do
    RDP_EVENT=$(cat <<EOF
{
  "EventId": "evt-rdp-$i",
  "Source": "windows-server-01",
  "SourceType": "windows-eventlog",
  "ReceivedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "Normalized": {
    "Id": "norm-rdp-$i",
    "Timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "EventType": "rdp_login_failed",
    "Severity": "high",
    "SourceIp": "203.0.113.42",
    "DestinationIp": "10.0.0.10",
    "Protocol": "rdp",
    "Metadata": {
      "username": "administrator",
      "event_code": "4625",
      "failure_reason": "invalid_password"
    }
  },
  "Raw": {
    "EventCode": 4625,
    "Message": "RDP login failed"
  },
  "Enrichment": {}
}
EOF
    )
    publish_event "$RDP_EVENT"
    echo "  ✓ Published RDP attempt $i/5"
    sleep 0.5
done

# Sample 7-10: Additional attempts to trigger aggregation threshold (>= 10)
echo ""
echo "Publishing 5 more attempts to trigger aggregation rule (threshold >= 10)..."
for i in {6..10}; do
    RDP_EVENT=$(cat <<EOF
{
  "EventId": "evt-rdp-$i",
  "Source": "windows-server-01",
  "SourceType": "windows-eventlog",
  "ReceivedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "Normalized": {
    "Id": "norm-rdp-$i",
    "Timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "EventType": "rdp_login_failed",
    "Severity": "high",
    "SourceIp": "203.0.113.42",
    "DestinationIp": "10.0.0.10",
    "Protocol": "rdp",
    "Metadata": {
      "username": "administrator",
      "event_code": "4625",
      "failure_reason": "invalid_password"
    }
  },
  "Raw": {
    "EventCode": 4625,
    "Message": "RDP login failed"
  },
  "Enrichment": {}
}
EOF
    )
    publish_event "$RDP_EVENT"
    echo "  ✓ Published RDP attempt $i/10"
    sleep 0.5
done

echo ""
echo "=== All sample events published successfully ==="
echo ""
echo "Check correlation engine logs to see rule evaluations:"
echo "  docker logs -f sakin-correlation-engine"
echo ""
echo "Check metrics:"
echo "  curl http://localhost:8080/metrics"
echo ""
echo "Query alerts from database:"
echo "  docker exec -it sakin-postgres-correlation psql -U postgres -d sakin_correlation -c 'SELECT id, rule_id, severity, triggered_at FROM public.alerts ORDER BY triggered_at DESC LIMIT 10;'"
