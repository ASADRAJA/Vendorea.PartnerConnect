# Vendorea PartnerConnect

A partner integration platform that manages trading partner connections, data feeds (pricing, inventory, product content), and synchronization with the Merchant360 platform.

## Purpose

Vendorea PartnerConnect serves as a **bounded solution** separate from Merchant360, providing:

- **Trading Partner Management**: Register and configure connections to wholesalers, distributors, and other trading partners
- **Feed Processing**: Ingest and process price lists, inventory feeds, and product content from partners
- **Merchant360 Integration**: Push processed data to dealer tenants in the Merchant360 platform
- **Multi-Dealer Support**: Manage different partner configurations across multiple dealers

## Relationship to Merchant360

This solution is designed to work alongside Merchant360 but remains **completely separate**:

- Uses the same .NET 8.0 stack and development conventions as Merchant360
- Communicates with Merchant360 via HTTP API (Merchant360Connector project)
- Does **not** share databases or direct code references with Merchant360
- Can be deployed, scaled, and maintained independently

## Project Structure

```
Vendorea.PartnerConnect/
├── src/
│   ├── Vendorea.PartnerConnect.API              # ASP.NET Core API (port 5010)
│   ├── Vendorea.PartnerConnect.Application      # Application services, use cases, repository interfaces
│   ├── Vendorea.PartnerConnect.BackgroundWorkers # Background processing workers
│   ├── Vendorea.PartnerConnect.Contracts        # DTOs, external service interfaces, events
│   ├── Vendorea.PartnerConnect.Domain           # Domain entities and business logic
│   ├── Vendorea.PartnerConnect.Infrastructure   # Cross-cutting concerns (correlation, tenant, auth)
│   ├── Vendorea.PartnerConnect.Merchant360Connector # HTTP client for Merchant360 API
│   ├── Vendorea.PartnerConnect.PartnerAdapters  # Partner-specific adapters (SPR, etc.)
│   ├── Vendorea.PartnerConnect.Persistence      # EF Core DbContext, repositories, migrations
│   └── Vendorea.PartnerConnect.Transport        # SFTP/file transport abstractions
├── tests/
│   ├── Vendorea.PartnerConnect.UnitTests
│   └── Vendorea.PartnerConnect.IntegrationTests
├── Directory.Build.props                        # Shared build configuration
├── .editorconfig                                # Code style rules
├── .gitignore
└── Vendorea.PartnerConnect.sln
```

### Clean Architecture

The solution follows Clean Architecture principles:

- **Domain**: Core business entities with no external dependencies
- **Contracts**: DTOs and external service interface definitions (IPartnerAdapter, IMerchant360Client)
- **Application**: Use case orchestration, repository interfaces, depends on Domain/Contracts
- **Infrastructure**: Cross-cutting concerns (correlation ID, tenant context, service auth)
- **Persistence**: EF Core DbContext, entity configurations, repository implementations
- **Transport**: SFTP and file system transport abstractions for partner file exchange
- **API/BackgroundWorkers**: Entry points

### Key Architectural Decisions

1. **Repository interfaces in Application**: Repository interfaces (ITradingPartnerRepository, etc.) live in Application layer, not Contracts. This keeps the Application layer as the core use-case orchestrator.

2. **Transport layer**: Dedicated Transport project for SFTP/file mechanics, keeping partner adapters focused on business logic.

3. **Cross-cutting concerns**: Middleware for correlation ID, tenant context, and service authentication propagation across all requests.

### Domain Entities

| Entity | Purpose |
|--------|---------|
| `TradingPartner` | External partner (wholesaler, distributor) |
| `DealerPartnerConnection` | Connection between a dealer and a partner (multi-tenant) |
| `PartnerCapabilityConfiguration` | Partner feature configuration |
| `PartnerDocument` | Trading documents (price lists, inventory feeds) |
| `PriceFeedBatch` | Batch of price updates from a partner |
| `InventoryFeedBatch` | Batch of inventory updates |
| `ContentSyncJob` | Product content synchronization job |

