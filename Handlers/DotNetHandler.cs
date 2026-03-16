using Aspire.Hosting;

namespace Aspire.Nexus.Handlers;

public sealed class DotNetHandler : ServiceHandlerBase
{
    public override ServiceType Type => ServiceType.DotNet;
    public override bool HasPreRunPhase => true;
    public override bool HasRebuildOnRestart => true;

    public override void Validate(string serviceName, ServiceDef def, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(def.ProjectPath))
            errors.Add($"\"{serviceName}\" (DotNet): \"ProjectPath\" is required.");

        if (def.Certificate is not null && !def.Certificate.IsPem && string.IsNullOrWhiteSpace(def.Certificate.Password))
            errors.Add($"\"{serviceName}\" (DotNet): PFX certificate requires \"Password\".");
    }

    public override void Register(IDistributedApplicationBuilder builder, string serviceName,
        ServiceDef def, RegistrationContext context)
    {
        builder.AddProject(serviceName, def.ProjectPath!, options =>
            {
                options.ExcludeLaunchProfile = true;
            })
            .WithServiceEndpoint(def)
            .WithCertificate(def, context.BasePath)
            .WithAllEnvironmentVariables(def, context.Environment);
    }

    public override async Task PreRunBatchAsync(
        IReadOnlyDictionary<string, ServiceDef> services, string buildConfiguration, CancellationToken ct)
    {
        if (services.Count == 0)
            return;

        // Build distinct solutions first
        var solutions = services.Values
            .Where(def => !string.IsNullOrEmpty(def.SolutionPath))
            .Select(def => def.SolutionPath!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var sln in solutions)
        {
            BuildLogger.Info($"[PRE-BUILD] Building solution: {sln} ({buildConfiguration})");
            var success = await ProcessRunner.RunAsync("dotnet", $"build \"{sln}\" -c {buildConfiguration}", ct: ct);
            if (!success)
                throw new InvalidOperationException(
                    $"Solution build failed: {sln}. Fix build errors before starting the host.");
        }

        // Build individual projects that have no solution
        var failed = new List<string>();
        foreach (var (name, def) in services.Where(kvp => string.IsNullOrEmpty(kvp.Value.SolutionPath)))
        {
            var config = def.BuildConfiguration ?? buildConfiguration;
            BuildLogger.Info($"[PRE-BUILD] Building project: {name} ({config})");
            var success = await ProcessRunner.RunAsync("dotnet", $"build \"{def.ProjectPath}\" -c {config}", ct: ct);
            if (!success)
                failed.Add(name);
        }

        if (failed.Count > 0)
        {
            BuildLogger.Error($"[PRE-BUILD FAILED] {string.Join(", ", failed)}");
            throw new InvalidOperationException(
                $"Pre-build failed for: {string.Join(", ", failed)}. Fix build errors before starting the host.");
        }

        BuildLogger.Success("[PRE-BUILD OK] All .NET services built successfully.");
    }

    public override async Task RebuildAsync(string serviceName, ServiceDef def,
        string buildConfiguration, CancellationToken ct)
    {
        var config = def.BuildConfiguration ?? buildConfiguration;
        BuildLogger.Info($"[REBUILD] {serviceName}: dotnet build ({config})...");
        var success = await ProcessRunner.RunAsync("dotnet", $"build \"{def.ProjectPath}\" -c {config}", ct: ct);
        if (!success)
            throw new InvalidOperationException(
                $"Build failed for {serviceName}. Fix build errors before restarting.");
        BuildLogger.Success($"[REBUILD OK] {serviceName}");
    }
}
