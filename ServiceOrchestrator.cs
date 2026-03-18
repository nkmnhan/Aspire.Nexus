using System.Collections.Concurrent;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Nexus;

/// <summary>
/// Orchestrates the full lifecycle: validate → pre-run → register → rebuild-on-restart.
/// Delegates all type-specific logic to <see cref="IServiceHandler"/> implementations.
/// </summary>
public static class ServiceOrchestrator
{
    private static readonly IServiceHandler[] Handlers =
    [
        new DotNetHandler(),
        new NodeJsHandler(),
        new PythonHandler(),
        new ClientHandler(),
        new ContainerHandler()
    ];

    private static readonly Dictionary<ServiceType, IServiceHandler> HandlerMap =
        Handlers.ToDictionary(handler => handler.Type);

    /// <summary>
    /// Validates all active services. Throws <see cref="InvalidOperationException"/>
    /// with a human-readable summary if any service is misconfigured.
    /// </summary>
    public static void Validate(AppHostConfig config)
    {
        var errors = new List<string>();

        foreach (var (name, def) in config.Services.Where(kvp => kvp.Value.Active))
        {
            if (HandlerMap.TryGetValue(def.Type, out var handler))
                handler.Validate(name, def, errors);

            ValidateCommon(name, def, errors);
        }

        if (errors.Count > 0)
        {
            var message = string.Join(Environment.NewLine, errors.Select(error => $"  - {error}"));
            throw new InvalidOperationException(
                $"Configuration errors found:{Environment.NewLine}{message}{Environment.NewLine}" +
                "Fix these in your user secrets (setup-secrets.cmd) and try again.");
        }
    }

    /// <summary>
    /// Runs pre-startup phases: infrastructure (docker-compose) + per-handler pre-run tasks.
    /// </summary>
    public static async Task PreRunAsync(AppHostConfig config, CancellationToken ct)
    {
        await StartInfrastructureAsync(config, ct);

        foreach (var handler in Handlers.Where(handler => handler.HasPreRunPhase))
        {
            var allServices = config.GetActive(handler.Type).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            if (allServices.Count == 0)
                continue;

            // Filter out services that are already prepared (polymorphic check)
            var notReady = new Dictionary<string, ServiceDef>();
            foreach (var (name, def) in allServices)
            {
                if (!await handler.IsServiceReadyAsync(name, def, ct))
                    notReady.Add(name, def);
            }

            if (notReady.Count > 0)
                await handler.PreRunBatchAsync(notReady, config.BuildConfiguration, ct);
        }
    }

    /// <summary>
    /// Registers all active services with the Aspire builder and subscribes to rebuild-on-restart events.
    /// </summary>
    public static void RegisterAll(
        IDistributedApplicationBuilder builder,
        AppHostConfig config,
        string? basePath = null)
    {
        var active = config.Services
            .Where(kvp => kvp.Value.Active)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (active.Count == 0)
        {
            PrintAvailableServices(config.Services);
            return;
        }

        var context = new RegistrationContext(config.Environment, basePath, config.BuildConfiguration);

        // Container services: docker-compose manages them, or register as Aspire containers
        var containerServices = active.Where(kvp => kvp.Value.Type == ServiceType.Container).ToDictionary();
        if (config.Infrastructure.IsDockerComposeManaged)
        {
            if (containerServices.Count > 0)
                BuildLogger.Info($"[CONTAINER] {containerServices.Count} container(s) managed by docker-compose ({config.Infrastructure.DockerComposeProject}).");
        }
        else
        {
            var containerHandler = HandlerMap[ServiceType.Container];
            foreach (var (name, def) in containerServices)
                containerHandler.Register(builder, name, def, context);
        }

        // Register all non-container services via their handlers
        foreach (var (name, def) in active.Where(kvp => kvp.Value.Type != ServiceType.Container))
        {
            if (HandlerMap.TryGetValue(def.Type, out var handler))
                handler.Register(builder, name, def, context);
        }

        SubscribeRebuildOnRestart(builder, active, config.BuildConfiguration);
    }

    // ── Private helpers ──────────────────────────────────

