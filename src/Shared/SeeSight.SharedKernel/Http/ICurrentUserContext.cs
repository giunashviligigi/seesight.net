namespace SeeSight.SharedKernel.Http;

/// <summary>
/// The identity of the caller for the current request, as forwarded by the
/// Gateway (see <see cref="ForwardedIdentityHeaders"/>). Pure data — no
/// authorization *decision* lives here (that stays inside each service, per
/// docs/Authorization.md §4).
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    string? Role { get; }
    Guid? CompanyId { get; }
    bool IsAuthenticated { get; }
}
