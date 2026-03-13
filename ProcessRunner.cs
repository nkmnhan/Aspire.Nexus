using System.Diagnostics;

namespace Aspire.Nexus;

public static class ProcessRunner
{
    public static async Task<bool> RunAsync(
        string command, string arguments, string? workingDirectory = null,
        bool silent = false, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // On Windows, .cmd/.bat files (npm, npx, etc.) require cmd.exe to execute
        if (OperatingSystem.IsWindows() && !Path.HasExtension(command))
        {
            psi.FileName = "cmd.exe";
            psi.Arguments = $"/c {command} {arguments}";
        }

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

        if (silent)
        {
            // Discard output silently
            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);
            await Task.WhenAll(outputTask, errorTask);
        }
        else
        {
            var outputTask = StreamLinesAsync(process.StandardOutput, line => BuildLogger.Info($"  {line}"), ct);
            var errorTask = StreamLinesAsync(process.StandardError, line => LogStderr(line), ct);
            await Task.WhenAll(outputTask, errorTask);
        }

        await process.WaitForExitAsync(ct);

        if (!silent && process.ExitCode != 0)
            BuildLogger.Error($"[FAILED] exit code {process.ExitCode}");

        return process.ExitCode == 0;
    }

    public static (string command, string args) ParseCommand(string fullCommand)
    {
        var parts = fullCommand.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return (parts[0], parts.Length > 1 ? parts[1] : "");
    }

    private static void LogStderr(string line)
    {
        // Stderr is not always an error — many tools write progress/warnings to stderr.
        // Only treat lines with actual error keywords as errors.
        if (string.IsNullOrWhiteSpace(line))
            return;

        var isError = line.StartsWith("error", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("FATAL", StringComparison.OrdinalIgnoreCase) ||
                      line.StartsWith("npm ERR!", StringComparison.OrdinalIgnoreCase);

        if (isError)
            BuildLogger.Error($"  {line}");
        else
            BuildLogger.Warn($"  {line}");
    }

    private static async Task StreamLinesAsync(
        StreamReader reader, Action<string> writeLine, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct) is { } line)
            writeLine(line);
    }
}