    private static async Task StartInfrastructureAsync(AppHostConfig config, CancellationToken ct)
    {
        var infra = config.Infrastructure;
        if (!infra.IsDockerComposeManaged)
        {
            BuildLogger.Warn("[INFRA] No docker-compose configuration found. Skipping infrastructure startup.");
            return;
        }

        // Ensure Docker network exists (ignore failure — network may already exist)
        if (!string.IsNullOrEmpty(infra.Network))
        {
            BuildLogger.Info($"[INFRA] Ensuring Docker network: {infra.Network}");
            await ProcessRunner.RunAsync("docker", $"network create {infra.Network}", silent: true, ct: ct);
        }

        // Determine which services to start
        var services = infra.DockerComposeServices.Count > 0
            ? infra.DockerComposeServices
            : config.GetActive(ServiceType.Container).Select(kvp => kvp.Key).ToList();

        if (services.Count == 0)
        {
            BuildLogger.Info("[INFRA] No active infrastructure services to start.");
            return;
        }

        // Check which services are already running and skip them
        try
        {
            var psArgs = $"-p {infra.DockerComposeProject} -f \"{infra.DockerComposePath}\" ps --format \"{{{{.Service}}}} {{{{.State}}}}\"";
            var output = await ProcessRunner.RunCaptureAsync("docker", $"compose {psArgs}", ct: ct);
            if (output is not null)
            {
                var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && parts[1].Contains("running", StringComparison.OrdinalIgnoreCase))
                        running.Add(parts[0]);
                }

                var alreadyRunning = services.Where(s => running.Contains(s)).ToList();
                if (alreadyRunning.Count > 0)
                    BuildLogger.Info($"[INFRA] Already running (skipping): {string.Join(" ", alreadyRunning)}");

                services = services.Where(s => !running.Contains(s)).ToList();
                if (services.Count == 0)
                {
                    BuildLogger.Success("[INFRA OK] All infrastructure services already running.");
                    return;
                }
            }
        }
        catch
        {
            // Graceful fallback — proceed with starting all services
        }

        var serviceList = string.Join(" ", services);
        BuildLogger.Info($"[INFRA] Starting: {serviceList}");
        BuildLogger.Info($"[INFRA] Project: {infra.DockerComposeProject}");

        var composeArgs = $"-p {infra.DockerComposeProject} -f \"{infra.DockerComposePath}\" up --remove-orphans -d {serviceList}";
        var success = await ProcessRunner.RunAsync("docker", $"compose {composeArgs}", ct: ct);

        if (!success)
        {
            // Fallback to docker-compose (v1)
            BuildLogger.Warn("[INFRA] docker compose failed, trying docker-compose (v1)...");
            success = await ProcessRunner.RunAsync("docker-compose", composeArgs, ct: ct);
        }

        if (!success)
            throw new InvalidOperationException(
                "Infrastructure startup failed. Check Docker is running and docker-compose file is valid.");

        BuildLogger.Success($"[INFRA OK] Infrastructure started under '{infra.DockerComposeProject}'.");
    }

    /// <summary>
    /// Subscribes to <see cref="BeforeResourceStartedEvent"/> so that when a service is
    /// restarted from the Aspire dashboard, it rebuilds/reinstalls before launching.
    /// </summary>
    private static void SubscribeRebuildOnRestart(
        IDistributedApplicationBuilder builder,
        Dictionary<string, ServiceDef> services,
        string buildConfiguration)
    {
        var rebuildableServices = services
            .Where(kvp => HandlerMap.TryGetValue(kvp.Value.Type, out var handler) && handler.HasRebuildOnRestart)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (rebuildableServices.Count == 0)
            return;

        var startedOnce = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        builder.Eventing.Subscribe<BeforeResourceStartedEvent>(async (@event, ct) =>
        {
            var resourceName = @event.Resource.Name;
            if (!rebuildableServices.TryGetValue(resourceName, out var def))
                return;

            // Skip the first start — dependencies were already installed during pre-run phases
            if (startedOnce.TryAdd(resourceName, true))
            {
                BuildLogger.Info($"[SKIP REBUILD] {resourceName} — already built/installed during startup.");
                return;
            }

            if (HandlerMap.TryGetValue(def.Type, out var handler))
                await handler.RebuildAsync(resourceName, def, buildConfiguration, ct);
        });
    }

    private static void PrintAvailableServices(Dictionary<string, ServiceDef> services)
    {
        BuildLogger.Warn("No active services. Set \"Active\": true for services to debug.");
        BuildLogger.Warn("Available:");

        var grouped = services
            .GroupBy(kvp => kvp.Value.Group ?? "")
            .OrderBy(group => group.Key);

        foreach (var group in grouped)
        {
            if (!string.IsNullOrEmpty(group.Key))
                BuildLogger.Warn($"  [{group.Key}]");

            var indent = string.IsNullOrEmpty(group.Key) ? "  " : "    ";
            foreach (var (name, def) in group)
            {
                var type = def.Type.ToString().ToLowerInvariant();
                var portDisplay = def.Port is > 0 ? $"{def.Scheme}://localhost:{def.Port}" : "no endpoint";
                BuildLogger.Warn($"{indent}\"{name}\" ({type}, {portDisplay})");
            }
        }
    }

    private static void ValidateCommon(string name, ServiceDef def, List<string> errors)
    {
        if (def.Port is < 0 or > 65535)
            errors.Add($"\"{name}\": \"Port\" must be between 1 and 65535 (got {def.Port}).");
    }
}
