using PlayForge.Domain.Entities;
using PlayForge.Domain.ValueObjects;
using PlayForge.Infrastructure.Persistence.Repositories;

namespace PlayForge.Integration.Tests;

/// <summary>
/// Verifies that the SteamId uniqueness contract is enforced at the service layer:
/// looking up by SteamId before inserting means the same Steam account is never
/// duplicated in the database.
/// </summary>
public class UserRepositoryTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task FindBySteamId_AfterAdd_ReturnsInsertedUser()
    {
        await using var ctx  = fixture.CreateContext();
        var repo = new UserRepository(ctx);

        var steamId = new SteamId(76561100000000001L);
        var user    = User.Create(steamId, "Player One", "https://example.com/avatar.jpg");

        await repo.AddAsync(user);
        await repo.SaveChangesAsync();

        await using var ctx2 = fixture.CreateContext();
        var repo2  = new UserRepository(ctx2);
        var found  = await repo2.FindBySteamIdAsync(steamId);

        Assert.NotNull(found);
        Assert.Equal(steamId.Value, found.SteamId.Value);
        Assert.Equal("Player One", found.DisplayName);
    }

    [Fact]
    public async Task UpsertPattern_SameSteamIdTwice_ProducesOnlyOneRow()
    {
        // Simulates what SteamAuthHandler does: FindBySteamId first, only Add if absent
        var steamId = new SteamId(76561100000000002L);

        await using var ctx1  = fixture.CreateContext();
        var repo1 = new UserRepository(ctx1);

        // First "login"
        var existing = await repo1.FindBySteamIdAsync(steamId);
        if (existing is null)
        {
            await repo1.AddAsync(User.Create(steamId, "Player Two", ""));
            await repo1.SaveChangesAsync();
        }

        await using var ctx2  = fixture.CreateContext();
        var repo2 = new UserRepository(ctx2);

        // Second "login" — same pattern
        var existing2 = await repo2.FindBySteamIdAsync(steamId);
        if (existing2 is null)
        {
            await repo2.AddAsync(User.Create(steamId, "Player Two", ""));
            await repo2.SaveChangesAsync();
        }

        // Verify only one row exists
        await using var ctx3 = fixture.CreateContext();
        var count = ctx3.Users.Count(u => u.SteamId.Value == steamId.Value);
        Assert.Equal(1, count);
    }
}
