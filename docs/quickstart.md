# S.A.K.I.N. Quick Start Guide

Get S.A.K.I.N. running locally in 5 minutes using Docker Compose!

## Prerequisites

- Docker & Docker Compose
- 4GB RAM minimum
- 10GB free disk space

## Step 1: Clone Repository (1 min)

```bash
git clone https://github.com/kaannsaydamm/sakin-core.git
cd sakin-core
```

## Step 2: Start Infrastructure (2 min)

```bash
cd deployments

# Copy environment file
cp .env.example .env

# Start all services
docker compose -f docker-compose.dev.yml up -d

# Wait for services to be ready
sleep 30
```

**Started services:**
- PostgreSQL (5432)
- Redis (6379)
- Kafka (9092)
- ClickHouse (8123)
- Prometheus (9090)
- Grafana (3000)
- Panel API (5000)
- Panel UI (5173)

## Step 3: Verify Services (1 min)

```bash
# Check all services are healthy
./scripts/verify-services.sh
```

Expected output:
```
✓ PostgreSQL is running
✓ Redis is running
✓ Kafka is running
✓ Prometheus is running
✓ Grafana is running
```

## Step 4: Access the Platform (1 min)

Open your browser:

| Service | URL | Credentials |
|---------|-----|-------------|
| **Panel UI** | http://localhost:5173 | No auth in dev |
| **Panel API** | http://localhost:5000/swagger | API docs |
| **Grafana** | http://localhost:3000 | admin/admin |
| **Prometheus** | http://localhost:9090 | No auth |

## Step 5: Send Test Events (Optional - 2 min)

### Send Test Alert via API

```bash
# Get authentication token
TOKEN=$(curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "admin"}' \
  | jq -r '.token')

# Send test alert
curl -X POST http://localhost:5000/api/alerts \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "RuleId": "test-rule",
    "RuleName": "Test Alert",
    "Severity": "High",
    "SourceIp": "192.168.1.100",
    "DestinationIp": "8.8.8.8",
    "Hostname": "testhost",
    "Message": "This is a test alert"
  }'
```

### View Alerts in Dashboard

1. Go to http://localhost:5173
2. You should see the test alert
3. Click to view details
4. Try acknowledging the alert

## Troubleshooting

### Services Not Starting

```bash
# Check service logs
docker compose logs -f correlation
docker compose logs -f ingest
docker compose logs -f panel-api
```

### Kafka Topics Missing

```bash
# Create required topics
docker exec -it kafka kafka-topics --bootstrap-server kafka:9092 --create \
  --topic raw-events --partitions 1 --replication-factor 1 || true

docker exec -it kafka kafka-topics --bootstrap-server kafka:9092 --create \
  --topic normalized-events --partitions 1 --replication-factor 1 || true

docker exec -it kafka kafka-topics --bootstrap-server kafka:9092 --create \
  --topic alerts --partitions 1 --replication-factor 1 || true
```

### High Memory Usage

```bash
# Stop some less-critical services to save memory
docker compose stop jaeger alertmanager

# Or increase Docker memory limit in Docker Desktop settings
```

### Can't Access Panel

```bash
# Check if port is already in use
lsof -i :5173

# If in use, change port in .env
echo "PANEL_PORT=5174" >> .env
docker compose restart panel-ui
```

## Next Steps

### 1. Send Real Events

#### Via Network Sensor
```bash
cd ../sakin-core/services/network-sensor
dotnet run
```

#### Via Syslog
```bash
echo "<34>Oct 11 22:14:15 hostname su: Test message" | nc -u localhost 514
```

#### Via HTTP
```bash
curl -X POST http://localhost:8080/api/events \
  -H "Content-Type: application/json" \
  -d '{
    "timestamp": "'$(date -u +%Y-%m-%dT%H:%M:%SZ)'",
    "source": "test",
    "message": "Test event"
  }'
```

### 2. Create Detection Rules

Create file `sakin-correlation/rules/test-rule.json`:

