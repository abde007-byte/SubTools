using System.Text.RegularExpressions;

namespace SubTools;
public static partial class Recalculator
{
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
            ConsoleHelper.Verbose($"Recalculating: {subFile.FilePath}");
            (int DialoguesAdjusted, int DurationAdded) = subFile.Recalculate( mode, NoOverlap, NoShrink, all, ReverseCharsPerMS, MinDurationMS, MaxDurationMS, GapMS );
            
            totalFiles++;
            totalAdjusted += DialoguesAdjusted;
            TotalMillisecondsAdded += DurationAdded;
        }

        ConsoleHelper.Success($"Completed. Files processed: {totalFiles} | Total entries adjusted: {totalAdjusted}    | Total seconds added: {Math.Round(TotalMillisecondsAdded/1000.0, 3)}");
        
    }
}