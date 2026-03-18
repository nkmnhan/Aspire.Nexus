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

    public override Task PreRunBatchAsync(
        IReadOnlyDictionary<string, ServiceDef> services, string buildConfiguration, CancellationToken ct)
        => RunInstallBatchAsync(services, ct);

    public override Task RebuildAsync(string serviceName, ServiceDef def,
        string buildConfiguration, CancellationToken ct)
        => RunInstallRebuildAsync(serviceName, def, ct);
}
