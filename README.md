<div align="center">
  <h1>SubTools</h1>
</div>

SubTools is a lightweight .NET command-line utility for working with subtitle files in SRT, VTT, ASS, and SSA formats. It helps you clean up broken subtitle timing, split or regroup dialogue, merge short lines, and save processed output safely with automatic backups.

## Overview

SubTools is designed for subtitle cleanup and repair workflows. It can break apart complex subtitle lines, regroup dialogue using style rules, merge short entries, and recompute timing based on readable subtitle length.

## Features

- Load subtitle files from files or folders
- Split subtitle entries by bracketed text, line breaks, and dialogue styles
- Regroup SSA/ASS dialogue using custom styling rules and override tags
- Merge similar or short subtitle entries to improve readability
- Recalculate subtitle timing based on character count and duration settings
- Save output to a selected directory with backup support
- Restore files from a generated backup archive

## Supported formats

| Format | Support |
| --- | --- |
| .srt | Yes |
| .vtt | Yes |
| .ass | Yes |
| .ssa | v3 and v4 |

## Requirements

- .NET 10 SDK
- Windows, macOS, or Linux

## Quick start

### Publish

```bash
dotnet publish -c Release
```

### Run from the project directory

```bash
dotnet run -- --input "input.srt"
```

### Run from anywhere

Add the generated executable from `bin\Release\net10.0` to your `PATH` variable, then use:

```bash
subtools -h
```

## Configuration

Edit the config and tune regrouping defaults to fit your workflow:

```bash
subtools
>config list
>config set Regroup_SSA_Dialogue_Styles Default,DefaultItalic
>regroup list
>regroup delete ()
>regroup set () True {\i1}
>config save
```

## Example workflow

```bash
subtools
>load *.ass *.srt
>break
>regroup
>merge --max-gap 0.5
>recalculate --mode Extend --chars-per-second 15 --min-duration 1 --max-duration 8
>save
```

Or, for the default automatic workflow:

```bash
subtools -i *
>save
```

This runs the default subtitle pipeline: load → break → regroup → merge → recalculate. You still need to run `save` to write the results to disk.

## Notes

- Backups are created automatically unless backup support is disabled.
- Use the built-in help for the latest command options:

```bash
subtools --help
subtools [Command] --help
```

## Command reference

### Global options

These options are available on the root command.

| Option | Description |
| --- | --- |
| `-v, --verbose` | Show more information; usable before any subcommand. |
| `-i, --input` | Input paths. Accepts files, wildcards, and folders. Can be specified multiple times. |
| `-o, --output-dir` | Output directory. If not set, the input directory is used and existing files are overwritten. |

When `--input` is supplied without an explicit subcommand, the application performs the default workflow: load → break → regroup → merge → recalculate.

### `load`

Load subtitle files into memory.

Usage:

```bash
subtools load <InputPaths...>
```

| Argument | Description |
| --- | --- |
| `InputPaths` | One or more input paths. Accepts files, wildcards, and folders. |

### `break`

Break or separate subtitle entries.

Usage:

```bash
subtools break [options]
```

| Option | Description |
| --- | --- |
| `-e, --enclosed` | Separate text enclosed in brackets and parentheses. |
| `-b, --line-breaks` | Separate multi-line text. |
| `-s, --styles` | SSA style names to modify. Use commas to separate multiple style names. |

### `regroup`

Use SSA/ASS styles and the styling list to regroup subtitle entries and add override styling and dialogue tags.

Usage:

```bash
subtools regroup [options]
subtools regroup list
subtools regroup set <Definers> <IsDialogue> [OverrideStyling]
subtools regroup delete <Definers>
```


| Option | Description |
| --- | --- |
| `-s, --styles` | SSA style names to use for regrouping. Use commas to separate multiple style names. |

| Subcommand | Description |
| --- | --- |
| `list` | View the current styling list. |
| `set` | Add or update a styling rule. |
| `delete` | Remove a styling rule. |

`set` arguments:

| Argument | Description |
| --- | --- |
| `Definers` | Styling list definers, such as `[start char][end char]` pairs. Must contain an even number of characters, for example `()[]<>`. |
| `IsDialogue` | Boolean flag indicating whether the matched group should be treated as dialogue. |
| `OverrideStyling` | Optional override style code added at the beginning of subtitle text. Defaults to an empty string. |

