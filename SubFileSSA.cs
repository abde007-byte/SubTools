using System.Text.RegularExpressions;
namespace SubTools;

public partial class SubFileSSA : SubFile
{
    private List<string> PreDialogue = new(40);
    public override string DefaultExtension => ".ssa";
    public override void Load( string filePath )
    {
        //load ssa file format subtitle entries, v3 & v4
        //by default all entries are tagged as NoneDialogue
        FilePath = filePath;
        using StreamReader inputFile = new(filePath);
        string? line;

        //at [Events] section
        DialoguesInitialize();
        while( (line = inputFile.ReadLine()) is not null )
        {
            //try to match dialogue lines
            Match match = SSADialogueRegex().Match( line );
            if (match.Success)
            {
                AddDialogue( new(
                    //it's bossible to use Restyler.IsSSADialogueStyle( match.Groups["style"].Value )?DefaultGroupValueForDialogue:DefaultGroupValueForNoneDialogue
                    //but we need Restyler.PrepareSSADialogueStyles( ssaStyles ); at loading, plus it's all not necessary.
                    /*Group = */DefaultGroupValueForNoneDialogue,
                    /*Start = */Common.GetMillisecondsFromCentiseconds(
                        match.Groups["start_tsn"].Success,
                        match.Groups["start_tsh"].Success ? int.Parse(match.Groups["start_tsh"].Value) : 0,
                        match.Groups["start_tsm"].Success ? int.Parse(match.Groups["start_tsm"].Value) : 0,
                        int.Parse(match.Groups["start_tss"].Value),
                        int.Parse(match.Groups["start_tsms"].Value)
                    ),
                    /*End = */Common.GetMillisecondsFromCentiseconds(
                        match.Groups["end_tsn"].Success,
                        match.Groups["end_tsh"].Success ? int.Parse(match.Groups["end_tsh"].Value) : 0,
                        match.Groups["end_tsm"].Success ? int.Parse(match.Groups["end_tsm"].Value) : 0,
                        int.Parse(match.Groups["end_tss"].Value),
                        int.Parse(match.Groups["end_tsms"].Value)
                    ),
                    match.Groups["text"].Value,
                    match.Groups["style"].Value,
                    match.Groups["extra"].Value,
                    int.Parse( match.Groups["layer"].Value )
                ) );
            }
            else
            {
                //if not a match add to ...
                PreDialogue.Add( line );
            }
        }
        DialoguesFinalize();
        //necessitate SortByStartTime
        SortByStartTime();
    }
    public override void Save( string filePath, bool removeGroupDefiners )
    {
        using StreamWriter outputFile = new(filePath);
        //add pre [Event] data
        foreach (var line in PreDialogue )
            outputFile.WriteLine( line );
        //add dialogue lines
        for( int i=0; i<_dialogues.Count; i++)
        {
            var dialogue = _dialogues[i];
            string textLines = Restyler.RestyleTextLines( dialogue._textLines, dialogue._group, removeGroupDefiners );
            outputFile.WriteLine( "Dialogue: Marked="
                + dialogue._extra_int.ToString() + ","
                + FormatSSATime(dialogue._start) + ","
                + FormatSSATime(dialogue._end) + ","
                + dialogue._style + ","
                + dialogue._extra_string + ","
                + textLines.Replace("\n", "\\N" )//line breaks in ssa are written as \N, even if removeLineBreaks=true other none dialogue need this.
            );
        }
    }
    public static string FormatSSATime( int ms ) {
        (bool _Sign, int _H, int _M, int _S, int _CS) = Common.GetTimeCentiseconds( ms );
        return (_Sign?"-":"") + _H.ToString("D2") + ':' + _M.ToString("D2") + ':' + _S.ToString("D2") + '.' + _CS.ToString("D2");
    }
    [GeneratedRegex(@"^Dialogue:\s*(?:Marked=)?(?<layer>-?\d+),(?<start_tsn>-)?(?:(?<start_tsh>\d+):)?(?:(?<start_tsm>\d+):)?(?<start_tss>\d+).(?<start_tsms>\d+),(?<end_tsn>-)?(?:(?<end_tsh>\d+):)?(?:(?<end_tsm>\d+):)?(?<end_tss>\d+).(?<end_tsms>\d+),(?<style>[^,]*),(?<extra>[^,]*,\d+,\d+,\d+,[^,]*),(?<text>.*)$", RegexOptions.Compiled | RegexOptions.Singleline )]
    internal static partial Regex SSADialogueRegex();
}
