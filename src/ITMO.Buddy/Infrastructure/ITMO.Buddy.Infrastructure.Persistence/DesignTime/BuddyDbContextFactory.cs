using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ITMO.Buddy.Infrastructure.Persistence.DesignTime;

public sealed class BuddyDbContextFactory
    : IDesignTimeDbContextFactory<BuddyDbContext>
{
    public BuddyDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__Postgres");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "PostgreSQL connection string is missing. " +
                "Set the ConnectionStrings__Postgres " +
                "environment variable before using dotnet ef.");
        }

        var options = new DbContextOptionsBuilder<BuddyDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BuddyDbContext(options);
    }
}