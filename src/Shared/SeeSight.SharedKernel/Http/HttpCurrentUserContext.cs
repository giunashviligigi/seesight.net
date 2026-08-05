using Microsoft.AspNetCore.Http;

namespace SeeSight.SharedKernel.Http;

/// <summary>
/// Populates <see cref="ICurrentUserContext"/> by reading the Gateway-forwarded
/// identity headers off the current request. Pure header-to-object mapping —
/// no business/authorization logic — so sharing this across every service does
/// not violate the "no business logic in shared libraries" rule (docs/CodingStandards.md §2).
/// Registered scoped (one instance per request) via <see cref="CurrentUserContextServiceCollectionExtensions"/>.
/// </summary>
internal sealed class HttpCurrentUserContext : ICurrentUserContext
{
    public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        var headers = httpContextAccessor.HttpContext?.Request.Headers;
        if (headers is null)
        {
            return;
        }

        if (Guid.TryParse(headers[ForwardedIdentityHeaders.UserId], out var userId))
        {
            UserId = userId;
        }

        var role = headers[ForwardedIdentityHeaders.UserRole].ToString();
        Role = string.IsNullOrEmpty(role) ? null : role;

        if (Guid.TryParse(headers[ForwardedIdentityHeaders.CompanyId], out var companyId))
        {
            CompanyId = companyId;
        }
    }

    public Guid? UserId { get; }
    public string? Role { get; }
    public Guid? CompanyId { get; }
    public bool IsAuthenticated => UserId is not null;
}
