#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
CDK_DIR="$ROOT_DIR/infra/aws"

# ── Validate required env ──────────────────────────────────────────────────
if [[ -z "${NEON_CONNECTION_STRING:-}" ]]; then
  echo "ERROR: NEON_CONNECTION_STRING is not set"
  echo "  export NEON_CONNECTION_STRING='postgres://...'"
  exit 1
fi

echo "==> CDK deploy (builds Docker image + updates infrastructure)..."
cd "$CDK_DIR"
npx cdk deploy --require-approval never --outputs-file cdk-outputs.json

# ── Extract outputs ────────────────────────────────────────────────────────
API_URL=$(jq -r '.CosydaysStack.ApiUrl' cdk-outputs.json)
CF_URL=$(jq -r '.CosydaysStack.CloudFrontUrl' cdk-outputs.json)
BUCKET=$(jq -r '.CosydaysStack.WebBucketName' cdk-outputs.json)
DIST_ID=$(jq -r '.CosydaysStack.DistributionId' cdk-outputs.json)
API_FN=$(jq -r '.CosydaysStack.ApiFunctionName' cdk-outputs.json)
WORKER_FN=$(jq -r '.CosydaysStack.WorkerFunctionName' cdk-outputs.json)

# ── Run EF Core migrations ────────────────────────────────────────────────
echo "==> Running database migrations..."
cd "$ROOT_DIR"
ConnectionStrings__cosydb="$NEON_CONNECTION_STRING" \
  dotnet ef database update --project OctopusCosyAnalyser.ApiService

# ── Prune old Lambda versions ──────────────────────────────────────────────
prune_versions() {
  local fn_name="$1"
  echo "==> Pruning old versions of $fn_name..."
  local versions
  versions=$(aws lambda list-versions-by-function \
    --function-name "$fn_name" \
    --query "Versions[?Version!='\$LATEST'].Version" \
    --output text)

  local count=0
  for v in $versions; do
    aws lambda delete-function --function-name "$fn_name" --qualifier "$v" 2>/dev/null && ((count++)) || true
  done
  echo "    Pruned $count old version(s)"
}

prune_versions "$API_FN"
prune_versions "$WORKER_FN"

# ── Print outputs ──────────────────────────────────────────────────────────
echo ""
echo "=========================================="
echo "  Deploy complete"
echo "=========================================="
echo "  API URL:        $API_URL"
echo "  CloudFront URL: $CF_URL"
echo "  S3 Bucket:      $BUCKET"
echo "  Distribution:   $DIST_ID"
echo "  API Lambda:     $API_FN"
echo "  Worker Lambda:  $WORKER_FN"
echo "=========================================="
