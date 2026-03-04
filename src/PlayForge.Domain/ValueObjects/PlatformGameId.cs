using PlayForge.Domain.Enums;
using PlayForge.Domain.Exceptions;

namespace PlayForge.Domain.ValueObjects;

public sealed record PlatformGameId
{
    public Platform Platform { get; }
    public string   ExternalId { get; }

    public PlatformGameId(Platform platform, string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new DomainException("PlatformGameId.ExternalId cannot be empty.");
        Platform   = platform;
        ExternalId = externalId;
    }

    public override string ToString() => $"{Platform}:{ExternalId}";
}
