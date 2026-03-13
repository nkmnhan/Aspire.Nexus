namespace Aspire.Nexus.Configuration;

/// <summary>
/// Docker-compose infrastructure settings.
/// When configured, Aspire.Nexus starts infrastructure containers via docker-compose
/// before launching .NET/Client services.
/// </summary>
public sealed class InfrastructureConfig
{
    /// <summary>Path to docker-compose file (e.g. "../../docker-compose.yml").</summary>
    public string? DockerComposePath { get; init; }

    /// <summary>Docker Compose project name. Containers are grouped under this name in Docker Desktop.</summary>
    public string? DockerComposeProject { get; init; }

    /// <summary>Docker network name. Created automatically if it doesn't exist.</summary>
    public string? Network { get; init; }

    /// <summary>
    /// Which docker-compose services to start (e.g. ["postgres", "rabbitmq"]).
    /// If empty, starts all active Container-type services.
    /// </summary>
    public List<string> DockerComposeServices { get; init; } = [];

    public bool IsDockerComposeManaged =>
        !string.IsNullOrEmpty(DockerComposePath) && !string.IsNullOrEmpty(DockerComposeProject);
}
