namespace SeeSight.Gateway.Proxy;

public sealed class TenantServiceOptions
{
    public const string SectionName = "TenantService";

    public string BaseUrl { get; set; } = "http://localhost:5076";
}
