#!/bin/bash
# Vendorea PartnerConnect - Apply EF Core migrations to Azure SQL
# Usage: ./migrate.sh <environment>
# Example: ./migrate.sh test

set -e

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'

ENVIRONMENT=${1:-test}
BASE_NAME="partnerconnect"
RESOURCE_GROUP="rg-${BASE_NAME}-${ENVIRONMENT}"
SQL_SERVER="${BASE_NAME}-${ENVIRONMENT}-sql"
SQL_FQDN="${SQL_SERVER}.database.windows.net"
DATABASE="PartnerConnect"
SQL_USER="pcadmin"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

echo -n "Enter SQL Server admin password: "; read -s SQL_PASSWORD; echo ""
if [ -z "$SQL_PASSWORD" ]; then echo -e "${RED}Error: SQL password is required${NC}"; exit 1; fi

# Open the SQL firewall for this machine so migrations can connect.
MY_IP=$(curl -s https://api.ipify.org)
echo -e "${YELLOW}Opening SQL firewall for ${MY_IP}...${NC}"
az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" --server "$SQL_SERVER" \
    --name "migrate-$(date +%s)" --start-ip-address "$MY_IP" --end-ip-address "$MY_IP" --output none

CONN="Server=tcp:${SQL_FQDN},1433;Initial Catalog=${DATABASE};User ID=${SQL_USER};Password=${SQL_PASSWORD};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;MultipleActiveResultSets=True;"

# Ensure the EF tool is available.
dotnet tool install --global dotnet-ef >/dev/null 2>&1 || true
export PATH="$PATH:$HOME/.dotnet/tools"

echo -e "${YELLOW}Applying migrations to ${SQL_FQDN}/${DATABASE}...${NC}"
dotnet ef database update \
    --project "${REPO_ROOT}/src/Vendorea.PartnerConnect.Persistence" \
    --startup-project "${REPO_ROOT}/src/Vendorea.PartnerConnect.API" \
    --connection "$CONN"

echo -e "${GREEN}Migrations applied.${NC}"
