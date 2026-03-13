namespace Aspire.Nexus.Configuration;

/// <summary>
/// Global environment settings applied to all .NET services.
/// Use <see cref="ExtraVariables"/> for settings that apply to all service types.
/// </summary>
public sealed class ServiceEnvironmentConfig
{
    /// <summary>Sets Logging__LogLevel__Default for .NET services (default: "Information").</summary>
    public string LogLevel { get; init; } = "Information";

    /// <summary>Sets ASPNETCORE_ENVIRONMENT for .NET services (default: "Development").</summary>
    public string AspNetCoreEnvironment { get; init; } = "Development";

    /// <summary>Extra env vars applied to ALL service types (DotNet, Client, Container).</summary>
    public Dictionary<string, string> ExtraVariables { get; init; } = [];
}
