using System.CommandLine;

namespace SubTools;
public static class UI
{
    private static readonly RootCommand root_command;

    public static int ParseInvoke( string[] args, bool loop=true )
    {
        var parseResult = root_command.Parse(args);
        //display options/help
        //if (parseResult.Errors.Count == 0) root_command.Parse("--help").Invoke();
        var exitCode = parseResult.Invoke();
        
        if( loop )
        {
            //add exit command, with no handlers!
            root_command.Subcommands.Add( new Command("exit", "Exit Subtitles Tools.") );
            //continue to run commands without exiting, until exit command
            while (true)
            {
                Console.Write("> ");
                string? line = Console.ReadLine();
                if (line == null) break;// EOF
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;//basically exit

                //args = CommandLineParser.SplitCommandLine(line).ToArray();//replace args
                exitCode = root_command.Parse( line ).Invoke();//refill args and continue to invoke!
            }
        }

        return exitCode;
    }

    static UI()
    {
        root_command = new("Subtitles Tools, supported formats(srt,vtt,ass,ssa\"v3&4\").");
        var optionVerbose = new Option<bool>("--verbose", "-v") { Description = "Show more info.", Recursive=true };
        var optionInputPath = new Option<string[]>("--input","-i"){ Description = "Input paths, Accepts files, wildcards and folders.", AllowMultipleArgumentsPerToken=true };
        var optionOutputDir = new Option<string>("--output-dir", "-o") { Description = "Output Directory, If not set, input directory will be used, and overwrite existing files." };
        root_command.Options.Add( optionVerbose );
        root_command.Options.Add( optionInputPath );
        root_command.Options.Add( optionOutputDir );
        root_command.SetAction( parseResult => {
            Config.Miscellaneous_Verbose = parseResult.GetValue(optionVerbose);
            var input = parseResult.GetValue( optionInputPath );
            if( (input != null) && input.Length > 0)
            {
                //default actions
                Console.WriteLine($"Loading: {string.Join(" ", input)}");
                FileLoader.Load( input );
                Console.WriteLine("Breaking:");
                Separator.Separate( Config.Separate_Enclosed, Config.Separate_Line_Breaks, Config.Regroup_Ssa_Dialogue_Styles );
                Console.WriteLine("Regrouping:");
                Restyler.Regroup( Config.Regroup_Ssa_Dialogue_Styles );
                Console.WriteLine("Merging:");
                Merger.Merge( 1, Config.Merge_Max_Gap, 0, false, false, false, false );
                Merger.Merge( 2, Config.Merge_Max_Gap, 0, false, false, Config.Merge_Make_Dialogue, false );
                Merger.Merge( 3, Config.Merge_Max_Gap, Config.Merge_Max_Chars, Config.Merge_Unpunctuated, Config.Merge_Strict_Test, Config.Merge_Make_Dialogue, Config.Merge_Remove_Line_Breaks );
                Console.WriteLine("Recalculating:");
                Recalculator.Recalculate( Config.Recalculate_Mode, Config.Recalculate_Overlap, Config.Recalculate_Shrink, Config.Recalculate_All,
                    Config.Recalculate_Chars_Per_Second, Config.Recalculate_Min_Duration, Config.Recalculate_Max_Duration, Config.Recalculate_Gap
                );
                //Console.WriteLine("Saving:");
                //FileSaver.Save( parseResult.GetValue(optionOutputDir), Config.Save_Skip_Backup, Config.Save_Remove_Line_Breaks, Config.Save_Remove_Group_Definers );
            }
        });

        /******************************************************************************************************/
        /***FileLoader*****************************************************************************************/
        /******************************************************************************************************/
        var commandLoad = new Command("load", "Load subtitles files.");
        var argumentInputPath = new Argument<string[]>("InputPaths"){ Description = "Input paths, Accepts files, wildcards and folders.", Arity = ArgumentArity.OneOrMore };
        commandLoad.Arguments.Add(argumentInputPath);
        commandLoad.SetAction( parseResult => {
            Config.Miscellaneous_Verbose = parseResult.GetValue(optionVerbose);
            FileLoader.Load( parseResult.GetValue(argumentInputPath) );
        });
        
        root_command.Subcommands.Add(commandLoad);
        /******************************************************************************************************/
        /***Separator******************************************************************************************/
        /******************************************************************************************************/
        var commandSeparate = new Command("break", "Break/Separate subtitles entries.");
        Option<bool> optionEnclosed = new("--enclosed", "-e") { Description = "separate text enclosed in brackets and parenthesis.", DefaultValueFactory = _ => Config.Separate_Enclosed };
        Option<bool> optionLineBreaks = new("--line-breaks", "-b") { Description = "separate multi lined text.", DefaultValueFactory = _ => Config.Separate_Line_Breaks };
        Option<string> optionSSADialogueStyles = new("--styles", "-s") { Description = "SSA style names that you want modified, use ',' to separate multiple style names.", DefaultValueFactory = _ => Config.Regroup_Ssa_Dialogue_Styles };

        commandSeparate.Options.Add(optionEnclosed);
        commandSeparate.Options.Add(optionLineBreaks);
        commandSeparate.Options.Add(optionSSADialogueStyles);
        root_command.Subcommands.Add(commandSeparate);

        commandSeparate.SetAction(parseResult => {
            Config.Miscellaneous_Verbose = parseResult.GetValue(optionVerbose);
            Separator.Separate( parseResult.GetValue(optionEnclosed), parseResult.GetValue(optionLineBreaks), parseResult.GetValue(optionSSADialogueStyles)! );
        });
        /******************************************************************************************************/
        /***Regroup*********************************************************************************************/
        /******************************************************************************************************/
        var commandRegroup = new Command("regroup", "Use ssa styles and styling list to regroup subtitles entries and add 'override styling' and 'dialogue tag' for subtitles entries. Entries tagged as dialog, will have their group definers removed from text lines.");
        commandRegroup.Options.Add( optionSSADialogueStyles );
        commandRegroup.SetAction(parseResult => {
            Config.Miscellaneous_Verbose = parseResult.GetValue(optionVerbose);
            Restyler.Regroup( parseResult.GetValue(optionSSADialogueStyles)! );
        });

        var argumentGKeys = new Argument<string>("Definers"){ Description = "groups definers: [start char][end char], even number of chars.", Arity=ArgumentArity.ExactlyOne };
        argumentGKeys.Validators.Add(result => {
            var gv = result.GetValue(argumentGKeys)!;
            if( (gv.Length>=2) && (gv.Length % 2) != 0 ) result.AddError("group definers must be in pairs (even number of chars). example: ()[]<> ");
        });
        var argumentIsDialogue = new Argument<bool>("IsDialogue"){ Description = "is group tagged as dialog?", Arity=ArgumentArity.ExactlyOne };
        var argumentOverrideStyling = new Argument<string>("OverrideStyling"){ Description = "group style override code.", Arity=ArgumentArity.ZeroOrOne, DefaultValueFactory = _ => "" };
        
        var commandStyleList = new Command("list"){ Description = "view styling list." };
        commandStyleList.SetAction(parseResult => Restyler.PrintTableGroups());

        var commandStyleSet = new Command("set"){ Description = "add or update styling list by:\n\t1 - one or more character pair(s), that are used to identify the entries that belong to their group(s).\n\t2 - true/false, whether or not the identified entries are tagged as dialog.\n\t3 - \"override styling code \" - optional - added to the beggining of subtitle etries text.\nexample : restyle add () true {\\i1}\nexample : restyle push <>== false {\\an8}" };
        commandStyleSet.Arguments.Add( argumentGKeys );
        commandStyleSet.Arguments.Add( argumentIsDialogue );
        commandStyleSet.Arguments.Add( argumentOverrideStyling );
        commandStyleSet.SetAction(_ => Restyler.SetGroups( _.GetValue(argumentGKeys)!, _.GetValue(argumentIsDialogue), _.GetValue(argumentOverrideStyling)!, true ));

        var commandStyleRemove = new Command("delete") { Description = "remove from styling list." };
        commandStyleRemove.Arguments.Add( argumentGKeys );
        commandStyleRemove.SetAction(parseResult => Restyler.RemoveGroups( parseResult.GetValue(argumentGKeys)! ));

        commandRegroup.Subcommands.Add(commandStyleList);
        commandRegroup.Subcommands.Add(commandStyleSet);
        commandRegroup.Subcommands.Add(commandStyleRemove);
        root_command.Subcommands.Add(commandRegroup);
        /******************************************************************************************************/
        /***Merger*********************************************************************************************/
        /******************************************************************************************************/
        var commandMerge = new Command("merge", "Merge subtitles entries, Perform all three merging operations, sametext->sametime->shortlines.");
        var optionMaxGap = new Option<double>("--max-gap", "-g") { Description = "Maximum Gap between subtitle entries to be merged.", DefaultValueFactory = _ => Config.Merge_Max_Gap };
        var optionMaxChars = new Option<int>("--max-chars", "-c") { Description = "Maximum number of characters of the merged entries.", DefaultValueFactory = _ => Config.Merge_Max_Chars };
        var optionUnpunctuated = new Option<bool>("--unpunctuated", "-u") { Description = "Merge unpunctuated text.", DefaultValueFactory = _ => Config.Merge_Unpunctuated };
        var optionStrictTest = new Option<bool>("--strict-test", "-s") { Description = "If set short lines won't merge unless the first sentence ends with Comma and the second's first char is not uppercase - with \"I and I'\" as an exception -, otherwise they will merge as long as the first sentence doesn't end with Period, Question Mark, Exclamation Mark, Semicolon, Ellipsis.", DefaultValueFactory = _ => Config.Merge_Strict_Test };
        var optionMakeDialogue = new Option<bool>("--make-dialogue", "-d") { Description = "Add \"-\" at the start of each line merged.", DefaultValueFactory = _ => Config.Merge_Make_Dialogue };
        var optionRemoveLineBreaks = new Option<bool>("--remove-line-breaks", "-N") { Description = "Remove line breaks from texts lines.", DefaultValueFactory = _ => Config.Merge_Remove_Line_Breaks};
        commandMerge.Options.Add(optionMaxGap);
        commandMerge.Options.Add(optionMaxChars);
        commandMerge.Options.Add(optionUnpunctuated);
        commandMerge.Options.Add(optionStrictTest);
        commandMerge.Options.Add(optionMakeDialogue);
        commandMerge.Options.Add(optionRemoveLineBreaks);
        commandMerge.SetAction(_ => {
            Config.Miscellaneous_Verbose = _.GetValue(optionVerbose);
            Merger.Merge( 1, _.GetValue(optionMaxGap), 0, false, false, false, false );
            Merger.Merge( 2, _.GetValue(optionMaxGap), 0, false, false, _.GetValue(optionMakeDialogue), false );
            Merger.Merge( 3, _.GetValue(optionMaxGap), _.GetValue(optionMaxChars), _.GetValue(optionUnpunctuated), _.GetValue(optionStrictTest), _.GetValue(optionMakeDialogue), _.GetValue(optionRemoveLineBreaks) );
        });

        var commandMergeSameText = new Command("sametext"){ Description = "Merge subtitle entries with the same text." };
        commandMergeSameText.Options.Add( optionMaxGap );
        commandMergeSameText.SetAction(_ => {
            Config.Miscellaneous_Verbose = _.GetValue(optionVerbose);
            Merger.Merge( 1, _.GetValue(optionMaxGap), 0, false, false, false, false );
        });
        commandMerge.Subcommands.Add( commandMergeSameText );

        var commandMergeSameTime = new Command("sametime"){ Description = "Merge subtitle entries with the same display time with a line break, affected by groupings." };
        commandMergeSameTime.Options.Add( optionMaxGap );
        commandMergeSameTime.Options.Add(optionMakeDialogue);
        commandMergeSameTime.SetAction(_ => {
            Config.Miscellaneous_Verbose = _.GetValue(optionVerbose);
            Merger.Merge( 2, _.GetValue(optionMaxGap), 0, false, false, _.GetValue(optionMakeDialogue), false );
        });
        commandMerge.Subcommands.Add( commandMergeSameTime );

        var commandMergeShortLines = new Command("shortlines"){ Description = "Merge short subtitle text lines for entries marked as dialog, affected by dialogue tag and groupings." };
        commandMergeShortLines.Options.Add( optionMaxGap );
        commandMergeShortLines.Options.Add(optionMaxChars);
        commandMergeShortLines.Options.Add(optionUnpunctuated);
        commandMergeShortLines.Options.Add(optionStrictTest);
        commandMergeShortLines.Options.Add(optionMakeDialogue);
        commandMergeShortLines.Options.Add(optionRemoveLineBreaks);
        commandMergeShortLines.SetAction(_ => {
            Config.Miscellaneous_Verbose = _.GetValue(optionVerbose);
            Merger.Merge( 3, _.GetValue(optionMaxGap), _.GetValue(optionMaxChars), _.GetValue(optionUnpunctuated), _.GetValue(optionStrictTest), _.GetValue(optionMakeDialogue), _.GetValue(optionRemoveLineBreaks) );
        });
        commandMerge.Subcommands.Add( commandMergeShortLines );

        root_command.Subcommands.Add( commandMerge );

        /******************************************************************************************************/
        /***Recalculator***************************************************************************************/
        /******************************************************************************************************/
        var commandRecalculate = new Command("recalculate", "Recalculate subtitles entries display durations, based on character count, only for entries tagged as dialog, unless --All option is used, affected by groupings.");
        var optionMode = new Option<Config.RecalculateModeType>("--mode", "-m") { Description = "Modes: Extend (no overlap), Shift (push later subtitles).", DefaultValueFactory = _ => Config.Recalculate_Mode };
        var optionOverlap = new Option<bool>("--overlap", "-o") { Description = "allow overlap in Extend mode.", DefaultValueFactory = _ => Config.Recalculate_Overlap };
        var optionShrink = new Option<bool>("--shrink", "-s") { Description = "if display durration is larger than needed, it will be reduced.", DefaultValueFactory = _ => Config.Recalculate_Shrink };
        var optionAll = new Option<bool>("--all", "-a") { Description = "recalculate durration of all entries, otherwise only dialogue ones.", DefaultValueFactory = _ => Config.Recalculate_All };
        var optionCharsPerSecond = new Option<double>("--chars-per-second", "-c") { Description = "Characters per second.", DefaultValueFactory = _ => Config.Recalculate_Chars_Per_Second };
        var optionMinDuration = new Option<double>("--min-duration", "-M") { Description = "Minimum display time duration in seconds.", DefaultValueFactory = _ => Config.Recalculate_Min_Duration };
        var optionMaxDuration = new Option<double>("--max-duration", "-X") { Description = "Maximum display time duration in seconds.", DefaultValueFactory = _ => Config.Recalculate_Max_Duration };
        var optionGap = new Option<double>("--gap", "-G") { Description = "Gap between dialogues in seconds.", DefaultValueFactory = _ => Config.Recalculate_Gap };

        commandRecalculate.Options.Add(optionMode);
        commandRecalculate.Options.Add(optionOverlap);
        commandRecalculate.Options.Add(optionShrink);
        commandRecalculate.Options.Add(optionAll);
        commandRecalculate.Options.Add(optionCharsPerSecond);
        commandRecalculate.Options.Add(optionMinDuration);
        commandRecalculate.Options.Add(optionMaxDuration);
        commandRecalculate.Options.Add(optionGap);

        root_command.Subcommands.Add(commandRecalculate);

        commandRecalculate.SetAction(parseResult => {
            Config.Miscellaneous_Verbose = parseResult.GetValue(optionVerbose);
            Recalculator.Recalculate(
                parseResult.GetValue(optionMode),
                parseResult.GetValue(optionOverlap),
                parseResult.GetValue(optionShrink),
                parseResult.GetValue(optionAll),
                parseResult.GetValue(optionCharsPerSecond),
                parseResult.GetValue(optionMinDuration),
                parseResult.GetValue(optionMaxDuration),
                parseResult.GetValue(optionGap)
            );
        });

        /******************************************************************************************************/
        /***CollusionFixer*************************************************************************************/
        /******************************************************************************************************/
        var commandCollusionFix = new Command("collusion-fix", "Reform dialogues in a way that emulates reverse collusion, only for entries tagged as dialog, affected by groupings.");
        var optionReverse = new Option<bool>("--reverse", "-r") { Description = "when set, subtitles are shifted to make way for new ones.", DefaultValueFactory = _ => Config.Collusion_Reverse };
        var optionTopStyles = new Option<string>("--top-styles", "-t") { Description = "SSA style names of styles that are positioned at the top of the screen, use ',' to separate multiple style names.", DefaultValueFactory = _ => Config.Collusion_Top_Styles };
        var optionTopGroups = new Option<string>("--top-groups", "-g") { Description = "group keys for groups that are positioned at the top of the screen.", DefaultValueFactory = _ => Config.Collusion_Top_Groups };
        optionTopGroups.Validators.Add(result => {
            var gv = result.GetValue(optionTopGroups)!;
            if( (gv.Length>=2) && (gv.Length % 2) != 0 ) result.AddError("group definers must be in pairs (even number of chars). example: ()[]<> ");
        });
        var optionScaleDown = new Option<int>("--scale-down", "-s") { Description = "add steps of scale-down percentage to the earliest of the overlapping dialogues, for example a value of 10 will scale the fist line by 100%, the second by 90%, the third by 80%, etc.", DefaultValueFactory = _ => Config.Collusion_Scale_Down };
        var optionLineSize = new Option<int>("--line-size", "-c") { Description = "apply forced line breaks, otherwise automatic line breaks will change the way subs are displayed when scaled-down.", DefaultValueFactory = _ => Config.Collusion_Line_Size };
        commandCollusionFix.Options.Add( optionReverse );
        commandCollusionFix.Options.Add( optionTopStyles );
        commandCollusionFix.Options.Add( optionTopGroups );
        commandCollusionFix.Options.Add( optionScaleDown );
        commandCollusionFix.Options.Add( optionLineSize );
        root_command.Subcommands.Add(commandCollusionFix);

        commandCollusionFix.SetAction(_ => {
            Config.Miscellaneous_Verbose = _.GetValue(optionVerbose);
            CollusionFixer.CollusionFix( _.GetValue( optionReverse ), _.GetValue( optionTopStyles )??"", _.GetValue( optionTopGroups )??"", _.GetValue( optionScaleDown ), _.GetValue( optionLineSize ) );
        });

        /******************************************************************************************************/
        /***FileSaver******************************************************************************************/
        /******************************************************************************************************/
        var commandSave = new Command("save", "Save subtitle files, overwriting existing files if Output directory is not set.");
        Option<bool> optionRemoveGroupDefiners = new("--remove-group-definers", "-G") { Description = "only for grouped entries tagged as dialog, remove group definers.", DefaultValueFactory = _ => Config.Save_Remove_Group_Definers};
        Option<bool> optionSkipBackup = new("--skip-backup", "-R") { Description = "skip automatic backup creation, otherwise backup your files before editing them unless a backup already exist.", DefaultValueFactory = _ => Config.Save_Skip_Backup};
        commandSave.Options.Add(optionOutputDir);
        commandSave.Options.Add(optionRemoveGroupDefiners);
        commandSave.Options.Add(optionSkipBackup);
        commandSave.SetAction(_ => {
            Config.Miscellaneous_Verbose = _.GetValue(optionVerbose);
            FileSaver.Save( _.GetValue(optionOutputDir), _.GetValue(optionSkipBackup), _.GetValue(optionRemoveGroupDefiners) );
        });
        root_command.Subcommands.Add(commandSave);
        /******************************************************************************************************/
        /***Backup/Restore*************************************************************************************/
        /******************************************************************************************************/
        var commandBackup = new Command("backup", $"Create backups for subtitle files ({BackupRestore.ArchiveFileName}).");
        commandBackup.Options.Add(optionOutputDir);
        commandBackup.SetAction(parseResult => {
            Config.Miscellaneous_Verbose = parseResult.GetValue(optionVerbose);
            BackupRestore.Backup( parseResult.GetValue(optionOutputDir) );
        });
        root_command.Subcommands.Add(commandBackup);
        var commandRestore = new Command("restore", $"Restore subtitle files from backups ({BackupRestore.ArchiveFileName}).");
        commandRestore.Options.Add(optionOutputDir);
        commandRestore.SetAction(parseResult => {
            Config.Miscellaneous_Verbose = parseResult.GetValue(optionVerbose);
            BackupRestore.Restore( parseResult.GetValue(optionOutputDir) );
        });
        root_command.Subcommands.Add(commandRestore);

        /******************************************************************************************************/
        /***config*********************************************************************************************/
        /******************************************************************************************************/
        //configure
        var commandConfig = new Command("config", "Configure default values.");
        //key
        var argumentConfigKey = new Argument<string>("key"){ Description = "Configuration key." };
        //value
        var argumentConfigValue = new Argument<string>("value"){ Description = "Configuration value." };
        //set key value
        var commandConfigSet = new Command("set", "Set a configuration value.");
        commandConfigSet.Arguments.Add(argumentConfigKey);
        commandConfigSet.Arguments.Add(argumentConfigValue);
        commandConfigSet.SetAction( _ => Config.SetValue(_.GetValue(argumentConfigKey)!, _.GetValue(argumentConfigValue)!));
        commandConfig.Subcommands.Add(commandConfigSet);
        //get value
        var commandConfigGet = new Command("get", "Get a configuration value.");
        commandConfigGet.Arguments.Add(argumentConfigKey);
        commandConfigGet.SetAction( _ => Console.WriteLine(Config.GetValue(_.GetValue(argumentConfigKey)!)) );
        commandConfig.Subcommands.Add(commandConfigGet);
        //list all
        var commandConfigList = new Command("list", "list configurations keys, types and values.");
        commandConfigList.SetAction( _ => Config.PrintTable());
        commandConfig.Subcommands.Add(commandConfigList);
        //save
        var commandConfigSave = new Command("save", $"save configurations file to {Config.FilePath}.");
        commandConfigSave.SetAction( _ => Config.Save());
        commandConfig.Subcommands.Add(commandConfigSave);
        
        root_command.Subcommands.Add(commandConfig);
        /******************************************************************************************************/

        
    }
}