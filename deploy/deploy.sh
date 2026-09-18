#!/usr/bin/env bash
# Runs on EC2 over SSH. GitHub Actions passes IMAGE and SHA.
# Stop on any error, and on unset variables.
set -euo pipefail

# The compose file and the .env file live here.
cd /opt/slowroad

# Make IMAGE and SHA visible to compose, so it can fill ${IMAGE}:${SHA}.
export IMAGE SHA

# Download the new app image built by CI.
docker compose -f docker-compose.prod.yml pull app

# Start or update the containers.
# Only changed services are recreated, so the database keeps running.
docker compose -f docker-compose.prod.yml up -d

# Delete old unused images, so the disk doesn't fill up.
docker image prune -f