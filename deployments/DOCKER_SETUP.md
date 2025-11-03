# Sakin Platform - Docker Development Environment

This guide explains how to set up and run the Sakin Security Platform development environment using Docker Compose.

## 📋 Prerequisites

- Docker Engine 24.0+ ([Install Docker](https://docs.docker.com/engine/install/))
- Docker Compose 2.20+ (included with Docker Desktop)
- At least 8GB of available RAM
- At least 20GB of free disk space

**Note:** This guide uses `docker compose` (Compose V2) commands. If you have the older `docker compose` (V1), the commands are the same - just use `docker compose` instead of `docker compose`.

## 🚀 Quick Start

### 1. Start All Infrastructure Services

From the `deployments` directory, run:

```bash
cd deployments
docker compose -f docker compose.dev.yml up -d
```

This will start:
- ✅ PostgreSQL (port 5432)
- ✅ Redis (port 6379)
- ✅ Zookeeper (port 2181)
- ✅ Kafka (ports 9092, 29092)
- ✅ OpenSearch (ports 9200, 9600)
- ✅ OpenSearch Dashboards (port 5601)
- ✅ ClickHouse (ports 8123, 9000)

### 2. Wait for Services to be Healthy

Check the status of all services:

```bash
docker compose -f docker compose.dev.yml ps
```

All services should show `healthy` status. This may take 1-2 minutes.

### 3. Initialize OpenSearch Indices

Once OpenSearch is healthy, run the initialization script:

```bash
./scripts/opensearch/init-indices.sh
```

Or if running from outside the container:

```bash
OPENSEARCH_HOST=localhost:9200 ./scripts/opensearch/init-indices.sh
```

### 4. Verify All Services

Run the verification script:

```bash
./scripts/verify-services.sh
```

## 🗄️ Database Access

### PostgreSQL

The PostgreSQL database is automatically initialized with the required schema.

**Connection Details:**
- Host: `localhost` (or `postgres` from within Docker network)
- Port: `5432`
- Database: `network_db`
- Username: `postgres`
- Password: `postgres_dev_password`

**Connect via psql:**
```bash
docker exec -it sakin-postgres psql -U postgres -d network_db
```

**Useful queries:**
```sql
-- View all tables
\dt

-- Check PacketData
SELECT COUNT(*) FROM "PacketData";
SELECT * FROM "PacketData" ORDER BY "timestamp" DESC LIMIT 10;

-- Check SniData
SELECT COUNT(*) FROM "SniData";
SELECT * FROM "SniData" ORDER BY "timestamp" DESC LIMIT 10;

-- Use the combined view
SELECT * FROM "PacketSniView" LIMIT 10;
```

### Redis

**Connection Details:**
- Host: `localhost` (or `redis` from within Docker network)
- Port: `6379`
- Password: `redis_dev_password`

**Connect via redis-cli:**
```bash
docker exec -it sakin-redis redis-cli -a redis_dev_password
```

**Test commands:**
```bash
PING
SET test "Hello Sakin"
GET test
```

### ClickHouse

**Connection Details:**
- Host: `localhost` (or `clickhouse` from within Docker network)
- HTTP Port: `8123`
- Native Port: `9000`
- Database: `sakin_analytics`
- Username: `clickhouse`
- Password: `clickhouse_dev_password`

**Connect via clickhouse-client:**
```bash
docker exec -it sakin-clickhouse clickhouse-client -u clickhouse --password clickhouse_dev_password -d sakin_analytics
```

**Useful queries:**
```sql
-- Show tables
SHOW TABLES;

-- Check network events
SELECT COUNT(*) FROM network_events;
SELECT * FROM network_events ORDER BY event_time DESC LIMIT 10;

-- Check security alerts
SELECT severity, COUNT(*) as count FROM security_alerts GROUP BY severity;

-- Top talkers
SELECT src_ip, dst_ip, SUM(bytes_sent + bytes_received) as total_bytes
FROM network_events
GROUP BY src_ip, dst_ip
ORDER BY total_bytes DESC
LIMIT 10;
```

**Web interface:**
```bash
# Run a query via HTTP
curl 'http://localhost:8123/?user=clickhouse&password=clickhouse_dev_password' \
  -d 'SELECT COUNT(*) FROM sakin_analytics.network_events'
```

## 🔍 OpenSearch & Kibana

### OpenSearch API

**Connection Details:**
- Host: `localhost` (or `opensearch` from within Docker network)
- Port: `9200`
- Authentication: Disabled for development

**Check cluster health:**
```bash
curl http://localhost:9200/_cluster/health?pretty
```

**List indices:**
```bash
curl http://localhost:9200/_cat/indices?v
```

**Search network events:**
```bash
curl -X GET "http://localhost:9200/network-events-*/_search?pretty" \
  -H 'Content-Type: application/json' \
  -d '{
  "query": {
    "match_all": {}
  },
  "size": 10
}'
```

### OpenSearch Dashboards

Access the web UI at: **http://localhost:5601**

Initial setup:
1. Navigate to Management → Index Patterns
2. Create index patterns for:
   - `network-events-*`
   - `security-alerts-*`
   - `application-logs-*`
3. Use `@timestamp` as the time field
4. Start exploring data in Discover

## 📨 Kafka & Zookeeper

### Kafka

**Connection Details:**
- Internal: `kafka:9092` (from within Docker network)
- External: `localhost:29092` (from host machine)

**List topics:**
```bash
docker exec -it sakin-kafka kafka-topics --bootstrap-server localhost:9092 --list
```

**Create a test topic:**
```bash
docker exec -it sakin-kafka kafka-topics \
  --bootstrap-server localhost:9092 \
  --create \
  --topic test-topic \
  --partitions 3 \
  --replication-factor 1
```

**Produce messages:**
```bash
docker exec -it sakin-kafka kafka-console-producer \
  --bootstrap-server localhost:9092 \
  --topic test-topic
```

**Consume messages:**
```bash
docker exec -it sakin-kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic test-topic \
  --from-beginning
```

## 🐳 Service Management

### Start Services
```bash
docker compose -f docker compose.dev.yml up -d
```

### Stop Services
```bash
docker compose -f docker compose.dev.yml down
```

### Stop Services and Remove Volumes (⚠️ Deletes all data)
```bash
docker compose -f docker compose.dev.yml down -v
```

### View Logs
```bash
# All services
docker compose -f docker compose.dev.yml logs -f

# Specific service
docker compose -f docker compose.dev.yml logs -f postgres
docker compose -f docker compose.dev.yml logs -f kafka
docker compose -f docker compose.dev.yml logs -f opensearch
```

### Restart a Service
```bash
docker compose -f docker compose.dev.yml restart postgres
```

### Check Service Health
```bash
docker compose -f docker compose.dev.yml ps
```

## 🔧 Application Services

### Network Sensor

The network-sensor service definition is commented out in docker compose.dev.yml because it requires host network mode for packet capture.

**To run manually:**
```bash
cd ../sakin-core/services/network-sensor

# Set environment variables
export Database__Host=localhost
export Database__Password=postgres_dev_password

# Run with elevated privileges
sudo dotnet run
```

### Ingest & Correlation Services

These services are placeholders and not yet implemented. Uncomment their definitions in docker compose.dev.yml when ready.

## 📊 Monitoring & Health Checks

### Check All Service Health
```bash
# PostgreSQL
docker exec sakin-postgres pg_isready -U postgres

# Redis
docker exec sakin-redis redis-cli -a redis_dev_password ping

# Kafka
docker exec sakin-kafka kafka-broker-api-versions --bootstrap-server localhost:9092

# OpenSearch
curl http://localhost:9200/_cluster/health

# ClickHouse
curl http://localhost:8123/ping
```

### Resource Usage
```bash
docker stats
```

## 🔒 Security Notes

⚠️ **Important:** These configurations are for **DEVELOPMENT ONLY**.

- Passwords are hardcoded and simple
- Security features are disabled (e.g., OpenSearch security plugin)
- Services are exposed on all interfaces
- No TLS/SSL encryption

**For production:**
- Use strong passwords from secrets management
- Enable authentication and authorization
- Use TLS/SSL for all connections
- Restrict network access
- Enable audit logging
- Regular security updates

## 🐛 Troubleshooting

### Services Won't Start

**Check Docker resources:**
```bash
docker system df
docker system prune  # Clean up unused resources
```

**Check port conflicts:**
```bash
netstat -tuln | grep -E '5432|6379|9092|9200|8123'
```

### OpenSearch Won't Start

**Increase vm.max_map_count on Linux:**
```bash
sudo sysctl -w vm.max_map_count=262144
# Make permanent:
echo "vm.max_map_count=262144" | sudo tee -a /etc/sysctl.conf
```

### PostgreSQL Connection Refused

Wait for the health check to pass:
```bash
docker compose -f docker compose.dev.yml ps postgres
```

Check logs:
```bash
docker compose -f docker compose.dev.yml logs postgres
```

### Kafka Won't Connect

Ensure Zookeeper is healthy first:
```bash
docker compose -f docker compose.dev.yml ps zookeeper
```

Check Kafka logs:
```bash
docker compose -f docker compose.dev.yml logs kafka
```

### Out of Memory

Increase Docker memory limit in Docker Desktop settings or adjust Java heap sizes in docker compose.dev.yml.

## 📚 Additional Resources

- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [OpenSearch Documentation](https://opensearch.org/docs/)
- [Kafka Documentation](https://kafka.apache.org/documentation/)
- [ClickHouse Documentation](https://clickhouse.com/docs/)

## 🆘 Getting Help

If you encounter issues:

1. Check service logs: `docker compose logs <service-name>`
2. Verify service health: `docker compose ps`
3. Review this troubleshooting guide
4. Check GitHub Issues: [sakin-platform/issues](https://github.com/kaannsaydamm/sakin-core/issues)
