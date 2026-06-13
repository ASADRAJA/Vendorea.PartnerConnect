#!/bin/bash
# Vendorea PartnerConnect - Infrastructure Deployment
# Usage: ./deploy.sh <environment> [location]
# Example: ./deploy.sh test westus2
#
# Deploys PartnerConnect into its OWN resource group (rg-partnerconnect-<env>),
# in the same subscription/region as Merchant360 but fully separate.

set -e

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'

ENVIRONMENT=${1:-test}
LOCATION=${2:-westus2}   # Merchant360 test lives in westus2
BASE_NAME="partnerconnect"
RESOURCE_GROUP="rg-${BASE_NAME}-${ENVIRONMENT}"
DEPLOYMENT_NAME="deploy-${BASE_NAME}-${ENVIRONMENT}-$(date +%Y%m%d%H%M%S)"

if [[ ! "$ENVIRONMENT" =~ ^(test|preprod|prod)$ ]]; then
    echo -e "${RED}Error: Environment must be 'test', 'preprod', or 'prod'${NC}"; exit 1
fi

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}PartnerConnect Infrastructure Deployment${NC}"
echo -e "${CYAN}========================================${NC}"
echo -e "${YELLOW}Environment:    ${ENVIRONMENT}${NC}"
echo -e "${YELLOW}Resource Group: ${RESOURCE_GROUP}${NC}"
echo -e "${YELLOW}Location:       ${LOCATION}${NC}"
echo ""

# Ensure logged in
if ! az account show > /dev/null 2>&1; then
    echo -e "${YELLOW}Not logged in to Azure. Running 'az login'...${NC}"; az login
fi
echo -e "${GREEN}Logged in: $(az account show --query name -o tsv)${NC}"
echo ""

# Secrets (not stored in parameters files)
echo -n "Enter SQL Server admin password: "; read -s SQL_PASSWORD; echo ""
if [ -z "$SQL_PASSWORD" ]; then echo -e "${RED}Error: SQL password is required${NC}"; exit 1; fi
echo -n "Enter Merchant360 base URL (e.g. https://merchant360-test-api.azurewebsites.net): "; read M360_URL; echo ""
echo -n "Enter Merchant360 shared X-Api-Key (blank to set later): "; read -s M360_KEY; echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Creating resource group '${RESOURCE_GROUP}'..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

echo ""
echo -e "${YELLOW}Deploying infrastructure (5-10 minutes)...${NC}"
RESULT=$(az deployment group create \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --template-file "${SCRIPT_DIR}/main.bicep" \
    --parameters "${SCRIPT_DIR}/parameters.${ENVIRONMENT}.json" \
    --parameters sqlAdminPassword="$SQL_PASSWORD" merchant360BaseUrl="$M360_URL" merchant360ApiKey="$M360_KEY" \
    --output json)

API_URL=$(echo "$RESULT" | jq -r '.properties.outputs.apiAppUrl.value')
ADMIN_URL=$(echo "$RESULT" | jq -r '.properties.outputs.adminAppUrl.value')
WORKERS=$(echo "$RESULT" | jq -r '.properties.outputs.workersAppName.value')
SQL_SERVER=$(echo "$RESULT" | jq -r '.properties.outputs.sqlServerFqdn.value')
STORAGE=$(echo "$RESULT" | jq -r '.properties.outputs.storageEndpoint.value')

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Deployment Successful!${NC}"
echo -e "${GREEN}========================================${NC}"
echo "  API:      ${API_URL}"
echo "  Admin:    ${ADMIN_URL}"
echo "  Workers:  ${WORKERS} (no public ingress)"
echo "  SQL:      ${SQL_SERVER}"
echo "  Storage:  ${STORAGE}"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "  1. ./migrate.sh ${ENVIRONMENT}    # apply EF Core migrations to Azure SQL"
echo "  2. ./publish.sh ${ENVIRONMENT}    # build & deploy api, admin, workers"
echo "  3. Set SPR SFTP settings on the workers app (az webapp config appsettings set ...)"
echo ""
