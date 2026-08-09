using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.UnitTests.TestSupport;

/// <summary>
/// A real EF Core <see cref="DbContext"/> backed by the InMemory provider —
/// fast, in-process, no external dependency. Deliberately has no tenant/soft-delete
/// query filters (those live on the real <c>TenantDbContext</c> in Infrastructure
/// and are exercised by integration tests instead) — Application-layer handler
/// unit tests exercise business logic assuming the filter's already been applied
/// by whatever context they're given, matching Identity.UnitTests' FakeIdentityDbContext convention.
/// </summary>
internal sealed class FakeTenantDbContext()
    : DbContext(new DbContextOptionsBuilder<FakeTenantDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options), ITenantDbContext
{
    public DbSet<Company> Companies => Set<Company>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Employee> Employees => Set<Employee>();

    /// <summary>
    /// Test-only seam for exercising a handler's failure-path handling (e.g.
    /// the createLogin compensating rollback) without needing a real database
    /// race condition to trigger a save failure deterministically.
    /// </summary>
    public Exception? ThrowOnSaveChanges { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        ThrowOnSaveChanges is not null
            ? Task.FromException<int>(ThrowOnSaveChanges)
            : base.SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(b => b.HasKey(c => c.Id));
        modelBuilder.Entity<Department>(b => b.HasKey(d => d.Id));
        modelBuilder.Entity<Employee>(b => b.HasKey(e => e.Id));
    }
}
