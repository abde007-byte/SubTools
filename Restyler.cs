using System.Drawing;

namespace SubTools;
public static class Restyler
{
    //Use styling list to add 'override styling' and 'dialogue tag' for subtitles entries.
    public static int Regroup( SubFile sf, string ssaStyles )
    {
        int dialoguesRestyled = 0;
        for( int i=0; i<sf._dialogues.Count; i++)
        {
            var dialogue = sf._dialogues[i];
            var newGroup = SubFile.DefaultGroupValueForNoneDialogue;

            if( TryGetSSADialogueStyleIndex( dialogue._style, out int index ))
            {
                newGroup = GetStyleGroup( dialogue._textLines );
                newGroup = GroupSSADialogueStyleUpgrade( newGroup, index );
            }
            
            //update only if group is changed
            if( newGroup != dialogue._group )
            {
                dialogue._group = newGroup;
                sf._dialogues[i] = dialogue;
                dialoguesRestyled++;
            }
        }
        return dialoguesRestyled;
    }

    internal static string RestyleTextLines( string textLines, int upGroup, bool removeGroupDefiners )
    {
        GroupParams GP;
        if( upGroup >= SubFile.DefaultGroupValueForDialogue )//must not be NoneDialogue
        {
            upGroup = GroupSSADialogueStyleDowngrade( upGroup );
            GP = GroupParamsArray[upGroup];
        }else{
            GP = GroupParams.Empty;
        }
        
        if( removeGroupDefiners )
        if( upGroup > 0 )//must not be -1 or 0
        if( GP.IsDialogue )//if is dialogue tagged
        if( textLines.Length >= 2 )       //and there will be something left after removing 2 chars
            textLines = textLines[1..^1]; //remove first and last chars, AKA group definers
            
        textLines = GP.StyleOverrideCode + textLines;//add styleOverrideCode

        return textLines;
    }
    internal static int GetStyleGroup( string TextLines )
    {
        int group = SubFile.DefaultGroupValueForDialogue;
        string textLines = TextLines;
        //must have at least two characters
        if( textLines.Length >= 2 )
        {
            //find the group  
            if ( TryGetGroupID( textLines[0], textLines[^1], out var id ) )
            {
                group = id;
            }
        }
        return group;
    }

    private static readonly Dictionary<GroupKeys, GroupParams> GroupsList = [];
    public struct GroupKeys(char cS, char cE)
    {
        public char CS=cS, CE=cE;
        //public GroupKeys() : this('[',']'){}//not needed, just me learning c#
        public override readonly string ToString() => $"{CS}{CE}";
    }
    public struct GroupParams
    {
        public bool IsDialogue;
        public string StyleOverrideCode;
        public GroupParams( bool isDialogue, string styleOverrideCode ){ IsDialogue=isDialogue; StyleOverrideCode=styleOverrideCode; }
        //public GroupParams(){ ID=SubFile.DefaultGroupValueForNoneDialogue; StyleOverrideCode="";}
        public static readonly GroupParams Empty = new(){ IsDialogue=false, StyleOverrideCode="" };
        public override readonly string ToString() => $"IsDialogue:{IsDialogue}, StyleOverrideCode:{StyleOverrideCode}";
        public readonly Dictionary<string, object?> ToDictionary()
        {
            return new Dictionary<string, object?>
            {
                ["IsDialogue"] = IsDialogue,
                ["StyleOverrideCode"] = StyleOverrideCode
            };
        }
        public static GroupParams FromDictionary(Dictionary<string, object?> data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            var isDialogue = data.TryGetValue("IsDialogue", out var rawIsDialogue) && ( (rawIsDialogue is bool b && b) ||
                                                                                        (rawIsDialogue is string st && bool.TryParse(st, out var pb) && pb) ||
                                                                                        (rawIsDialogue is int i && i != 0) ||
                                                                                        (rawIsDialogue is long l && l != 0L)
                                                                                      );
            var styleOverrideCode = data.TryGetValue("StyleOverrideCode", out var rawStyle) && rawStyle is string s ? s : "";
            return new GroupParams(isDialogue, styleOverrideCode);
        }
    }
    public static Dictionary<GroupKeys, GroupParams> CreateGroups( string keys, bool isDialogue, string overrideStyling )
    {
        Dictionary<GroupKeys, GroupParams> output = [];
        foreach( var key in CreateGroupsKeys(keys) )
            output[ key ] = new( isDialogue, overrideStyling );//just like that, and it's inserted or updated!
        return output;
    }
    public static GroupKeys[] CreateGroupsKeys( string input )
    {
        if( (input.Length % 2) != 0 )
            throw new ArgumentException("Definers argument must be in pairs (even number of chars).", nameof(input));
        var output = new GroupKeys[ input.Length/2 ];
        int i=0;
        int j=0;
        while ( i < input.Length )
        {
            char cS = input[i]; i++;
            char cE = input[i]; i++;
            output[j] = new(cS,cE);
            j++;
        }
        return output;
    }

