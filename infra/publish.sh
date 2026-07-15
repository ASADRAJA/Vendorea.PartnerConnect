#!/bin/bash
# Vendorea PartnerConnect - Build & deploy apps to App Service
# Usage: ./publish.sh <environment> [apps]
# Example: ./publish.sh test            # deploys api, admin, workers (default set)
#          ./publish.sh test "api"      # deploys only the api
#          ./publish.sh test "portal"   # deploys the customer portal (provision it via deploy.sh first)
#
# Note: written for bash 3.2 (macOS default) — no associative arrays.

set -e

GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'

ENVIRONMENT=${1:-test}
APPS=${2:-"api,admin,workers"}
BASE_NAME="partnerconnect"
RESOURCE_GROUP="rg-${BASE_NAME}-${ENVIRONMENT}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# app key -> project path
project_for() {
    case "$1" in
        api)     echo "src/Vendorea.PartnerConnect.API" ;;
        admin)   echo "src/Vendorea.PartnerConnect.AdminPortal" ;;
        workers) echo "src/Vendorea.PartnerConnect.BackgroundWorkers" ;;
        portal)  echo "src/Vendorea.PartnerConnect.CustomerPortal" ;;
        *)       echo "" ;;
    esac
}

deploy_app() {
    local app="$1"
    local project; project="$(project_for "$app")"
    if [ -z "$project" ]; then echo "Unknown app: $app (expected api|admin|workers|portal)"; exit 1; fi

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
    app="$(echo "$app" | xargs)"  # trim whitespace
    deploy_app "$app"
done

echo -e "${GREEN}Done.${NC}"
