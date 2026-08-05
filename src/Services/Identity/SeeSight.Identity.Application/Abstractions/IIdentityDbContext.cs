using Microsoft.EntityFrameworkCore;
using SeeSight.Identity.Domain;

namespace SeeSight.Identity.Application.Abstractions;

/// <summary>
/// The persistence contract Application depends on; implemented by the real EF
/// Core <c>IdentityDbContext</c> in Infrastructure. <see cref="DbSet{TEntity}"/>
/// is the ORM abstraction itself (Microsoft.EntityFrameworkCore only, no
/// provider package) — not a leak of Infrastructure-specific detail.
/// </summary>
public interface IIdentityDbContext
{
    DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
