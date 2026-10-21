using System.Text.RegularExpressions;

namespace SubTools;
public static partial class Recalculator
{
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
    public static ( int DialoguesAdjusted, int DurationAdded ) Recalculate( SubFile sf, Config.RecalculateModeType Mode, bool NoOverlap, bool NoShrink, bool All, double ReverseCharsPerMS, int MinDurationMS, int MaxDurationMS, int GapMS  )
    {
        int dialoguesAdjusted = 0;
        int durationAdded = 0;

        //affected by groupings and dialogue tag.
        var DialoguesIndices = SubFile.GetDialoguesIndicesInGroups( sf._dialogues );

        for ( int g = 0; g < DialoguesIndices.Length; g++ )
        {
            var downG = Restyler.GroupSSADialogueStyleDowngrade( g );
            var groupParams = Restyler.GetGroupParams( downG );
            var indicesList = DialoguesIndices[g];
            if ( groupParams.IsDialogue || All )
            //all these belong to the same group
            for ( int x = 0; x < indicesList.Count; x++ )
            {
                int i = indicesList[x];//get Dialogues index
                var dialogue = sf._dialogues[i];
                var neededDuration = CalculateNeededDuration( dialogue._start, dialogue._end, dialogue._textLines, ReverseCharsPerMS, MinDurationMS, MaxDurationMS );
                if( neededDuration != 0 )
                {
                    //in extend mode, make sure Desired End is not overlapping the incoming dialogue + gap
                    if ( Mode == Config.RecalculateModeType.Extend ) {
                        if ( NoOverlap && ( x+1 < indicesList.Count ) ) {
                            neededDuration = CalculateNeededDurationNoOverlap( neededDuration, dialogue._start, dialogue._end, sf._dialogues[indicesList[x+1]]._start, GapMS );
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
                        sf._dialogues[i] = dialogue;
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
                            var nextDialogue = sf._dialogues[j];
                            int Shift = CalculateNeededShift( sf._dialogues[ indicesList[y-1] ]._end, nextDialogue._start, GapMS );
                            if ( Shift > 0 )
                            {
                                nextDialogue._start += Shift;
                                nextDialogue._end += Shift;
                                sf._dialogues[j] = nextDialogue;
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

    //returns the amount of time to add/subtract to the current dialogue End, based on the number of visible characters and the configured min/max duration and chars per second
    public static int CalculateNeededDuration( int Start, int End, string Text, double ReverseCharsPerMS, int MinDurationMS, int MaxDurationMS )
    {
        int CharsCount = VisibleCharsCount( Text );
        //set Minimum Duration
        int RequiredDuration = Math.Max( MinDurationMS, (int)(CharsCount * ReverseCharsPerMS ) );
        //and Maximum Duration, if MaxDurationMS =< 0 then RequiredDuration is uncapped
        if ( MaxDurationMS > 0 ) RequiredDuration = Math.Min( MaxDurationMS, RequiredDuration);
        int CurrentDuration = End - Start;
        int NeededDuration = RequiredDuration - CurrentDuration;

        return NeededDuration;
    }

    //returns the amount of time to add to the next dialogue Start and End, to shift the dialogue forward in time, to avoid overlap with the current dialogue + gap
    public static int CalculateNeededShift( int PreviousEnd, int CurrentStart, int GapMS )
    {
        int MinNextStart = PreviousEnd + GapMS;
        if ( CurrentStart < MinNextStart )
            return MinNextStart - CurrentStart; //value allways positive.
        else
            return 0;
    }

    public static int CalculateNeededDurationNoOverlap( int neededDuration, int Start, int End, int nextStart, int GapMS )
    {
        int MaxEnd = nextStart - GapMS;
        int ExpectedEnd = End + neededDuration;
        if ( ExpectedEnd > MaxEnd ) {
            neededDuration = MaxEnd - End;
            //check if applying neededDuration would make the End time less than Start time, if so, then limit neededDuration to make End = Start
            //its possible only because of gap!
            if ( ExpectedEnd < Start)
            {
                neededDuration = Start - End;
            }
        }
        return neededDuration;
    }


    [GeneratedRegex(@"<[^>]+>|\{[^}]+\}|^\s+|\s+$|(?<=\s)\s+", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex Text_Cleaning_RegEX();
    public static int VisibleCharsCount(string Text) => Text_Cleaning_RegEX().Replace( Text, "").Length;

    public static void Recalculate( Config.RecalculateModeType mode, bool overlap, bool shrink, bool all, double charsPerSecond, double minDuration, double maxDuration, double gap )
    {
        if( FileLoader.SubFiles.Count == 0)
        {
            ConsoleHelper.Warning("need to load files!");
            return;
        }

        bool NoOverlap = ! overlap;
        bool NoShrink = ! shrink;
        double ReverseCharsPerMS = 1000.0 / charsPerSecond;
        int MinDurationMS = (int)(minDuration * 1000);
        int MaxDurationMS = (int)(maxDuration * 1000);
        int GapMS = (int)(gap * 1000);

        int totalFiles = 0;
        int totalAdjusted = 0;
        int TotalMillisecondsAdded = 0;
        foreach( SubFile subFile in FileLoader.SubFiles )
        {
            //Recalculate
            ConsoleHelper.Verbose($"{subFile.FilePath}", "Recalculating: ");
            (int DialoguesAdjusted, int DurationAdded) = Recalculate( subFile, mode, NoOverlap, NoShrink, all, ReverseCharsPerMS, MinDurationMS, MaxDurationMS, GapMS );
            
            ConsoleHelper.Verbose($"\t\tdialogues adjusted: {DialoguesAdjusted}\t\tseconds added: {Math.Round(DurationAdded/1000.0, 3)}", "");
            totalFiles++;
            totalAdjusted += DialoguesAdjusted;
            TotalMillisecondsAdded += DurationAdded;
        }

        ConsoleHelper.Success($"Files processed: {totalFiles} | Total entries adjusted: {totalAdjusted}    | Total seconds added: {Math.Round(TotalMillisecondsAdded/1000.0, 3)}", "Completed. ");
        
    }
}