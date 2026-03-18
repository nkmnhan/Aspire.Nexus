using Aspire.Nexus;
using Microsoft.Extensions.Configuration;

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
