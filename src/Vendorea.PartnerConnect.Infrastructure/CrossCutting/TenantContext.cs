namespace Vendorea.PartnerConnect.Infrastructure.CrossCutting;

public interface ITenantContext
{
    int? DealerId { get; }
    string? DealerCode { get; }
    bool IsMultiTenant { get; }
}

public class TenantContext : ITenantContext
{
    public const string DealerIdHeaderName = "X-Dealer-ID";
    public const string DealerCodeHeaderName = "X-Dealer-Code";

    public int? DealerId { get; set; }
    public string? DealerCode { get; set; }
    public bool IsMultiTenant => DealerId.HasValue;
}
