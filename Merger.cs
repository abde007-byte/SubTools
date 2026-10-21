namespace SubTools;
public static class Merger
{
    //Merge subtitles entries
    //maxGap: Maximum Gap between subtitle entries to be merged.
    //maxCharacters: Maximum number of characters of the merged entries.
    //allowUnpunctuated: Merge unpunctuated text.
    //strictTest: use IsCommaLike and Not uppercase, otherwise use notPeriodLike.
    //makeDialogue: Add "-" at the start of each line merged.
    //
    //Merge subtitle entries with the same text.
    public static int MergeSameText( SubFile sf, int maxGap )
    {
        int dialoguesMerged = 0;
        var toRemove = Common.GetEmptyToRemoveList( sf._dialogues.Count );
        //affected by groups.
        //if they have the same text, they are goin to have the same group,
        //but we need to skip unwanted SSA styles!
        var DialoguesIndices = SubFile.GetDialoguesIndicesInGroups( sf._dialogues );

        foreach (var indicesList in DialoguesIndices)
        for ( int x = 0; x < indicesList.Count - 1; x++ )//no need to go to the end
        {
            int i = indicesList[x];
            if( ! toRemove[i] )
            {
                var aDialogue = sf._dialogues[i];
                bool aChanged = false;
                for ( int y = x+1; y < indicesList.Count; y++ )
                {
                    int j = indicesList[y];
                    if( ! toRemove[j] )//skip if index 'j' is to be removed
                    {
                        var bDialogue = sf._dialogues[j];
                        if( ! IsSimilarTimes( maxGap, aDialogue._end, bDialogue._start ) )//tests 'A' end time vs 'B' start time
                            break;//break from 'y:j' loop

                        //test a and b IsSameText
                        if ( IsSameText( aDialogue._textLines, bDialogue._textLines ) )
                        {
                            //pick the longest runtime
                            aDialogue._end = PickLongestRuntime( aDialogue._end, bDialogue._end );
                            aChanged = true;
                            //add dialogue at index 'j', to be removed later!
                            toRemove[j] = true;
                            dialoguesMerged++;
                            //continue to merge
                        }
                    }
                }
                //apply update of dialogue 'a' to List
                if( aChanged ) sf._dialogues[i] = aDialogue;
            }
        }

        //apply removals
        sf._dialogues = Common.RemoveFromList( sf._dialogues, toRemove );

        return dialoguesMerged;
    }
    //Merge subtitle entries with the same display time with a line break,
    //affected by groupings.
    public static int MergeSameTime( SubFile sf, int maxGap, bool makeDialogue )
    {
        int dialoguesMerged = 0;
        var toRemove = Common.GetEmptyToRemoveList( sf._dialogues.Count );
        //affected by groupings.
        var DialoguesIndices = SubFile.GetDialoguesIndicesInGroups( sf._dialogues );

        foreach (var indicesList in DialoguesIndices)
        for ( int x = 0; x < indicesList.Count - 1; x++ )
        {
            int i = indicesList[x];
            if( ! toRemove[i] )
            {
                var aDialogue = sf._dialogues[i];
                bool aChanged = false;
                for ( int y = x+1; y < indicesList.Count; y++)
                {
                    int j = indicesList[y];
                    if( ! toRemove[j] )
                    {
                        var bDialogue = sf._dialogues[j];
                        //this does not test if they are overlapping,
                        //to test for overlap, test if 'B' start time is between 'A' start and end times!
                        if( ! IsSimilarTimes( maxGap, aDialogue._start, bDialogue._start ) )//tests 'A' & 'B' start times
                            break;//break from 'y' loop

                        //test a and b IsSameTime
                        if( AbsDistanceVsGap( maxGap, aDialogue._end, bDialogue._end ) )
                        {
                            //pick the longest runtime
                            aDialogue._end = PickLongestRuntime( aDialogue._end, bDialogue._end );
                            //if Restyler.GroupSSADialogueStyleDowngrade( aDialogue._group ) is used insted of 0, group definers at the center will be stripped!
                            aDialogue._textLines = MergeTextLines( aDialogue._textLines, bDialogue._textLines, 0, makeDialogue );
                            aChanged = true;
                            //add dialogue at index 'j', to be removed later!
                            toRemove[j] = true;
                            dialoguesMerged++;
                            //continue
                        }
                    }
                }
                //apply update of dialogue 'a' to List
                if( aChanged ) sf._dialogues[i] = aDialogue;
            }
        }
        //apply removals
        sf._dialogues = Common.RemoveFromList( sf._dialogues, toRemove );

        return dialoguesMerged;
    }
    //Merge short subtitle text lines for entries marked as dialog,
    //affected by dialogue tag and groupings.
    public static int MergeShortLines( SubFile sf, int maxGap, int maxCharacters, bool unpunctuated, bool strictTest, bool makeDialogue, bool removeLineBreaks )
    {
        int dialoguesMerged = 0;
        var toRemove = Common.GetEmptyToRemoveList( sf._dialogues.Count );
        //affected by dialogue tag and groupings.
        var DialoguesIndices = SubFile.GetDialoguesIndicesInGroups( sf._dialogues );

        for ( int g = 0; g < DialoguesIndices.Length; g++ )
        {
            var downG = Restyler.GroupSSADialogueStyleDowngrade( g );
            var groupParams = Restyler.GetGroupParams( downG );
            var indicesList = DialoguesIndices[g];
            if ( groupParams.IsDialogue )
            //all these belong to the same group
            for ( int x = 0; x < indicesList.Count - 1; x++ )
            {
                int i = indicesList[x];//get Dialogues index
                if( ! toRemove[i] )
                {
                    var aDialogue = sf._dialogues[i];
                    bool aChanged = removeLineBreaks;
                    if( removeLineBreaks)
                        aDialogue._textLines = RemoveLineBreaks( aDialogue._textLines );
                        
                    for ( int y = x+1; y < indicesList.Count; y++ )
                    {
                        int j = indicesList[y];
                        if( ! toRemove[j] )
                        {
                            var bDialogue = sf._dialogues[j];
                            if( ! IsSimilarTimes( maxGap, aDialogue._end, bDialogue._start ) )//tests 'A' end time vs 'B' start time
                                break;//break from 'j' loop
                            
                            if( removeLineBreaks)
                                bDialogue._textLines = RemoveLineBreaks( bDialogue._textLines );

                            if( MergeShortLines( aDialogue._textLines, bDialogue._textLines, downG, maxCharacters, unpunctuated, strictTest, makeDialogue, out string mergedTextLines, out bool aEndCharCanContinue ) )
                            {
                                aDialogue._end = PickLongestRuntime( aDialogue._end, bDialogue._end );
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
                    if( aChanged )
                        sf._dialogues[i] = aDialogue;
                }
            }
        }
        //apply removals
        sf._dialogues = Common.RemoveFromList( sf._dialogues, toRemove );
        return dialoguesMerged;
    }

    //public const string CommaChars = ",，、،";//usage: CommaChars.Contains(ch)
    private static bool IsCommaLike(char ch) => ch switch { ',' or '，' or '،' or '、' => true, _ => false };
    private static bool NotPeriodLike(char ch) => ch switch { '.' or '。' or '?' or '？' or '؟' or '!' or '！' or ';' or '；' or '؛' or ';' or '…' => false, _ => true };
    
    internal static string RemoveLineBreaks( string input )
    {
        char[] inputChars = input.ToCharArray();
        int topMinus = inputChars.Length - 1;
        for( int i=0; i<topMinus; i++ )
            if( (inputChars[i] == '\n') && (inputChars[i+1] != '-') )
                inputChars[i] = ' ';//replace with space
        return new string(inputChars);
    }
    private static bool MergeShortLines( string aTextLines, string bTextLines, int downGroup, int maxCharacters, bool unpunctuated, bool strictTest, bool makeDialogue, out string mergedTextLines, out bool AEndCharCanContinue )
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
    private static string MergeTextLines( string aTextLines, string bTextLines, int downGroup, bool MakeDialogue, char JoinBy = '\n' )
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
    private static bool IsSameText( string aTextLines, string bTextLines )
    {
        //exact copy of textlines, i don't need anything else!
        return aTextLines == bTextLines;
    }
    private static bool IsSimilarTimes( int maxGap, int a, int b )
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
    private static bool AbsDistanceVsGap( int maxGap, int a, int b )
    {
        //distance of 'b' from 'a'
        int delta = b - a;
        return delta <= maxGap && delta >= - maxGap;
        //alternatively use Math.Abs(delta)
    }
    private static int PickLongestRuntime( int aEnd, int bEnd ){
        //start time 'a' is always less or equal to 'b' 
        return (aEnd > bEnd)? aEnd : bEnd;
    }
    public static void Merge( byte command, double maxGap, int maxCharacters, bool unpunctuated, bool strictTest, bool makeDialogue, bool removeLineBreaks )
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
            ConsoleHelper.Verbose($"{subFile.FilePath}", $"Merging {CommandText}: ");
            int DialoguesMerged = command switch
            {
                //Merge same text
                1 => MergeSameText( subFile, maxGapMS ),
                //Merge same time
                2 => MergeSameTime( subFile, maxGapMS, makeDialogue ),
                //Merge short lines
                _ => MergeShortLines( subFile, maxGapMS, maxCharacters, unpunctuated, strictTest, makeDialogue, removeLineBreaks )
            };
            ConsoleHelper.Verbose($"\t\tdialogues merged: {DialoguesMerged}", "");
            totalDialoguesMerged += DialoguesMerged;
            totalFiles++;
        }
        ConsoleHelper.Success($"Files processed: {totalFiles} | Total entries merged: {totalDialoguesMerged}" ,$"Merging {CommandText} Completed. ");
    }

}
