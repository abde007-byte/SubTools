namespace SubTools;

public struct DialogueData ( int group, int start, int end, string textLines, string style, string extraString, int extraInteger )
{
    public int _group=group, _start=start, _end=end;
    public string _textLines=textLines;
    public string _style=style, _extra_string=extraString;
    public int _extra_int=extraInteger;
    public static readonly DialogueData Empty = new( SubFile.DefaultGroupValueForNoneDialogue, 0, 0, "", "", "", 0 );
    public void InsertLine(string line)
    {
        if( _textLines.Length != 0 )
            _textLines += '\n';
        _textLines += line;
    }
}

public abstract class SubFile
{
    //List<T> If you exceed capacity, the list Allocates a new array (usually double the size), Copies all elements into the new array, Discards the old array.
    //System.Collections.ArrayList //slow!, stores items as object → boxing occurs for value types.
    //Memory<T> //represents a fixed-size region of memory. no add etc.
    internal List<DialogueData> _dialogues = [];

    internal const int DefaultGroupValueForNoneDialogue = -1;
    internal const int DefaultGroupValueForDialogue = 0;
    internal const string DefaultSSAStyle = "Default";//ssa empty style equals Default
    public virtual string DefaultExtension => "";
    public string? FilePath {get => field??"NONAME"+DefaultExtension; set; }//NONAME should not happen!
    public int DialoguesCount {get => _dialogues.Count;}
    protected void DialoguesInitialize()
    {
        _dialogues = new( Config.InitialDialogueCapacity );
    }
    internal void DialoguesFinalize()
    {
        _dialogues.TrimExcess();
    }
    protected void AddDialogue( DialogueData dialogue )
    {
        //don't add empty entries!
        //merger can't handle 0 length strings, empty lines will merge!
        //if( dialogue._textLines != "" )
            _dialogues.Add( dialogue );
    }
    internal void SortByStartTime()
    {
        _dialogues.Sort((a, b) => a._start.CompareTo(b._start));
        /*for (int i = 0; i < _dialogues.Count-1; i++)
        {
            var lowestValue = _dialogues[i]._start;
            for (int j = i+1; j < _dialogues.Count; j++)
            if ( _dialogues[j]._start < lowestValue )
            {
                lowestValue = _dialogues[j]._start;
                // i <-> j switch positions
                ( _dialogues[i], _dialogues[j] ) = ( _dialogues[j], _dialogues[i] );
            }
        }*/
    }
    public abstract void Load( string filePath );
    public abstract void Save( string filePath, bool removeGroupDefiners );

    internal static List<int>[] GetDialoguesIndicesInGroups( List<DialogueData> inDialogues )
    {
        var DialoguesIndicesInGroupsCount = Restyler.GetUpgradedStyleListCount();
        var DialoguesIndicesInGroups = new List<int>[ DialoguesIndicesInGroupsCount ];
        //setup the first with a capacity equals entries count
        if( DialoguesIndicesInGroupsCount > 0 )
            DialoguesIndicesInGroups[0] = new( inDialogues.Count );
        //the rest have normal empty list
        for( int i = 1; i < DialoguesIndicesInGroupsCount; i++ )
            DialoguesIndicesInGroups[i] = [];

        //fill the Groups of indices
        for( int i = 0; i < inDialogues.Count; i++ )
        {
            var group = inDialogues[i]._group;
            if( group != DefaultGroupValueForNoneDialogue )//skip the 'no groups'
            {
                //now DialoguesIndicesInGroups[g] has index i
                DialoguesIndicesInGroups[group].Add( i );
            }
        }
        return DialoguesIndicesInGroups;
    }

}
