using System.Text;
using System.Text.RegularExpressions;
namespace SubTools;

public struct DialogueData ( int group, int start, int end, string textLines, string style, string extraString, int extraInteger )
{
    public int _group=group, _start=start, _end=end;
    public string _textLines=textLines, _style=style, _extra_string=extraString;
    public int _extra_int=extraInteger;
    public static readonly DialogueData Empty = new( SubFile.DefaultGroupValueForNoneDialogue, 0, 0, "", "", "", 0 );
    public void InsertLine(string line)
    {
        if( _textLines.Length != 0 ) _textLines += '\n';
        _textLines += line;
    }
}

public abstract class SubFile
{
    //List<T> If you exceed capacity, the list Allocates a new array (usually double the size), Copies all elements into the new array, Discards the old array.
    //System.Collections.ArrayList //slow!, stores items as object → boxing occurs for value types.
    //Memory<T> //represents a fixed-size region of memory. no add etc.
    protected List<DialogueData> _dialogues = [];

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
    protected void DialoguesFinalize()
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
    protected void SortByStartTime()
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
    public abstract void Save( string filePath, bool removeLineBreaks, bool removeGroupDefiners );

    //separate text enclosed in brackets and parenthesis, separate/split text by line breaks.
    //if entry is white space, keep it
    public void Separate( bool Enclosed, bool LineBreaks )
    {
        var newDialogues = new List<DialogueData>( _dialogues.Count );
        
        for( int i=0; i<_dialogues.Count; i++)
        {
            var dialogue = _dialogues[i];
            if( Restyler.IsSSADialogueStyle( dialogue._style ) )
            {
                List<string> separatedTextLines;
                if( Enclosed ){
                    //separate enclosed text
                    separatedTextLines = Separator.SeparateEnclosedText( dialogue._textLines );
                }
                else
                {
                    separatedTextLines = [dialogue._textLines];
                }
                if( LineBreaks ){
                    //separate multi lined text
                    separatedTextLines = Separator.SeparateMultiLinedText( separatedTextLines );
                }

                //add unmodified textLines, in case TextLines is empty space separatedTextLines will be empty
                if ( separatedTextLines.Count <= 1 )
                {
                    //included in SSADialogueStyles
                    dialogue._group = DefaultGroupValueForNoneDialogue;//*
                    newDialogues.Add( dialogue );
                }
                else
                {
                    foreach( string textLine in separatedTextLines )
                    {
                        newDialogues.Add( new DialogueData(
                            DefaultGroupValueForNoneDialogue,//* //in restyle/regroup this will be set!
                            dialogue._start,
                            dialogue._end,
                            textLine,
                            dialogue._style,
                            dialogue._extra_string,
                            dialogue._extra_int
                        ));
                    }
                }
            }
            else
            {
                //not included in SSADialogueStyles
                dialogue._group = DefaultGroupValueForNoneDialogue;
                newDialogues.Add( dialogue );
            }
        }

        _dialogues = newDialogues;
        DialoguesFinalize();
    }
    //Use styling list to add 'override styling' and 'dialogue tag' for subtitles entries.
    public int Regroup( string ssaStyles )
    {
        int dialoguesRestyled = 0;
        for( int i=0; i<_dialogues.Count; i++)
        {
            var dialogue = _dialogues[i];
            var newGroup = DefaultGroupValueForNoneDialogue;

            if( Restyler.TryGetSSADialogueStyleIndex( dialogue._style, out int index ))
            {
                newGroup = Restyler.GetStyleGroup( dialogue._textLines );
                newGroup = Restyler.GroupSSADialogueStyleUpgrade( newGroup, index );
            }
            
            //update only if group is changed
            if( newGroup != dialogue._group )
            {
                dialogue._group = newGroup;
                _dialogues[i] = dialogue;
                dialoguesRestyled++;
            }
        }
        return dialoguesRestyled;
    }
    private List<int>[] GetDialoguesIndicesInGroups()
    {
        var DialoguesIndicesInGroupsCount = Restyler.GetUpgradedStyleListCount();
        var DialoguesIndicesInGroups = new List<int>[ DialoguesIndicesInGroupsCount ];
        //setup the first with a capacity equals entries count
        if( DialoguesIndicesInGroupsCount > 0 )
            DialoguesIndicesInGroups[0] = new( _dialogues.Count );
        //the rest have normal empty list
        for( int i = 1; i < DialoguesIndicesInGroupsCount; i++ )
            DialoguesIndicesInGroups[i] = [];

        //fill the Groups of indices
        for( int i = 0; i < _dialogues.Count; i++ )
        {
            var group = _dialogues[i]._group;
            if( group != DefaultGroupValueForNoneDialogue )//skip the 'no groups'
            {
                //now DialoguesIndicesInGroups[g] has index i
                DialoguesIndicesInGroups[group].Add( i );
            }
        }
        return DialoguesIndicesInGroups;
    }

