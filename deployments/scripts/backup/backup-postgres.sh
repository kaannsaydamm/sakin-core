#!/bin/bash
set -e

# PostgreSQL backup script for S.A.K.I.N.
# Performs full backup with compression

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="${BACKUP_DIR:-/backups/postgres}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
S3_BUCKET="${S3_BUCKET:-}"

# Database connection
PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGDATABASE="${PGDATABASE:-sakin_db}"
PGUSER="${PGUSER:-postgres}"
PGPASSWORD="${PGPASSWORD:-postgres}"

export PGPASSWORD

echo "🔄 Starting PostgreSQL backup: ${TIMESTAMP}"
mkdir -p "${BACKUP_DIR}"

# Full database backup
BACKUP_FILE="${BACKUP_DIR}/sakin_db_${TIMESTAMP}.sql.gz"
echo "  📦 Backing up database to ${BACKUP_FILE}"

pg_dump -h "${PGHOST}" -p "${PGPORT}" -U "${PGUSER}" -d "${PGDATABASE}" \
    --format=custom --compress=9 --verbose \
    | gzip > "${BACKUP_FILE}"

BACKUP_SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)
echo "  ✓ Backup complete: ${BACKUP_SIZE}"

# Create metadata file
cat > "${BACKUP_FILE}.meta" <<EOF
{
  "timestamp": "${TIMESTAMP}",
  "database": "${PGDATABASE}",
  "host": "${PGHOST}",
  "size": "${BACKUP_SIZE}",
  "retention_days": ${RETENTION_DAYS}
}
EOF

# Upload to S3 if configured
if [ -n "${S3_BUCKET}" ]; then
    echo "  ☁️  Uploading to S3: ${S3_BUCKET}"
    aws s3 cp "${BACKUP_FILE}" "s3://${S3_BUCKET}/postgres/${TIMESTAMP}/"
    aws s3 cp "${BACKUP_FILE}.meta" "s3://${S3_BUCKET}/postgres/${TIMESTAMP}/"
    echo "  ✓ S3 upload complete"
fi

# Cleanup old backups
echo "  🗑️  Cleaning up backups older than ${RETENTION_DAYS} days"
find "${BACKUP_DIR}" -name "sakin_db_*.sql.gz" -mtime +${RETENTION_DAYS} -delete
find "${BACKUP_DIR}" -name "*.meta" -mtime +${RETENTION_DAYS} -delete

echo "✅ Backup complete: ${BACKUP_FILE}"
