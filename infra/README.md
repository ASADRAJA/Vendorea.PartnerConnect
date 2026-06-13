# Vendorea PartnerConnect — Azure Infrastructure

Bicep templates + scripts to deploy PartnerConnect into its **own resource group**
(`rg-partnerconnect-<env>`), in the **same subscription and region as Merchant360**
(`westus2` for test) but fully separate. Modeled on the Merchant360 infra conventions.

## Prerequisites
1. [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
2. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
3. An Azure subscription, `az login` completed
4. `jq` and `zip` available (for the deploy/publish scripts)

## Quick start (test)
```bash
cd infra
./deploy.sh test westus2     # 1. provision infra (prompts for SQL password + M360 URL/key)
./migrate.sh test            # 2. apply EF Core migrations to Azure SQL
./publish.sh test            # 3. build & deploy api, admin, workers
```

## Resources created
- **Resource Group**: `rg-partnerconnect-<env>`
- **App Service Plan**: `partnerconnect-<env>-plan` (shared)
- **Web Apps**:
  - `partnerconnect-<env>-api` — inbound order submission from M360, data/admin APIs, M360 callbacks
  - `partnerconnect-<env>-admin` — Blazor admin portal (calls the API)
  - `partnerconnect-<env>-workers` — background workers (outbox→M360, SPR polling, doc processing); `alwaysOn`, exposes `/health`
- **Azure SQL**: own server `partnerconnect-<env>-sql`, database `PartnerConnect`
- **Storage Account**: `pc<env><suffix>` with a private `partner-documents` blob container
- **Application Insights** (+ Log Analytics workspace)

## Environments / SKUs
| Environment | App Service | SQL  |
|-------------|-------------|------|
| test        | B1          | Basic |
| preprod     | S1          | S0    |
| prod        | P1v2        | S1    |

(Create `parameters.preprod.json` / `parameters.prod.json` from `parameters.test.json` as needed.)

## Configuration & secrets
Set via Bicep app settings; secrets are **not** stored in the parameters files:
- `ConnectionStrings:DefaultConnection` — Azure SQL (from the SQL module)
- `Storage:AzureBlob:ConnectionString` / `:ContainerName` — document storage
- `Merchant360:BaseUrl`, `Merchant360:ApiKey` — prompted at deploy time
- **SPR SFTP** settings (`PartnerAdapters:*`) are set post-deploy on the workers app:
  ```bash
  az webapp config appsettings set -g rg-partnerconnect-test -n partnerconnect-test-workers \
    --settings "PartnerAdapters__SPR__Host=..." "PartnerAdapters__SPR__Username=..." "PartnerAdapters__SPR__Password=..."
  ```

## Common commands
```bash
# List apps
az webapp list -g rg-partnerconnect-test -o table
# Tail logs
az webapp log tail -g rg-partnerconnect-test -n partnerconnect-test-api
# Open SQL firewall for your IP (for ad-hoc DB access)
MYIP=$(curl -s https://api.ipify.org); az sql server firewall-rule create \
  -g rg-partnerconnect-test -s partnerconnect-test-sql -n MyIP --start-ip-address $MYIP --end-ip-address $MYIP
```

## Teardown
```bash
az group delete --name rg-partnerconnect-test --yes --no-wait
```

## Notes
- The `workers` app runs as a normal App Service (Windows, .NET 8) with `alwaysOn=true`
  and a minimal `/health` endpoint so the platform keeps it loaded.
- No VNet / Key Vault / private endpoints — matches the Merchant360 test pattern. Add
  hardening (private endpoints, Key Vault references) before production.
