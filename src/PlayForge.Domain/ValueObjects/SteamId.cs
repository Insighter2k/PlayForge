using PlayForge.Domain.Exceptions;

namespace PlayForge.Domain.ValueObjects;

public sealed record SteamId
{
    public long Value { get; }

    public SteamId(long value)
    {
        if (value <= 0)
            throw new DomainException($"Invalid SteamId: {value}");
        Value = value;
    }

    public static SteamId Parse(string raw)
    {
        if (!long.TryParse(raw, out var value))
            throw new DomainException($"SteamId must be numeric, got: '{raw}'");
        return new SteamId(value);
    }

    public override string ToString() => Value.ToString();
}
