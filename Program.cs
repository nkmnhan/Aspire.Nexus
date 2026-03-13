using Aspire.Nexus;
using Microsoft.Extensions.Configuration;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    var builder = DistributedApplication.CreateBuilder(args);

    var config = builder.Configuration.GetSection("AppHost").Get<AppHostConfig>()
        ?? throw new InvalidOperationException(
            "AppHost configuration is missing. Check appsettings.json and/or user secrets.");

    await PreRunPhases.StartInfrastructureAsync(config, cts.Token);
    await PreRunPhases.BuildDotNetServicesAsync(config, cts.Token);
    await PreRunPhases.InstallClientDependenciesAsync(config, cts.Token);

    ServiceRegistrar.RegisterServices(builder, config);

    builder.Build().Run();
}
catch (OperationCanceledException)
{
    BuildLogger.Warn("Cancelled by user.");
}
catch (Exception ex)
{
    BuildLogger.Error($"[FATAL] {ex.Message}");
    Environment.ExitCode = 1;
}
