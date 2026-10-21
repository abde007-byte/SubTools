namespace SubTools;
using System.IO.Compression;

public static class BackupRestore
{
    public const string ArchiveFileName = "subtitles_backup.zip";
    public static void CreateBackup( string outputDir )
    {
        //Common.ResolveOutDir( ref outputDir );
        string zipPath =  Path.Combine( outputDir, ArchiveFileName );
        if( ! File.Exists(zipPath))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var subFile in FileLoader.SubFiles )
            {
                //name used in archive
                string fileName = Path.GetFileName( subFile.FilePath! );
                //if fileName already used/exists in the archive, add counter before the extention of fileName
                string extension = Path.GetExtension(fileName);
                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                int counter = 1;
                while (!usedNames.Add(fileName))
                {
                    fileName = $"{nameWithoutExt}_{counter++}{extension}";
                }
                //add subFile.FilePath as fileName to the archive
                archive.CreateEntryFromFile( subFile.FilePath!, fileName);
            }
            ConsoleHelper.Success($"{zipPath}. Files processed: {FileLoader.SubFiles.Count}", "Backup created: ");
        }
        else
            ConsoleHelper.Verbose($"{zipPath}", "Backup already exists: ");

        //catch { ConsoleHelper.Error($"Failed to create backup for {zipPath} : $($_.Exception.Message)"; }
    }

    public static void Backup( string? outputDir )
    {
        if( FileLoader.SubFiles.Count == 0)
        {
            ConsoleHelper.Warning("need to load files!");
            return;
        }

        Common.ResolveOutDir( ref outputDir );

        CreateBackup( outputDir! );
    }

    public static void Restore( string? outputDir )
    {
        Common.ResolveOutDir( ref outputDir );
        string zipPath = Path.Combine( outputDir!, ArchiveFileName );
        if( File.Exists(zipPath) )
        {
            ZipFile.ExtractToDirectory(zipPath, outputDir!, overwriteFiles: true);
            ConsoleHelper.Success("", "Restore Completed.");
        }else
            ConsoleHelper.Error($"Backup file not found: {zipPath}");
    }
}