### Contract Namespaces

- `Contracts.DTOs.TradingDocuments` - Price lists, POs, invoices
- `Contracts.DTOs.CommercialData` - Inventory, availability
- `Contracts.DTOs.ProductContent` - Product descriptions, images
- `Contracts.DTOs.IntegrationManagement` - Partner and connection management
- `Contracts.Interfaces` - External service contracts (IPartnerAdapter, IMerchant360Client)

## How to Run Locally

### Prerequisites

- .NET 8.0 SDK
- SQL Server LocalDB (or SQL Server)
- Merchant360 API running on `http://localhost:5003` (optional, for full integration)

### Database Setup

Create the database and apply migrations:

```bash
cd src/Vendorea.PartnerConnect.Persistence
dotnet ef migrations add InitialCreate --startup-project ../Vendorea.PartnerConnect.API
dotnet ef database update --startup-project ../Vendorea.PartnerConnect.API
```

### Running the API

```bash
cd src/Vendorea.PartnerConnect.API
dotnet run
```

The API will start on `http://localhost:5010` with Swagger UI available at `/swagger`.

### Running the Background Workers

```bash
cd src/Vendorea.PartnerConnect.BackgroundWorkers
dotnet run
```

### Running Tests

```bash
dotnet test
```

## Configuration

### appsettings.json

Key configuration sections:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=PartnerConnect;..."
  },
  "Merchant360": {
    "BaseUrl": "http://localhost:5003",
    "ApiKey": "REPLACE_WITH_API_KEY"
  },
  "PartnerAdapters": {
    "SPR": {
      "BaseUrl": "https://api.sprpartner.example.com",
      "Username": "REPLACE_WITH_USERNAME",
      "Password": "REPLACE_WITH_PASSWORD"
    }
  }
}
```

### Cross-Cutting Headers

The API supports the following request headers for context propagation:

| Header | Purpose |
|--------|---------|
| `X-Correlation-ID` | Request correlation for distributed tracing |
| `X-Dealer-ID` | Tenant/dealer identifier for multi-tenant operations |
| `X-Dealer-Code` | Alternative tenant identifier |
| `X-Service-Name` | Calling service name for service-to-service auth |
| `X-API-Key` | API key for service authentication |

### Environment-Specific Configuration

- `appsettings.Development.json` - Local development settings
- Use user secrets or environment variables for sensitive values in production

## Implementation Status

### Fully Implemented

- Solution structure and project references
- Build configuration (Directory.Build.props, .editorconfig)
- API with Swagger, health endpoint, controllers
- Background worker skeleton with Serilog logging
- Domain entities with EF Core entity configurations
- EF Core DbContext with SQL Server support
- Repository interfaces and EF Core implementations
- Cross-cutting concerns (correlation ID, tenant context, service auth middleware)
- Transport layer (SFTP client, local file client)
- Test project structure with sample tests

### Placeholder / TODO

- EF Core migrations (run `dotnet ef migrations add` to create)
- Full feed processing logic
- Partner adapter implementations (SPR adapter is a placeholder)
- Merchant360 API integration (client is scaffolded, endpoints need implementation)
- Production authentication and authorization

## Development Conventions

This solution mirrors Merchant360's conventions:

- **Framework**: .NET 8.0
- **Logging**: Serilog with structured logging
- **Testing**: xUnit + FluentAssertions + Moq
- **API Documentation**: Swagger/OpenAPI
- **ORM**: Entity Framework Core with SQL Server
- **Code Style**: Enforced via .editorconfig
- **Naming**: Interfaces prefixed with `I`, private fields with `_`

## Next Steps

1. Create initial EF Core migration
2. Complete SPR adapter for real partner integration
3. Implement full feed processing pipeline
4. Add production authentication/authorization
5. Set up CI/CD pipeline
