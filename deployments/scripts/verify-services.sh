#!/bin/bash
# Sakin Platform - Service Verification Script
# This script verifies that all infrastructure services are running and accessible

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo "🔍 Sakin Platform - Service Health Check"
echo "========================================"
echo ""

ERRORS=0

# Function to check service
check_service() {
    local service_name=$1
    local check_command=$2
    local description=$3
    
    echo -n "Checking ${service_name}... "
    
    if eval "$check_command" > /dev/null 2>&1; then
        echo -e "${GREEN}✅ OK${NC} - $description"
        return 0
    else
        echo -e "${RED}❌ FAILED${NC} - $description"
        ERRORS=$((ERRORS + 1))
        return 1
    fi
}

# Check Docker
echo -e "${BLUE}📦 Docker${NC}"
check_service "Docker Daemon" \
    "docker info" \
    "Docker is running"
echo ""

# Check PostgreSQL
echo -e "${BLUE}🐘 PostgreSQL${NC}"
check_service "PostgreSQL Container" \
    "docker ps | grep -q sakin-postgres" \
    "Container is running"

check_service "PostgreSQL Health" \
    "docker exec sakin-postgres pg_isready -U postgres -d network_db" \
    "Database is ready"

check_service "PostgreSQL Tables" \
    "docker exec sakin-postgres psql -U postgres -d network_db -tAc \"SELECT COUNT(*) FROM information_schema.tables WHERE table_name IN ('PacketData', 'SniData');\" | grep -q '2'" \
    "PacketData and SniData tables exist"
echo ""

# Check Redis
echo -e "${BLUE}🗄️  Redis${NC}"
check_service "Redis Container" \
    "docker ps | grep -q sakin-redis" \
    "Container is running"

check_service "Redis Health" \
    "docker exec sakin-redis redis-cli -a redis_dev_password ping" \
    "Redis responds to PING"
echo ""

# Check Zookeeper
echo -e "${BLUE}🦓 Zookeeper${NC}"
check_service "Zookeeper Container" \
    "docker ps | grep -q sakin-zookeeper" \
    "Container is running"

check_service "Zookeeper Health" \
    "docker exec sakin-zookeeper bash -c 'echo ruok | nc localhost 2181' | grep -q imok" \
    "Zookeeper responds"
echo ""

# Check Kafka
echo -e "${BLUE}📨 Kafka${NC}"
check_service "Kafka Container" \
    "docker ps | grep -q sakin-kafka" \
    "Container is running"

check_service "Kafka Health" \
    "docker exec sakin-kafka kafka-broker-api-versions --bootstrap-server localhost:9092" \
    "Kafka broker is accessible"
echo ""

# Check OpenSearch
echo -e "${BLUE}🔍 OpenSearch${NC}"
check_service "OpenSearch Container" \
    "docker ps | grep -q sakin-opensearch" \
    "Container is running"

check_service "OpenSearch Health" \
    "curl -s http://localhost:9200/_cluster/health | grep -q '\"status\":\"\\(green\\|yellow\\)\"'" \
    "Cluster is healthy"

check_service "OpenSearch Indices" \
    "curl -s http://localhost:9200/_cat/indices | grep -q 'network-events\\|security-alerts\\|application-logs'" \
    "Indices are created"
echo ""

# Check OpenSearch Dashboards
echo -e "${BLUE}📊 OpenSearch Dashboards${NC}"
check_service "OpenSearch Dashboards Container" \
    "docker ps | grep -q sakin-opensearch-dashboards" \
    "Container is running"

check_service "OpenSearch Dashboards Health" \
    "curl -s http://localhost:5601/api/status" \
    "Dashboards is accessible"
echo ""

# Check ClickHouse
echo -e "${BLUE}📈 ClickHouse${NC}"
check_service "ClickHouse Container" \
    "docker ps | grep -q sakin-clickhouse" \
    "Container is running"

check_service "ClickHouse Health" \
    "curl -s http://localhost:8123/ping" \
    "HTTP interface responds"

check_service "ClickHouse Tables" \
    "docker exec sakin-clickhouse clickhouse-client -u clickhouse --password clickhouse_dev_password -d sakin_analytics --query 'SHOW TABLES' | grep -q 'network_events'" \
    "Tables are created"
echo ""

# Summary
echo "========================================"
if [ $ERRORS -eq 0 ]; then
    echo -e "${GREEN}✅ All services are healthy!${NC}"
    echo ""
    echo "🌐 Access Points:"
    echo "   PostgreSQL:        localhost:5432"
    echo "   Redis:             localhost:6379"
    echo "   Kafka:             localhost:29092 (external), kafka:9092 (internal)"
    echo "   OpenSearch:        http://localhost:9200"
    echo "   OpenSearch UI:     http://localhost:5601"
    echo "   ClickHouse HTTP:   http://localhost:8123"
    echo "   ClickHouse Native: localhost:9000"
    echo ""
    echo "📚 See DOCKER_SETUP.md for connection details and examples"
    exit 0
else
    echo -e "${RED}❌ ${ERRORS} service(s) failed health check${NC}"
    echo ""
    echo "🔧 Troubleshooting tips:"
    echo "   1. Check logs: docker-compose -f docker-compose.dev.yml logs <service>"
    echo "   2. Restart services: docker-compose -f docker-compose.dev.yml restart"
    echo "   3. Check resources: docker stats"
    echo "   4. See DOCKER_SETUP.md for detailed troubleshooting"
    exit 1
fi
