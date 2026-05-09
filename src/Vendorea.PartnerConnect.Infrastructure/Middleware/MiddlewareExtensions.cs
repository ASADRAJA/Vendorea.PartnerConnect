using Microsoft.AspNetCore.Builder;

namespace Vendorea.PartnerConnect.Infrastructure.Middleware;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UsePartnerConnectMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<TenantContextMiddleware>();
        app.UseMiddleware<ServiceAuthMiddleware>();

        return app;
    }
}
