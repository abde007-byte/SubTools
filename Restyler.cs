namespace SubTools;
public static class Restyler
{
    public static string RemoveLineBreaks( string input )
    {
        char[] inputChars = input.ToCharArray();
        int topMinus = inputChars.Length - 1;
        for( int i=0; i<topMinus; i++ )
            if( (inputChars[i] == '\n') && (inputChars[i+1] != '-') )
                inputChars[i] = ' ';//replace with space
        return new string(inputChars);
    }
    public static string RestyleTextLines( string textLines, int upGroup, bool removeLineBreaks, bool removeGroupDefiners )
    {
        GroupParams GP;
        if( upGroup >= 0 )//must not be -1
        {
            upGroup = GroupSSADialogueStyleDowngrade( upGroup );
            GP = GroupsParams[upGroup];
        }else{
            GP = GroupParams.Empty;
        }
        
        if( removeGroupDefiners )
        if( upGroup > 0 )//must not be -1 or 0
        if( GP.IsDialogue )//if is dialogue tagged
        if( textLines.Length >= 2 )       //and there will be something left after removing 2 chars
            textLines = textLines[1..^1]; //remove first and last chars, AKA group definers
            
        textLines = GP.StyleOverrideCode + textLines;//add styleOverrideCode
        
        if( removeLineBreaks && GP.IsDialogue )
            textLines = RemoveLineBreaks( textLines );////remove line-breaks

        return textLines;
    }
    public static int GetStyleGroup( string TextLines )
    {
        int group = SubFile.DefaultGroupValueForDialogue;
        string textLines = TextLines;
        //must have at least two characters
        if( textLines.Length >= 2 )
        {
            //find the group  
            if (StyleListWithPosition.TryGetValue( new GroupKeys( textLines[0], textLines[^1] ), out var gValue))
            {
                group = gValue.Position;
            }
        }
        return group;
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
        int totalDialoguesStyled = 0;
        for( int i=0; i<FileLoader.SubFiles.Count; i++ )
        {
            SubFile subFile = FileLoader.SubFiles[i];
            //Style
            ConsoleHelper.Verbose($"Regrouping: {subFile.FilePath}");
            totalDialoguesStyled += subFile.Regroup( ssaStyles );
            
            totalFiles++;
        }

        ConsoleHelper.Success($"Completed. Files processed: {totalFiles} | Total entries grouped: {totalDialoguesStyled}");
        
    }

    
    public static void ViewStyleGroup()
    {
        if( StyleList.Count == 0 ) ConsoleHelper.Error("Empty style list!");
        else foreach( var gv in StyleList )
            Console.WriteLine( $"   => {gv.ToString()}" );
    }

    public static void SetStyleGroup( string keys, bool isDialogue, string overrideStyling, bool showMsg=true )
    {
        foreach( var gv in CreateGroups( keys, isDialogue, overrideStyling ) )
        {
            string _txt = StyleList.ContainsKey( gv.Key )? "UPDATED: ":"INSERTED: ";
            StyleList[gv.Key] = gv.Value;
            if( showMsg ) ConsoleHelper.Success( $"    {_txt} => {gv.ToString()}" );
        }
    }
    public static void RemoveStyleGroup( string keys )
    {
        if( StyleList.Count == 0 ) ConsoleHelper.Error("Empty style list!");
        else
        {
            foreach( var key in CreateGroupsKeys(keys))
            if ( StyleList.ContainsKey( key ) )
            {
                ConsoleHelper.Success( $"    REMOVED: => {StyleList[key].ToString()}" );
                Console.WriteLine( StyleList.Remove( key) );
            }else
                 ConsoleHelper.Error( $"Key not found{{{key}}}" );
        }
    }

    public static readonly Dictionary<GroupKeys, GroupParams> StyleList = [];
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
        public GroupParams(bool isDialogue, string styleOverrideCode ){ IsDialogue=isDialogue; StyleOverrideCode=styleOverrideCode; }
        public GroupParams() => StyleOverrideCode="";
        public static readonly GroupParams Empty = new(false, "");
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
    internal struct GroupParamsWithPosition
    {
        internal bool IsDialogue;
        internal string StyleOverrideCode;
        internal int Position;
    }

    //used in retrieving "Group number" from styling list, using "GroupKeys(StartCharacter & EndCharacter)"
    private static Dictionary<GroupKeys, GroupParamsWithPosition> StyleListWithPosition = [];
    //used in retrieving "GroupParams(IsDialogue & StyleOverrideCode)" from styling list, using "Group number"
    private static GroupParams[] GroupsParams = [];
    //used in checking if a character is a group definer and retrieving the closing chararcter.
    private static Dictionary<char, char> GroupsKeys = [];
    //used in GroupSSADialogueStyleUpgrade 
    private static int StyleListCount = 0;//=> StyleList.Count + 1 (plus extra one for default entries not included in StyleList)

    //run at restyler
    internal static void PrepareStyleListExtras()
    {
        StyleListWithPosition = new( StyleList.Count );
        GroupsParams = new GroupParams[StyleList.Count+1];
        GroupsKeys = new( StyleList.Count );
        //group '0' is default group, "IsDialogue=true"
        GroupsParams[ SubFile.DefaultGroupValueForDialogue ] = new GroupParams(
            true,
            ""
        );

        //we will start from 1;
        int i = 1;
        foreach( var gv in StyleList )
        {
            StyleListWithPosition[gv.Key] = new GroupParamsWithPosition(){
                IsDialogue = gv.Value.IsDialogue,
                StyleOverrideCode = gv.Value.StyleOverrideCode,
                Position = i
            };
            GroupsParams[i] = gv.Value;
            GroupsKeys[gv.Key.CS] = gv.Key.CE;

            i++;
        }
        StyleListCount = i;
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
        return group>-1? group+(styleIndex*StyleListCount) :group;
    }
    internal static int GetUpgradedStyleListCount()
    {
        return SSADialogueStylesCount * StyleListCount;
    }
    internal static int GroupSSADialogueStyleDowngrade( int group )
    {
        return group>-1? group%StyleListCount :group;
    }
    internal static GroupParams GetGroupParams( int downGroup )
    {
        return GroupsParams[ downGroup ];
    }
}