`delete` arguments:

| Argument | Description |
| --- | --- |
| `Definers` | The styling definers to remove. |

### `merge`

Merge subtitle entries. This command performs the full merge flow: same text → same time → short lines.

Usage:

```bash
subtools merge [options]
subtools merge sametext [options]
subtools merge sametime [options]
subtools merge shortlines [options]
```

| Option | Description |
| --- | --- |
| `-g, --max-gap` | Maximum gap in seconds between subtitle entries to merge. |
| `-c, --max-chars` | Maximum character count for merged subtitle entries. |
| `-u, --unpunctuated` | Merge unpunctuated text. |
| `-s, --strict-test` | Use stricter short-line merge rules. |
| `-d, --make-dialogue` | Add `-` at the start of each merged line. |
| `-N, --remove-line-breaks` | Remove line breaks from text lines. |

| Subcommand | Description |
| --- | --- |
| `sametext` | Merge subtitle entries with the same text. |
| `sametime` | Merge subtitle entries with the same display time using a line break, affected by grouping. |
| `shortlines` | Merge short subtitle text lines for dialogue entries, affected by dialogue tags and grouping. |

### `recalculate`

Recalculate subtitle display durations based on character count. This applies to dialogue entries by default unless `--all` is used.

Usage:

```bash
subtools recalculate [options]
```

| Option | Description |
| --- | --- |
| `-m, --mode` | Recalculation mode. Supported values: `Extend` (no overlap) and `Shift` (push later subtitles). |
| `-o, --overlap` | Allow overlap when using `Extend` mode. |
| `-s, --shrink` | Shrink timing if the display duration is larger than needed. |
| `-a, --all` | Recalculate all entries instead of only dialogue entries. |
| `-c, --chars-per-second` | Characters per second. |
| `-M, --min-duration` | Minimum display duration in seconds. |
| `-X, --max-duration` | Maximum display duration in seconds. |
| `-G, --gap` | Gap between dialogues in seconds. |

### `collusion-fix`

Reform dialogue timings to emulate reverse collusion, only for entries tagged as dialogue and affected by grouping.

Usage:

```bash
subtools collusion-fix [options]
```

| Option | Description |
| --- | --- |
| `-r, --reverse` | When set, subtitles are shifted to make way for new ones. |
| `-t, --top-styles` | SSA style names of styles that are positioned at the top of the screen, use ',' to separate multiple style names. |

### `save`

Save subtitle files, overwriting existing files if no output directory is set.

Usage:

```bash
subtools save [options]
```

| Option | Description |
| --- | --- |
| `-o, --output-dir` | Output directory. If not set, the input directory is used. |
| `-G, --remove-group-definers` | For grouped dialogue entries, remove group definers. |
| `-R, --skip-backup` | Skip automatic backup creation. |

### `backup`

Create a backup archive for subtitle files.

Usage:

```bash
subtools backup [options]
```

| Option | Description |
| --- | --- |
| `-o, --output-dir` | Output directory for the backup archive. |

### `restore`

Restore subtitle files from the backup archive.

Usage:

```bash
subtools restore [options]
```

| Option | Description |
| --- | --- |
| `-o, --output-dir` | Directory used to restore files from backup. |

### `config`

Configure default values used by the application.

Usage:

```bash
subtools config set <key> <value>
subtools config get <key>
subtools config list
subtools config save
```

| Subcommand | Description |
| --- | --- |
| `set` | Set a configuration value. |
| `get` | Get a configuration value. |
| `list` | List configuration keys, types, and values. |
| `save` | Save the configuration file to the configured config path. |

`set` arguments:

| Argument | Description |
| --- | --- |
| `key` | Configuration key name. |
| `value` | Configuration value to store. |

`get` arguments:

| Argument | Description |
| --- | --- |
| `key` | Configuration key name to fetch. |


### `exit`

Exit the interactive CLI loop.

Usage:

```bash
subtools
> exit
```


## License

This project is licensed under the MIT License. You may use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the software, provided that the copyright notice and permission notice are included in all copies or substantial portions of the software.
