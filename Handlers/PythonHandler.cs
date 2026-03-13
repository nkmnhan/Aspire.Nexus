using Aspire.Hosting;
using Aspire.Hosting.Python;

namespace Aspire.Nexus.Handlers;

public sealed class PythonHandler : ServiceHandlerBase
{
    public override ServiceType Type => ServiceType.Python;
    public override bool HasPreRunPhase => true;
    public override bool HasRebuildOnRestart => true;

    public override void Validate(string serviceName, ServiceDef def, List<string> errors)
    {
        RequireWorkingDirectory(serviceName, def, errors);

        if (string.IsNullOrWhiteSpace(def.ScriptPath))
            errors.Add($"\"{serviceName}\" (Python): \"ScriptPath\" is required — entry-point script (e.g. \"app.py\").");

        RequirePort(serviceName, def, errors);
    }

    public override void Register(IDistributedApplicationBuilder builder, string serviceName,
        ServiceDef def, RegistrationContext context)
    {
        BuildLogger.Info($"[PYTHON] {serviceName}: {def.ScriptPath} -> {def.Scheme}://localhost:{def.Port}");

        var pythonApp = builder.AddPythonApp(serviceName, def.WorkingDirectory!, def.ScriptPath!);

        if (!string.IsNullOrEmpty(def.VirtualEnvironmentPath))
            pythonApp.WithVirtualEnvironment(def.VirtualEnvironmentPath);

        pythonApp
            .WithServiceEndpoint(def)
            .WithAllEnvironmentVariables(def);
    }

    public override async Task PreRunBatchAsync(
        IReadOnlyDictionary<string, ServiceDef> services, CancellationToken ct)
    {
        foreach (var (name, def) in services)
        {
            var installCmd = ResolveInstallCommand(def);
            await RunInstallCommandAsync(name, installCmd, def.WorkingDirectory, ct);
        }
    }

    public override async Task RebuildAsync(string serviceName, ServiceDef def, CancellationToken ct)
    {
        var installCmd = ResolveInstallCommand(def);
        if (string.IsNullOrWhiteSpace(installCmd))
            return;

        BuildLogger.Info($"[REINSTALL] {serviceName}: {installCmd}...");
        var (command, args) = ProcessRunner.ParseCommand(installCmd);
        var success = await ProcessRunner.RunAsync(command, args, def.WorkingDirectory, ct: ct);
        if (!success)
            BuildLogger.Warn($"[REINSTALL] {serviceName} failed — service may still work if dependencies exist.");
        else
            BuildLogger.Success($"[REINSTALL OK] {serviceName}");
    }

    /// <summary>
    /// Resolves the install command, replacing bare "pip" with the venv pip path when applicable.
    /// </summary>
    private static string ResolveInstallCommand(ServiceDef def)
    {
        var installCmd = def.InstallCommand ?? ServiceDef.Defaults.PipInstallCommand;

        if (string.IsNullOrWhiteSpace(installCmd) || string.IsNullOrEmpty(def.VirtualEnvironmentPath))
            return installCmd;

        var venvPip = GetVenvPipPath(def.WorkingDirectory!, def.VirtualEnvironmentPath);
        if (venvPip is not null)
            installCmd = installCmd.Replace("pip", $"\"{venvPip}\"");

        return installCmd;
    }

    /// <summary>
    /// Returns the path to pip inside a virtual environment, or null if not found.
    /// On Windows: venv/Scripts/pip.exe, on Linux/macOS: venv/bin/pip.
    /// </summary>
    private static string? GetVenvPipPath(string workingDirectory, string venvPath)
    {
        var venvRoot = Path.Combine(workingDirectory, venvPath);
        var pipPath = OperatingSystem.IsWindows()
            ? Path.Combine(venvRoot, "Scripts", "pip.exe")
            : Path.Combine(venvRoot, "bin", "pip");

        return File.Exists(pipPath) ? pipPath : null;
    }
}
