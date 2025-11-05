#!/bin/bash

echo "=== Testing Rule Loading with Direct Path ==="

# Test if we can load the rules
cd /home/engine/project/sakin-correlation/Sakin.Correlation

echo "Building the project..."
dotnet build

echo "Testing rule directory access..."
ls -la /home/engine/project/configs/rules/

echo ""
echo "Running application with detailed output..."
echo "Note: This will fail to connect to Kafka and PostgreSQL, but we should see rule loading logs"

# Run the application and capture all output
timeout 8s dotnet run --no-build --verbosity normal > /tmp/app_output.txt 2>&1 &
APP_PID=$!

# Wait a bit for the app to start and load rules
sleep 3

# Kill the app
kill $APP_PID 2>/dev/null
wait $APP_PID 2>/dev/null

echo ""
echo "=== Application Output ==="
cat /tmp/app_output.txt | grep -i "rule\|load\|error" || echo "No rule-related output found"

echo ""
echo "=== Full Output (last 20 lines) ==="
tail -20 /tmp/app_output.txt

rm -f /tmp/app_output.txt

echo ""
echo "=== Test completed ==="