using PlayForge.Domain.Exceptions;

namespace PlayForge.Domain.ValueObjects;

public sealed record AppId
{
    public long Value { get; }

    public AppId(long value)
    {
        if (value <= 0)
            throw new DomainException($"Invalid AppId: {value}");
        Value = value;
    }

    public override string ToString() => Value.ToString();
}
