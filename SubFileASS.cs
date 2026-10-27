using System.Text.RegularExpressions;
namespace SubTools;

public class SubFileASS : SubFile
{
    private List<string> PreEvents = new(40);
    private List<string> PostEvents = [];
    public override string DefaultExtension => ".ass";
    public override void Load( string filePath )
    {
        //load ass file format subtitle entries
        //by default all entries are tagged as NoneDialogue
        FilePath = filePath;
        using StreamReader inputFile = new(filePath);
        string? line;

        //not yet at [Events] section
        while( (line = inputFile.ReadLine()) is not null )
        {
            //add to ...
            PreEvents.Add( line );

            if( line == "[Events]" )
                break; //break from this loop to  continue to past [Events] entries
        }

        //at [Events] section
        DialoguesInitialize();
        while( (line = inputFile.ReadLine()) is not null )
        {
            if (line.Length == 0)
                continue;//skip empty lines at [Events] section

            if( line == "[Fonts]")
            {
                //add empty line to separate between sections and section starter
                // added at the bottom
                PostEvents.Add( "" );
                PostEvents.Add( line );
                break;
            }

            int colon = line.IndexOf(':');
            if (colon <= 0)
                PostEvents.Add( line );//unknown data line,add at the bottom
            else
            {
                if( line[..colon] != "Dialogue")
                {
                    //possible values "Format,Comment,Picture,Sound,Movie,Command"
                    //no matter what it is add at the top
                    PreEvents.Add( line );
                }
                else
                {
                    //try to match dialogue lines
                    Match match = SubFileSSA.SSADialogueRegex().Match( line );//line[(colon+1)..]
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
                            LineBreakSpace.Replace( match.Groups["text"].Value ),
                            match.Groups["style"].Value,
                            match.Groups["extra"].Value,
                            int.Parse( match.Groups["layer"].Value )
                        ) );
                    }
                    else
                    {
                        //if not a match add at the bottom to ...
                        PostEvents.Add( line );
                    }
                }
            }
        }
        DialoguesFinalize();
        //necessitate SortByStartTime
        SortByStartTime();

        //continue, possibly at [Fonts] section
        while( (line = inputFile.ReadLine()) is not null )
            PostEvents.Add( line );
    }
    public override void Save( string filePath, bool removeGroupDefiners )
    {
        //var GroupsGroupParams = Restyler.GetGroupsGroupParams( true );//entries with group 0 are dialogue entries by default
        using StreamWriter outputFile = new(filePath);
        //add pre [Event] data
        foreach (var line in PreEvents )
            outputFile.WriteLine( line );
        //add dialogue lines
        for( int i=0; i<_dialogues.Count; i++)
        {
            var dialogue = _dialogues[i];
            string textLines = Restyler.RestyleTextLines( dialogue._textLines, dialogue._group, removeGroupDefiners );
            outputFile.WriteLine( "Dialogue: "
                + dialogue._extra_int.ToString() + ","
                + SubFileSSA.FormatSSATime(dialogue._start) + ","
                + SubFileSSA.FormatSSATime(dialogue._end) + ","
                + dialogue._style + ","
                + dialogue._extra_string + ","
                + textLines.Replace("\n", "\\N" )//line breaks in ssa are written as \N, even if removeLineBreaks=true other none dialogue need this.
            );
        }
        //add post [Event] data
        foreach (var line in PostEvents )
            outputFile.WriteLine( line );
    }
    //[GeneratedRegex(@"^\s*(?<layer>\d+),(?<start_tsn>-)?(?:(?<start_tsh>\d+):)?(?:(?<start_tsm>\d+):)?(?<start_tss>\d+).(?<start_tsms>\d+),(?<end_tsn>-)?(?:(?<end_tsh>\d+):)?(?:(?<end_tsm>\d+):)?(?<end_tss>\d+).(?<end_tsms>\d+),(?<style>[^,]*),(?<extra>[^,]*,\d+,\d+,\d+,[^,]*),(?<text>.*)$", RegexOptions.Compiled | RegexOptions.Singleline )]
    //private static partial Regex ASSDialogueRegex();
}
