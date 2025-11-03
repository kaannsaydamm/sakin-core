#!/bin/bash
# Sakin Security Platform - OpenSearch Index Initialization
# This script creates the required indices and index templates for the platform

set -e

OPENSEARCH_HOST="${OPENSEARCH_HOST:-localhost:9200}"
RETRY_COUNT=30
RETRY_DELAY=5

echo "🔍 Waiting for OpenSearch to be ready at $OPENSEARCH_HOST..."

# Wait for OpenSearch to be ready
for i in $(seq 1 $RETRY_COUNT); do
    if curl -s "http://$OPENSEARCH_HOST/_cluster/health" > /dev/null 2>&1; then
        echo "✅ OpenSearch is ready!"
        break
    fi
    
    if [ $i -eq $RETRY_COUNT ]; then
        echo "❌ OpenSearch did not become ready in time"
        exit 1
    fi
    
    echo "⏳ Waiting for OpenSearch... ($i/$RETRY_COUNT)"
    sleep $RETRY_DELAY
done

echo ""
echo "📋 Creating index templates and indices..."

# Create index template for network events
echo "Creating network-events index template..."
curl -X PUT "http://$OPENSEARCH_HOST/_index_template/network-events-template" \
  -H 'Content-Type: application/json' \
  -d '{
  "index_patterns": ["network-events-*"],
  "template": {
    "settings": {
      "number_of_shards": 1,
      "number_of_replicas": 0,
      "refresh_interval": "5s",
      "index": {
        "codec": "best_compression"
      }
    },
    "mappings": {
      "properties": {
        "@timestamp": {
          "type": "date"
        },
        "event_id": {
          "type": "keyword"
        },
        "event_type": {
          "type": "keyword"
        },
        "severity": {
          "type": "keyword"
        },
        "source": {
          "type": "keyword"
        },
        "src_ip": {
          "type": "ip"
        },
        "dst_ip": {
          "type": "ip"
        },
        "protocol": {
          "type": "keyword"
        },
        "port": {
          "type": "integer"
        },
        "message": {
          "type": "text",
          "fields": {
            "keyword": {
              "type": "keyword",
              "ignore_above": 256
            }
          }
        },
        "metadata": {
          "type": "object",
          "enabled": true
        }
      }
    }
  },
  "priority": 100,
  "version": 1
}'

echo ""
echo "Creating security-alerts index template..."
curl -X PUT "http://$OPENSEARCH_HOST/_index_template/security-alerts-template" \
  -H 'Content-Type: application/json' \
  -d '{
  "index_patterns": ["security-alerts-*"],
  "template": {
    "settings": {
      "number_of_shards": 1,
      "number_of_replicas": 0,
      "refresh_interval": "5s"
    },
    "mappings": {
      "properties": {
        "@timestamp": {
          "type": "date"
        },
        "alert_id": {
          "type": "keyword"
        },
        "alert_type": {
          "type": "keyword"
        },
        "severity": {
          "type": "keyword"
        },
        "status": {
          "type": "keyword"
        },
        "title": {
          "type": "text"
        },
        "description": {
          "type": "text"
        },
        "src_ip": {
          "type": "ip"
        },
        "dst_ip": {
          "type": "ip"
        },
        "indicators": {
          "type": "keyword"
        },
        "mitre_tactics": {
          "type": "keyword"
        },
        "mitre_techniques": {
          "type": "keyword"
        },
        "risk_score": {
          "type": "integer"
        }
      }
    }
  },
  "priority": 100,
  "version": 1
}'

echo ""
echo "Creating application-logs index template..."
curl -X PUT "http://$OPENSEARCH_HOST/_index_template/application-logs-template" \
  -H 'Content-Type: application/json' \
  -d '{
  "index_patterns": ["application-logs-*"],
  "template": {
    "settings": {
      "number_of_shards": 1,
      "number_of_replicas": 0,
      "refresh_interval": "10s"
    },
    "mappings": {
      "properties": {
        "@timestamp": {
          "type": "date"
        },
        "level": {
          "type": "keyword"
        },
        "logger": {
          "type": "keyword"
        },
        "service": {
          "type": "keyword"
        },
        "message": {
          "type": "text"
        },
        "exception": {
          "type": "text"
        },
        "trace_id": {
          "type": "keyword"
        },
        "span_id": {
          "type": "keyword"
        }
      }
    }
  },
  "priority": 100,
  "version": 1
}'

echo ""
echo "Creating initial indices..."

# Create initial indices
TODAY=$(date +%Y.%m.%d)

curl -X PUT "http://$OPENSEARCH_HOST/network-events-$TODAY" -H 'Content-Type: application/json' -d '{}'
curl -X PUT "http://$OPENSEARCH_HOST/security-alerts-$TODAY" -H 'Content-Type: application/json' -d '{}'
curl -X PUT "http://$OPENSEARCH_HOST/application-logs-$TODAY" -H 'Content-Type: application/json' -d '{}'

echo ""
echo "Inserting sample documents..."

# Insert sample network event
curl -X POST "http://$OPENSEARCH_HOST/network-events-$TODAY/_doc" \
  -H 'Content-Type: application/json' \
  -d '{
  "@timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
  "event_id": "evt-001",
  "event_type": "network_connection",
  "severity": "info",
  "source": "network-sensor",
  "src_ip": "192.168.1.100",
  "dst_ip": "8.8.8.8",
  "protocol": "UDP",
  "port": 53,
  "message": "DNS query to Google DNS",
  "metadata": {
    "dns_query": "example.com",
    "query_type": "A"
  }
}'

# Insert sample security alert
curl -X POST "http://$OPENSEARCH_HOST/security-alerts-$TODAY/_doc" \
  -H 'Content-Type: application/json' \
  -d '{
  "@timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
  "alert_id": "alert-001",
  "alert_type": "suspicious_connection",
  "severity": "medium",
  "status": "open",
  "title": "Suspicious outbound connection detected",
  "description": "Connection to known suspicious IP address",
  "src_ip": "192.168.1.100",
  "dst_ip": "198.51.100.42",
  "indicators": ["suspicious-ip", "unusual-port"],
  "mitre_tactics": ["command-and-control"],
  "mitre_techniques": ["T1071"],
  "risk_score": 65
}'

# Insert sample application log
curl -X POST "http://$OPENSEARCH_HOST/application-logs-$TODAY/_doc" \
  -H 'Content-Type: application/json' \
  -d '{
  "@timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
  "level": "info",
  "logger": "Sakin.Core.Sensor",
  "service": "network-sensor",
  "message": "Network sensor service started successfully",
  "trace_id": "trace-12345",
  "span_id": "span-67890"
}'

echo ""
echo "Refreshing indices..."
curl -X POST "http://$OPENSEARCH_HOST/_refresh"

echo ""
echo "✅ OpenSearch initialization complete!"
echo ""
echo "📊 Index Summary:"
curl -s "http://$OPENSEARCH_HOST/_cat/indices?v&s=index"

echo ""
echo "🔍 You can access OpenSearch Dashboards at: http://localhost:5601"
