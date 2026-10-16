using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace SubTools;

public static class Config
{
    internal const int InitialDialogueCapacity = 100;
    public static readonly string FilePath = Path.Combine( AppContext.BaseDirectory, "config.json" );
    public enum RecalculateModeType {Extend, Shift};

    /*************************************************************************/
    /************************ options, default values ************************/
    /*************************************************************************/
    internal static bool Miscellaneous_Verbose;
    /*************************************************************************/
    internal static bool Separate_Enclosed = false;
    internal static bool Separate_Line_Breaks = false;
    /*************************************************************************/
    internal static string Regroup_Ssa_Dialogue_Styles = SubFile.DefaultSSAStyle;
    /*************************************************************************/
    internal static double Merge_Max_Gap = 0.0;
    internal static int Merge_Max_Chars = 80;
    internal static bool Merge_Unpunctuated = false;
    internal static bool Merge_Make_Dialogue = false;
    internal static bool Merge_Strict_Test = false;
    /*************************************************************************/
    internal static RecalculateModeType Recalculate_Mode = RecalculateModeType.Extend;
    internal static bool Recalculate_Overlap = false;
    internal static bool Recalculate_Shrink = false;
    internal static bool Recalculate_All = false;
    internal static double Recalculate_Chars_Per_Second = 12.0;
    internal static double Recalculate_Min_Duration = 1.0;
    internal static double Recalculate_Max_Duration = 6.0;
    internal static double Recalculate_Gap = 0.0;
    /*************************************************************************/
    internal static bool Save_Remove_Line_Breaks = false;
    internal static bool Save_Remove_Group_Definers = false;
    internal static bool Save_Skip_Backup = false;
    /*************************************************************************/


    internal static readonly string[] ParamsKeysList =
    [
        "Miscellaneous:Verbose",

        "Separate:Enclosed",
        "Separate:Line Breaks",

        "Regroup:SSA Dialogue Styles",

        "Merge:Max Gap",
        "Merge:Max Chars",
        "Merge:Unpunctuated",
        "Merge:Make Dialogue",
        "Merge:Strict Test",

        "Recalculate:Mode",
        "Recalculate:Overlap",
        "Recalculate:Shrink",
        "Recalculate:All",
        "Recalculate:Chars Per Second",
        "Recalculate:Min Duration",
        "Recalculate:Max Duration",
        "Recalculate:Gap",

        "Save:Remove Line Breaks",
        "Save:Skip Backup",
        "Save:Remove Group Definers"
    ];

    public static void SetValue(string paramKey, string value)
    {
        try
        {
            var field = GetFieldInfoFromName( paramKey );
            object converted = field.FieldType switch
            {
                var t when t == typeof(bool) => bool.Parse(value),
                var t when t == typeof(int) => int.Parse(value),
                var t when t == typeof(double) => double.Parse(value),
                var t when t == typeof(RecalculateModeType) => Enum.Parse<RecalculateModeType>(value, true),
                var t when t == typeof(string) => value,
                _ => throw new NotSupportedException($"Type not supported: {field.FieldType}")
            };
            field.SetValue(null, converted);
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error( ex.Message );
        }
    }

    public static object? GetValue(string paramKey)
    {
        try
        {
            var field = GetFieldInfoFromName( paramKey );
            return field.GetValue(null);
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error( ex.Message );
            return null;
        }
    }
    
