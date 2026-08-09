namespace SeeSight.Tenant.Infrastructure.Identity;

public sealed class IdentityServiceOptions
{
    public const string SectionName = "IdentityService";

    public string BaseUrl { get; set; } = "http://localhost:5075";
}
