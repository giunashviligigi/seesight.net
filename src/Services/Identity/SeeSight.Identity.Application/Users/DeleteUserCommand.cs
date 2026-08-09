using MediatR;

namespace SeeSight.Identity.Application.Users;

/// <summary>
/// Hard delete — reserved for the <c>createLogin: true</c> compensating-rollback
/// path (docs/TenantArchitecture.md §6): Tenant Service calls this only when its
/// own local <c>Employee</c> save failed after this very user was just created,
/// to avoid leaving an orphaned Identity Service account. Never a general
/// user-deletion workflow — ordinary employee offboarding uses
/// <see cref="DeactivateUserCommand"/>. Idempotent: a missing user is treated as
/// already-deleted, not an error, since the caller may retry this rollback.
/// </summary>
public sealed record DeleteUserCommand(Guid UserId) : IRequest;
