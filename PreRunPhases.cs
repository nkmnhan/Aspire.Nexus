namespace Aspire.Nexus;

public static class PreRunPhases
{
    public static async Task BuildDotNetServicesAsync(AppHostConfig config, CancellationToken ct = default)
    {
        var dotnetServices = config.GetActive(ServiceType.DotNet).ToDictionary();

        if (dotnetServices.Count == 0)
            return;

        // Build distinct solutions first
        var solutions = dotnetServices.Values
            .Where(d => !string.IsNullOrEmpty(d.SolutionPath))
            .Select(d => d.SolutionPath!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var sln in solutions)
        {
            BuildLogger.Info($"[PRE-BUILD] Building solution: {sln}");
            var success = await ProcessRunner.RunAsync("dotnet", $"build \"{sln}\" -c Debug", ct: ct);
            if (!success)
                throw new InvalidOperationException(
                    $"Solution build failed: {sln}. Fix build errors before starting the host.");
        }

        // Build individual projects that have no solution
        var failed = new List<string>();
        foreach (var (name, def) in dotnetServices.Where(kvp => string.IsNullOrEmpty(kvp.Value.SolutionPath)))
        {
            BuildLogger.Info($"[PRE-BUILD] Building project: {name}");
            var success = await ProcessRunner.RunAsync("dotnet", $"build \"{def.ProjectPath}\" -c Debug", ct: ct);
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

    public static async Task InstallClientDependenciesAsync(AppHostConfig config, CancellationToken ct = default)
    {
        var clientServices = config.GetActive(ServiceType.Client).ToList();

        if (clientServices.Count == 0)
            return;

        foreach (var (name, def) in clientServices)
        {
            var installCmd = def.ResolvedInstallCommand;

            if (string.IsNullOrWhiteSpace(installCmd))
            {
                BuildLogger.Info($"[INSTALL] Skipping {name} — install command is empty.");
                continue;
            }

            var label = ServiceRegistrar.FormatLabel(installCmd, def.InstallCommand);
            BuildLogger.Info($"[INSTALL] {name}: {label}");

            var (cmd, args) = ProcessRunner.ParseCommand(installCmd);
            var success = await ProcessRunner.RunAsync(cmd, args, def.WorkingDirectory, ct);
            if (!success)
                BuildLogger.Warn($"[INSTALL] {name} failed — dev server may still work if dependencies exist.");
        }

        BuildLogger.Success("[INSTALL OK] Client install phase completed.");
    }
}
