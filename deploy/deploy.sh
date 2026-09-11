#!/usr/bin/env bash
# Deploys on EC2. Needs env vars: IMAGE and SHA.
set -euo pipefail

IMAGE_TAG="${IMAGE}:${SHA}"

# Get the new image
docker pull "$IMAGE_TAG"

# Remove old container
docker stop slowroad || true
docker rm slowroad || true

# Start new container (nginx-only, port 8080)
docker run -d --name slowroad --restart unless-stopped \
  -p 127.0.0.1:8080:8080 "$IMAGE_TAG"

# Clean up old images
docker image prune -f
