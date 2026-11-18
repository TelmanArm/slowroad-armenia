#!/usr/bin/env bash
# Dumps the db, keeps the 2 newest dumps, uploads to S3.
set -euo pipefail

cd /opt/slowroad
source ./preflight.sh
require_pgdata

mkdir -p backups
rm -f backups/*.sql.tmp

docker compose -f docker-compose.prod.yml up -d --wait db

# Dump to .tmp first, so a failed dump never looks complete.
FILE="backups/$(date +%F-%H%M%S)-${1:-manual}.sql"
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > "$FILE.tmp"
mv "$FILE.tmp" "$FILE"
echo "backup: $FILE ($(du -h "$FILE" | cut -f1))"

# Keep the 2 newest.
ls -t backups/*.sql | tail -n +3 | xargs -r rm --

# Upload to S3, if a bucket is set.
bucket="$(grep -m1 '^BACKUP_BUCKET=' .env | cut -d= -f2- || true)"
key="postgres/$(basename "$FILE").gz"

if [ -z "$bucket" ]; then
  echo "WARNING: BACKUP_BUCKET not set — skipping S3 upload" >&2
elif gzip -c "$FILE" | aws s3 cp - "s3://$bucket/$key"; then
  echo "uploaded: s3://$bucket/$key"
else
  echo "WARNING: S3 upload failed — local backup still at $FILE" >&2
fi
