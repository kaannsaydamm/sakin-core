#!/bin/bash

ENDPOINT="${ENDPOINT:-http://localhost:8080}"

echo "=== SAKIN Correlation Engine - Metrics Check ==="
echo ""
echo "Checking health endpoint..."
curl -s "$ENDPOINT/health" && echo "" || echo "❌ Health check failed"

echo ""
echo "Checking metrics endpoint..."
curl -s "$ENDPOINT/metrics" | grep -E "^(# HELP|# TYPE|sakin_correlation_)" | head -30

echo ""
echo "=== Metrics Summary ==="
echo ""

EVENTS=$(curl -s "$ENDPOINT/metrics" | grep "^sakin_correlation_events_processed_total" | awk '{print $2}')
RULES=$(curl -s "$ENDPOINT/metrics" | grep "^sakin_correlation_rules_evaluated_total" | awk '{print $2}')
ALERTS=$(curl -s "$ENDPOINT/metrics" | grep "^sakin_correlation_alerts_created_total" | awk '{print $2}')
REDIS=$(curl -s "$ENDPOINT/metrics" | grep "^sakin_correlation_redis_ops_total" | awk '{print $2}')

echo "Events Processed: ${EVENTS:-0}"
echo "Rules Evaluated:  ${RULES:-0}"
echo "Alerts Created:   ${ALERTS:-0}"
echo "Redis Operations: ${REDIS:-0}"
echo ""

if [ -n "$EVENTS" ] && [ "$EVENTS" != "0" ]; then
    echo "✓ Correlation engine is processing events"
else
    echo "⚠ No events processed yet"
fi
