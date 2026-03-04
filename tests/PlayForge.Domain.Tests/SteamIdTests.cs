using FluentAssertions;
using PlayForge.Domain.Exceptions;
using PlayForge.Domain.ValueObjects;

namespace PlayForge.Domain.Tests;

public class SteamIdTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Constructor_ZeroOrNegative_ThrowsDomainException(long value)
    {
        var act = () => new SteamId(value);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Constructor_ValidValue_SetsValue()
    {
        var steamId = new SteamId(76561198000000001L);

        steamId.Value.Should().Be(76561198000000001L);
    }

    [Theory]
    [InlineData("")]
    [InlineData("notanumber")]
    [InlineData("abc123")]
    [InlineData("-1")]
    public void Parse_InvalidInput_ThrowsDomainException(string input)
    {
        var act = () => SteamId.Parse(input);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Parse_ValidString_ReturnsCorrectSteamId()
    {
        var steamId = SteamId.Parse("76561198000000001");

        steamId.Value.Should().Be(76561198000000001L);
    }

    [Fact]
    public void ToString_ReturnsStringRepresentationOfValue()
    {
        var steamId = new SteamId(76561198000000001L);

        steamId.ToString().Should().Be("76561198000000001");
    }

    [Fact]
    public void Equality_SameSteamId_AreEqual()
    {
        var a = new SteamId(76561198000000001L);
        var b = new SteamId(76561198000000001L);

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentSteamId_AreNotEqual()
    {
        var a = new SteamId(76561198000000001L);
        var b = new SteamId(76561198000000002L);

        a.Should().NotBe(b);
    }
}
