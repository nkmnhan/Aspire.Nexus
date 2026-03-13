namespace Aspire.Nexus;

public static class BuildLogger
{
    public static void Info(string message) => Log(ConsoleColor.Cyan, message);
    public static void Success(string message) => Log(ConsoleColor.Green, message);
    public static void Warn(string message) => Log(ConsoleColor.Yellow, message);
    public static void Error(string message) => Log(ConsoleColor.Red, message);

    private static void Log(ConsoleColor color, string message)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}
