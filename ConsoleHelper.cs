namespace SubTools;
public static class ConsoleHelper
{
    public static void Writeline( string message, string? header=default, ConsoleColor consoleColor=default)
    {
        if( ! string.IsNullOrWhiteSpace(header))
        {
            Console.ForegroundColor = consoleColor;
            Console.Write(header);
            Console.ResetColor();
        }
        Console.WriteLine(message);
    }

    public static void Success( string message, string? header=default )
    {
        Writeline( message, header??"SUCCESS: ", ConsoleColor.Green);
    }

    public static void Error( string message, string? header=default )
    {
        Writeline( message, header??"ERROR: ", ConsoleColor.Red);
    }

    public static void Warning( string message, string? header=default )
    {
        Writeline( message, header??"WARNING: ", ConsoleColor.Yellow);
    }

    public static void Verbose( string message, string? header=default )
    {
        if (!Config.Miscellaneous_Verbose) return;
        Writeline( message, header??"VERBOSE: ", ConsoleColor.DarkGray);
    }

    static ConsoleHelper()
    {
        
    }
}