    private static FieldInfo GetFieldInfoFromName( string fieldName ){
        if ( ! ParamsKeysList.Any(x => string.Equals(x, fieldName.Replace('_', ' '), StringComparison.OrdinalIgnoreCase)) )
            throw new InvalidOperationException($"Parameter not supported: {fieldName}");
           
        
        //var parts = fieldName.Split(':');
        var parts = fieldName.Split(new[] { ':', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        fieldName = string.Join( '_', 
            parts.Select(p =>
                //for each part => first char to upper and the rest to lower case
                char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant()
            )
        );

        return typeof(Config).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic )
            ?? throw new InvalidOperationException($"Field not found: {fieldName}");
    }

    // Static constructor runs once before first use
    static Config()
    {
        try
        {
            var Builder = new ConfigurationBuilder();
            if (File.Exists(FilePath))
            {
                Builder.AddJsonFile( FilePath, optional: true, reloadOnChange: false);
                ConsoleHelper.Verbose($"Loading Configurations File!, {FilePath}");
            }

            var config = Builder.Build();

            foreach (var pair in config.AsEnumerable())
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
                    continue;

                if (ParamsKeysList.Any(x =>
                    string.Equals(x, pair.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    SetValue(pair.Key, pair.Value);
                }
            }

            //read values of Restyler.StyleList
            var loadedStyleGroups = config.GetSection("Style:Groups").GetChildren();//.Get<Dictionary<string, Restyler.GroupParams>>();
            if( loadedStyleGroups != null ) foreach( var styleGroup in loadedStyleGroups! )
            {
                var value = Restyler.GroupParams.FromDictionary(
                    styleGroup.GetChildren().ToDictionary(x => x.Key, x => (object?)x.Value)
                );
                Restyler.SetStyleGroup( styleGroup.Key, value.IsDialogue, value.StyleOverrideCode, false);
            }
        }
        catch (Exception err)
        {
            ConsoleHelper.Error(err.Message);
        }

        if( Restyler.StyleList.Count == 0)
        {
            Restyler.SetStyleGroup( "[]「」『』【】〔〕［］", false, "{\\an8}", false);
            Restyler.SetStyleGroup( "()==《》〈〉（）｛｝«»“”‘’", true, "{\\i1}", false);
            Restyler.SetStyleGroup( "♪♪♫♫", true, "{\\an8\\i1}", false);
            //Restyler.SetStyleGroup( "()[]==《》〈〉「」『』【】〔〕（）［］｛｝«»“”‘’♪♪♫♫", false, "{\\an8\\i1}", false);
        }
    }

    public static void Save()
    {
        var root = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in ParamsKeysList)
        {
            var value = GetValue(key);
            AddValueToTree(root, key, value);
        }

        //insert values of Restyler.StyleList
        var styleGroups = Restyler.StyleList.ToDictionary(
                x => x.Key.ToString(),
                x => x.Value.ToDictionary(),
                StringComparer.OrdinalIgnoreCase
        );
        AddValueToTree(root, "Style:Groups", styleGroups );

        var json = JsonSerializer.Serialize(root, new JsonSerializerOptions{ WriteIndented = true });
        File.WriteAllText( FilePath, json);

        ConsoleHelper.Success($"Config file saved! {FilePath}");
    }

    private static void AddValueToTree(Dictionary<string, object?> root, string key, object? value)
    {
        var parts = key.Split(':');
        Dictionary<string, object?> current = root;
        //runs only for all parts except the last one.
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            //makes sure the parent section existsif object
            if (!current.TryGetValue(part, out var existing) || existing is not Dictionary<string, object?> next)
            {
                //creat 'string' key and 'object' value
                next = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                current[part] = next;
            }
            //continue creating nested Dictionary<string, object?>
            current = next;
        }
        //set the value to the last part
        current[parts[^1]] = value;
    }

    public static void PrintTable()
    {
        string keyHeader = " Key :";
        string typeHeader = " Type :";
        string valueHeader = " Value :";
        //row initial size
        int widthKey = keyHeader.Length;
        int widthType = typeHeader.Length;
        var rows = new List<string>();
        foreach (var key in ParamsKeysList)
        {
            var field = GetFieldInfoFromName(key);
            var value = field.GetValue(null);
            string type = field.FieldType.Name;
            rows.Add($"{key} | {type} | {value}");
            //row max size
            widthKey = Math.Max( key.Length, widthKey );
            widthType = Math.Max( type.Length, widthType );
        }
        //print
        //Console.WriteLine($"{keyHeader.PadRight(widthKey)} | {typeHeader.PadRight(widthType)} | {valueHeader}");
        Console.WriteLine(string.Format("{0,-" + widthKey + "} | {1,-" + widthType + "} | {2}",keyHeader,typeHeader,valueHeader));
        Console.WriteLine(new string('-', widthKey + widthType + 20));
        foreach (var row in rows)
        {
            var parts = row.Split(" | ");
            //Console.WriteLine($"{parts[0].PadRight(widthKey)} | {parts[1].PadRight(widthType)} | {parts[2]}");
            Console.WriteLine(string.Format("{0,-" + widthKey + "} | {1,-" + widthType + "} | {2}",parts[0],parts[1],parts[2]));
        }
        Console.WriteLine(new string('-', widthKey + widthType + 20));
        //print restyle info
        Console.WriteLine("**Use [regroup] to view/edit StylingList.");
    }

}