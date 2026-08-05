using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SeeSight.Identity.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> construct <see cref="IdentityDbContext"/>
/// without needing the full Api project's DI container wired up — a standard EF
/// Core design-time pattern. Not used at runtime (Program.cs uses
/// <c>AddIdentityInfrastructure</c> instead).
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=identity;Username=seesight;Password=seesight";

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
