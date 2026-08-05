using Microsoft.EntityFrameworkCore;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Domain;

namespace SeeSight.Identity.UnitTests.TestSupport;

/// <summary>
/// A real EF Core <see cref="DbContext"/> backed by the InMemory provider —
/// fast, in-process, no external dependency — used because faking
/// <see cref="DbSet{TEntity}"/>'s async LINQ surface (SingleOrDefaultAsync,
/// AnyAsync) by hand is impractical. Each instance gets its own isolated
/// database (a fresh Guid name), so tests never share state.
/// </summary>
internal sealed class FakeIdentityDbContext()
    : DbContext(new DbContextOptionsBuilder<FakeIdentityDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options), IIdentityDbContext
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(u => u.Id);
            builder.HasIndex(u => u.Email).IsUnique();
        });
    }
}
