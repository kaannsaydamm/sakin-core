#!/bin/bash
set -e

# Sakin Core - Start All Services
# This script starts the full Sakin platform using Docker Compose.

echo "Starting Sakin Core Services..."

if ! command -v docker-compose &> /dev/null; then
    echo "Error: docker-compose is not installed."
    exit 1
fi

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$PROJECT_ROOT/deployments/docker-compose.dev.yml"

if [ ! -f "$COMPOSE_FILE" ]; then
    echo "Error: Compose file not found at $COMPOSE_FILE"
    exit 1
fi

echo "Starting services from $COMPOSE_FILE..."
docker-compose -f "$COMPOSE_FILE" up -d

echo "Checking service status..."
docker-compose -f "$COMPOSE_FILE" ps

echo "Sakin Core is running!"
