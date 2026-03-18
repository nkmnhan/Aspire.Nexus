using Aspire.Nexus;
using Microsoft.Extensions.Configuration;

// Prevent MSBuild from keeping long-lived node processes after DCP's "dotnet run" builds.
// Without this, 14 services × ~13 MSBuild nodes = 150+ zombie processes at 100% CPU.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");
Environment.SetEnvironmentVariable("DOTNET_CLI_USE_MSBUILD_SERVER", "0");

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
