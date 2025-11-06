#!/bin/bash

set -e

echo "Testing Sakin.HttpCollector..."

BASE_URL="http://localhost:8080"
ENDPOINT="/api/events"

echo ""
echo "Test 1: Sending CEF message..."
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}${ENDPOINT}" \
  -H "Content-Type: text/plain" \
  -d "CEF:0|Security|IDS|1.0|100|Attack Detected|9|src=192.168.1.100 dst=10.0.0.1")

STATUS=$(echo "$RESPONSE" | tail -n1)
if [ "$STATUS" = "202" ]; then
  echo "✅ CEF message accepted (202)"
else
  echo "❌ Expected 202, got $STATUS"
  exit 1
fi

echo ""
echo "Test 2: Sending Syslog message..."
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}${ENDPOINT}" \
  -H "Content-Type: text/plain" \
  -d "Jan 15 10:30:00 firewall sshd[1234]: Failed password for admin from 192.168.1.100")

STATUS=$(echo "$RESPONSE" | tail -n1)
if [ "$STATUS" = "202" ]; then
  echo "✅ Syslog message accepted (202)"
else
  echo "❌ Expected 202, got $STATUS"
  exit 1
fi

echo ""
echo "Test 3: Sending JSON CEF message..."
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}${ENDPOINT}" \
  -H "Content-Type: application/json" \
  -d '{"message":"CEF:0|Vendor|Product|1.0|200|Event|5|","source_ip":"192.168.1.50"}')

STATUS=$(echo "$RESPONSE" | tail -n1)
if [ "$STATUS" = "202" ]; then
  echo "✅ JSON CEF message accepted (202)"
else
  echo "❌ Expected 202, got $STATUS"
  exit 1
fi

echo ""
echo "Test 4: Sending with X-Source header..."
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}${ENDPOINT}" \
  -H "Content-Type: text/plain" \
  -H "X-Source: firewall-01" \
  -d "CEF:0|Firewall|ASA|5.0|302013|Built outbound TCP connection|2|")

STATUS=$(echo "$RESPONSE" | tail -n1)
if [ "$STATUS" = "202" ]; then
  echo "✅ Message with X-Source header accepted (202)"
else
  echo "❌ Expected 202, got $STATUS"
  exit 1
fi

echo ""
echo "Test 5: Sending empty body (should fail)..."
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}${ENDPOINT}" \
  -H "Content-Type: text/plain" \
  -d "")

STATUS=$(echo "$RESPONSE" | tail -n1)
if [ "$STATUS" = "400" ]; then
  echo "✅ Empty body rejected (400)"
else
  echo "❌ Expected 400, got $STATUS"
  exit 1
fi

echo ""
echo "Test 6: Checking metrics endpoint..."
METRICS=$(curl -s "${BASE_URL}/metrics")
if echo "$METRICS" | grep -q "sakin_http_requests_total"; then
  echo "✅ Metrics endpoint working"
else
  echo "❌ Metrics endpoint not working"
  exit 1
fi

echo ""
echo "All tests passed! ✅"
