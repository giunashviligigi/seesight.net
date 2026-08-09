namespace SeeSight.SharedKernel.Http;

/// <summary>
/// Role name constants matching <c>UserRole.ToString()</c> exactly (Identity
/// Service issues these as the JWT role claim's value — see
/// SeeSight.Identity.Infrastructure.Security.RsaJwtIssuer). Defined once here
/// so every downstream service's role checks and tenant-context mapping use
/// the same literal, never a per-service copy that can drift.
/// </summary>
public static class SeeSightRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string CompanyAdmin = "CompanyAdmin";
    public const string Employee = "Employee";
}
