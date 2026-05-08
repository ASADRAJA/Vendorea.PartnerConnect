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
│   ├── Vendorea.PartnerConnect.Application      # Application services and use cases
│   ├── Vendorea.PartnerConnect.BackgroundWorkers # Background processing workers
│   ├── Vendorea.PartnerConnect.Contracts        # DTOs, interfaces, events
│   ├── Vendorea.PartnerConnect.Domain           # Domain entities and business logic
│   ├── Vendorea.PartnerConnect.Infrastructure   # DI registration, cross-cutting concerns
│   ├── Vendorea.PartnerConnect.Merchant360Connector # HTTP client for Merchant360 API
│   ├── Vendorea.PartnerConnect.PartnerAdapters  # Partner-specific adapters (SPR, etc.)
│   └── Vendorea.PartnerConnect.Persistence      # Repositories and database access
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
- **Contracts**: Interface definitions and DTOs
- **Application**: Use case orchestration, depends on Domain/Contracts
- **Infrastructure/Persistence**: Technical implementations
- **API/BackgroundWorkers**: Entry points

### Domain Placeholders

The following domain entities are scaffolded for multi-dealer support:

| Entity | Purpose |
|--------|---------|
| `TradingPartner` | External partner (wholesaler, distributor) |
| `DealerPartnerConnection` | Connection between a dealer and a partner |
| `PartnerCapabilityConfiguration` | Partner feature configuration |
| `PartnerDocument` | Trading documents (price lists, inventory feeds) |
| `PriceFeedBatch` | Batch of price updates from a partner |
| `InventoryFeedBatch` | Batch of inventory updates |
| `ContentSyncJob` | Product content synchronization job |

### Placeholder Namespaces

The following namespaces are scaffolded for future development:

- `Contracts.DTOs.TradingDocuments` - Price lists, POs, invoices
- `Contracts.DTOs.CommercialData` - Inventory, availability
- `Contracts.DTOs.ProductContent` - Product descriptions, images
- `Contracts.DTOs.IntegrationManagement` - Partner and connection management

## How to Run Locally

### Prerequisites

- .NET 8.0 SDK
- SQL Server LocalDB (or SQL Server)
- Merchant360 API running on `http://localhost:5003` (optional, for full integration)

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

### Environment-Specific Configuration

- `appsettings.Development.json` - Local development settings
- Use user secrets or environment variables for sensitive values in production

## What is Scaffolded vs Placeholder

### Fully Implemented

- Solution structure and project references
- Build configuration (Directory.Build.props, .editorconfig)
- API with Swagger, health endpoint, basic controller
- Background worker skeleton with Serilog logging
- Domain entities (structure only, no persistence)
- Service interfaces and one sample implementation
- Test project structure with sample tests

### Placeholder / TODO

- Database context and migrations (Persistence)
- Repository implementations
- Full feed processing logic
- Partner adapter implementations (SPR adapter is a placeholder)
- Authentication and authorization
- Merchant360 API integration (client is scaffolded, endpoints need implementation)

## Development Conventions

This solution mirrors Merchant360's conventions:

- **Framework**: .NET 8.0
- **Logging**: Serilog with structured logging
- **Testing**: xUnit + FluentAssertions + Moq
- **API Documentation**: Swagger/OpenAPI
- **Code Style**: Enforced via .editorconfig
- **Naming**: Interfaces prefixed with `I`, private fields with `_`

## Next Steps

1. Implement database context and migrations
2. Implement repository layer
3. Complete SPR adapter for real partner integration
4. Add authentication/authorization
5. Implement full feed processing pipeline
6. Set up CI/CD pipeline