    //used in retrieving "Group number" from styling list, using "GroupKeys(StartCharacter & EndCharacter)"
    private static Dictionary<GroupKeys, int> GroupsIDs = [];
    //used in retrieving "GroupParams(IsDialogue & StyleOverrideCode)" from styling list, using "Group number"
    private static GroupParams[] GroupParamsArray = [];
    //used in checking if a character is a group definer and retrieving the closing chararcter.
    private static Dictionary<char, char> GroupsKeys = [];
    //used in GroupSSADialogueStyleUpgrade 
    private static int GroupsListCount = 0;//=> StyleList.Count + 1 (plus extra one for default entries not included in StyleList)
    public static object? GroupsListToDictionary() => GroupsList.ToDictionary( x => x.Key.ToString(), x => x.Value.ToDictionary(), StringComparer.OrdinalIgnoreCase );

    //run at restyler
    internal static void PrepareStyleListExtras()
    {
        GroupsIDs = new( GroupsList.Count );
        GroupParamsArray = new GroupParams[GroupsList.Count+1];
        GroupsKeys = new( GroupsList.Count );
        //group '0' is default group, "IsDialogue=true"
        GroupParamsArray[ SubFile.DefaultGroupValueForDialogue ] = new GroupParams(
            true,
            ""
        );

        //we will start from 1;
        int i = 1;
        foreach( var gv in GroupsList )
        {
            GroupsIDs[gv.Key] = i;
            GroupParamsArray[i] = gv.Value;
            GroupsKeys[gv.Key.CS] = gv.Key.CE;

            i++;
        }
        GroupsListCount = i;
    }
    internal static bool TestGroupID( int gID, string groupDefiners )
    {
        if( gID == SubFile.DefaultGroupValueForNoneDialogue || groupDefiners == string.Empty )
            return false;
        foreach (GroupKeys key in CreateGroupsKeys(groupDefiners) )
        {
            if (GroupsIDs.TryGetValue(key, out int id ))
            {
                if( id == gID )
                {
                    return true;
                }
            }
        }
        return false;
    }
    internal static bool TryGetGroupID( char CS, char CE, out int gID )
    {
        return GroupsIDs.TryGetValue( new GroupKeys( CS, CE ), out gID);
    }
    internal static bool TryGetGroupKey( char CS, out char CE)
    {
        return GroupsKeys.TryGetValue( CS, out CE );
    }
    //list of SSA styles, used to find if a style is included in the list.
    private static HashSet<string> SSADialogueStylesHashSet = [];
    private static Dictionary<string, int> SSADialogueStylesMap = [];
    private static int SSADialogueStylesCount = 0;
    
    //run at Separator and restyler
    internal static void PrepareSSADialogueStyles( string ssaStyles )
    {
        string styles = ( ssaStyles == "" ) ? SubFile.DefaultSSAStyle : ssaStyles;
        SSADialogueStylesMap.Clear();
        SSADialogueStylesHashSet.Clear();
        int i = 0;//start from ZERO
        foreach( var style in styles.Split(','))
        {
            SSADialogueStylesHashSet.Add( style );
            SSADialogueStylesMap[ style ] = i;
            i++;
        }
        SSADialogueStylesCount = i;
    }
    internal static bool IsSSADialogueStyle( string style )
    {
        if (style == "")
            return SSADialogueStylesHashSet.Contains( SubFile.DefaultSSAStyle );
        else if ( style[0] == '*' )
            return SSADialogueStylesHashSet.Contains( style[1..] );
        else
            return SSADialogueStylesHashSet.Contains( style );
    }
    internal static bool TryGetSSADialogueStyleIndex( string style, out int index )
    {
        if (style == "")
            return SSADialogueStylesMap.TryGetValue( SubFile.DefaultSSAStyle, out index );
        else if ( style[0] == '*' )
            return SSADialogueStylesMap.TryGetValue( style[1..], out index );
        else
            return SSADialogueStylesMap.TryGetValue( style, out index );
    }
    internal static int GroupSSADialogueStyleUpgrade( int group , int styleIndex )
    {
        return group>SubFile.DefaultGroupValueForNoneDialogue? group+(styleIndex*GroupsListCount) :group;
    }
    internal static int GetUpgradedStyleListCount()
    {
        return SSADialogueStylesCount * GroupsListCount;
    }
    internal static int GroupSSADialogueStyleDowngrade( int group )
    {
        return group>SubFile.DefaultGroupValueForNoneDialogue? group%GroupsListCount :group;
    }
    internal static GroupParams GetGroupParams( int downGroup )
    {
        return GroupParamsArray[ downGroup ];
    }



