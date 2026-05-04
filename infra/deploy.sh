#!/usr/bin/env bash
# One-shot deploy of the OctopusCosyAnalyser stack to Azure.
#
# Prereqs:
#   - az CLI logged in (az login)
#   - Bicep installed (az bicep install)
#   - main.parameters.json copied from main.parameters.example.json with real values
#
# Usage:
#   ./deploy.sh                 # uses imageTag=latest
#   ./deploy.sh sha-abc1234     # deploy a specific commit's image

set -euo pipefail

cd "$(dirname "$0")"

IMAGE_TAG="${1:-latest}"
LOCATION="${LOCATION:-uksouth}"

if [[ ! -f main.parameters.json ]]; then
  echo "main.parameters.json not found. Copy main.parameters.example.json and fill in real values."
  exit 1
fi

echo "Deploying with imageTag=${IMAGE_TAG} to ${LOCATION}..."
az deployment sub create \
  --location "${LOCATION}" \
  --template-file main.bicep \
  --parameters @main.parameters.json \
  --parameters imageTag="${IMAGE_TAG}"

echo "Done. Outputs:"
az deployment sub show --name main --query properties.outputs -o table 2>/dev/null || true
