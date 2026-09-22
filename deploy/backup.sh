#!/usr/bin/env bash
set -euo pipefail

cd /opt/slowroad

LABEL="${1:-manual}"

backup_db() {
  mkdir -p backups
  rm -f backups/*.sql.tmp

  docker compose -f docker-compose.prod.yml up -d --wait db

  BACKUP_FILE="backups/$(date +%F-%H%M%S)-$LABEL.sql"

  docker compose -f docker-compose.prod.yml exec -T db \
    sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > "$BACKUP_FILE.tmp"

  mv "$BACKUP_FILE.tmp" "$BACKUP_FILE"
  echo "backup: $BACKUP_FILE ($(du -h "$BACKUP_FILE" | cut -f1))"

  ls -t backups/*.sql | tail -n +3 | xargs -r rm --
}

upload_backup() {
  local bucket
  bucket="$(grep -m1 '^BACKUP_BUCKET=' .env | cut -d= -f2- || true)"

  if [ -z "$bucket" ]; then
    echo "WARNING: BACKUP_BUCKET not set in .env — skipping S3 upload" >&2
    return 0
  fi

  local key="postgres/$(basename "$BACKUP_FILE").gz"

  if gzip -c "$BACKUP_FILE" | aws s3 cp - "s3://$bucket/$key"; then
    echo "uploaded: s3://$bucket/$key"
  else
    echo "WARNING: S3 upload failed — local backup still at $BACKUP_FILE" >&2
  fi
}

backup_db
upload_backup