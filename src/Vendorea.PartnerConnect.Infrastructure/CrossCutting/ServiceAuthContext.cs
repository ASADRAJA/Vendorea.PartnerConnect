namespace Vendorea.PartnerConnect.Infrastructure.CrossCutting;

public interface IServiceAuthContext
{
    string? ServiceName { get; }
    string? ApiKey { get; }
    bool IsAuthenticated { get; }
}

public class ServiceAuthContext : IServiceAuthContext
{
    public const string ServiceNameHeaderName = "X-Service-Name";
    public const string ApiKeyHeaderName = "X-API-Key";

    public string? ServiceName { get; set; }
    public string? ApiKey { get; set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(ApiKey);
}
