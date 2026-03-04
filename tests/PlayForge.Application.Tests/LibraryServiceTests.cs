using FluentAssertions;
using Moq;
using PlayForge.Application.Interfaces;
using PlayForge.Application.Services;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Enums;
using PlayForge.Domain.Interfaces;
using PlayForge.Domain.ValueObjects;

namespace PlayForge.Application.Tests;

public class LibraryServiceTests
{
    private static LibraryService Build(
        IUserRepository            users,
        IGameRepository            games,
        IPlatformConnectorRegistry registry) =>
        new(users, games, registry);

    // ── Not stale — connector must not be called ──────────────────────────────

    [Fact]
    public async Task SyncAndGetLibrary_WhenLibraryFresh_DoesNotCallConnector()
    {
        // Arrange
        var game = Game.Create(new AppId(570), "Dota 2", Platform.Steam);
        var user = User.Create(new SteamId(76561198000000001L), "TestUser", "");
        user.AddGame(UserGame.Create(user.Id, game.Id, 120));
        // User.Create sets LastSyncedAt = UtcNow; game was added → not stale

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.FindByIdAsync(user.Id, default)).ReturnsAsync(user);

        var gameRepo = new Mock<IGameRepository>();
        gameRepo.Setup(r => r.FindByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
                .ReturnsAsync(new Dictionary<Guid, Game> { [game.Id] = game });

        var registry = new Mock<IPlatformConnectorRegistry>();

        var svc = Build(userRepo.Object, gameRepo.Object, registry.Object);

        // Act
        var result = await svc.SyncAndGetLibraryAsync(user.Id);

        // Assert
        registry.Verify(r => r.Get(It.IsAny<Platform>()), Times.Never,
            "connector should not be invoked for a fresh library");
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Dota 2");
        result[0].PlaytimeMinutes.Should().Be(120);
    }

    // ── Stale (no games) — connector must be called ───────────────────────────

    [Fact]
    public async Task SyncAndGetLibrary_WhenUserHasNoGames_CallsConnector()
    {
        // Arrange — fresh user, zero games → isStale = true
        var user = User.Create(new SteamId(76561198000000002L), "EmptyUser", "");

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.FindByIdAsync(user.Id, default)).ReturnsAsync(user);
        userRepo.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);

        var gameRepo = new Mock<IGameRepository>();
        gameRepo.Setup(r => r.FindByAppIdsAsync(It.IsAny<IEnumerable<long>>(), default))
                .ReturnsAsync(new Dictionary<long, Game>());
        gameRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Game>>(), default))
                .Returns(Task.CompletedTask);
        gameRepo.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);

        var connector = new Mock<IPlatformConnector>();
        connector.Setup(c => c.Platform).Returns(Platform.Steam);
        connector.Setup(c => c.GetOwnedGamesAsync(It.IsAny<string>(), It.IsAny<bool>(), default))
                 .ReturnsAsync(Array.Empty<PlatformGame>());

        var registry = new Mock<IPlatformConnectorRegistry>();
        registry.Setup(r => r.Get(Platform.Steam)).Returns(connector.Object);

        var svc = Build(userRepo.Object, gameRepo.Object, registry.Object);

        // Act
        await svc.SyncAndGetLibraryAsync(user.Id);

        // Assert
        registry.Verify(r => r.Get(Platform.Steam), Times.Once,
            "connector should be called when library is stale");
    }

    // ── Result ordering ───────────────────────────────────────────────────────

    [Fact]
    public async Task SyncAndGetLibrary_ResultsOrderedByPlaytimeDescending()
    {
        var gameA = Game.Create(new AppId(10), "Game A", Platform.Steam);
        var gameB = Game.Create(new AppId(20), "Game B", Platform.Steam);
        var user  = User.Create(new SteamId(76561198000000003L), "TestUser", "");
        user.AddGame(UserGame.Create(user.Id, gameA.Id, 60));
        user.AddGame(UserGame.Create(user.Id, gameB.Id, 300));

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.FindByIdAsync(user.Id, default)).ReturnsAsync(user);

        var gameRepo = new Mock<IGameRepository>();
        gameRepo.Setup(r => r.FindByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default))
                .ReturnsAsync(new Dictionary<Guid, Game>
                {
                    [gameA.Id] = gameA,
                    [gameB.Id] = gameB,
                });

        var svc = Build(userRepo.Object, gameRepo.Object, new Mock<IPlatformConnectorRegistry>().Object);

        var result = await svc.SyncAndGetLibraryAsync(user.Id);

        result[0].Name.Should().Be("Game B"); // 300 min first
        result[1].Name.Should().Be("Game A"); // 60 min second
    }
}
