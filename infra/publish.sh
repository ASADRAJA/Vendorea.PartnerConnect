#!/bin/bash
# Vendorea PartnerConnect - Build & deploy apps to App Service
# Usage: ./publish.sh <environment> [apps]
# Example: ./publish.sh test            # deploys api, admin, workers
#          ./publish.sh test "api"      # deploys only the api

set -e

GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'

ENVIRONMENT=${1:-test}
APPS=${2:-"api,admin,workers"}
BASE_NAME="partnerconnect"
RESOURCE_GROUP="rg-${BASE_NAME}-${ENVIRONMENT}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# app key -> project path
declare -A PROJECTS=(
  [api]="src/Vendorea.PartnerConnect.API"
  [admin]="src/Vendorea.PartnerConnect.AdminPortal"
  [workers]="src/Vendorea.PartnerConnect.BackgroundWorkers"
)

deploy_app() {
    local app="$1"
    local project="${PROJECTS[$app]}"
    local webapp="${BASE_NAME}-${ENVIRONMENT}-${app}"
    local outdir; outdir="$(mktemp -d)"
    local zip="${outdir}/${app}.zip"

    echo -e "${YELLOW}Publishing ${app} (${project})...${NC}"
    dotnet publish "${REPO_ROOT}/${project}" -c Release -o "${outdir}/publish" --nologo

    (cd "${outdir}/publish" && zip -qr "$zip" .)

    echo -e "${YELLOW}Deploying ${app} -> ${webapp}...${NC}"
    az webapp deploy --resource-group "$RESOURCE_GROUP" --name "$webapp" \
        --src-path "$zip" --type zip --output none

    echo -e "${GREEN}  ${webapp} deployed.${NC}"
    rm -rf "$outdir"
}

IFS=',' read -ra APP_LIST <<< "$APPS"
for app in "${APP_LIST[@]}"; do
    app="$(echo "$app" | xargs)"  # trim
    if [ -z "${PROJECTS[$app]}" ]; then echo "Unknown app: $app (expected api|admin|workers)"; exit 1; fi
    deploy_app "$app"
done

echo -e "${GREEN}Done.${NC}"
