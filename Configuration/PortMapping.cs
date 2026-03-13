namespace Aspire.Nexus.Configuration;

/// <summary>
/// Port mapping for container services (e.g. { Port: 15672, TargetPort: 15672 }).
/// </summary>
public sealed class PortMapping
{
    public required int Port { get; init; }
    public required int TargetPort { get; init; }
}
