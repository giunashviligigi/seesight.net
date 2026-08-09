namespace SeeSight.Tenant.Application.Exceptions;

/// <summary>
/// Maps to 403 — the caller's role is not one of the roles allowed for this
/// endpoint (docs/Authorization.md §3). Downstream services have no
/// authentication scheme of their own (JWT validation happens once, at the
/// Gateway — docs/Authentication.md §8), so role gating is this explicit
/// check rather than an ASP.NET Core <c>[Authorize(Roles = ...)]</c> attribute,
/// mirroring Identity.Api's established pattern.
/// </summary>
public sealed class InsufficientRoleException() : Exception("You do not have permission to perform this action.");
