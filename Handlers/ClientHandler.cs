using Aspire.Hosting;

namespace Aspire.Nexus.Handlers;

public sealed class ClientHandler : ServiceHandlerBase
{
    public override ServiceType Type => ServiceType.Client;
    public override bool HasPreRunPhase => true;
    public override bool HasRebuildOnRestart => true;

    public override void Validate(string serviceName, ServiceDef def, List<string> errors)
    {
        RequireWorkingDirectory(serviceName, def, errors);
        RequirePort(serviceName, def, errors);
    }

    public override void Register(IDistributedApplicationBuilder builder, string serviceName,
        ServiceDef def, RegistrationContext context)
    {
        var devCmd = def.ResolvedDevCommand;
        BuildLogger.Info($"[CLIENT] {serviceName}: {devCmd} -> {def.Scheme}://localhost:{def.Port}");

        var (command, args) = ProcessRunner.ParseCommand(devCmd);
        var (executable, execArgs) = ProcessRunner.ResolveCommand(command, args);

        builder.AddExecutable(serviceName, executable, def.WorkingDirectory!, execArgs)
            .WithServiceEndpoint(def)
            .WithAllEnvironmentVariables(def);
    }

    public override async Task PreRunBatchAsync(
        IReadOnlyDictionary<string, ServiceDef> services, CancellationToken ct)
    {
        foreach (var (name, def) in services)
        {
            var installCmd = def.InstallCommand ?? ServiceDef.Defaults.NpmInstallCommand;
            await RunInstallCommandAsync(name, installCmd, def.WorkingDirectory, ct);
        }
    }

    public override async Task RebuildAsync(string serviceName, ServiceDef def, CancellationToken ct)
    {
        var installCmd = def.InstallCommand ?? ServiceDef.Defaults.NpmInstallCommand;
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
}
