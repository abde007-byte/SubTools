using System.Text;

namespace SubTools;
public static class CollisionFixer
{
    struct CDialogueData
    {
        public int _id,_start,_end,_extra_int;
        public string _textLines,_extra_string;
    }
    
    //collision fixer: reform dialogues in a way that emulates reverse collision.
    public static int CollisionFix( SubFile sf, bool reverse, string topStyles, string topGroups, int scaleDownEffects, int lineSize )
    {
        int collisions = 0;
        var newDialogues = new List<DialogueData>( sf._dialogues.Count );
        var DialoguesIndices = SubFile.GetDialoguesIndicesInGroups( sf._dialogues );

        bool addScaleEffects = scaleDownEffects > 0;

        HashSet<string> topStylesHash = new( topStyles.Split(',') );

        for ( int g = 0; g < DialoguesIndices.Length; g++ )
        {
            var downG = Restyler.GroupSSADialogueStyleDowngrade( g );
            var groupParams = Restyler.GetGroupParams( downG );
            var indicesList = DialoguesIndices[g];
            if ( groupParams.IsDialogue )
            {
                //check if this group's style is in top positioned styles
                string gStyle = "";
                if( indicesList.Count > 0)
                    gStyle = sf._dialogues[ indicesList[0] ]._style;
                if( gStyle == "" )
                    gStyle = SubFile.DefaultSSAStyle;
                bool flipLinePositions = ! topStylesHash.Contains( gStyle );
                //check if this group is in top positioned groups
                flipLinePositions &= ! Restyler.TestGroupID( downG, topGroups);
                //xor reverse
                flipLinePositions ^= reverse;

                //lopp through indices list
                int x = 0;
                while ( x < indicesList.Count )
                {
                    //part 1:
                    //create a cluster of colliding dialogues

                    var aDialogue = sf._dialogues[ indicesList[x] ];
                    aDialogue._textLines = ApplyForcedLineBreaks( aDialogue._textLines, lineSize );
                    x++;//move to next
                    int id = 0;
                    var cluster = new List<CDialogueData> { new(){
                        _id = id,
                        _start=aDialogue._start,
                        _end=aDialogue._end,
                        _textLines=aDialogue._textLines,
                        _extra_string=aDialogue._extra_string,
                        _extra_int=aDialogue._extra_int
                    } };
                    int clusterEnd = aDialogue._end;
                    //start from next
                    while ( x < indicesList.Count)
                    {
                        var nextDialogue = sf._dialogues[ indicesList[x] ];
                        if ( nextDialogue._start < clusterEnd )
                        {
                            id++;
                            cluster.Add( new(){
                                _id = id,
                                _start=nextDialogue._start,
                                _end=nextDialogue._end,
                                _textLines=ApplyForcedLineBreaks( nextDialogue._textLines, lineSize ),
                                _extra_string=nextDialogue._extra_string,
                                _extra_int=nextDialogue._extra_int
                            } );
                            clusterEnd = Math.Max( clusterEnd, nextDialogue._end);
                            //current x is added to cluster, move to next index
                            x++;
                            collisions++;
                        }
                        else
                        {
                            //continue to process current x index in the next loop
                            break;
                        }
                    }

                    if( cluster.Count == 1)
                    {
                        //part 2:
                        //add the no collision dialogue
                        newDialogues.Add( aDialogue );
                    }
                    else
                    {
                        //part 3:
                        //process the cluster of colliding dialogues

                        //create a list of Boundary times, as keyframes
                        var timeKeyFrames = cluster
                            .SelectMany(d => new[] { d._start, d._end })
                            .OrderBy(x => x)
                            .Distinct()
                            .ToList();
                        
                        //create list to record lowest scale seen so far for each CDialogueData
                        int[] clusterDialoguesHighestIndex = [];
                        clusterDialoguesHighestIndex = new int[ cluster.Count ];
                        //for (int i = 0; i < clusterDialoguesHighestIndex.Length; i++) clusterDialoguesHighestIndex[i] = 0;
                        
                        //loop through times keyframes
                        for (int i = 0; i < timeKeyFrames.Count - 1; i++)
                        {
                            int start = timeKeyFrames[i];
                            int end = timeKeyFrames[i + 1];

                            if (start >= end)
                                continue;

                            //select all Dialogues in the cluster within the start/end keyframes
                            var active = cluster
                                .Where(d => d._start <= start && d._end >= end)
                                .ToList();
                            
                            var activeLastIndex = active.Count - 1;
                            if (activeLastIndex == -1)
                                continue;
                            
                            var mainDialogue = active[activeLastIndex];
                            int previousDialogueHighestIndex = 0;
                            var ordered = active.Select( (d, index) =>
                            {
                                var dialogueIndex = activeLastIndex - index;//reverse index order
                                var dialogueHighestIndex = clusterDialoguesHighestIndex[d._id];
                                if( dialogueIndex > dialogueHighestIndex )
                                {
                                        clusterDialoguesHighestIndex[d._id] = dialogueIndex;
                                        dialogueHighestIndex = dialogueIndex;
                                }

                                string result = d._textLines;
                                if( addScaleEffects)
                                {
                                    //calculate scale; latest entry scale = 100%
                                    int scalePercent = Math.Max(0, 100 - ( dialogueHighestIndex * scaleDownEffects ) );
                                    result = $"{{\\fscx{scalePercent}\\fscy{scalePercent}}}{result}";
                                }
                                
                                //add previous empty lines
                                for( int emptyIndex = dialogueHighestIndex + 1; emptyIndex < previousDialogueHighestIndex; emptyIndex++ )
                                {
                                    string emptyLine = "\u200B";
                                    if(addScaleEffects)
                                    {
                                        int scalePercent = Math.Max(0, 100 - ( emptyIndex * scaleDownEffects ) );
                                        emptyLine = $"{{\\fscx{scalePercent}\\fscy{scalePercent}}}{emptyLine}";
                                    }
                                    if ( flipLinePositions )
                                        result += "\n" + emptyLine;
                                    else
                                        result = emptyLine + "\n" + result;
                                }

                                previousDialogueHighestIndex = dialogueHighestIndex;
                                return result;
                            });

                            // if later dialogue should appear on top of earlier dialogue
                            if ( flipLinePositions ) ordered = ordered.Reverse();
                            //overlap segment: merge all active dialogues textLines
                            string mergedText = string.Join("\n", ordered );
                            
                            //add next empty lines
                            if( previousDialogueHighestIndex > 0 )
                            {
                                for( int emptyIndex = previousDialogueHighestIndex - 1; emptyIndex >= 0; emptyIndex-- )
                                {
                                    string emptyLine = "\u200B";
                                    if( addScaleEffects )
                                    {
                                        var scalePercent = Math.Max(0, 100 - ( emptyIndex * scaleDownEffects ) );
                                        emptyLine = $"{{\\fscx{scalePercent}\\fscy{scalePercent}}}{emptyLine}";
                                    }
                                    if( flipLinePositions )
                                        mergedText = emptyLine + "\n" + mergedText ;
                                    else
                                        mergedText += "\n" + emptyLine ;
                                }
                            }
                            
                            newDialogues.Add(new DialogueData(
                                aDialogue._group,
                                start,
                                end,
                                mergedText,
                                aDialogue._style,
                                mainDialogue._extra_string,
                                mainDialogue._extra_int
                            ));
                        }
                    }
                }
            }
            else
            {
                //part 4:
                //add the NoneDialogues
                //lopp through indices list
                for (int x = 0; x < indicesList.Count; x++)
                {
                    newDialogues.Add( sf._dialogues[ indicesList[x] ] );
                }
            }
        }

        //part 5:
        //add the NoneGroups
        foreach (var dialogue in sf._dialogues)
        if( dialogue._group == SubFile.DefaultGroupValueForNoneDialogue )
        {
            newDialogues.Add( dialogue );
        }

        //replace and sort the final list
        sf._dialogues = newDialogues;
        sf.SortByStartTime();
        return collisions;
    }

