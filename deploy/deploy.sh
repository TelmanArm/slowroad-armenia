#!/usr/bin/env bash
# Runs on EC2 over SSH. GitHub Actions passes IMAGE and SHA.
# Stop on any error, and on unset variables.
set -euo pipefail

# The compose file and the .env file live here.
cd /opt/slowroad

# Make IMAGE and SHA visible to compose, so it can fill ${IMAGE}:${SHA}.
export IMAGE SHA

backup_db() {
  mkdir -p backups

  # clear leftovers from a previous failed dump
  rm -f backups/*.sql.tmp

  # db must be up AND accepting connections before we dump it
  docker compose -f docker-compose.prod.yml up -d --wait db

  local file="backups/$(date +%F-%H%M%S).sql"

  # dump to .tmp; set -e aborts the deploy if pg_dump fails
  docker compose -f docker-compose.prod.yml exec -T db \
    sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > "$file.tmp"

  # only a successful dump becomes a real .sql file
  mv "$file.tmp" "$file"
  echo "backup: $file ($(du -h "$file" | cut -f1))"

  # keep the last 2, delete older ones
  ls -t backups/*.sql | tail -n +3 | xargs -r rm --
}

migrate_db() {
  echo "applying migrations..."
  docker compose -f docker-compose.prod.yml exec -T db \
    sh -c 'psql -v ON_ERROR_STOP=1 \
             -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < migrate.sql
  echo "migrations applied"
}

backup_db
migrate_db

# Download the new app image built by CI.
docker compose -f docker-compose.prod.yml pull app

# Start or update the containers.
# Only changed services are recreated, so the database keeps running.
docker compose -f docker-compose.prod.yml up -d

# Delete old unused images, so the disk doesn't fill up.
docker image prune -f
