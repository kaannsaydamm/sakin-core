#!/bin/bash
# Sakin Platform - Quick Start Script for Development Environment

set -e

BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 Sakin Platform - Development Environment Setup${NC}"
echo "================================================="
echo ""

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker and try again."
    exit 1
fi

# Navigate to deployments directory
cd "$(dirname "$0")/.."

echo -e "${BLUE}📦 Step 1: Starting infrastructure services...${NC}"
docker compose -f docker-compose.dev.yml up -d

echo ""
echo -e "${YELLOW}⏳ Waiting for services to be healthy (this may take 1-2 minutes)...${NC}"
echo ""

# Wait for each service to be healthy
services=("postgres" "redis" "zookeeper" "kafka" "opensearch" "clickhouse")
for service in "${services[@]}"; do
    echo -n "Waiting for $service... "
    timeout=60
    elapsed=0
    while [ $elapsed -lt $timeout ]; do
        if docker compose -f docker-compose.dev.yml ps "$service" | grep -q "healthy"; then
            echo -e "${GREEN}✓${NC}"
            break
        fi
        sleep 2
        elapsed=$((elapsed + 2))
    done
    if [ $elapsed -ge $timeout ]; then
        echo -e "${YELLOW}⚠ Timeout${NC}"
    fi
done

echo ""
echo -e "${BLUE}📊 Step 2: Initializing OpenSearch indices...${NC}"
if [ -f "scripts/opensearch/init-indices.sh" ]; then
    OPENSEARCH_HOST=localhost:9200 ./scripts/opensearch/init-indices.sh
else
    echo "⚠️  OpenSearch init script not found, skipping..."
fi

echo ""
echo -e "${BLUE}🔍 Step 3: Verifying all services...${NC}"
if [ -f "scripts/verify-services.sh" ]; then
    ./scripts/verify-services.sh
else
    echo ""
    echo -e "${GREEN}✅ Infrastructure services started successfully!${NC}"
fi

echo ""
echo "================================================="
echo -e "${GREEN}🎉 Development environment is ready!${NC}"
echo ""
echo "📝 Next steps:"
echo "   1. Run network sensor:"
echo "      cd ../sakin-core/services/network-sensor"
echo "      export Database__Host=localhost"
echo "      export Database__Password=postgres_dev_password"
echo "      sudo dotnet run"
echo ""
echo "   2. Access services:"
echo "      - OpenSearch Dashboards: http://localhost:5601"
echo "      - PostgreSQL: localhost:5432 (user: postgres, password: postgres_dev_password)"
echo ""
echo "📚 For more information, see DOCKER_SETUP.md"
echo ""
