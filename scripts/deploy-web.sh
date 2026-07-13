#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
WEB_DIR="$ROOT_DIR/octopus-cosy-web"
CDK_DIR="$ROOT_DIR/infra/aws"
CDK_OUTPUTS="$CDK_DIR/cdk-outputs.json"

# ── Read CDK outputs ──────────────────────────────────────────────────────
if [[ ! -f "$CDK_OUTPUTS" ]]; then
  echo "ERROR: $CDK_OUTPUTS not found"
  echo "  Run deploy-backend.sh first, or manually run 'cdk deploy --outputs-file cdk-outputs.json'"
  exit 1
fi

BUCKET=$(jq -r '.CosydaysStack.WebBucketName' "$CDK_OUTPUTS")
DIST_ID=$(jq -r '.CosydaysStack.DistributionId' "$CDK_OUTPUTS")

if [[ "$BUCKET" == "null" || "$DIST_ID" == "null" ]]; then
  echo "ERROR: Could not read bucket or distribution ID from CDK outputs"
  exit 1
fi

# ── Build ──────────────────────────────────────────────────────────────────
echo "==> Building React PWA..."
cd "$WEB_DIR"
npm ci
npm run build

# ── S3 sync ────────────────────────────────────────────────────────────────
echo "==> Syncing to S3 bucket: $BUCKET"

# Fingerprinted assets — immutable, long cache
aws s3 sync dist/assets "s3://$BUCKET/assets" \
  --cache-control "public, max-age=31536000, immutable" \
  --delete

# PWA files that must revalidate every load
for f in index.html sw.js manifest.webmanifest registerSW.js; do
  if [[ -f "dist/$f" ]]; then
    aws s3 cp "dist/$f" "s3://$BUCKET/$f" \
      --cache-control "no-cache, no-store"
  fi
done

# Remaining static files (icons, fonts, etc.)
aws s3 sync dist "s3://$BUCKET" \
  --cache-control "public, max-age=86400" \
  --exclude "assets/*" \
  --exclude "index.html" \
  --exclude "sw.js" \
  --exclude "manifest.webmanifest" \
  --exclude "registerSW.js"

# ── CloudFront invalidation ───────────────────────────────────────────────
echo "==> Invalidating CloudFront cache..."
aws cloudfront create-invalidation \
  --distribution-id "$DIST_ID" \
  --paths "/index.html" "/sw.js" "/manifest.webmanifest"

echo ""
echo "==> Web deploy complete"
echo "    https://$(jq -r '.CosydaysStack.CloudFrontUrl' "$CDK_OUTPUTS" | sed 's|https://||')"
