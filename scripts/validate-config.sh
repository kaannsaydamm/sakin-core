#!/bin/bash
# Configuration validation script for Sakin platform
# Tests JSON syntax and .NET configuration binding

echo "==================================="
echo "Configuration Validation Script"
echo "==================================="
echo ""

PASSED=0
FAILED=0

echo "Validating JSON Syntax..."
echo "-----------------------------------"

CONFIG_FILES=(
    "sakin-core/services/network-sensor/appsettings.json"
    "sakin-core/services/network-sensor/appsettings.Development.json"
    "sakin-ingest/appsettings.json"
    "sakin-ingest/appsettings.Development.json"
    "sakin-correlation/appsettings.json"
    "sakin-correlation/appsettings.Development.json"
)

for config_file in "${CONFIG_FILES[@]}"; do
    if [ ! -f "$config_file" ]; then
        echo "❌ File not found: $config_file"
        FAILED=$((FAILED + 1))
        continue
    fi
    
    if python3 -m json.tool "$config_file" > /dev/null 2>&1; then
        echo "✓ Valid JSON: $config_file"
        PASSED=$((PASSED + 1))
    else
        echo "✗ Invalid JSON: $config_file"
        FAILED=$((FAILED + 1))
    fi
done

echo ""
echo "Checking UserSecrets Configuration..."
echo "-----------------------------------"

if grep -q "UserSecretsId" sakin-core/services/network-sensor/Sakin.Core.Sensor.csproj 2>/dev/null; then
    echo "✓ Network Sensor: UserSecretsId configured"
    PASSED=$((PASSED + 1))
else
    echo "✗ Network Sensor: UserSecretsId not configured"
    FAILED=$((FAILED + 1))
fi

echo ""
echo "==================================="
echo "Validation Complete"
echo "==================================="
echo "Passed: $PASSED"
echo "Failed: $FAILED"
echo ""

if [ $FAILED -eq 0 ]; then
    echo "All configuration files are valid!"
    exit 0
else
    echo "Some validation checks failed."
    exit 1
fi
