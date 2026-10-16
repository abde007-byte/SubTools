namespace SubTools;
public static class ConsoleHelper
{
    public static void WritelineError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void Success(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"ERROR: {message}");
        Console.ResetColor();
    }

    public static void Warning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"WARNING: {message}");
        Console.ResetColor();
    }

    public static void Verbose(string message)
    {
        if (!Config.Miscellaneous_Verbose) return;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"VERBOSE: {message}");
        Console.ResetColor();
    }

    static ConsoleHelper()
    {
        
    }
}
