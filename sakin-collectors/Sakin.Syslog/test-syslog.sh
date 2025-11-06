#!/bin/bash

# Test script for Sakin Syslog Agent
# This script tests basic UDP syslog functionality

echo "Testing Sakin Syslog Agent..."

# Check if the service is running on UDP port 514
if ! nc -u -z localhost 514 2>/dev/null; then
    echo "❌ Syslog service is not listening on UDP port 514"
    echo "Please start the service first: dotnet run"
    exit 1
fi

echo "✅ Syslog service is listening on UDP port 514"

# Test RFC3164 format message
echo "📤 Sending RFC3164 format test message..."
echo "<34>Oct 11 22:14:15 mymachine su: 'su root' failed for lonvick on /dev/pts/8" | nc -u localhost 514

# Test RFC5424 format message  
echo "📤 Sending RFC5424 format test message..."
echo "<34>1 2003-10-11T22:14:15.003Z mymachine su 4711 - 'su root' failed for lonvick on /dev/pts/8" | nc -u localhost 514

# Test simple message
echo "📤 Sending simple test message..."
echo "test message from nc" | nc -u localhost 514

echo "✅ Test messages sent successfully!"
echo "📋 Check Kafka topic 'raw-events' for the messages"
echo ""
echo "Example Kafka consumer command:"
echo "kafka-console-consumer --bootstrap-server kafka:9092 --topic raw-events --from-beginning"