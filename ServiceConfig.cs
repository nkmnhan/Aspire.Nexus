namespace Aspire.Nexus;

public sealed class AppHostConfig
{
    public ServiceEnvironmentConfig Environment { get; init; } = new();
    public Dictionary<string, ServiceDef> Services { get; init; } = [];
}

public sealed class ServiceDef
{
    public required string ProjectPath { get; init; }
    public string? SolutionPath { get; init; }
    public required int Port { get; init; }
    public bool Active { get; init; }
    public CertificateConfig? Certificate { get; init; }

    /// <summary>
    /// Optional per-service environment variables that override the global ones.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; init; } = [];

    public bool IsHttps => Certificate is not null;
    public string Scheme => IsHttps ? "https" : "http";
}

public sealed class CertificateConfig
{
    public required string Path { get; init; }

    /// <summary>
    /// Certificate password. Store this in user secrets instead of appsettings.json:
    /// dotnet user-secrets set "AppHost:Services:my-api:Certificate:Password" "my-secret"
    /// </summary>
    public required string Password { get; init; }
}

public sealed class ServiceEnvironmentConfig
{
    public string LogLevel { get; init; } = "Information";
    public string AspNetCoreEnvironment { get; init; } = "Development";
    public Dictionary<string, string> ExtraVariables { get; init; } = [];
}
