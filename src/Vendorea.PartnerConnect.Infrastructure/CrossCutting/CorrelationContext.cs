namespace Vendorea.PartnerConnect.Infrastructure.CrossCutting;

public interface ICorrelationContext
{
    string CorrelationId { get; }
}

public class CorrelationContext : ICorrelationContext
{
    public const string HeaderName = "X-Correlation-ID";

    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
}
