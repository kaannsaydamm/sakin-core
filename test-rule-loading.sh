#!/bin/bash

echo "=== Testing Rule Loading ==="

# Test if we can load the rules
cd /home/engine/project/sakin-correlation/Sakin.Correlation

echo "Building the project..."
dotnet build

echo "Running a quick test to see if rules load..."
echo "Note: This will fail to connect to Kafka and PostgreSQL, but we should see rule loading logs"

# Run the application briefly to see if rules load correctly
timeout 10s dotnet run --no-build 2>&1 | grep -i "rule\|load\|error" || echo "Process ended or no rule-related output found"

echo ""
echo "=== Checking rule files ==="
ls -la /home/engine/project/configs/rules/

echo ""
echo "=== Sample rule content ==="
head -20 /home/engine/project/configs/rules/simple-failed-login.json

echo ""
echo "=== Test completed ==="