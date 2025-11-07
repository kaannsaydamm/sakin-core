#!/bin/bash
set -e

# Redis backup script for S.A.K.I.N.

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="${BACKUP_DIR:-/backups/redis}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
S3_BUCKET="${S3_BUCKET:-}"

REDIS_HOST="${REDIS_HOST:-localhost}"
REDIS_PORT="${REDIS_PORT:-6379}"
REDIS_PASSWORD="${REDIS_PASSWORD:-}"

echo "🔄 Starting Redis backup: ${TIMESTAMP}"
mkdir -p "${BACKUP_DIR}"

# Trigger Redis BGSAVE
echo "  💾 Triggering Redis BGSAVE..."
if [ -n "${REDIS_PASSWORD}" ]; then
    redis-cli -h "${REDIS_HOST}" -p "${REDIS_PORT}" -a "${REDIS_PASSWORD}" BGSAVE
else
    redis-cli -h "${REDIS_HOST}" -p "${REDIS_PORT}" BGSAVE
fi

# Wait for BGSAVE to complete
echo "  ⏳ Waiting for BGSAVE to complete..."
while true; do
    if [ -n "${REDIS_PASSWORD}" ]; then
        LASTSAVE=$(redis-cli -h "${REDIS_HOST}" -p "${REDIS_PORT}" -a "${REDIS_PASSWORD}" LASTSAVE)
    else
        LASTSAVE=$(redis-cli -h "${REDIS_HOST}" -p "${REDIS_PORT}" LASTSAVE)
    fi
    
    sleep 2
    
    if [ -n "${REDIS_PASSWORD}" ]; then
        CURRENT=$(redis-cli -h "${REDIS_HOST}" -p "${REDIS_PORT}" -a "${REDIS_PASSWORD}" LASTSAVE)
    else
        CURRENT=$(redis-cli -h "${REDIS_HOST}" -p "${REDIS_PORT}" LASTSAVE)
    fi
    
    if [ "${CURRENT}" != "${LASTSAVE}" ]; then
        break
    fi
done

# Copy dump.rdb
BACKUP_FILE="${BACKUP_DIR}/redis_dump_${TIMESTAMP}.rdb"
DUMP_PATH="/data/dump.rdb"  # Adjust based on Redis config

if [ -f "${DUMP_PATH}" ]; then
    cp "${DUMP_PATH}" "${BACKUP_FILE}"
    gzip "${BACKUP_FILE}"
    BACKUP_FILE="${BACKUP_FILE}.gz"
    
    BACKUP_SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)
    echo "  ✓ Backup complete: ${BACKUP_SIZE}"
    
    # Upload to S3 if configured
    if [ -n "${S3_BUCKET}" ]; then
        echo "  ☁️  Uploading to S3: ${S3_BUCKET}"
        aws s3 cp "${BACKUP_FILE}" "s3://${S3_BUCKET}/redis/${TIMESTAMP}/"
        echo "  ✓ S3 upload complete"
    fi
else
    echo "  ⚠️  Warning: dump.rdb not found at ${DUMP_PATH}"
fi

# Cleanup old backups
echo "  🗑️  Cleaning up backups older than ${RETENTION_DAYS} days"
find "${BACKUP_DIR}" -name "redis_dump_*.rdb.gz" -mtime +${RETENTION_DAYS} -delete

echo "✅ Backup complete: ${BACKUP_FILE}"
