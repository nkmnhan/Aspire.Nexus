using System.Diagnostics;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Nexus;

public static class ServiceRegistrar
{
    public static void RegisterServices(
        IDistributedApplicationBuilder builder,
        AppHostConfig config)
    {
        var activeServices = config.Services
            .Where(kvp => kvp.Value.Active)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (activeServices.Count == 0)
        {
            PrintAvailableServices(config.Services);
            return;
        }

        foreach (var (name, def) in activeServices)
            RegisterService(builder, name, def, config.Environment);

        SubscribeRebuildOnRestart(builder, activeServices);
    }

    public static async Task PreBuildActiveServicesAsync(
        AppHostConfig config,
        CancellationToken ct = default)
    {
        var activeServices = config.Services
            .Where(kvp => kvp.Value.Active)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (activeServices.Count == 0)
            return;

        // Build distinct solutions first
        var solutions = activeServices.Values
            .Where(d => !string.IsNullOrEmpty(d.SolutionPath))
            .Select(d => d.SolutionPath!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var sln in solutions)
        {
            BuildLogger.Info($"[PRE-BUILD] Building solution: {sln}");
            var success = await RunDotnetBuildAsync(sln, ct);
            if (!success)
                throw new InvalidOperationException(
                    $"Solution build failed: {sln}. Fix build errors before starting the host.");
        }

        // Build individual projects that have no solution
        var projectOnly = activeServices
            .Where(kvp => string.IsNullOrEmpty(kvp.Value.SolutionPath))
            .ToList();

        var failed = new List<string>();
        foreach (var (name, def) in projectOnly)
        {
            BuildLogger.Info($"[PRE-BUILD] Building project: {name}");
            var success = await RunDotnetBuildAsync(def.ProjectPath, ct);
            if (!success)
                failed.Add(name);
        }

        if (failed.Count > 0)
        {
            BuildLogger.Error($"[PRE-BUILD FAILED] {string.Join(", ", failed)}");
            throw new InvalidOperationException(
                $"Pre-build failed for: {string.Join(", ", failed)}. Fix build errors before starting the host.");
        }

        BuildLogger.Success("[PRE-BUILD OK] All active services built successfully.");
    }

    private static void RegisterService(
        IDistributedApplicationBuilder builder,
        string serviceName,
        ServiceDef def,
        ServiceEnvironmentConfig env)
    {
        var url = $"{def.Scheme}://localhost:{def.Port}";

        var project = builder.AddProject(serviceName, def.ProjectPath, options =>
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

        // Global extra variables
        foreach (var (key, value) in env.ExtraVariables)
            project.WithEnvironment(key, value);

        // Per-service environment variables (override globals)
        foreach (var (key, value) in def.EnvironmentVariables)
            project.WithEnvironment(key, value);
    }

    private static void SubscribeRebuildOnRestart(
        IDistributedApplicationBuilder builder,
        Dictionary<string, ServiceDef> activeServices)
    {
        var startedOnce = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        builder.Eventing.Subscribe<BeforeResourceStartedEvent>(async (@event, ct) =>
        {
            var resourceName = @event.Resource.Name;
            if (!activeServices.ContainsKey(resourceName))
                return;

            if (startedOnce.Add(resourceName))
            {
                BuildLogger.Info($"[SKIP BUILD] {resourceName} — already pre-built.");
                return;
            }

            var def = activeServices[resourceName];
            BuildLogger.Info($"[BUILD] Rebuilding {resourceName}...");

            var success = await RunDotnetBuildAsync(def.ProjectPath, ct);
            if (!success)
                throw new InvalidOperationException(
                    $"Build failed for {resourceName}. Fix build errors before restarting.");

            BuildLogger.Success($"[BUILD OK] {resourceName}");
        });
    }

    private static async Task<bool> RunDotnetBuildAsync(string path, CancellationToken ct)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{path}\" -c Debug",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });

        if (process is null)
            return false;

        await using var reg = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { /* already exited */ }
        });

        var outputTask = StreamLinesAsync(process.StandardOutput, line => BuildLogger.Info($"  {line}"), ct);
        var errorTask = StreamLinesAsync(process.StandardError, line => BuildLogger.Error($"  {line}"), ct);

        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            BuildLogger.Error($"[BUILD FAILED] exit code {process.ExitCode}");

        return process.ExitCode == 0;
    }

    private static async Task StreamLinesAsync(
        StreamReader reader, Action<string> writeLine, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct) is { } line)
            writeLine(line);
    }

    private static void PrintAvailableServices(Dictionary<string, ServiceDef> services)
    {
        BuildLogger.Warn("No active services. Set \"Active\": true in appsettings.json for services to debug.");
        BuildLogger.Warn("Available:");
        foreach (var (name, def) in services)
            BuildLogger.Warn($"  \"{name}\" ({def.Scheme}://localhost:{def.Port})");
    }
}
