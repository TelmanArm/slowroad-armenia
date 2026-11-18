#!/usr/bin/env bash
# Runs on EC2 over SSH. CI passes IMAGE and SHA.
set -euo pipefail

cd /opt/slowroad
source ./preflight.sh
require_pgdata

# Compose fills ${IMAGE}:${SHA} from these.
export IMAGE SHA

bash ./backup.sh deploy

# Apply migrations.
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < migrate.sql

# Pull the new app image.
echo "$GHCR_TOKEN" | docker login ghcr.io -u "$GHCR_USER" --password-stdin
docker compose -f docker-compose.prod.yml pull app
docker logout ghcr.io

# Recreate changed containers only; the db keeps running.
docker compose -f docker-compose.prod.yml up -d

# Free disk space.
docker image prune -f
