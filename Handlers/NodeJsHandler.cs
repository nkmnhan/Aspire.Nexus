using Aspire.Hosting;
using Aspire.Hosting.JavaScript;

namespace Aspire.Nexus.Handlers;

public sealed class NodeJsHandler : ServiceHandlerBase
{
    public override ServiceType Type => ServiceType.NodeJs;
    public override bool HasPreRunPhase => false;
    public override bool HasRebuildOnRestart => false;

    public override void Validate(string serviceName, ServiceDef def, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(def.WorkingDirectory))
            errors.Add($"\"{serviceName}\" (NodeJs): \"WorkingDirectory\" is required — path to the npm project root.");

        RequirePort(serviceName, def, errors);
    }

    public override void Register(IDistributedApplicationBuilder builder, string serviceName,
        ServiceDef def, RegistrationContext context)
    {
        var scriptName = def.ResolvedScriptName;
        var packageManager = def.ResolvedPackageManager;
        BuildLogger.Info($"[NODEJS] {serviceName}: {packageManager} run {scriptName} -> {def.Scheme}://localhost:{def.Port}");

        var nodeApp = builder.AddJavaScriptApp(serviceName, def.WorkingDirectory!, scriptName);

        // Aspire handles install automatically via the package manager extension
        switch (packageManager.ToLowerInvariant())
        {
            case "yarn":
                nodeApp.WithYarn();
                break;
            case "pnpm":
                nodeApp.WithPnpm();
                break;
            default:
                nodeApp.WithNpm();
                break;
        }

        nodeApp
            .WithServiceEndpoint(def)
            .WithAllEnvironmentVariables(def);
    }

    public override Task PreRunBatchAsync(
        IReadOnlyDictionary<string, ServiceDef> services, string buildConfiguration, CancellationToken ct)
        => Task.CompletedTask;

    public override Task RebuildAsync(string serviceName, ServiceDef def,
        string buildConfiguration, CancellationToken ct)
        => Task.CompletedTask;
}
