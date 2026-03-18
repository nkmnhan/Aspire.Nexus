using Aspire.Hosting;

namespace Aspire.Nexus.Handlers;

public sealed class DotNetHandler : ServiceHandlerBase
{
    public override ServiceType Type => ServiceType.DotNet;
    public override bool HasPreRunPhase => true;
    public override bool HasRebuildOnRestart => true;

    public override async Task<bool> IsServiceReadyAsync(
        string serviceName, ServiceDef def, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(def.ProjectPath))
            return false;

        var projectFile = new FileInfo(def.ProjectPath);
        if (!projectFile.Exists)
            return false;

        // Check if any build output exists in bin/ that is newer than the project file
        var binDir = Path.Combine(projectFile.DirectoryName!, "bin");
        if (!Directory.Exists(binDir))
            return false;

        var projectName = Path.GetFileNameWithoutExtension(def.ProjectPath);
        var outputDll = Directory.GetFiles(binDir, $"{projectName}.dll", SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        if (outputDll is null)
            return false;

        var isReady = outputDll.LastWriteTimeUtc >= projectFile.LastWriteTimeUtc;
        if (isReady)
            BuildLogger.Info($"[READY] {serviceName} — build output is fresh, skipping pre-build.");

        return isReady;
    }

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
        var config = def.BuildConfiguration ?? context.BuildConfiguration;
        var dllPath = ResolveBuildOutput(def.ProjectPath!, config);

        if (dllPath is not null)
        {
            // Use "dotnet exec" to run the pre-built DLL directly — no MSBuild overhead.
            // AddProject uses "dotnet run" which triggers a full MSBuild per service.
            // With 14 services that's 150+ MSBuild nodes at 100% CPU.
            var projectDir = Path.GetDirectoryName(Path.GetFullPath(def.ProjectPath!))!;
            BuildLogger.Info($"[EXEC] {serviceName}: dotnet exec {Path.GetFileName(dllPath)}");

            builder.AddExecutable(serviceName, "dotnet", projectDir, ["exec", dllPath])
                .WithServiceEndpoint(def)
                .WithCertificate(def, context.BasePath)
                .WithAllEnvironmentVariables(def, context.Environment);
        }
        else
        {
            // Fallback to AddProject if DLL not found (first run, build failed, etc.)
            BuildLogger.Warn($"[FALLBACK] {serviceName}: using AddProject (build output not found)");
            builder.AddProject(serviceName, def.ProjectPath!, options =>
                {
                    options.ExcludeLaunchProfile = true;
                })
                .WithServiceEndpoint(def)
                .WithCertificate(def, context.BasePath)
                .WithAllEnvironmentVariables(def, context.Environment);
        }
    }

    /// <summary>
    /// Finds the compiled DLL for a project in its bin/{config} output directory.
    /// </summary>
    private static string? ResolveBuildOutput(string projectPath, string buildConfiguration)
    {
        var projectFile = new FileInfo(projectPath);
        if (!projectFile.Exists)
            return null;

        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var binDir = Path.Combine(projectFile.DirectoryName!, "bin", buildConfiguration);
        if (!Directory.Exists(binDir))
            return null;

        // Find the DLL in bin/{config}/{tfm}/ — pick the most recently written one
        return Directory.GetFiles(binDir, $"{projectName}.dll", SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()
            ?.FullName;
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
