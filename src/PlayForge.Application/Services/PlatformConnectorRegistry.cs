using PlayForge.Application.Interfaces;
using PlayForge.Domain.Enums;
using PlayForge.Domain.Interfaces;

namespace PlayForge.Application.Services;

public class PlatformConnectorRegistry : IPlatformConnectorRegistry
{
    private readonly Dictionary<Platform, IPlatformConnector> _connectors;

    public PlatformConnectorRegistry(IEnumerable<IPlatformConnector> connectors)
    {
        _connectors = connectors.ToDictionary(c => c.Platform);
    }

    public IPlatformConnector Get(Platform platform)
    {
        if (!_connectors.TryGetValue(platform, out var connector))
            throw new InvalidOperationException($"No connector registered for platform '{platform}'.");
        return connector;
    }

    public IEnumerable<IPlatformConnector> GetAll() => _connectors.Values;
}
