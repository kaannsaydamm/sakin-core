#!/bin/bash
set -e

# Sakin Core - Download All Services
# This script pulls all necessary Docker images for the Sakin platform.

echo "Downloading Sakin Core Services..."

if ! command -v docker &> /dev/null; then
    echo "Error: docker is not installed."
    exit 1
fi

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$PROJECT_ROOT/deployments/docker-compose.dev.yml"

if [ ! -f "$COMPOSE_FILE" ]; then
    echo "Error: Compose file not found at $COMPOSE_FILE"
    exit 1
fi

echo "Pulling images defined in $COMPOSE_FILE..."
docker-compose -f "$COMPOSE_FILE" pull

echo "Download complete!"
