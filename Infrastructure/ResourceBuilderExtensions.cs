using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Nexus.Infrastructure;

/// <summary>
/// Fluent extensions for <see cref="IResourceBuilder{T}"/> that encapsulate
/// HTTP/HTTPS branching so registration call sites stay clean.
/// </summary>
public static class ResourceBuilderExtensions
{
    /// <summary>
    /// Counter for assigning ephemeral ports to .NET services without an explicit port.
    /// Starts at 19000 to avoid conflicts with common services.
    /// </summary>
    private static int _ephemeralPort = 19000;

    /// <summary>
    /// Registers the appropriate endpoint based on <see cref="ServiceDef.IsHttps"/> and <see cref="ServiceDef.Port"/>.
    /// For DotNet services, also sets <c>ASPNETCORE_URLS</c>.
    /// No-op for non-DotNet services without a port.
    /// DotNet services without a port get an ephemeral port to avoid Kestrel's default port 5000 collision.
    /// </summary>
    public static IResourceBuilder<T> WithServiceEndpoint<T>(
        this IResourceBuilder<T> resource,
        ServiceDef def) where T : IResourceWithEndpoints, IResourceWithEnvironment
    {
        if (def.Port is > 0)
        {
            var port = def.Port.Value;
            resource.WithEndpoint(def.IsHttps, port);

            if (def.Type == ServiceType.DotNet)
                resource.WithEnvironment("ASPNETCORE_URLS", def.Scheme + "://localhost:" + port.ToString());
        }
        else if (def.Type == ServiceType.DotNet)
        {
            // Background workers using WebApplication.CreateBuilder still bind a port.
            // Assign an ephemeral port to prevent collisions on the Kestrel default (5000).
            var ephemeral = Interlocked.Increment(ref _ephemeralPort).ToString();
            resource.WithEnvironment("ASPNETCORE_URLS", "http://localhost:" + ephemeral);
        }

        return resource;
    }

    /// <summary>
    /// Configures Kestrel certificate environment variables (PEM or PFX).
    /// No-op when <see cref="ServiceDef.Certificate"/> is null.
    /// </summary>
    public static IResourceBuilder<T> WithCertificate<T>(
        this IResourceBuilder<T> resource,
        ServiceDef def,
        string? basePath = null) where T : IResourceWithEnvironment
    {
        if (def.Certificate is null)
            return resource;

        var certPath = ResolvePath(def.Certificate.Path, basePath);

        resource.WithEnvironment("Kestrel__Certificates__Default__Path", certPath);

        if (def.Certificate.IsPem)
            resource.WithEnvironment("Kestrel__Certificates__Default__KeyPath", ResolvePath(def.Certificate.KeyPath!, basePath));
        else
            resource.WithEnvironment("Kestrel__Certificates__Default__Password", def.Certificate.Password!);

        return resource;
    }

    /// <summary>
    /// Applies environment variables to the resource.
    /// <para>
    /// For <see cref="ServiceType.DotNet"/> services: sets <c>ASPNETCORE_ENVIRONMENT</c> and
    /// <c>Logging__LogLevel__Default</c> from the global <see cref="ServiceEnvironmentConfig"/>.
    /// </para>
    /// <para>
    /// For all service types: applies <see cref="ServiceEnvironmentConfig.ExtraVariables"/>
    /// (global) and <see cref="ServiceDef.EnvironmentVariables"/> (per-service).
    /// Per-service values override global values for the same key.
    /// </para>
    /// </summary>
    public static IResourceBuilder<T> WithAllEnvironmentVariables<T>(
        this IResourceBuilder<T> resource,
        ServiceDef def,
        ServiceEnvironmentConfig? env = null) where T : IResourceWithEnvironment
    {
        if (env is not null)
        {
            // ASP.NET-specific defaults — only for .NET services
            if (def.Type == ServiceType.DotNet)
            {
                resource.WithEnvironment("ASPNETCORE_ENVIRONMENT", env.AspNetCoreEnvironment)
                        .WithEnvironment("Logging__LogLevel__Default", env.LogLevel);

                // Use Workstation GC in dev to reduce per-service memory (~50% less).
                // Server GC is the ASP.NET default but wastes memory when running many services locally.
                if (!def.EnvironmentVariables.ContainsKey("DOTNET_gcServer"))
                    resource.WithEnvironment("DOTNET_gcServer", "0");
            }

            // Global extra variables — all service types
            foreach (var (key, value) in env.ExtraVariables)
                resource.WithEnvironment(key, value);
        }

        // Per-service variables — always applied
        foreach (var (key, value) in def.EnvironmentVariables)
            resource.WithEnvironment(key, value);

        return resource;
    }

    /// <summary>
    /// Single point that bridges Aspire's split <c>WithHttpEndpoint</c>/<c>WithHttpsEndpoint</c> API
    /// so no other method needs an HTTP/HTTPS if-else.
    /// </summary>
    private static IResourceBuilder<T> WithEndpoint<T>(
        this IResourceBuilder<T> resource,
        bool isHttps,
        int port) where T : IResourceWithEndpoints
    {
        return isHttps
            ? resource.WithHttpsEndpoint(port: port, targetPort: port, name: "https", isProxied: false)
            : resource.WithHttpEndpoint(port: port, targetPort: port, name: "http", isProxied: false);
    }

    /// <summary>
    /// Forwards the Aspire dashboard's OTLP endpoint to the resource so services with
    /// OpenTelemetry configured (e.g. Aspire ServiceDefaults) send structured logs, traces,
    /// and metrics directly to the dashboard via OTLP.
    /// <para>
    /// This is critical for <c>AddExecutable</c> resources: unlike <c>AddProject</c>, the DCP
    /// does not automatically inject OTLP environment variables for executables. Without this,
    /// the dashboard only receives stdout/stderr console output, which can break on restart.
    /// With OTLP forwarding, structured logs flow reliably regardless of process lifecycle.
    /// </para>
    /// </summary>
    public static IResourceBuilder<T> WithOtlpForwarding<T>(
        this IResourceBuilder<T> resource,
        string serviceName) where T : IResourceWithEnvironment
    {
        // Read OTLP endpoint from the AppHost's environment (set by the Aspire launcher/DCP).
        var otlpEndpoint = Environment.GetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_GRPC_ENDPOINT_URL")
                        ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        if (string.IsNullOrEmpty(otlpEndpoint))
            return resource;

        resource
            .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
            .WithEnvironment("OTEL_SERVICE_NAME", serviceName)
            .WithEnvironment("OTEL_RESOURCE_ATTRIBUTES", $"service.instance.id={serviceName}");

        // Forward OTLP auth headers if present (required when dashboard uses API key auth).
        var otlpHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
        if (!string.IsNullOrEmpty(otlpHeaders))
            resource.WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", otlpHeaders);

        // Reduce batching delay for faster log delivery to the dashboard during development.
        resource
            .WithEnvironment("OTEL_BLRP_SCHEDULE_DELAY", "1000")
            .WithEnvironment("OTEL_BSP_SCHEDULE_DELAY", "1000");

        return resource;
    }

    private static string ResolvePath(string path, string? basePath)
        => basePath is not null ? Path.GetFullPath(Path.Combine(basePath, path)) : path;
}
