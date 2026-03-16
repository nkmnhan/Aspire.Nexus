using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Nexus.Handlers;

public sealed class ContainerHandler : ServiceHandlerBase
{
    public override ServiceType Type => ServiceType.Container;
    public override bool HasPreRunPhase => false;
    public override bool HasRebuildOnRestart => false;

    public override void Validate(string serviceName, ServiceDef def, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(def.Image))
            errors.Add($"\"{serviceName}\" (Container): \"Image\" is required.");
    }

    public override void Register(IDistributedApplicationBuilder builder, string serviceName,
        ServiceDef def, RegistrationContext context)
    {
        BuildLogger.Info($"[CONTAINER] {serviceName}: {def.Image}:{def.Tag} -> localhost:{def.Port}");

        var container = builder.AddContainer(serviceName, def.Image!, def.Tag)
            .WithLifetime(ContainerLifetime.Persistent);

        if (def.Port is > 0)
        {
            // Primary port: Port = host port, TargetPort = container port (defaults to same as Port)
            var targetPort = def.TargetPort ?? def.Port.Value;
            container.WithEndpoint(port: def.Port.Value, targetPort: targetPort, name: serviceName, isProxied: false);
        }

        // Additional ports (e.g. RabbitMQ management UI on 15672)
        foreach (var mapping in def.AdditionalPorts)
            container.WithEndpoint(port: mapping.Port, targetPort: mapping.TargetPort, name: $"{serviceName}-{mapping.Port}", isProxied: false);

        foreach (var (key, value) in def.EnvironmentVariables)
            container.WithEnvironment(key, value);

        foreach (var (source, containerPath) in def.Volumes)
        {
            if (Path.IsPathRooted(source) || source.StartsWith("./") || source.StartsWith("../"))
                container.WithBindMount(source, containerPath);
            else
                container.WithVolume(source, containerPath);
        }

        foreach (var arg in def.Args)
            container.WithArgs(arg.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public override Task PreRunBatchAsync(
        IReadOnlyDictionary<string, ServiceDef> services, string buildConfiguration, CancellationToken ct)
        => Task.CompletedTask;

    public override Task RebuildAsync(string serviceName, ServiceDef def,
        string buildConfiguration, CancellationToken ct)
        => Task.CompletedTask;
}
