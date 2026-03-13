using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Nexus;

public static class ServiceRegistrar
{
    public static void RegisterServices(
        IDistributedApplicationBuilder builder,
        AppHostConfig config)
    {
        var active = config.Services
            .Where(kvp => kvp.Value.Active)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (active.Count == 0)
        {
            PrintAvailableServices(config.Services);
            return;
        }

        var containerServices = active.Where(kvp => kvp.Value.Type == ServiceType.Container).ToDictionary();
        var dotnetServices = active.Where(kvp => kvp.Value.Type == ServiceType.DotNet).ToDictionary();
        var clientServices = active.Where(kvp => kvp.Value.Type == ServiceType.Client).ToDictionary();

        // Infrastructure first, then backends, then frontends
        foreach (var (name, def) in containerServices)
            RegisterContainerService(builder, name, def);

        foreach (var (name, def) in dotnetServices)
            RegisterDotNetService(builder, name, def, config.Environment);

        foreach (var (name, def) in clientServices)
            RegisterClientService(builder, name, def);

        SubscribeRebuildOnRestart(builder, dotnetServices);
    }

    private static void RegisterDotNetService(
        IDistributedApplicationBuilder builder,
        string serviceName,
        ServiceDef def,
        ServiceEnvironmentConfig env)
    {
        var url = $"{def.Scheme}://localhost:{def.Port}";

        var project = builder.AddProject(serviceName, def.ProjectPath!, options =>
        {
            options.ExcludeLaunchProfile = true;
        });

        if (def.IsHttps)
            project.WithHttpsEndpoint(port: def.Port, targetPort: def.Port, name: "https", isProxied: false);
        else
            project.WithHttpEndpoint(port: def.Port, targetPort: def.Port, name: "http", isProxied: false);

        project.WithEnvironment("ASPNETCORE_URLS", url)
               .WithEnvironment("ASPNETCORE_ENVIRONMENT", env.AspNetCoreEnvironment)
               .WithEnvironment("Logging__LogLevel__Default", env.LogLevel);

        if (def.IsHttps)
        {
            project.WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", def.Certificate!.Path)
                   .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", def.Certificate.Password);
        }

        foreach (var (key, value) in env.ExtraVariables)
            project.WithEnvironment(key, value);

        foreach (var (key, value) in def.EnvironmentVariables)
            project.WithEnvironment(key, value);
    }

    private static void RegisterContainerService(
        IDistributedApplicationBuilder builder,
        string containerName,
        ServiceDef def)
    {
        BuildLogger.Info($"[CONTAINER] {containerName}: {def.Image}:{def.Tag} -> localhost:{def.Port}");

        var container = builder.AddContainer(containerName, def.Image!, def.Tag);

        // Primary port: Port = host port, TargetPort = container port (defaults to same as Port)
        var targetPort = def.TargetPort ?? def.Port;
        container.WithEndpoint(port: def.Port, targetPort: targetPort, name: containerName, isProxied: false);

        // Additional ports (e.g. RabbitMQ management UI on 15672)
        foreach (var mapping in def.AdditionalPorts)
            container.WithEndpoint(port: mapping.Port, targetPort: mapping.TargetPort, name: $"{containerName}-{mapping.Port}", isProxied: false);

        foreach (var (key, value) in def.EnvironmentVariables)
            container.WithEnvironment(key, value);

        foreach (var (hostPath, containerPath) in def.Volumes)
            container.WithBindMount(hostPath, containerPath);

        foreach (var arg in def.Args)
            container.WithArgs(arg.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static void RegisterClientService(
        IDistributedApplicationBuilder builder,
        string clientName,
        ServiceDef def)
    {
        var devCmd = def.ResolvedDevCommand;
        var label = FormatLabel(devCmd, def.DevCommand);
        BuildLogger.Info($"[CLIENT] {clientName}: {label} -> http://localhost:{def.Port}");

        var (command, args) = ProcessRunner.ParseCommand(devCmd);

        var app = builder.AddExecutable(clientName, command, def.WorkingDirectory!, args.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .WithHttpEndpoint(port: def.Port, targetPort: def.Port, name: "http", isProxied: false);

        foreach (var (key, value) in def.EnvironmentVariables)
            app.WithEnvironment(key, value);
    }

    private static void SubscribeRebuildOnRestart(
        IDistributedApplicationBuilder builder,
        Dictionary<string, ServiceDef> dotnetServices)
    {
        var startedOnce = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        builder.Eventing.Subscribe<BeforeResourceStartedEvent>(async (@event, ct) =>
        {
            var resourceName = @event.Resource.Name;
            if (!dotnetServices.ContainsKey(resourceName))
                return;

            if (startedOnce.Add(resourceName))
            {
                BuildLogger.Info($"[SKIP BUILD] {resourceName} — already pre-built.");
                return;
            }

            var def = dotnetServices[resourceName];
            BuildLogger.Info($"[BUILD] Rebuilding {resourceName}...");

            var success = await ProcessRunner.RunAsync("dotnet", $"build \"{def.ProjectPath}\" -c Debug", ct: ct);
            if (!success)
                throw new InvalidOperationException(
                    $"Build failed for {resourceName}. Fix build errors before restarting.");

            BuildLogger.Success($"[BUILD OK] {resourceName}");
        });
    }

    internal static string FormatLabel(string resolvedCmd, string? userCmd)
        => userCmd is not null ? $"{resolvedCmd} (custom)" : $"{resolvedCmd} (default)";

    private static void PrintAvailableServices(Dictionary<string, ServiceDef> services)
    {
        BuildLogger.Warn("No active services. Set \"Active\": true for services to debug.");
        BuildLogger.Warn("Available:");

        var grouped = services
            .GroupBy(kvp => kvp.Value.Group ?? "")
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            if (!string.IsNullOrEmpty(group.Key))
                BuildLogger.Warn($"  [{group.Key}]");

            var indent = string.IsNullOrEmpty(group.Key) ? "  " : "    ";
            foreach (var (name, def) in group)
            {
                var type = def.Type.ToString().ToLowerInvariant();
                BuildLogger.Warn($"{indent}\"{name}\" ({type}, http://localhost:{def.Port})");
            }
        }
    }
}
