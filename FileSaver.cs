namespace SubTools;

public static class FileSaver
{
    public static void Save( string? outputDir, bool skipBackup, bool removeLineBreaks, bool removeGroupDefiners )
    {
        if( FileLoader.SubFiles.Count == 0)
        {
            ConsoleHelper.Warning("need to load files!");
            return;
        }

        //Determine final output directory behavior
        //when not specified, each file will use its own input directory (and thus be overwritten)
        var useOutputDir = Common.ResolveOutDir( ref outputDir );
        
        if ( ! skipBackup )
        {
            //create backups
            BackupRestore.CreateBackup( outputDir! );
        }

        var totalFiles = 0;
        foreach( SubFile subFile in FileLoader.SubFiles )
        {
            //output
            var filePath = new FileInfo(subFile.FilePath!);
            string outputFilePath = filePath.FullName;
            if ( useOutputDir )
                outputFilePath = Path.Combine( outputDir!, filePath.Name );
            
            //if( outputFilePath.Extension == ".vtt" ){
            //  outputFilePath = Path.ChangeExtension( outputFilePath, subFile.DefaultExtension );
            //  remove file filePath.FullName;
            //}
            
            //save
            ConsoleHelper.Verbose($"Saving: {outputFilePath}");
            try
            {
                subFile.Save( outputFilePath, removeLineBreaks, removeGroupDefiners );
                totalFiles++;
            }
            catch (Exception err)
            {
                ConsoleHelper.Error(err.Message);
            }
        }
        ConsoleHelper.Success($"Completed. Files Saved: {totalFiles}");
    }

}
