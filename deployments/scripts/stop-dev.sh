#!/bin/bash
# Sakin Platform - Stop Development Environment Script

set -e

RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}🛑 Stopping Sakin Platform Development Environment${NC}"
echo "================================================="

cd "$(dirname "$0")/.."

# Check if user wants to remove volumes
if [ "$1" == "--clean" ] || [ "$1" == "-c" ]; then
    echo ""
    echo -e "${RED}⚠️  WARNING: This will remove all data (volumes will be deleted)${NC}"
    read -p "Are you sure? (yes/no): " confirm
    if [ "$confirm" == "yes" ]; then
        echo "Stopping services and removing volumes..."
        docker compose -f docker-compose.dev.yml down -v
        echo -e "${YELLOW}✅ Services stopped and all data removed${NC}"
    else
        echo "Operation cancelled."
        exit 0
    fi
else
    echo "Stopping services (data will be preserved)..."
    docker compose -f docker-compose.dev.yml down
    echo -e "${YELLOW}✅ Services stopped (use --clean to remove data)${NC}"
fi

echo ""
echo "To start again, run: ./scripts/start-dev.sh"
echo ""
