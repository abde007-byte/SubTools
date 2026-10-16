using System.Text.RegularExpressions;
namespace SubTools;
public class SubFileVTT : SubFileSRT
{
    public override string DefaultExtension => ".vtt";
    public override void Save( string filePath, bool removeLineBreaks, bool removeGroupDefiners )
    {
        using StreamWriter outputFile = new(filePath);
        outputFile.WriteLine( "WEBVTT" );
        outputFile.WriteLine( "" );
        for( int i=0; i<_dialogues.Count; i++)
        {
            DialogueData dialogue = _dialogues[i];
            outputFile.WriteLine( FormatSRTTime(dialogue._start, '.') + " --> " + FormatSRTTime(dialogue._end, '.') + dialogue._extra_string );
            //output TextLines, line by line
            string textLines = Restyler.RestyleTextLines( dialogue._textLines, dialogue._group, removeLineBreaks, removeGroupDefiners );
            foreach( string textLine in textLines.Split('\n') )
                outputFile.WriteLine( textLine );
            //extra empty line, is how vtt separate entries
            outputFile.WriteLine( );
        }
    }
}