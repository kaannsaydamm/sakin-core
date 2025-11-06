#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
DEPLOYMENTS_DIR="$PROJECT_ROOT/deployments"

echo "=== SAKIN Correlation Engine - Development Setup ==="
echo ""
echo "This script will start:"
echo "  - PostgreSQL (port 5433)"
echo "  - Redis (port 6380)"
echo "  - Zookeeper (port 2182)"
echo "  - Kafka (port 9093)"
echo "  - Correlation Engine (port 8080)"
echo ""

cd "$DEPLOYMENTS_DIR"

case "${1:-up}" in
  up)
    echo "Starting services..."
    docker-compose -f docker-compose.correlation-dev.yml up -d
    echo ""
    echo "Waiting for services to be healthy..."
    sleep 5
    echo ""
    echo "Services started! Access points:"
    echo "  - Correlation Engine: http://localhost:8080"
    echo "  - Metrics endpoint: http://localhost:8080/metrics"
    echo "  - Health endpoint: http://localhost:8080/health"
    echo "  - PostgreSQL: localhost:5433 (sakin_correlation/postgres/postgres)"
    echo "  - Redis: localhost:6380"
    echo "  - Kafka: localhost:9093"
    echo ""
    echo "View logs with: docker-compose -f docker-compose.correlation-dev.yml logs -f sakin-correlation"
    ;;
  down)
    echo "Stopping services..."
    docker-compose -f docker-compose.correlation-dev.yml down
    echo "Services stopped."
    ;;
  logs)
    docker-compose -f docker-compose.correlation-dev.yml logs -f "${2:-sakin-correlation}"
    ;;
  restart)
    echo "Restarting services..."
    docker-compose -f docker-compose.correlation-dev.yml restart
    echo "Services restarted."
    ;;
  rebuild)
    echo "Rebuilding and restarting correlation engine..."
    docker-compose -f docker-compose.correlation-dev.yml up -d --build sakin-correlation
    echo "Correlation engine rebuilt and restarted."
    ;;
  clean)
    echo "Removing all containers and volumes..."
    docker-compose -f docker-compose.correlation-dev.yml down -v
    echo "Cleanup complete."
    ;;
  *)
    echo "Usage: $0 {up|down|logs|restart|rebuild|clean}"
    echo ""
    echo "Commands:"
    echo "  up       - Start all services (default)"
    echo "  down     - Stop all services"
    echo "  logs     - Follow logs (optionally specify service name)"
    echo "  restart  - Restart all services"
    echo "  rebuild  - Rebuild and restart correlation engine"
    echo "  clean    - Remove all containers and volumes"
    exit 1
    ;;
esac
