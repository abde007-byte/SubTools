using System.Text.RegularExpressions;
namespace SubTools;
public partial class SubFileSRT : SubFile
{
    public override string DefaultExtension => ".srt";
    public override void Load( string filePath)
    {
        //load srt/vtt file format subtitle entries
        //by default all entries are tagged as Dialogue
        FilePath = filePath;

        DialoguesInitialize();

        bool isDialogue = false;
        DialogueData newDialogue = DialogueData.Empty;
        
        foreach( string line in File.ReadLines( filePath ) )
        {
            if( isDialogue )
            {
                if( line.Length == 0 )
                {
                    AddDialogue( newDialogue );
                    isDialogue = false;
                }
                else
                {
                    newDialogue.InsertLine( line );//trim option! - DO NOT TRIM -
                }
            }
            //lines that follow after a match are dialogue text-lines,
            //until an empty line is found, then the dialogue is closed.
            else
            {
                Match match = SRTDialogueRegex().Match( line );
                if (match.Success)
                {
                    newDialogue = new( DefaultGroupValueForNoneDialogue,
                        /*Start = */Common.GetMilliseconds(
                            match.Groups["start_n"].Success,
                            match.Groups["start_h"].Success ? int.Parse(match.Groups["start_h"].Value) : 0,
                            match.Groups["start_m"].Success ? int.Parse(match.Groups["start_m"].Value) : 0,
                            int.Parse(match.Groups["start_s"].Value),
                            int.Parse(match.Groups["start_ms"].Value)
                        ),
                        /*End = */Common.GetMilliseconds(
                            match.Groups["end_n"].Success,
                            match.Groups["end_h"].Success ? int.Parse(match.Groups["end_h"].Value) : 0,
                            match.Groups["end_m"].Success ? int.Parse(match.Groups["end_m"].Value) : 0,
                            int.Parse(match.Groups["end_s"].Value),
                            int.Parse(match.Groups["end_ms"].Value)
                        ),
                        /*TextLines = */"",
                        /*Style = */"",//DefaultSSAStyle,
                        match.Groups["extra"].Value,
                        /*Layer = */0
                    );
                    isDialogue = true;
                }
            }
        }

        //add remaining, if any
        if( isDialogue ) AddDialogue( newDialogue );

        DialoguesFinalize();
        
        //necessitate SortByStartTime
        SortByStartTime();
    }
    public override void Save( string filePath, bool removeLineBreaks, bool removeGroupDefiners )
    {
        using StreamWriter outputFile = new(filePath);
        for( int i=0; i<_dialogues.Count; i++)
        {
            DialogueData dialogue = _dialogues[i];
            outputFile.WriteLine( (i+1).ToString() );
            outputFile.WriteLine( FormatSRTTime(dialogue._start) + " --> " + FormatSRTTime(dialogue._end) + dialogue._extra_string );
            //output TextLines, line by line
            string textLines = Restyler.RestyleTextLines( dialogue._textLines, dialogue._group, removeLineBreaks, removeGroupDefiners );
            foreach( string textLine in textLines.Split('\n') )
                outputFile.WriteLine( textLine );
            //extra empty line, is how srt separate entries
            outputFile.WriteLine( );
        }
    }

    //[GeneratedRegex(@"(?:^ *(?<idx>\d+) *$\r?\n)?^(?<start_tsn>-)?(?<start_tsh>\d+):(?<start_tsm>\d{1,2}):(?<start_tss>\d{1,2}),(?<start_tsms>\d{1,3}) *--> *(?<end_tsn>-)?(?<end_tsh>\d+):(?<end_tsm>\d{1,2}):(?<end_tss>\d{1,2}),(?<end_tsms>\d{1,3})(?<extra>.*)$(?<textlines>(?:(?!(?:\r?\n){2})(?<newline>\r?\n)^.+$)*)", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.AnyNewLine )]
    [GeneratedRegex(@"^\s*(?<start_n>-)?(?:(?<start_h>\d+):(?<start_m>\d+):|(?<start_m>\d+):)?(?<start_s>\d+)[,.](?<start_ms>\d+)\s*-->\s*(?<end_n>-)?(?:(?<end_h>\d+):(?<end_m>\d+):|(?<end_m>\d+):)?(?<end_s>\d+)[,.](?<end_ms>\d+)(?<extra>.*)$", RegexOptions.Compiled | RegexOptions.Singleline )]//be mindful of \s when in multiline, \s inludes more!
    internal static partial Regex SRTDialogueRegex();
    public static string FormatSRTTime( int ms, char decimalSeparator=',' ) {
        (bool _Sign, int _H, int _M, int _S, int _MS) = Common.GetTime( ms );
        return (_Sign?"-":"") + _H.ToString("D2") + ':' + _M.ToString("D2") + ':' + _S.ToString("D2") + decimalSeparator + _MS.ToString("D3");
    }
    [GeneratedRegex(@"^(?<n>-)?(?:(?<h>\d+):(?<m>\d+):|(?<m>\d+):)?(?<s>\d+)[,.](?<ms>\d+)$", RegexOptions.Compiled | RegexOptions.Singleline)]
    internal static partial Regex ParseSRTTimeRegEx();
    public static int ParseSrtTime( string ts ) {
        Match match = ParseSRTTimeRegEx().Match( ts );
        if ( match.Success ) {
            return Common.GetMilliseconds(
                match.Groups["n"].Success,
                match.Groups["h"].Success ? int.Parse(match.Groups["h"].Value) : 0,
                match.Groups["m"].Success ? int.Parse(match.Groups["m"].Value) : 0,
                int.Parse(match.Groups["s"].Value),
                int.Parse(match.Groups["ms"].Value)
            );
        } else {
            throw new InvalidOperationException("Invalid SRT timestamp: "+ts);
        }
    }
}