using Aspire.Nexus;
using Microsoft.Extensions.Configuration;

// Prevent thread pool starvation when many services run concurrently.
// Aspire's DCP uses thread pool threads for log forwarding and resource lifecycle events.
// Without this, concurrent rebuilds and process I/O can exhaust the pool, causing
// Kestrel heartbeat warnings and dropped log streams on service restart.
ThreadPool.SetMinThreads(workerThreads: 64, completionPortThreads: 64);

// Kill MSBuild worker nodes after each build completes instead of keeping them alive.
// By default, MSBuild reuses nodes across builds (nodeReuse:true), which leaves 10-15+
// dotnet processes (~100-250 MB each) lingering after pre-build and rebuild phases.
// With many services, this wastes 1-2 GB of RAM for idle MSBuild nodes.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => ProcessRunner.KillAll();

try
{
    var builder = DistributedApplication.CreateBuilder(args);

    var config = builder.Configuration.GetSection("AppHost").Get<AppHostConfig>()
        ?? throw new InvalidOperationException(
            "AppHost configuration is missing. Check appsettings.json and/or user secrets.");

    ServiceOrchestrator.Validate(config);

    await ServiceOrchestrator.PreRunAsync(config, cts.Token);

    ServiceOrchestrator.RegisterAll(builder, config, basePath: builder.AppHostDirectory);

    builder.Build().Run();
}
catch (OperationCanceledException)
{
    ProcessRunner.KillAll();
    BuildLogger.Warn("Cancelled by user.");
}
catch (Exception ex)
{
    BuildLogger.Error($"[FATAL] {ex.Message}");
    Environment.ExitCode = 1;
}

// Always pause so double-click users can read output before the window closes.
BuildLogger.Info("");
BuildLogger.Info("Press any key to exit...");
Console.ReadKey(intercept: true);
