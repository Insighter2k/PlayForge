using PlayForge.Application.Interfaces;
using PlayForge.Application.Services;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Enums;
using PlayForge.Domain.Interfaces;
using PlayForge.Domain.ValueObjects;
using PlayForge.Infrastructure.Persistence.Repositories;

namespace PlayForge.Integration.Tests;

/// <summary>
/// Tests group creation, member joining, and the common-games intersection query
/// against a real PostgreSQL database via Testcontainers.
/// </summary>
public class GroupIntegrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    // Minimal no-op registry — GroupService only uses it for SearchGamesAsync,
    // which is not exercised in these tests.
    private sealed class NoOpRegistry : IPlatformConnectorRegistry
    {
        public IPlatformConnector Get(Platform _) => throw new NotImplementedException();
        public IEnumerable<IPlatformConnector> GetAll() => [];
    }

    private GroupService BuildGroupService(
        IGroupRepository  groups,
        IUserRepository   users,
        IGameRepository   games) =>
        new(groups, users, games, new NoOpRegistry());

    [Fact]
    public async Task CreateGroup_PersistsGroupAndHostMembership()
    {
        await using var ctx  = fixture.CreateContext();
        var groups = new GroupRepository(ctx);
        var users  = new UserRepository(ctx);
        var games  = new GameRepository(ctx);
        var svc    = BuildGroupService(groups, users, games);

        var hostId = Guid.NewGuid();
        var dto    = await svc.CreateGroupAsync(hostId, "Test Group");

        await using var ctx2 = fixture.CreateContext();
        var group = ctx2.GameGroups
            .First(g => g.Id == dto.Id);

        Assert.Equal("Test Group", group.Name);
        Assert.Equal(hostId, group.HostUserId);
        Assert.Equal(8, group.InviteCode.Length);
    }

    [Fact]
    public async Task JoinGroupByCode_AddsMemberToGroup()
    {
        await using var ctx   = fixture.CreateContext();
        var groups = new GroupRepository(ctx);
        var users  = new UserRepository(ctx);
        var games  = new GameRepository(ctx);
        var svc    = BuildGroupService(groups, users, games);

        var hostId   = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var dto      = await svc.CreateGroupAsync(hostId, "Join Test Group");

        await using var ctx2   = fixture.CreateContext();
        var groups2 = new GroupRepository(ctx2);
        var svc2    = BuildGroupService(groups2, new UserRepository(ctx2), new GameRepository(ctx2));
        await svc2.JoinGroupByCodeAsync(memberId, dto.InviteCode);

        await using var ctx3 = fixture.CreateContext();
        var memberships = ctx3.GroupMemberships
            .Where(m => m.GroupId == dto.Id)
            .ToList();

        Assert.Equal(2, memberships.Count);
        Assert.Contains(memberships, m => m.UserId == hostId);
        Assert.Contains(memberships, m => m.UserId == memberId);
    }

    [Fact]
    public async Task GetGroupCommonGames_ReturnsOnlyGamesOwnedByAllMembers()
    {
        await using var ctx  = fixture.CreateContext();
        var userRepo  = new UserRepository(ctx);
        var gameRepo  = new GameRepository(ctx);
        var groupRepo = new GroupRepository(ctx);

        // Seed two users
        var user1 = User.Create(new SteamId(76561200000000001L), "User1", "");
        var user2 = User.Create(new SteamId(76561200000000002L), "User2", "");
        await userRepo.AddAsync(user1);
        await userRepo.AddAsync(user2);

        // Seed three games
        var gameShared = Game.Create(new AppId(440),  "Team Fortress 2",  Platform.Steam);
        var gameOnly1  = Game.Create(new AppId(730),  "CS2",              Platform.Steam);
        var gameOnly2  = Game.Create(new AppId(4000), "Garry's Mod",      Platform.Steam);
        await gameRepo.AddRangeAsync([gameShared, gameOnly1, gameOnly2]);

        // User1 owns shared + only1; User2 owns shared + only2
        user1.AddGame(UserGame.Create(user1.Id, gameShared.Id, 100));
        user1.AddGame(UserGame.Create(user1.Id, gameOnly1.Id, 50));
        user2.AddGame(UserGame.Create(user2.Id, gameShared.Id, 200));
        user2.AddGame(UserGame.Create(user2.Id, gameOnly2.Id, 30));

        // Create group with both members
        var group = GameGroup.Create(user1.Id, "Common Games Group");
        group.AddMember(user2.Id);
        await groupRepo.AddAsync(group);
        await ctx.SaveChangesAsync();

        // Act
        await using var ctx2   = fixture.CreateContext();
        var svc2 = BuildGroupService(
            new GroupRepository(ctx2),
            new UserRepository(ctx2),
            new GameRepository(ctx2));

        var common = await svc2.GetGroupCommonGamesAsync(group.Id);

        // Assert: only Team Fortress 2 is owned by both
        Assert.Single(common);
        Assert.Equal("Team Fortress 2", common[0].Name);
    }
}
