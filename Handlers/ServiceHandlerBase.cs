namespace Aspire.Nexus.Handlers;

/// <summary>
/// Abstract base class providing shared validation and install helpers for all handlers.
/// </summary>
public abstract class ServiceHandlerBase : IServiceHandler
{
    public abstract ServiceType Type { get; }

    public abstract void Validate(string serviceName, ServiceDef def, List<string> errors);

    public abstract void Register(Aspire.Hosting.IDistributedApplicationBuilder builder,
        string serviceName, ServiceDef def, RegistrationContext context);

    public abstract bool HasPreRunPhase { get; }

    public virtual Task<bool> IsServiceReadyAsync(string serviceName, ServiceDef def, CancellationToken ct)
        => Task.FromResult(false);

    public abstract Task PreRunBatchAsync(IReadOnlyDictionary<string, ServiceDef> services,
        string buildConfiguration, CancellationToken ct);

    public abstract bool HasRebuildOnRestart { get; }

    public abstract Task RebuildAsync(string serviceName, ServiceDef def,
        string buildConfiguration, CancellationToken ct);

    // ── Shared validation helpers ────────────────────────

    protected string TypeName => Type.ToString();

    protected void RequireWorkingDirectory(string name, ServiceDef def, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(def.WorkingDirectory))
            errors.Add($"\"{name}\" ({TypeName}): \"WorkingDirectory\" is required.");
    }

    protected void RequirePort(string name, ServiceDef def, List<string> errors)
    {
        if (def.Port is null or <= 0)
            errors.Add($"\"{name}\" ({TypeName}): \"Port\" is required.");
    }

    // ── Shared install helpers ─────────────────────────────

    protected virtual string ResolveInstallCommand(ServiceDef def)
        => def.InstallCommand ?? ServiceDef.Defaults.NpmInstallCommand;

    protected async Task RunInstallBatchAsync(
        IReadOnlyDictionary<string, ServiceDef> services, CancellationToken ct)
    {
        foreach (var (name, def) in services)
        {
            var installCmd = ResolveInstallCommand(def);
            await RunInstallCommandAsync(name, installCmd, def.WorkingDirectory, ct);
        }
    }

    protected async Task RunInstallRebuildAsync(
        string serviceName, ServiceDef def, CancellationToken ct)
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

    protected static async Task RunInstallCommandAsync(
        string serviceName, string installCommand, string? workingDirectory, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(installCommand))
            return;

        BuildLogger.Info($"[INSTALL] {serviceName}: {installCommand}");
        var (command, args) = ProcessRunner.ParseCommand(installCommand);
        var success = await ProcessRunner.RunAsync(command, args, workingDirectory, ct: ct);
        if (!success)
            BuildLogger.Warn($"[INSTALL] {serviceName} failed — service may still work if dependencies exist.");
    }
}
