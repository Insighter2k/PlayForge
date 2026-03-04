using Microsoft.EntityFrameworkCore;
using PlayForge.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PlayForge.Integration.Tests;

/// <summary>
/// Spins up a Postgres container once per test class.
/// Each test should call <see cref="CreateContext"/> to get an isolated DbContext
/// (avoids EF's first-level cache leaking between test methods).
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Apply all EF migrations once
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Returns a fresh DbContext backed by the test database.</summary>
    public AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(opts);
    }
}
