using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using Vendorea.PartnerConnect.Infrastructure.CrossCutting;

namespace Vendorea.PartnerConnect.Infrastructure.Middleware;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        var dealerIdHeader = context.Request.Headers[TenantContext.DealerIdHeaderName].FirstOrDefault();
        var dealerCodeHeader = context.Request.Headers[TenantContext.DealerCodeHeaderName].FirstOrDefault();

        if (int.TryParse(dealerIdHeader, out var dealerId))
        {
            tenantContext.DealerId = dealerId;
        }

        if (!string.IsNullOrEmpty(dealerCodeHeader))
        {
            tenantContext.DealerCode = dealerCodeHeader;
        }

        using (LogContext.PushProperty("DealerId", tenantContext.DealerId))
        using (LogContext.PushProperty("DealerCode", tenantContext.DealerCode))
        {
            if (tenantContext.IsMultiTenant)
            {
                _logger.LogDebug("Request has tenant context: DealerId={DealerId}, DealerCode={DealerCode}",
                    tenantContext.DealerId, tenantContext.DealerCode);
            }

            await _next(context);
        }
    }
}