    //Merge subtitles entries
    //maxGap: Maximum Gap between subtitle entries to be merged.
    //maxCharacters: Maximum number of characters of the merged entries.
    //allowUnpunctuated: Merge unpunctuated text.
    //strictTest: use IsCommaLike and Not uppercase, otherwise use notPeriodLike.
    //makeDialogue: Add "-" at the start of each line merged.
    //
    //Merge subtitle entries with the same text.
    public int MergeSameText( int maxGap )
    {
        int dialoguesMerged = 0;
        var toRemove = Common.GetEmptyToRemoveList( _dialogues.Count );
        //affected by groups.
        //if they have the same text, they are goin to have the same group,
        //but we need to skip unwanted SSA styles!
        var DialoguesIndices = GetDialoguesIndicesInGroups();

        foreach (var indicesList in DialoguesIndices)
        for ( int x = 0; x < indicesList.Count - 1; x++ )//no need to go to the end
        {
            int i = indicesList[x];
            if( ! toRemove[i] )
            {
                var aDialogue = _dialogues[i];
                bool aChanged = false;
                for ( int y = x+1; y < indicesList.Count; y++ )
                {
                    int j = indicesList[y];
                    if( ! toRemove[j] )//skip if index 'j' is to be removed
                    {
                        var bDialogue = _dialogues[j];
                        if( ! Merger.IsSimilarTimes( maxGap, aDialogue._end, bDialogue._start ) )//tests 'A' end time vs 'B' start time
                            break;//break from 'y:j' loop

                        //test a and b IsSameText
                        if ( Merger.IsSameText( aDialogue._textLines, bDialogue._textLines ) )
                        {
                            //pick the longest runtime
                            aDialogue._end = Merger.PickLongestRuntime( aDialogue._end, bDialogue._end );
                            aChanged = true;
                            //add dialogue at index 'j', to be removed later!
                            toRemove[j] = true;
                            dialoguesMerged++;
                            //continue to merge
                        }
                    }
                }
                //apply update of dialogue 'a' to List
                if( aChanged ) _dialogues[i] = aDialogue;
            }
        }

        //apply removals
        _dialogues = Common.RemoveFromList( _dialogues, toRemove );

        return dialoguesMerged;
    }
    //Merge subtitle entries with the same display time with a line break,
    //affected by groupings.
    public int MergeSameTime( int maxGap, bool makeDialogue )
    {
        int dialoguesMerged = 0;
        var toRemove = Common.GetEmptyToRemoveList( _dialogues.Count );
        //affected by groupings.
        var DialoguesIndices = GetDialoguesIndicesInGroups();

        foreach (var indicesList in DialoguesIndices)
        for ( int x = 0; x < indicesList.Count - 1; x++ )
        {
            int i = indicesList[x];
            if( ! toRemove[i] )
            {
                var aDialogue = _dialogues[i];
                bool aChanged = false;
                for ( int y = x+1; y < indicesList.Count; y++)
                {
                    int j = indicesList[y];
                    if( ! toRemove[j] )
                    {
                        var bDialogue = _dialogues[j];
                        //this does not test if they are overlapping,
                        //to test for overlap, test if 'B' start time is between 'A' start and end times!
                        if( ! Merger.IsSimilarTimes( maxGap, aDialogue._start, bDialogue._start ) )//tests 'A' & 'B' start times
                            break;//break from 'y' loop

                        //test a and b IsSameTime
                        if( Merger.AbsDistanceVsGap( maxGap, aDialogue._end, bDialogue._end ) )
                        {
                            //pick the longest runtime
                            aDialogue._end = Merger.PickLongestRuntime( aDialogue._end, bDialogue._end );
                            //if Restyler.GroupSSADialogueStyleDowngrade( aDialogue._group ) is used insted of 0, group definers at the center will be stripped!
                            aDialogue._textLines = Merger.MergeTextLines( aDialogue._textLines, bDialogue._textLines, 0, makeDialogue );
                            aChanged = true;
                            //add dialogue at index 'j', to be removed later!
                            toRemove[j] = true;
                            dialoguesMerged++;
                            //continue
                        }
                    }
                }
                //apply update of dialogue 'a' to List
                if( aChanged ) _dialogues[i] = aDialogue;
            }
        }
        //apply removals
        _dialogues = Common.RemoveFromList( _dialogues, toRemove );

        return dialoguesMerged;
    }
    //Merge short subtitle text lines for entries marked as dialog,
    //affected by dialogue tag and groupings.
    public int MergeShortLines( int maxGap, int maxCharacters, bool unpunctuated, bool strictTest, bool makeDialogue )
    {
        int dialoguesMerged = 0;
        var toRemove = Common.GetEmptyToRemoveList( _dialogues.Count );
        //affected by dialogue tag and groupings.
        var DialoguesIndices = GetDialoguesIndicesInGroups();

        for ( int g = 0; g < DialoguesIndices.Length; g++ )
        {
            var downG = Restyler.GroupSSADialogueStyleDowngrade( g );
            var groupParams = Restyler.GetGroupParams( downG );
            var indicesList = DialoguesIndices[g];
            //all these belong to the same group
            if ( groupParams.IsDialogue )
            //all these belong to the same group
            for ( int x = 0; x < indicesList.Count - 1; x++ )
            {
                int i = indicesList[x];//get Dialogues index
                if( ! toRemove[i] )
                {
                    var aDialogue = _dialogues[i];
                    bool aChanged = false;
                    for ( int y = x+1; y < indicesList.Count; y++ )
                    {
                        int j = indicesList[y];
                        if( ! toRemove[j] )
                        {
                            var bDialogue = _dialogues[j];
                            if( ! Merger.IsSimilarTimes( maxGap, aDialogue._end, bDialogue._start ) )//tests 'A' end time vs 'B' start time
                                break;//break from 'j' loop
                            
                            if( Merger.MergeShortLines( aDialogue._textLines, bDialogue._textLines, downG, maxCharacters, unpunctuated, strictTest, makeDialogue, out string mergedTextLines, out bool aEndCharCanContinue ) )
                            {
                                aDialogue._end = Merger.PickLongestRuntime( aDialogue._end, bDialogue._end );
                                aDialogue._textLines = mergedTextLines;
                                aChanged = true;
                                toRemove[j] = true;
                                dialoguesMerged++;
                            }
                            //if text A doesn't end with a comma and unpunctuated text is not allowed, no need to continue.
                            if( ! aEndCharCanContinue && ! unpunctuated )
                                break;
                        }
                    }
                    //apply update of dialogue 'a' to List
                    if( aChanged ) _dialogues[i] = aDialogue;
                }
            }
        }
        //apply removals
        _dialogues = Common.RemoveFromList( _dialogues, toRemove );
        return dialoguesMerged;
    }