```json
{
  "Id": "rule-test-high-severity",
  "Name": "Test High Severity Events",
  "Description": "Alert on any high severity events",
  "Enabled": true,
  "RuleType": "Stateless",
  "Conditions": [
    {
      "Field": "Severity",
      "Operator": "GreaterThan",
      "Value": 7
    }
  ],
  "AlertMessage": "High severity event detected",
  "Severity": "High"
}
```

### 3. Create Playbook

Create file `sakin-soar/playbooks/test-playbook.json`:

```json
{
  "Id": "playbook-test-notification",
  "Name": "Test Notification",
  "Description": "Send notification on alert",
  "Enabled": true,
  "TriggerRules": ["rule-test-high-severity"],
  "Steps": [
    {
      "Id": "step-1",
      "Name": "Send Alert",
      "Type": "Notification",
      "Channel": "email",
      "To": "admin@example.com",
      "Message": "Alert triggered: {alert.Message}"
    }
  ]
}
```

### 4. Configure Notifications

Edit `.env` to add credentials:

```bash
# Slack
SLACK_WEBHOOK_URL=https://hooks.slack.com/services/YOUR/WEBHOOK/URL

# Email (Gmail example)
SMTP_SERVER=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=your-email@gmail.com
SMTP_PASSWORD=your-app-password

# Jira
JIRA_URL=https://jira.company.com
JIRA_USERNAME=bot-user
JIRA_API_TOKEN=your-api-token
```

### 5. Explore Documentation

- **[Architecture](./architecture.md)** — System design and data flow
- **[Rule Development](./rule-development.md)** — Write detection rules
- **[SOAR Playbooks](./sprint7-soar.md)** — Automate incident response
- **[Monitoring](./monitoring.md)** — Set up observability
- **[Deployment](./deployment.md)** — Production setup

## Common Tasks

### View Logs

```bash
# Correlation service
docker compose logs -f correlation

# Ingest service
docker compose logs -f ingest

# SOAR service
docker compose logs -f soar

# Panel API
docker compose logs -f panel-api
```

### Access Databases

```bash
# PostgreSQL
docker exec -it postgres psql -U postgres -d sakin

# Redis CLI
docker exec -it redis redis-cli

# ClickHouse
docker exec -it clickhouse clickhouse-client

# View Kafka topics
docker exec -it kafka kafka-topics --list --bootstrap-server kafka:9092
```

### Scale Services

```bash
# Scale correlation to 3 instances
docker compose up -d --scale correlation=3

# Check running instances
docker compose ps
```

### Clean Up

```bash
# Stop all services
docker compose down

# Remove volumes (WARNING: deletes all data)
docker compose down -v

# Remove images
docker image rm sakin-*
```

## Performance Tuning

### Increase Throughput

```bash
# Increase Kafka partitions
docker exec -it kafka kafka-topics --alter \
  --topic normalized-events \
  --partitions 10 \
  --bootstrap-server kafka:9092

# Scale correlation service
docker compose up -d --scale correlation=3
```

### Reduce Memory Usage

```bash
# Stop analytics services if not needed
docker compose stop clickhouse-sink baseline-worker

# Or adjust memory limits in docker-compose.dev.yml
```

### Enable Tracing

```bash
# Uncomment Jaeger in docker-compose.dev.yml
docker compose up -d jaeger

# View traces at http://localhost:16686
```

## Getting Help

- **Questions**: [GitHub Discussions](https://github.com/kaannsaydamm/sakin-core/discussions)
- **Bugs**: [GitHub Issues](https://github.com/kaannsaydamm/sakin-core/issues)
- **Documentation**: [Full Docs](../README.md)
- **Architecture**: [Architecture Guide](./architecture.md)

---

**Next: Read [Architecture Overview](./architecture.md) to understand system design**

**Or: Jump to [Rule Development](./rule-development.md) to create detection rules**

**Or: Jump to [Deployment](./deployment.md) for production setup**
