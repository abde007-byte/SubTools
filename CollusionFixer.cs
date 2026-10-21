namespace SubTools;
public static class CollusionFixer
{
    
    //collusion fixer: reform dialogues in a way that emulates reverse collusion.
    public static int CollusionFix( SubFile sf, bool reverse )
    {
        int collusions = 0;
        var newDialogues = new List<DialogueData>( sf._dialogues.Count );
        var DialoguesIndices = SubFile.GetDialoguesIndicesInGroups( sf._dialogues );

        for ( int g = 0; g < DialoguesIndices.Length; g++ )
        {
            var downG = Restyler.GroupSSADialogueStyleDowngrade( g );
            var groupParams = Restyler.GetGroupParams( downG );
            var indicesList = DialoguesIndices[g];
            if ( groupParams.IsDialogue )
            {
                int x = 0;
                while ( x < indicesList.Count )
                {
                    //part 1:
                    //create a cluster of colliding dialogues

                    var aDialogue = sf._dialogues[ indicesList[x] ];
                    x++;//move to next
                    var cluster = new List<DialogueData> { aDialogue };
                    int clusterEnd = aDialogue._end;
                    //start from next
                    while ( x < indicesList.Count)
                    {
                        var nextDialogue = sf._dialogues[ indicesList[x] ];
                        if ( nextDialogue._start < clusterEnd )
                        {
                            cluster.Add( nextDialogue );
                            clusterEnd = Math.Max( clusterEnd, nextDialogue._end);
                            //current x is added to cluster, move to next index
                            x++;
                            collusions++;
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

                            if (active.Count == 0)
                                continue;
                            
                            var fd = active[0];//firstDialogue
                            if (active.Count == 1)
                            {
                                //add single
                                newDialogues.Add(new DialogueData(
                                    fd._group,
                                    start,
                                    end,
                                    fd._textLines,
                                    fd._style,
                                    fd._extra_string,
                                    fd._extra_int
                                ));
                            }
                            else
                            {
                                var ordered = active.Select(d => d._textLines);
                                // if later dialogue should appear before earlier
                                if ( ! reverse ) ordered = ordered.Reverse(); 
                                //overlap segment: merge all active dialogues textLines
                                string mergedText = string.Join("\n", ordered );
                                
                                newDialogues.Add(new DialogueData(
                                    fd._group,
                                    start,
                                    end,
                                    mergedText,
                                    fd._style,
                                    fd._extra_string,
                                    fd._extra_int
                                ));
                            }
                        }
                    }
                }
            }
        }

        //part 4:
        //add the NoneDialogues
        foreach (var dialogue in sf._dialogues)
        if( dialogue._group == SubFile.DefaultGroupValueForNoneDialogue )
        {
            newDialogues.Add( dialogue );
        }

        //replace and sort the final list
        sf._dialogues = newDialogues;
        sf.SortByStartTime();
        return collusions;
    }

    public static void CollusionFix( bool reverse )
    {
        if( FileLoader.SubFiles.Count == 0)
        {
            ConsoleHelper.Warning("need to load files!");
            return;
        }

        int totalFiles = 0;
        int totalCollusions = 0;
        for( int i=0; i<FileLoader.SubFiles.Count; i++ )
        {
            SubFile subFile = FileLoader.SubFiles[i];
            //CollusionFix
            ConsoleHelper.Verbose($"{subFile.FilePath}", "Collusion fixing: ");
            var collusions = CollusionFix( subFile, reverse );
            
            ConsoleHelper.Verbose($"\t\tcollusions detected: {collusions}", "");
            totalFiles++;
            totalCollusions += collusions;
        }

        ConsoleHelper.Success($"Files processed: {totalFiles} | Total collusions detected: {totalCollusions}", "Completed. ");
    }
}