    public static string ApplyForcedLineBreaks( string input, int lineSize, bool looseLineSize = true )
    {
        if ( string.IsNullOrEmpty( input ) || lineSize <= 0 || input.Contains('\n') )
            return input;

        if ( input.Length <= lineSize )
            return input;

        int parts = (int)Math.Ceiling( (double)input.Length / lineSize );
        int approxLineSize = (int)Math.Ceiling( (double)input.Length / parts );

        var textLines = new List<string>( parts );
        int start = 0;

        while ( start < input.Length )
        {
            // If no whitespace was found, use the chunk boundary
            int breakIndex = input.Length;

            //set Lines count equal calculated parts
            if( looseLineSize && textLines.Count+1 == parts )
            {
                textLines.Add( input.Substring( start, input.Length - start ) );
                break;
            }

            int end = start + approxLineSize;
            if ( end < input.Length )
            {
                // Try forward first
                var limit = Math.Min(start + lineSize, input.Length-1 );
                for ( int i = end; i <= limit; i++ )
                {
                    if ( char.IsWhiteSpace( input[i] ) )
                    {
                        breakIndex = i;
                        break;
                    }
                }
                // If the chunk is still too large, fall back to reverse scan
                if ( breakIndex > limit )
                {
                    for ( int i = end - 1; i > start; i-- )
                    {
                        if ( char.IsWhiteSpace( input[i] ) )
                        {
                            breakIndex = i;
                            break;
                        }
                    }
                }
            }

            // Use the exact slice up to the break
            textLines.Add( input.Substring( start, breakIndex - start ) );

            //set the new start pos
            start = breakIndex+1;
            //remove any additional whitespaces
            while ( start < input.Length && char.IsWhiteSpace( input[start] ) )
                start++;
        }

        return string.Join( "\\N", textLines );//using \N will add to the styling!
    }

