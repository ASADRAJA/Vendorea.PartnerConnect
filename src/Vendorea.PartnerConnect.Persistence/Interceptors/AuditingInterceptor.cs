using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Interceptors;

/// <summary>
/// EF Core interceptor for automatic audit logging.
/// </summary>
public class AuditingInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Entity types to audit
    private static readonly HashSet<Type> _auditedTypes = new()
    {
        typeof(TradingPartner),
        typeof(PartnerDocument),
        typeof(WebhookSubscription),
        typeof(QuarantinedDocument),
        typeof(MerchantSubscriptionRequest),
        typeof(PriceFeedUpload),
        typeof(SprContentUpload)
    };

    // Properties to exclude from audit (sensitive data)
    private static readonly HashSet<string> _excludedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Secret",
        "Password",
        "ApiKey",
        "Token",
        "Credentials"
    };

    public AuditingInterceptor(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is PartnerConnectDbContext dbContext)
        {
            var auditEntries = CreateAuditEntries(dbContext);

            foreach (var auditEntry in auditEntries)
            {
                dbContext.Set<AuditLog>().Add(auditEntry);
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is PartnerConnectDbContext dbContext)
        {
            var auditEntries = CreateAuditEntries(dbContext);

            foreach (var auditEntry in auditEntries)
            {
                dbContext.Set<AuditLog>().Add(auditEntry);
            }
        }

        return base.SavingChanges(eventData, result);
    }

    private List<AuditLog> CreateAuditEntries(PartnerConnectDbContext dbContext)
    {
        var auditEntries = new List<AuditLog>();
        var httpContext = _httpContextAccessor?.HttpContext;

        foreach (var entry in dbContext.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog ||
                entry.State == EntityState.Detached ||
                entry.State == EntityState.Unchanged)
            {
                continue;
            }

            // Only audit configured entity types
            if (!_auditedTypes.Contains(entry.Entity.GetType()))
            {
                continue;
            }

            var auditLog = new AuditLog
            {
                EntityType = entry.Entity.GetType().Name,
                EntityId = GetPrimaryKeyValue(entry),
                UserId = httpContext?.User?.Identity?.Name ?? "System",
                UserName = httpContext?.User?.Identity?.Name ?? "System",
                IpAddress = GetClientIpAddress(httpContext),
                UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
                RequestPath = httpContext?.Request?.Path.ToString(),
                HttpMethod = httpContext?.Request?.Method,
                CorrelationId = httpContext?.TraceIdentifier,
                Timestamp = DateTime.UtcNow
            };

            // Extract dealer ID if available
            auditLog.DealerId = GetDealerIdFromEntity(entry.Entity);

            switch (entry.State)
            {
                case EntityState.Added:
                    auditLog.Action = AuditAction.Create;
                    auditLog.NewValues = SerializeValues(entry.CurrentValues);
                    break;

                case EntityState.Modified:
                    auditLog.Action = AuditAction.Update;
                    auditLog.OldValues = SerializeValues(entry.OriginalValues);
                    auditLog.NewValues = SerializeValues(entry.CurrentValues);
                    auditLog.ChangedProperties = GetChangedProperties(entry);
                    break;

                case EntityState.Deleted:
                    auditLog.Action = AuditAction.Delete;
                    auditLog.OldValues = SerializeValues(entry.OriginalValues);
                    break;
            }

            auditEntries.Add(auditLog);
        }

        return auditEntries;
    }

    private static string GetPrimaryKeyValue(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProperties == null || !keyProperties.Any())
        {
            return "Unknown";
        }

        var keyValues = keyProperties
            .Select(p => entry.CurrentValues[p]?.ToString() ?? "null");

        return string.Join(",", keyValues);
    }

    private static int? GetDealerIdFromEntity(object entity)
    {
        var dealerIdProperty = entity.GetType().GetProperty("DealerId");
        if (dealerIdProperty != null)
        {
            var value = dealerIdProperty.GetValue(entity);
            if (value is int dealerId)
            {
                return dealerId;
            }
        }
        return null;
    }

    private static string? GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null) return null;

        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string? SerializeValues(PropertyValues values)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var property in values.Properties)
        {
            if (_excludedProperties.Contains(property.Name))
            {
                dict[property.Name] = "[REDACTED]";
            }
            else
            {
                var value = values[property];
                // Don't serialize large text fields or binary data
                if (value is string str && str.Length > 1000)
                {
                    dict[property.Name] = str[..1000] + "...[truncated]";
                }
                else if (value is byte[])
                {
                    dict[property.Name] = "[BINARY DATA]";
                }
                else
                {
                    dict[property.Name] = value;
                }
            }
        }

        return JsonSerializer.Serialize(dict, _jsonOptions);
    }

    private static string? GetChangedProperties(EntityEntry entry)
    {
        var changedProperties = new List<string>();

        foreach (var property in entry.OriginalValues.Properties)
        {
            var originalValue = entry.OriginalValues[property];
            var currentValue = entry.CurrentValues[property];

            if (!Equals(originalValue, currentValue))
            {
                changedProperties.Add(property.Name);
            }
        }

        return changedProperties.Count > 0
            ? string.Join(",", changedProperties)
            : null;
    }
}
