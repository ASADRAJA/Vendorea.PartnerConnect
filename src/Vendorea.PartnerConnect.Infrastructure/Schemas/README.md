# XSD Schema Files

This directory contains XSD schemas for document validation. Schemas are embedded as resources in the Infrastructure assembly at build time.

## SPR Schemas

| File | Status | Source |
|------|--------|--------|
| `SPR/EZASNS.xsd` | **PRODUCTION** | Obtained from SPR documentation |
| `SPR/EZINV4.xsd` | **PRODUCTION** | Obtained from SPR documentation |
| `SPR/EZPO4.xsd` | **PLACEHOLDER** | Needs official schema from SPR |
| `SPR/EZPOACK.xsd` | **PLACEHOLDER** | Needs official schema from SPR |
| `SPR/Inventory.xsd` | **PLACEHOLDER** | Needs official schema from SPR |

## Production Deployment

Before deploying to production, ensure all **PLACEHOLDER** schemas are replaced with official schemas from the trading partner:

1. Contact SPR to obtain official XSD specifications for:
   - EZPO4 (Purchase Order)
   - EZPOACK (Purchase Order Acknowledgment)
   - Inventory Feed

2. Replace the placeholder files in `Schemas/SPR/`

3. Rebuild the solution to embed the new schemas

## Configuration

The `XsdSchemaProvider` loads schemas in this priority order:

1. **File System Override** (if `XsdSchemas:SchemaBasePath` is configured)
2. **Embedded Resources** (default, built into assembly)

For development, you can override embedded schemas by setting:

```json
{
  "XsdSchemas": {
    "SchemaBasePath": "C:/path/to/schemas",
    "AllowMissingSchemas": true
  }
}
```

## Adding New Partners

To add schemas for a new trading partner:

1. Create a subdirectory: `Schemas/{PartnerCode}/`
2. Add XSD files with standard names
3. Update `XsdSchemaProvider.SchemaMap` with the mappings
4. Rebuild to embed the new schemas
