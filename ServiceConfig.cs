namespace Aspire.Nexus;

public sealed class AppHostConfig
{
    public ServiceEnvironmentConfig Environment { get; init; } = new();
    public Dictionary<string, ServiceDef> Services { get; init; } = [];

    public IEnumerable<KeyValuePair<string, ServiceDef>> GetActive(ServiceType type)
        => Services.Where(kvp => kvp.Value.Active && kvp.Value.Type == type);
}

public enum ServiceType
{
    DotNet,
    Client,
    Container
}

public sealed class ServiceDef
{
    public static class Defaults
    {
        public const string InstallCommand = "npm install";
        public const string DevCommand = "npm run dev";
    }

    // ── Common ──────────────────────────────────────────
    public required ServiceType Type { get; init; }
    public required int Port { get; init; }
    public bool Active { get; init; }
    public string? Group { get; init; }
    public Dictionary<string, string> EnvironmentVariables { get; init; } = [];

    // ── DotNet ──────────────────────────────────────────
    public string? ProjectPath { get; init; }
    public string? SolutionPath { get; init; }
    public CertificateConfig? Certificate { get; init; }

    public bool IsHttps => Certificate is not null;
    public string Scheme => IsHttps ? "https" : "http";

    // ── Client ──────────────────────────────────────────
    public string? WorkingDirectory { get; init; }
    public string? InstallCommand { get; init; }
    public string? DevCommand { get; init; }

    public string ResolvedInstallCommand => InstallCommand ?? Defaults.InstallCommand;
    public string ResolvedDevCommand => DevCommand ?? Defaults.DevCommand;

    // ── Container ───────────────────────────────────────
    public string? Image { get; init; }
    public string Tag { get; init; } = "latest";
    public int? TargetPort { get; init; }
    public List<PortMapping> AdditionalPorts { get; init; } = [];
    public Dictionary<string, string> Volumes { get; init; } = [];
    public int? MemoryLimitMB { get; init; }
    public List<string> Args { get; init; } = [];
}

public sealed class PortMapping
{
    public required int Port { get; init; }
    public required int TargetPort { get; init; }
}

public sealed class CertificateConfig
{
    public required string Path { get; init; }
    public required string Password { get; init; }
}

public sealed class ServiceEnvironmentConfig
{
    public string LogLevel { get; init; } = "Information";
    public string AspNetCoreEnvironment { get; init; } = "Development";
    public Dictionary<string, string> ExtraVariables { get; init; } = [];
}
