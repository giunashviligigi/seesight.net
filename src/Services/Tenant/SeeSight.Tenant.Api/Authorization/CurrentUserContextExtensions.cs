using SeeSight.SharedKernel.Http;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Api.Authorization;

/// <summary>
/// Role gating for controller actions. Downstream services have no
/// authentication scheme of their own (JWT validation happens once, at the
/// Gateway — docs/Authentication.md §8), so this is an explicit check against
/// the Gateway-forwarded role rather than an ASP.NET Core
/// <c>[Authorize(Roles = ...)]</c> attribute — mirroring Identity.Api's
/// established pattern (docs/Authorization.md §3).
/// </summary>
public static class CurrentUserContextExtensions
{
    public static void RequireRole(this ICurrentUserContext currentUser, params ReadOnlySpan<string> allowedRoles)
    {
        if (currentUser.Role is null || !allowedRoles.Contains(currentUser.Role))
        {
            throw new InsufficientRoleException();
        }
    }
}
