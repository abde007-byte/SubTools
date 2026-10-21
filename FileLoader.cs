namespace SubTools;
public static class FileLoader
{
    public static SubFile? CreateSubFile( string extension )
    {
        switch( extension)
        {
            case ".srt" :
                return new SubFileSRT();
            case ".vtt" :
                return new SubFileVTT();
            case ".ssa" :
                return new SubFileSSA();
            case ".ass" :
                return new SubFileASS();
            default : return null;
        }
    }
    internal static readonly List<SubFile> SubFiles = [];
    public static bool Load( string filePath )
    {
        string extension = Path.GetExtension(filePath).ToLower();
        SubFile? subFile = CreateSubFile( extension );
        if( subFile == null ){
            ConsoleHelper.Warning($"File type {extension} not supported");
            return false;
        }

        subFile.Load( filePath );
        if ( subFile.DialoguesCount == 0 )
        {
            ConsoleHelper.Warning($"No valid entries in {filePath}");
            return false;
        }
        else
        {
            SubFiles.Add( subFile );
            return true;
        }
    }

    public static void Load( string[]? inputPaths )
    {
        //remove old
        SubFiles.Clear();

        var matchedFiles = new List<string>();
        if( inputPaths != null )
        foreach( string inputPath in inputPaths )
        {
            if ( Directory.Exists(inputPath) )
            {
                //add all directory files, TopDirectoryOnly
                var directory = Path.GetFullPath(inputPath);//get full directory name
                matchedFiles.AddRange( Directory.GetFiles( directory, "*", SearchOption.TopDirectoryOnly) );
            }
            else
            {
                //add file or files( with pattern )
                var directory = Path.GetDirectoryName(inputPath);//extract Directory
                directory = string.IsNullOrEmpty(directory)? Directory.GetCurrentDirectory() : directory; //use CurrentDirectory as the default if directory is empty
                directory = Path.GetFullPath(directory);//get full directory name
                var pattern = Path.GetFileName(inputPath)??inputPath;
                matchedFiles.AddRange( Directory.GetFiles( directory, pattern ) );
            }
        }
        
        if ( matchedFiles.Count == 0 ) //throw new InvalidOperationException("No input files found for the provided pattern.");
            ConsoleHelper.Warning("No input files found for the provided pattern.");

        var totalFiles = 0;
        foreach ( string inputFilePath in matchedFiles )
        {
            try
            {
                //load
                ConsoleHelper.Verbose($"{inputFilePath}", "Loading: ");
                if ( Load( inputFilePath ) )
                    totalFiles++;
            }
            catch (Exception err)
            {
                ConsoleHelper.Error(err.Message);
            }
        }
        ConsoleHelper.Success($"Files Loaded: {totalFiles}", "Completed. ");
    }

}
