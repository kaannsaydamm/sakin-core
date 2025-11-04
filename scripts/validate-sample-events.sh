#!/bin/bash
#
# Validate sample events against the JSON schema
#
# Usage: ./scripts/validate-sample-events.sh
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SCHEMA_DIR="$PROJECT_ROOT/schema"

echo "================================================="
echo "SAKIN Event Schema - Sample Event Validation"
echo "================================================="
echo ""

cd "$PROJECT_ROOT"

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    echo "⚠️  jq is not installed. Install it to validate JSON samples manually."
    echo "   On Ubuntu/Debian: sudo apt-get install jq"
    echo "   On macOS: brew install jq"
    echo ""
fi

# Run .NET tests to validate events
echo "Running validation tests..."
echo ""

dotnet test sakin-utils/Sakin.Common.Tests/Sakin.Common.Tests.csproj \
    --filter "FullyQualifiedName~Validation" \
    --verbosity quiet \
    --nologo

echo ""
echo "✅ All validation tests passed!"
echo ""

# Display sample event count
SAMPLE_COUNT=$(jq '.sampleEvents | length' "$SCHEMA_DIR/sample-events.json")
echo "📊 Sample Events Available: $SAMPLE_COUNT"
echo ""

# Display sample descriptions
echo "📝 Sample Event Types:"
jq -r '.sampleEvents[].description' "$SCHEMA_DIR/sample-events.json" | nl
echo ""

echo "================================================="
echo "Schema Location: $SCHEMA_DIR/event-schema.json"
echo "Documentation: $PROJECT_ROOT/docs/event-schema.md"
echo "================================================="
