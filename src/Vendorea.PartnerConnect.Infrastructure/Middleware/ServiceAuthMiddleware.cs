using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Infrastructure.CrossCutting;

namespace Vendorea.PartnerConnect.Infrastructure.Middleware;

public class ServiceAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ServiceAuthMiddleware> _logger;

    public ServiceAuthMiddleware(RequestDelegate next, ILogger<ServiceAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ServiceAuthContext authContext)
    {
        var serviceName = context.Request.Headers[ServiceAuthContext.ServiceNameHeaderName].FirstOrDefault();
        var apiKey = context.Request.Headers[ServiceAuthContext.ApiKeyHeaderName].FirstOrDefault();

        authContext.ServiceName = serviceName;
        authContext.ApiKey = apiKey;

        if (authContext.IsAuthenticated)
        {
            _logger.LogDebug("Request authenticated from service: {ServiceName}", serviceName);
        }

        await _next(context);
    }
}