    public static void CollisionFix( bool reverse, string topStyles, string topGroups, int scaleDownEffects, int lineSize )
    {
        if( FileLoader.SubFiles.Count == 0)
        {
            ConsoleHelper.Warning("need to load files!");
            return;
        }

        /*
        private static string[] _ScalePercent = [];
        private static int _ScalePercentHigh;
        //create ScalePercent lookup data
        if( scaleDownEffects > 0)
        {
            _ScalePercentHigh = (int)Math.Ceiling( (double)100.0 / scaleDownEffects);
            _ScalePercent = new string[_ScalePercentHigh+1];
            int scalePercent = 100;
            for (int i = 0; i < _ScalePercentHigh; i++)
            {
                _ScalePercent[i] = $"{{\\fscx{scalePercent}\\fscy{scalePercent}}}";
                scalePercent -= scaleDownEffects;
            }
            _ScalePercent[_ScalePercentHigh] = "{\\fscx0\\fscy0}";
        }
        else
        {
            _ScalePercent = [""];
            _ScalePercentHigh = 0;
        }
        */

        int totalFiles = 0;
        int totalCollisions = 0;
        for( int i=0; i<FileLoader.SubFiles.Count; i++ )
        {
            SubFile subFile = FileLoader.SubFiles[i];
            //CollisionFix
            ConsoleHelper.Verbose($"{subFile.FilePath}", "Collision fixing: ");
            var collisions = CollisionFix( subFile, reverse, topStyles, topGroups, scaleDownEffects, lineSize );
            
            ConsoleHelper.Verbose($"\t\tcollisions detected: {collisions}", "");
            totalFiles++;
            totalCollisions += collisions;
        }

        ConsoleHelper.Success($"Files processed: {totalFiles} | Total collisions detected: {totalCollisions}", "Completed. ");
    }
}
