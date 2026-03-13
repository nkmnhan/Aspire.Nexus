namespace Aspire.Nexus.Configuration;

/// <summary>
/// Top-level configuration loaded from appsettings.json + user secrets
/// under the "AppHost" section.
/// </summary>
public sealed class AppHostConfig
{
    public ServiceEnvironmentConfig Environment { get; init; } = new();
    public InfrastructureConfig Infrastructure { get; init; } = new();
    public Dictionary<string, ServiceDef> Services { get; init; } = [];

    public IEnumerable<KeyValuePair<string, ServiceDef>> GetActive(ServiceType type)
        => Services.Where(kvp => kvp.Value.Active && kvp.Value.Type == type);
}

/// <summary>
/// The kinds of services Aspire.Nexus can orchestrate.
/// </summary>
public enum ServiceType
{
    /// <summary>.NET APIs and workers. Launched via <c>dotnet run</c>.</summary>
    DotNet,

    /// <summary>
    /// Generic frontend dev servers (React, Angular, Vue, PHP, Go, Ruby, etc.).
    /// Launched via a custom command with <c>AddExecutable</c>.
    /// Use <see cref="NodeJs"/> instead for npm-based projects — it provides Aspire-native lifecycle management.
    /// </summary>
    Client,

    /// <summary>Infrastructure containers (databases, message brokers). Managed by docker-compose or Aspire.</summary>
    Container,

    /// <summary>
    /// Node.js / JavaScript applications managed via npm, yarn, or pnpm scripts.
    /// Uses Aspire's native <c>AddJavaScriptApp</c> for proper lifecycle, health checks, and dashboard integration.
    /// Requires <see cref="ServiceDef.WorkingDirectory"/> (project root) and optionally <see cref="ServiceDef.ScriptName"/>.
    /// </summary>
    NodeJs,

    /// <summary>
    /// Python applications.
    /// Uses Aspire's native <c>AddPythonApp</c> for proper lifecycle, health checks, and dashboard integration.
    /// Requires <see cref="ServiceDef.WorkingDirectory"/> (project root) and <see cref="ServiceDef.ScriptPath"/> (entry point).
    /// </summary>
    Python
}
