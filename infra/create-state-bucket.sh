#!/usr/bin/env bash
set -euo pipefail

BUCKET="slowroad-tfstate-gtslowroad"
REGION="eu-north-1"

# Skip if bucket already exists
if aws s3api head-bucket --bucket "$BUCKET" 2>/dev/null; then
  echo "Bucket $BUCKET already exists"
  exit 0
fi

aws s3api create-bucket \
  --bucket "$BUCKET" \
  --region "$REGION" \
  --create-bucket-configuration LocationConstraint="$REGION"

echo "Created $BUCKET"