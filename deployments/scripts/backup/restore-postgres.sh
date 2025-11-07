#!/bin/bash
set -e

# PostgreSQL restore script for S.A.K.I.N.
# Restores from backup file or S3

BACKUP_FILE="${1:-}"
S3_BUCKET="${S3_BUCKET:-}"
RESTORE_DB="${RESTORE_DB:-sakin_db}"

# Database connection
PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-postgres}"
PGPASSWORD="${PGPASSWORD:-postgres}"

export PGPASSWORD

if [ -z "${BACKUP_FILE}" ]; then
    echo "Usage: $0 <backup-file.sql.gz> [or S3 path]"
    echo ""
    echo "Examples:"
    echo "  $0 /backups/postgres/sakin_db_20250106_120000.sql.gz"
    echo "  $0 s3://my-bucket/postgres/20250106_120000/sakin_db_20250106_120000.sql.gz"
    exit 1
fi

# Download from S3 if path starts with s3://
if [[ "${BACKUP_FILE}" == s3://* ]]; then
    echo "📥 Downloading from S3..."
    TMP_FILE="/tmp/sakin_restore_$(date +%s).sql.gz"
    aws s3 cp "${BACKUP_FILE}" "${TMP_FILE}"
    BACKUP_FILE="${TMP_FILE}"
fi

if [ ! -f "${BACKUP_FILE}" ]; then
    echo "❌ Error: Backup file not found: ${BACKUP_FILE}"
    exit 1
fi

echo "⚠️  WARNING: This will replace database '${RESTORE_DB}'"
echo "   Host: ${PGHOST}:${PGPORT}"
echo "   Backup: ${BACKUP_FILE}"
echo ""
read -p "Continue? (yes/no): " CONFIRM

if [ "${CONFIRM}" != "yes" ]; then
    echo "Aborted."
    exit 0
fi

echo "🔄 Starting restore..."

# Drop existing connections
echo "  🔌 Terminating existing connections..."
psql -h "${PGHOST}" -p "${PGPORT}" -U "${PGUSER}" -d postgres <<EOF
SELECT pg_terminate_backend(pid) 
FROM pg_stat_activity 
WHERE datname = '${RESTORE_DB}' AND pid <> pg_backend_pid();
EOF

# Drop and recreate database
echo "  🗑️  Dropping database ${RESTORE_DB}..."
psql -h "${PGHOST}" -p "${PGPORT}" -U "${PGUSER}" -d postgres \
    -c "DROP DATABASE IF EXISTS ${RESTORE_DB};"

echo "  📦 Creating database ${RESTORE_DB}..."
psql -h "${PGHOST}" -p "${PGPORT}" -U "${PGUSER}" -d postgres \
    -c "CREATE DATABASE ${RESTORE_DB};"

# Restore backup
echo "  ⏳ Restoring backup..."
gunzip -c "${BACKUP_FILE}" | pg_restore -h "${PGHOST}" -p "${PGPORT}" \
    -U "${PGUSER}" -d "${RESTORE_DB}" --verbose --no-owner --no-privileges

# Cleanup temp file if downloaded from S3
if [[ "${BACKUP_FILE}" == /tmp/sakin_restore_* ]]; then
    rm -f "${BACKUP_FILE}"
fi

echo "✅ Restore complete!"
echo ""
echo "Verify with:"
echo "  psql -h ${PGHOST} -p ${PGPORT} -U ${PGUSER} -d ${RESTORE_DB} -c '\\dt'"
