using Aspire.Hosting;

namespace Aspire.Nexus.Handlers;

/// <summary>
/// Strategy interface — each framework type implements its own validation,
/// registration, pre-run, and rebuild logic in a single class.
/// </summary>
public interface IServiceHandler
{
    ServiceType Type { get; }

    /// <summary>Validate config at startup. Append errors to the list.</summary>
    void Validate(string serviceName, ServiceDef def, List<string> errors);

    /// <summary>Register the service with the Aspire builder.</summary>
    void Register(IDistributedApplicationBuilder builder, string serviceName,
                  ServiceDef def, RegistrationContext context);

    /// <summary>Whether this handler needs a pre-run phase (build/install before Aspire starts).</summary>
    bool HasPreRunPhase { get; }

    /// <summary>
    /// Check if a service is already prepared and can skip pre-run.
    /// Return true to skip PreRunBatchAsync for this service.
    /// </summary>
    Task<bool> IsServiceReadyAsync(string serviceName, ServiceDef def, CancellationToken ct);

    /// <summary>Run pre-startup tasks for all services of this type (build, install).</summary>
    Task PreRunBatchAsync(IReadOnlyDictionary<string, ServiceDef> services,
                          string buildConfiguration, CancellationToken ct);

    /// <summary>Whether this handler supports rebuild-on-restart from the Aspire dashboard.</summary>
    bool HasRebuildOnRestart { get; }

    /// <summary>Rebuild/reinstall a single service when restarted from the dashboard.</summary>
    Task RebuildAsync(string serviceName, ServiceDef def,
                      string buildConfiguration, CancellationToken ct);
}

/// <summary>
/// Shared state passed to <see cref="IServiceHandler.Register"/>.
/// </summary>
public sealed record RegistrationContext(
    ServiceEnvironmentConfig Environment,
    string? BasePath,
    string BuildConfiguration = "Debug");
