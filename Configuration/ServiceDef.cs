namespace Aspire.Nexus.Configuration;

/// <summary>
/// Defines a single service. Properties are grouped by service type.
/// Only set the properties for your service's <see cref="Type"/> — the rest are ignored.
/// </summary>
public sealed class ServiceDef
{
    public static class Defaults
    {
        public const string NpmInstallCommand = "npm install";
        public const string PipInstallCommand = "pip install -r requirements.txt";
        public const string DevCommand = "npm run dev";
        public const string NpmScriptName = "start";
        public const string PackageManagerName = "npm";
    }

    // ── Common (all types) ───────────────────────────────

    /// <summary>Service type: DotNet, Client, Container, NodeJs, or Python.</summary>
    public required ServiceType Type { get; init; }

    /// <summary>Host port. Null for background workers without endpoints.</summary>
    public int? Port { get; init; }

    /// <summary>Set to true to include this service when Aspire starts.</summary>
    public bool Active { get; init; }

    /// <summary>Display group in console output (e.g. "Backend", "Frontend").</summary>
    public string? Group { get; init; }

    /// <summary>Key-value environment variables injected into the service.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; init; } = [];

    /// <summary>
    /// Mark as HTTPS even without a Kestrel certificate.
    /// Use for client apps (e.g. Vite) where the dev server handles TLS itself.
    /// </summary>
    public bool Https { get; init; }

    public bool IsHttps => Certificate is not null || Https;
    public string Scheme => IsHttps ? "https" : "http";

    // ── DotNet only ──────────────────────────────────────

    /// <summary>Path to .csproj file. Required for DotNet services.</summary>
    public string? ProjectPath { get; init; }

    /// <summary>Path to .sln file. When set, the solution is built instead of individual projects.</summary>
    public string? SolutionPath { get; init; }

    /// <summary>HTTPS certificate config (PEM or PFX). Enables HTTPS for this service.</summary>
    public CertificateConfig? Certificate { get; init; }

    // ── Client / NodeJs / Python (shared) ────────────────

    /// <summary>
    /// Path to the project root directory.
    /// Required for Client, NodeJs, and Python services.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Custom dependency install command.
    /// <list type="bullet">
    ///   <item><b>Client / NodeJs</b>: default "npm install"</item>
    ///   <item><b>Python</b>: default "pip install -r requirements.txt"</item>
    /// </list>
    /// Set to empty string to skip the install phase.
    /// </summary>
    public string? InstallCommand { get; init; }

    // ── Client only ──────────────────────────────────────

    /// <summary>Custom dev server command (default: "npm run dev"). Client type only.</summary>
    public string? DevCommand { get; init; }

    public string ResolvedDevCommand => DevCommand ?? Defaults.DevCommand;

    // ── NodeJs only ──────────────────────────────────────

    /// <summary>
    /// Package manager script name to run (e.g. "dev", "start", "serve").
    /// Default: "start". Aspire runs this as <c>npm run {ScriptName}</c> (or yarn/pnpm).
    /// </summary>
    public string? ScriptName { get; init; }

    /// <summary>
    /// JavaScript package manager: "npm" (default), "yarn", or "pnpm".
    /// Aspire uses this to install dependencies and run scripts.
    /// </summary>
    public string? PackageManager { get; init; }

    public string ResolvedScriptName => ScriptName ?? Defaults.NpmScriptName;
    public string ResolvedPackageManager => PackageManager ?? Defaults.PackageManagerName;

    // ── Python only ──────────────────────────────────────

    /// <summary>
    /// Path to the Python entry-point script, relative to <see cref="WorkingDirectory"/>.
    /// Required for Python services. Example: "app.py", "src/main.py".
    /// </summary>
    public string? ScriptPath { get; init; }

    /// <summary>
    /// Path to the Python virtual environment directory, relative to <see cref="WorkingDirectory"/>.
    /// Example: ".venv", "venv". When set, Aspire activates this venv before running the script.
    /// </summary>
    public string? VirtualEnvironmentPath { get; init; }

    // ── Container only ───────────────────────────────────

    /// <summary>Docker image name. Required for Container services.</summary>
    public string? Image { get; init; }

    /// <summary>Image tag (default: "latest").</summary>
    public string Tag { get; init; } = "latest";

    /// <summary>Container port if different from host port.</summary>
    public int? TargetPort { get; init; }

    /// <summary>Extra port mappings (e.g. RabbitMQ management UI on 15672).</summary>
    public List<PortMapping> AdditionalPorts { get; init; } = [];

    /// <summary>Volume mounts: named volumes or bind mounts.</summary>
    public Dictionary<string, string> Volumes { get; init; } = [];

    /// <summary>Memory limit in MB (not yet implemented).</summary>
    public int? MemoryLimitMB { get; init; }

    /// <summary>Extra command arguments passed to the container.</summary>
    public List<string> Args { get; init; } = [];
}
