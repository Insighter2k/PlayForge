using PlayForge.Domain.Enums;
using PlayForge.Domain.Interfaces;

namespace PlayForge.Application.Interfaces;

public interface IPlatformConnectorRegistry
{
    IPlatformConnector Get(Platform platform);
    IEnumerable<IPlatformConnector> GetAll();
}
