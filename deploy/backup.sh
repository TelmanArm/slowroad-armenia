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

bucket="$(grep -m1 '^BACKUP_BUCKET=' .env | cut -d= -f2- || true)"
if [ -z "$bucket" ]; then
  echo "FATAL: BACKUP_BUCKET not set in .env" >&2
  exit 1
fi

key="postgres/$(basename "$FILE").gz"
gzip -c "$FILE" | aws s3 cp - "s3://$bucket/$key"
echo "uploaded: s3://$bucket/$key"

# Only now is it safe to drop older local dumps.
ls -t backups/*.sql | tail -n +3 | xargs -r rm --
