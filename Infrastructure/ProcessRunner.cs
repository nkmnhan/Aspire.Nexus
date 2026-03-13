using System.Diagnostics;

namespace Aspire.Nexus.Infrastructure;

public static class ProcessRunner
{
    /// <summary>
    /// Windows extensions to search when a command has no file extension.
    /// Matches the order Windows CMD uses to resolve commands.
    /// </summary>
    private static readonly string[] WindowsExecutableExtensions = [".cmd", ".bat", ".exe", ".com"];

    public static async Task<bool> RunAsync(
        string command, string arguments, string? workingDirectory = null,
        bool silent = false, CancellationToken ct = default)
    {
        var resolvedCommand = ResolveExecutable(command);

        var psi = new ProcessStartInfo
        {
            FileName = resolvedCommand,
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

        if (silent)
        {
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

    /// <summary>
    /// Splits "npm run dev" into ("npm", "run dev").
    /// </summary>
    public static (string command, string args) ParseCommand(string fullCommand)
    {
        var parts = fullCommand.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return (parts[0], parts.Length > 1 ? parts[1] : "");
    }

    /// <summary>
    /// Resolves a command and arguments into an executable path and argument array.
    /// On Windows, resolves "npm" → "C:\...\npm.cmd" so it can run directly without cmd.exe.
    /// </summary>
    public static (string executable, string[] args) ResolveCommand(string command, string arguments)
    {
        var resolvedCommand = ResolveExecutable(command);
        var argParts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return (resolvedCommand, argParts);
    }

    /// <summary>
    /// Resolves a bare command name to its full executable path.
    /// On Windows, searches PATH for .cmd/.bat/.exe/.com variants
    /// (e.g. "npm" → "C:\Program Files\nodejs\npm.cmd").
    /// On Linux/macOS, returns the command as-is (the OS resolves it).
    /// </summary>
    private static string ResolveExecutable(string command)
    {
        // Already has an extension (e.g. "dotnet.exe") or not on Windows — use as-is
        if (Path.HasExtension(command) || !OperatingSystem.IsWindows())
            return command;

        // Search PATH for the command with common Windows extensions
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];

        foreach (var directory in pathDirs)
        {
            foreach (var extension in WindowsExecutableExtensions)
            {
                var candidate = Path.Combine(directory, command + extension);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        // Not found in PATH — return as-is, let the OS throw a clear error
        return command;
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