    private static int GetDisplayWidth(string text)
    {
        int width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            int c = rune.Value;
            bool isWide =
                c == 0x3000 ||
                (c >= 0x1100 && c <= 0x115F) ||
                (c >= 0x2329 && c <= 0x232A) ||
                (c >= 0x2E80 && c <= 0xA4CF) ||
                (c >= 0xAC00 && c <= 0xD7A3) ||
                (c >= 0xF900 && c <= 0xFAFF) ||
                (c >= 0xFE10 && c <= 0xFE19) ||
                (c >= 0xFE30 && c <= 0xFE6F) ||
                (c >= 0xFF00 && c <= 0xFF60) ||
                (c >= 0xFFE0 && c <= 0xFFE6);

            width += isWide ? 2 : 1;
        }
        return width;
    }
    private static string PadRightForDisplay(string text, int width)
    {
        int pad = width - GetDisplayWidth(text);
        return text + new string(' ', Math.Max(0, pad));
    }
    
    public static void PrintTableGroups()
    {
        if( GroupsList.Count == 0 ){
            ConsoleHelper.Error("Empty style list!");
            return;
        }
    
        string definerHeader = " Key : ";
        string isDialogueHeader = " IsDialogue :";
        string styleOverrideCodeHeader = " StyleOverrideCode :";
        Console.WriteLine(string.Format("{0} | {1} | {2}",definerHeader,isDialogueHeader,styleOverrideCodeHeader));
        //row initial size
        int widthDefiner = definerHeader.Length;
        int widthIsDialogue = isDialogueHeader.Length;
        Console.WriteLine(new string('-', widthDefiner + widthIsDialogue + 30));
        foreach( var gv in GroupsList )
        {
            Console.WriteLine(string.Format("{0} | {1,-" + widthIsDialogue + "} | {2}", PadRightForDisplay(gv.Key.ToString(),widthDefiner), gv.Value.IsDialogue, gv.Value.StyleOverrideCode));
        }
        Console.WriteLine(new string('-', widthDefiner + widthIsDialogue + 30));
    }

    public static void SetGroups( string keys, bool isDialogue, string overrideStyling, bool showMsg=false )
    {
        foreach( var gv in CreateGroups( keys, isDialogue, overrideStyling ) )
        {
            string _txt = GroupsList.ContainsKey( gv.Key )? "UPDATED: ":"INSERTED: ";
            GroupsList[gv.Key] = gv.Value;
            if( showMsg ) ConsoleHelper.Success( $"=> {gv.ToString()}", $"\t{_txt}\t " );
        }
    }
    public static void RemoveGroups( string keys )
    {
        if( GroupsList.Count == 0 ) ConsoleHelper.Error("Empty style list!");
        else
        {
            foreach( var key in CreateGroupsKeys(keys))
            if ( GroupsList.ContainsKey( key ) )
            {
                ConsoleHelper.Success( $"=> {GroupsList[key].ToString()}", $"\tREMOVED: \t " );
                Console.WriteLine( GroupsList.Remove( key) );
            }else
                 ConsoleHelper.Error( $"Key not found{{{key}}}" );
        }
    }

    public static void Regroup( string ssaStyles )
    {
        if( FileLoader.SubFiles.Count == 0)
        {
            ConsoleHelper.Warning("need to load files!");
            return;
        }

        PrepareSSADialogueStyles( ssaStyles );
        PrepareStyleListExtras();

        int totalFiles = 0;
        int totalDialoguesGrouped = 0;
        for( int i=0; i<FileLoader.SubFiles.Count; i++ )
        {
            SubFile subFile = FileLoader.SubFiles[i];
            //Style
            ConsoleHelper.Verbose($"{subFile.FilePath}", "Regrouping: ");
            int DialoguesGrouped = Regroup( subFile, ssaStyles );

            ConsoleHelper.Verbose($"\t\tdialogues grouped: {DialoguesGrouped}", "");
            
            totalDialoguesGrouped += DialoguesGrouped;
            totalFiles++;
        }

        ConsoleHelper.Success($"Files processed: {totalFiles} | Total entries grouped: {totalDialoguesGrouped}" ,"Completed. ");
        
    }
}