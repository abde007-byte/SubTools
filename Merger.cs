namespace SubTools;
public static class Merger
{
    //public const string CommaChars = ",，、،";//usage: CommaChars.Contains(ch)
    public static bool IsCommaLike(char ch) => ch switch { ',' or '，' or '،' or '、' => true, _ => false };
    public static bool NotPeriodLike(char ch) => ch switch { '.' or '。' or '?' or '？' or '؟' or '!' or '！' or ';' or '；' or '؛' or ';' or '…' => false, _ => true };
    public static bool MergeShortLines( string aTextLines, string bTextLines, int downGroup, int maxCharacters, bool unpunctuated, bool strictTest, bool makeDialogue, out string mergedTextLines, out bool AEndCharCanContinue )
    {
        //to skip group definers and find correct char positions:
        // first : 0 , last : ^1 , second : 1 , second last : ^2
        char bStart;
        char aEnd;
        bool bStartISpace;
        if( downGroup>0 )
        {
            bStart = bTextLines[1];
            aEnd = aTextLines[^2];
            bStartISpace = bTextLines.Length>2 && bTextLines[1..3]=="I ";//second and third
        }
        else
        {
            bStart = (bTextLines.Length > 0)?bTextLines[0]:' ';
            aEnd = (aTextLines.Length > 0)?aTextLines[^1]:' ';
            bStartISpace = bTextLines.Length>1 && (bTextLines[..2]=="I " || bTextLines[..2]=="I'");//first and second
        }
        
        bool AB_CanMerge;
        if ( strictTest)
        {
            //STRICT TEST! - text 'A' last char is a comma
            AEndCharCanContinue = IsCommaLike( aEnd );
            //AND text 'B' first char is not uppercase, with "I " as an exception
            AB_CanMerge = AEndCharCanContinue && ( ! char.IsUpper(bStart) || bStartISpace );
        }
        else
        {
            AEndCharCanContinue = NotPeriodLike( aEnd );
            AB_CanMerge = AEndCharCanContinue;
        }
        
        if(
            (
            //check if texts can be merged, for properly punctuated text, or if it can allow unpunctuated is used
                unpunctuated || AB_CanMerge 
            ) && (
            //sum of strings length is no more than MaxCharacters
                ( aTextLines.Length + bTextLines.Length ) <= maxCharacters
            )
        )
        {
            mergedTextLines = MergeTextLines( aTextLines, bTextLines, downGroup, makeDialogue, ' ' );//use space to make it look like a one continuous text line
            return true;
        }
        else
        {
            mergedTextLines = "";
            return false;
        }
    }
    public static string MergeTextLines( string aTextLines, string bTextLines, int downGroup, bool MakeDialogue, char JoinBy = '\n' )
    {
        if( downGroup > 0)
        {
            //trim group deffiner, [ startpos .. endpos ] "endpos not included in output string! - ^1 is last char - "
            if( MakeDialogue )
                return aTextLines[0]+'-'+aTextLines[1..^1] + '\n' + '-'+bTextLines[1..];
            else
                return aTextLines[..^1] + JoinBy + bTextLines[1..];
        }
        else
        {
            if( MakeDialogue )
                return '-'+aTextLines + '\n' + '-'+bTextLines; //subtitle edit uses "- " but i prefer "-" no extra space
            else
                return aTextLines + JoinBy + bTextLines;
        }
    }
    public static bool IsSameText( string aTextLines, string bTextLines )
    {
        //exact copy of textlines, i don't need anything else!
        return aTextLines == bTextLines;
    }
    public static bool IsSimilarTimes( int maxGap, int a, int b )
    {
        //close or overlapping
        //works when comparing :
        // - a[start] & b[start] | 'b'>='a' | 'b'-'a'>=0 | same result as AbsDistanceVsGap
        // - a[End] & b[Start] 'b' could be less than 'a' which means they are overlapping and should return true!
        return b - a <= maxGap;
        //since subtitle entries are sorted by startTime,
        //if current entry fails this test future entries are goin to fail too!
        //if in a loop, we should break!
    }
    public static bool AbsDistanceVsGap( int maxGap, int a, int b )
    {
        //distance of 'b' from 'a'
        int delta = b - a;
        return delta <= maxGap && delta >= - maxGap;
        //alternatively use Math.Abs(delta)
    }
    public static int PickLongestRuntime( int aEnd, int bEnd ){
        //start time 'a' is always less or equal to 'b' 
        return (aEnd > bEnd)? aEnd : bEnd;
    }
    public static void Merge( byte command, double maxGap, int maxCharacters, bool unpunctuated, bool strictTest, bool makeDialogue )
    {
        if( FileLoader.SubFiles.Count == 0)
        {
            ConsoleHelper.Warning("need to load files!");
            return;
        }
        
        int maxGapMS = (int)(maxGap * 1000);

        int totalFiles = 0;
        int totalDialoguesMerged = 0;
        string CommandText = command switch{ 1=>"Same Text",  2=>"Same Time", _=>"Short Lines" };
        for( int i=0; i<FileLoader.SubFiles.Count; i++ )
        {
            SubFile subFile = FileLoader.SubFiles[i];
            ConsoleHelper.Verbose($"Merging {CommandText} : {subFile.FilePath}");
            totalDialoguesMerged += command switch
            {
                //Merge same text
                1 => subFile.MergeSameText( maxGapMS ),
                //Merge same time
                2 => subFile.MergeSameTime( maxGapMS, makeDialogue ),
                //Merge short lines
                _ => subFile.MergeShortLines( maxGapMS, maxCharacters, unpunctuated, strictTest, makeDialogue )
            };
            totalFiles++;
        }
        ConsoleHelper.Success($"Merging {CommandText} Completed. Files processed: {totalFiles} | Total entries merged: {totalDialoguesMerged}");
    }

}
