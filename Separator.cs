namespace SubTools;
public static class Separator
{
    //separate text enclosed in brackets and parenthesis, separate/split text by line breaks.
    //if entry is white space, keep it
    public static void Separate( SubFile sf, bool Enclosed, bool LineBreaks )
    {
        var newDialogues = new List<DialogueData>( sf._dialogues.Count );
        
        for( int i=0; i<sf._dialogues.Count; i++)
        {
            var dialogue = sf._dialogues[i];
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
                    dialogue._group = SubFile.DefaultGroupValueForNoneDialogue;//*
                    newDialogues.Add( dialogue );
                }
                else
                {
                    foreach( string textLine in separatedTextLines )
                    {
                        newDialogues.Add( new DialogueData(
                            SubFile.DefaultGroupValueForNoneDialogue,//* //in restyle/regroup this will be set!
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
                dialogue._group = SubFile.DefaultGroupValueForNoneDialogue;
                newDialogues.Add( dialogue );
            }
        }

        sf._dialogues = newDialogues;
        sf.DialoguesFinalize();
    }

    public static List<string> SeparateMultiLinedText( List<string> input)
    {
        //separate multi lined
        List<string> tmp = [];
        foreach( string line in input)
        {
            tmp.AddRange( line.Split('\n') );
        }
        return tmp;
    }

    public static List<string> SeparateEnclosedText( string input )
    {
        //break lines and trim output
        // - the only time that we trim is here
        // - in order to remove leftovers of line breaks and white space!

        List<string> output = new(1);
        var enclosedSegment = false;
        char openingChar = ' ';
        char closingChar = ' ';
        int enclosingCount = 0;
        var nextSegmentStartIndex = 0;
        int i;

        for( i = 0; i < input.Length; i++ ){
            char c = input[i];
            if( enclosedSegment ){
                if( c == closingChar ) enclosingCount--; // found closingChar like ], decrease enclosingCount
                else if( c == openingChar ) enclosingCount++; // found closingChar like [, increase enclosingCount
                
                //enclosed segment is complete
                if( enclosingCount == 0 ){
                    //add previews enclosed segment to output, include current closing character
                    //there is no previews segment when i == 0
                    if( i != 0 )
                        if ( input[nextSegmentStartIndex..(i+1)].Trim() is { Length: not 0 } segment )
                            output.Add( segment );
                    
                    //set the next index as next segment starting index
                    nextSegmentStartIndex = i+1;
                    enclosedSegment = false;
                }
            }else{
                //if( (c == '[') || (c == '(') ){
                //closingChar = c switch { '[' => ']', _ => ')' };
                if( Restyler.TryGetGroupKey( c, out closingChar ) ){ 
                    // brackets and parenthesis work as a closers then as an openers!
                    //add previews segment to output, exclude current opening character
                    //if previous segment is enclosed, we will get i == nextSegmentStartIndex
                    if( i != nextSegmentStartIndex )
                        if ( input[nextSegmentStartIndex..i].Trim() is { Length: not 0 } segment )
                            output.Add( segment );

                    //set the current index as next segment starting index, include current opening character
                    nextSegmentStartIndex = i;
                    enclosedSegment = true;
                    openingChar = c;
                    enclosingCount = 1;
                }
            }
        }
        
        //add the rest
        if( i != nextSegmentStartIndex )
            if ( input[nextSegmentStartIndex..i].Trim() is { Length: not 0 } segment )
                output.Add( segment );
        return output;
    }

    public static void Separate( bool Enclosed, bool LineBreaks, string ssaStyles )
    {
        if( FileLoader.SubFiles.Count == 0)
        {
            ConsoleHelper.Warning("need to load files!");
            return;
        }
        
        if( !Enclosed && !LineBreaks )
        {
            ConsoleHelper.Warning("nothing to do! choose an option!");
            return;
        }

        Restyler.PrepareSSADialogueStyles( ssaStyles );
        Restyler.PrepareStyleListExtras();

        int totalFiles = 0;
        int totalDialoguesSeparated = 0;
        for( int i=0; i<FileLoader.SubFiles.Count; i++ )
        {
            SubFile subFile = FileLoader.SubFiles[i];
            var DialoguesCountDelta = subFile.DialoguesCount;
            //Separate
            ConsoleHelper.Verbose($"{subFile.FilePath}", "Separating: ");
            Separate( subFile, Enclosed, LineBreaks );
            DialoguesCountDelta = subFile.DialoguesCount - DialoguesCountDelta;

            ConsoleHelper.Verbose($"\t\tdialogues separated: {DialoguesCountDelta}" ,"");

            totalFiles++;
            totalDialoguesSeparated += DialoguesCountDelta;
        }

        ConsoleHelper.Success($"Files processed: {totalFiles} | Total entries separated: {totalDialoguesSeparated}" ,"Completed. ");
        
    }
}