    //Recalculate subtitles entries display durations, based on character count,
    //only for entries tagged as dialog, unless All option is used,
    //and affected by groupings.
    //Modes: Extend, Shift (push later subtitles).
    //!Overlap: allow overlap in Extend mode.
    //!Shrink: if display durration is larger than needed, it will be reduced.
    //All: recalculate durration of all entries, otherwise only Dialogue ones.
    //ReverseCharsPerMS: 1000/(Characters per second).
    //MinDurationMS: Minimum display time duration in milliseconds.
    //MaxDurationMS: Maximum display time duration in milliseconds.
    //GapMS: Gap between dialogues in milliseconds.
    public ( int DialoguesAdjusted, int DurationAdded ) Recalculate( Config.RecalculateModeType Mode, bool NoOverlap, bool NoShrink, bool All, double ReverseCharsPerMS, int MinDurationMS, int MaxDurationMS, int GapMS  )
    {
        int dialoguesAdjusted = 0;
        int durationAdded = 0;

        //affected by groupings and dialogue tag.
        var DialoguesIndices = GetDialoguesIndicesInGroups();

        for ( int g = 0; g < DialoguesIndices.Length; g++ )
        {
            var downG = Restyler.GroupSSADialogueStyleDowngrade( g );
            var groupParams = Restyler.GetGroupParams( downG );
            var indicesList = DialoguesIndices[g];
            //all these belong to the same group
            if ( groupParams.IsDialogue || All )
            //all these belong to the same group
            for ( int x = 0; x < indicesList.Count; x++ )
            {
                int i = indicesList[x];//get Dialogues index
                var dialogue = _dialogues[i];
                var neededDuration = Recalculator.CalculateNeededDuration( dialogue._start, dialogue._end, dialogue._textLines, ReverseCharsPerMS, MinDurationMS, MaxDurationMS );
                if( neededDuration != 0 )
                {
                    //in extend mode, make sure Desired End is not overlapping the incoming dialogue + gap
                    if ( Mode == Config.RecalculateModeType.Extend ) {
                        if ( NoOverlap && ( x+1 < indicesList.Count ) ) {
                            neededDuration = Recalculator.CalculateNeededDurationNoOverlap( neededDuration, dialogue._start, dialogue._end, _dialogues[indicesList[x+1]]._start, GapMS );
                        }
                    }

                    //if Shrink is disabled, then don't allow negative Need
                    if ( ( neededDuration < 0 ) && NoShrink )
                        neededDuration = 0;

                    //apply changes
                    if( neededDuration != 0)
                    {
                        //can't do _dialogues[i]._end += ... because the indexer returns a copy of the struct, not a reference to it. So we need to assign it back after modifying it.
                        dialogue._end += neededDuration;
                        _dialogues[i] = dialogue;
                        dialoguesAdjusted++;
                        durationAdded += neededDuration;
                    }

                    if ( Mode == Config.RecalculateModeType.Shift )
                    {
                        //loop through dialogues and shift the next dialogue forward, break loop if shit is zero.
                        //the first Previous is the original Current!
                        int y = x + 1;
                        while ( y < indicesList.Count )
                        {
                            var j = indicesList[y];//get Dialogues index
                            var nextDialogue = _dialogues[j];
                            int Shift = Recalculator.CalculateNeededShift( _dialogues[ indicesList[y-1] ]._end, nextDialogue._start, GapMS );
                            if ( Shift > 0 )
                            {
                                nextDialogue._start += Shift;
                                nextDialogue._end += Shift;
                                _dialogues[j] = nextDialogue;
                                y++;//next index of indicesOfGroup
                            }
                            else
                            {
                                break; // No more shifting needed
                            }
                        }
                    }
                }
            }
        }
        return ( dialoguesAdjusted, durationAdded );
    }    

}
