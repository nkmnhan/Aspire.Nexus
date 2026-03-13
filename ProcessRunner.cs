using System.Diagnostics;

namespace Aspire.Nexus;

public static class ProcessRunner
{
    public static async Task<bool> RunAsync(
        string command, string arguments, string? workingDirectory = null, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        if (workingDirectory is not null)
            psi.WorkingDirectory = workingDirectory;

        using var process = Process.Start(psi);

        if (process is null)
            return false;

        await using var reg = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { /* already exited */ }
        });

        var outputTask = StreamLinesAsync(process.StandardOutput, line => BuildLogger.Info($"  {line}"), ct);
        var errorTask = StreamLinesAsync(process.StandardError, line => BuildLogger.Error($"  {line}"), ct);

        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            BuildLogger.Error($"[FAILED] exit code {process.ExitCode}");

        return process.ExitCode == 0;
    }

    public static (string command, string args) ParseCommand(string fullCommand)
    {
        var parts = fullCommand.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return (parts[0], parts.Length > 1 ? parts[1] : "");
    }

    private static async Task StreamLinesAsync(
        StreamReader reader, Action<string> writeLine, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct) is { } line)
            writeLine(line);
    }
}
