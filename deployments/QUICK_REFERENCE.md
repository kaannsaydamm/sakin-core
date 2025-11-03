# Sakin Platform - Quick Reference

## 🚀 Start/Stop Commands

```bash
# Quick start (automated)
./scripts/start-dev.sh

# Manual start
docker compose -f docker-compose.dev.yml up -d

# Stop (preserve data)
./scripts/stop-dev.sh
# or
docker compose -f docker-compose.dev.yml down

# Stop and remove all data
./scripts/stop-dev.sh --clean
# or
docker compose -f docker-compose.dev.yml down -v
```

## 🔍 Monitoring

```bash
# Check all services status
docker compose -f docker-compose.dev.yml ps

# Verify health
./scripts/verify-services.sh

# View logs (all services)
docker compose -f docker-compose.dev.yml logs -f

# View logs (specific service)
docker compose -f docker-compose.dev.yml logs -f postgres
docker compose -f docker-compose.dev.yml logs -f kafka
docker compose -f docker-compose.dev.yml logs -f opensearch
```

## 🗄️ Database Access

### PostgreSQL
```bash
# Connect
docker exec -it sakin-postgres psql -U postgres -d network_db

# Common queries
SELECT COUNT(*) FROM "PacketData";
SELECT * FROM "PacketData" ORDER BY "timestamp" DESC LIMIT 10;
SELECT * FROM "SniData" ORDER BY "timestamp" DESC LIMIT 10;
```

### Redis
```bash
# Connect
docker exec -it sakin-redis redis-cli -a redis_dev_password

# Test
PING
KEYS *
```

### ClickHouse
```bash
# Connect
docker exec -it sakin-clickhouse clickhouse-client \
  -u clickhouse --password clickhouse_dev_password -d sakin_analytics

# Common queries
SHOW TABLES;
SELECT COUNT(*) FROM network_events;
SELECT * FROM network_events ORDER BY event_time DESC LIMIT 10;
```

## 📨 Kafka Operations

```bash
# List topics
docker exec -it sakin-kafka kafka-topics --bootstrap-server localhost:9092 --list

# Create topic
docker exec -it sakin-kafka kafka-topics \
  --bootstrap-server localhost:9092 \
  --create --topic events --partitions 3 --replication-factor 1

# Produce messages
docker exec -it sakin-kafka kafka-console-producer \
  --bootstrap-server localhost:9092 --topic events

# Consume messages
docker exec -it sakin-kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 --topic events --from-beginning
```

## 🔍 OpenSearch

```bash
# Cluster health
curl http://localhost:9200/_cluster/health?pretty

# List indices
curl http://localhost:9200/_cat/indices?v

# Search
curl -X GET "http://localhost:9200/network-events-*/_search?pretty" \
  -H 'Content-Type: application/json' -d '{"query": {"match_all": {}}}'

# Initialize indices
./scripts/opensearch/init-indices.sh
```

## 🌐 Service Endpoints

| Service | Internal | External (Host) | UI |
|---------|----------|-----------------|-----|
| PostgreSQL | `postgres:5432` | `localhost:5432` | - |
| Redis | `redis:6379` | `localhost:6379` | - |
| Kafka | `kafka:9092` | `localhost:29092` | - |
| Zookeeper | `zookeeper:2181` | `localhost:2181` | - |
| OpenSearch | `opensearch:9200` | `localhost:9200` | - |
| OpenSearch Dashboards | - | - | http://localhost:5601 |
| ClickHouse HTTP | `clickhouse:8123` | `localhost:8123` | - |
| ClickHouse Native | `clickhouse:9000` | `localhost:9000` | - |

## 🔑 Default Credentials

**⚠️ Development Only - Change for Production**

| Service | Username | Password |
|---------|----------|----------|
| PostgreSQL | `postgres` | `postgres_dev_password` |
| Redis | - | `redis_dev_password` |
| ClickHouse | `clickhouse` | `clickhouse_dev_password` |
| OpenSearch | - | (auth disabled) |

## 🐛 Troubleshooting

```bash
# Restart a service
docker compose -f docker-compose.dev.yml restart <service-name>

# View container logs
docker compose -f docker-compose.dev.yml logs --tail=100 <service-name>

# Enter container shell
docker exec -it sakin-<service-name> sh

# Check resource usage
docker stats

# Clean up everything
docker compose -f docker-compose.dev.yml down -v
docker system prune -a --volumes  # ⚠️ Removes ALL unused Docker resources
```

## 📝 Environment Variables

Copy `.env.example` to `.env` and customize if needed. All services have sensible defaults.

## 🏃 Running Services

### Network Sensor
```bash
cd ../sakin-core/services/network-sensor
export Database__Host=localhost
export Database__Password=postgres_dev_password
sudo dotnet run
```

## 📚 More Information

- [DOCKER_SETUP.md](./DOCKER_SETUP.md) - Comprehensive setup guide
- [DELIVERY_SUMMARY.md](./DELIVERY_SUMMARY.md) - Implementation details
- [README.md](./README.md) - Overview
