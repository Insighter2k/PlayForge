using PlayForge.Application.Services;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Enums;
using PlayForge.Domain.ValueObjects;
using PlayForge.Infrastructure.Persistence.Repositories;

namespace PlayForge.Integration.Tests;

/// <summary>
/// Full vote lifecycle: start → cast votes → close → verify winner persisted in DB.
/// </summary>
public class VoteSessionIntegrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static VoteService BuildVoteService(
        VoteSessionRepository sessions,
        GroupRepository       groups,
        UserRepository        users,
        GameRepository        games) =>
        new(sessions, groups, users, games);

    [Fact]
    public async Task VoteLifecycle_StartCastClose_WinnerPersistedInDb()
    {
        await using var ctx  = fixture.CreateContext();
        var userRepo    = new UserRepository(ctx);
        var gameRepo    = new GameRepository(ctx);
        var groupRepo   = new GroupRepository(ctx);
        var sessionRepo = new VoteSessionRepository(ctx);

        // Seed host + member
        var host   = User.Create(new SteamId(76561300000000001L), "VoteHost",   "");
        var member = User.Create(new SteamId(76561300000000002L), "VoteMember", "");
        await userRepo.AddAsync(host);
        await userRepo.AddAsync(member);

        // Seed shared games
        var game1 = Game.Create(new AppId(1000), "Game Alpha", Platform.Steam);
        var game2 = Game.Create(new AppId(2000), "Game Beta",  Platform.Steam);
        await gameRepo.AddRangeAsync([game1, game2]);

        // Both users own both games
        host.AddGame(UserGame.Create(host.Id, game1.Id, 10));
        host.AddGame(UserGame.Create(host.Id, game2.Id, 20));
        member.AddGame(UserGame.Create(member.Id, game1.Id, 30));
        member.AddGame(UserGame.Create(member.Id, game2.Id, 40));

        // Create group
        var group = GameGroup.Create(host.Id, "Vote Group");
        group.AddMember(member.Id);
        await groupRepo.AddAsync(group);
        await ctx.SaveChangesAsync();

        // Act — use fresh contexts to avoid caching
        await using var ctx2 = fixture.CreateContext();
        var svc2 = BuildVoteService(
            new VoteSessionRepository(ctx2),
            new GroupRepository(ctx2),
            new UserRepository(ctx2),
            new GameRepository(ctx2));

        var sessionDto = await svc2.StartSessionAsync(group.Id, host.Id);
        Assert.Equal(VoteSessionStatus.Active, sessionDto.Status);
        Assert.Equal(2, sessionDto.CandidateGameIds.Length);

        // Both users vote for game1
        await using var ctx3 = fixture.CreateContext();
        var svc3 = BuildVoteService(
            new VoteSessionRepository(ctx3),
            new GroupRepository(ctx3),
            new UserRepository(ctx3),
            new GameRepository(ctx3));

        var tally1 = await svc3.CastVoteAsync(sessionDto.Id, host.Id,   game1.Id);
        var tally2 = await svc3.CastVoteAsync(sessionDto.Id, member.Id, game1.Id);

        Assert.Equal(2, tally2.TotalVotes);
        Assert.Equal(2, tally2.Tally[game1.Id]);

        // Host closes the session
        await using var ctx4 = fixture.CreateContext();
        var svc4 = BuildVoteService(
            new VoteSessionRepository(ctx4),
            new GroupRepository(ctx4),
            new UserRepository(ctx4),
            new GameRepository(ctx4));

        var closed = await svc4.CloseSessionAsync(sessionDto.Id, group.Id, host.Id);

        Assert.Equal(VoteSessionStatus.Closed, closed.Status);
        Assert.Equal(game1.Id, closed.WinnerGameId);

        // Verify winner is persisted
        await using var ctx5 = fixture.CreateContext();
        var persisted = ctx5.VoteSessions.First(s => s.Id == sessionDto.Id);
        Assert.Equal(VoteSessionStatus.Closed, persisted.Status);
        Assert.Equal(game1.Id, persisted.WinnerGameId);
        Assert.NotNull(persisted.ClosedAt);
    }

    [Fact]
    public async Task StartSession_WhenAlreadyActive_ThrowsDomainException()
    {
        await using var ctx  = fixture.CreateContext();
        var userRepo    = new UserRepository(ctx);
        var gameRepo    = new GameRepository(ctx);
        var groupRepo   = new GroupRepository(ctx);

        var host   = User.Create(new SteamId(76561300000000003L), "Host2", "");
        var game   = Game.Create(new AppId(3000), "Game Gamma", Platform.Steam);
        await userRepo.AddAsync(host);
        await gameRepo.AddRangeAsync([game]);
        host.AddGame(UserGame.Create(host.Id, game.Id, 5));

        var group = GameGroup.Create(host.Id, "Active Session Group");
        await groupRepo.AddAsync(group);
        await ctx.SaveChangesAsync();

        await using var ctx2 = fixture.CreateContext();
        var svc2 = BuildVoteService(
            new VoteSessionRepository(ctx2),
            new GroupRepository(ctx2),
            new UserRepository(ctx2),
            new GameRepository(ctx2));

        await svc2.StartSessionAsync(group.Id, host.Id);

        await using var ctx3 = fixture.CreateContext();
        var svc3 = BuildVoteService(
            new VoteSessionRepository(ctx3),
            new GroupRepository(ctx3),
            new UserRepository(ctx3),
            new GameRepository(ctx3));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            svc3.StartSessionAsync(group.Id, host.Id));
    }
}
