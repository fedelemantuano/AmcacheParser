# AmcacheParser

## Command Line Interface
    
    AmcacheParser version 2026.5.2
    
    Author: Eric Zimmerman (saericzimmerman@gmail.com)
    https://github.com/EricZimmerman/AmcacheParser
    
            b               Path to file containing SHA-1 hashes to *include* from the results. Blacklisting overrides whitelisting
            f               Amcache.hve file to parse. Required
            i               Include file entries for Programs entries
            w               Path to file containing SHA-1 hashes to *exclude* from the results. Blacklisting overrides whitelisting
    
            csv             Directory where CSV results will be saved to. This or --json is required
            csvf            File name to save CSV formatted results to. When present, overrides default name
            json            Directory where the single JSON Lines output will be saved. This or --csv is required
            jsonf           File name for JSON Lines output. Overrides the default name; requires --json
    
            dt              The custom date/time format for CSV and displayed timestamps. Default: yyyy-MM-dd HH:mm:ss. JSON always uses ISO 8601
            mp              When true, use higher precision for CSV and displayed timestamps. JSON always uses milliseconds. Default is FALSE
            nl              When true, ignore transaction log files for dirty hives. Default is FALSE
    
            debug           Show debug information during processing
            trace           Show trace information during processing
    
    
    Examples: AmcacheParser.exe -f "C:\Temp\amcache\AmcacheWin10.hve" --csv C:\temp
              AmcacheParser.exe -f "C:\Temp\amcache\AmcacheWin10.hve" -i --csv C:\temp --csvf foo.csv
              AmcacheParser.exe -f "C:\Temp\amcache\AmcacheWin10.hve" -w "c:\temp\whitelist.txt" --csv C:\temp
              AmcacheParser.exe -f "C:\Temp\amcache\AmcacheWin10.hve" -i --json C:\temp --jsonf results.json
              AmcacheParser.exe -f "C:\Temp\amcache\AmcacheWin10.hve" --csv C:\temp\csv --json C:\temp\json
    
              Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes
                            
## Documentation

This program is different from other Amcache parsers in that it does not dump everything available. Rather, it looks at both File entries and Program entries.

Program entries are found under `Root\Programs` and File entries are found under `Root\File`.

AmcacheParser gathers information about all the Program entries, then looks at all the File entries. In each file entry is a pointer to a Program ID (value 100). If this Program ID exists in Program entries, the File entry is associated with the Program entry.

At the end of this process you are left with things that didn't come from some kind of installed application.
              
Using the minimum options, AmcacheParser exports Unassociated file entries. New-format hives also export the available shortcut, driver and device categories.

If you use the `-i` option, AmcacheParser will export a Programs entry list and its associated File entry list.

With whitelisting and blacklisting you can further reduce the output by excluding certain File entries (whitelisting) or including only File entries whose SHA-1 is found in a blacklist file.

Note that AmcacheParser strips the extra 0s from the front of the SHA-1 value, so you can use the SHA-1s as shown in the output files for generating whitelists or blacklists.

### JSON Lines and Elasticsearch

`--json` produces one UTF-8 file containing one compact JSON object per line. The default filename is `<timestamp>_<hiveName>_AmcacheParser.json`; `--jsonf results.json` uses `results.json` exactly, without category suffixes. An existing output file is replaced. `--csv` and `--json` can be used together.

Each record has the same fields as its corresponding CSV export, plus a `RecordType` discriminator. Program records contain no nested file lists. Record selection, `-i`, whitelist and blacklist behavior are shared with CSV output.

| `RecordType` | Evidence category |
| --- | --- |
| `UnassociatedFileEntry` | File not linked to a program entry |
| `ProgramEntry` | Program entry; requires `-i` |
| `AssociatedFileEntry` | File linked to a program entry; requires `-i` |
| `Shortcut` | Application shortcut |
| `DriverBinary` | Driver binary |
| `DeviceContainer` | Device container |
| `DriverPackage` | Driver package |
| `DevicePnp` | Plug and Play device |

Timestamps always use ISO 8601 UTC with millisecond precision, for example `2026-10-09T10:15:30.123Z`, regardless of `--dt` or `--mp`. Sub-millisecond fractions are truncated. Missing or invalid dates are explicit JSON `null`, including the parser's invalid `LinkDate` recovery sentinel. Raw date strings accept `yyyy-MM-dd`, `yyyyMMdd`, month/day/year with `/` or `-`, and ISO timestamps with an explicit `Z` or `±HH:mm` timezone. Date-only values become `yyyy-MM-dd`; strings with an unspecified timezone or unsupported format become `null`.

Numbers and booleans retain their JSON types. `BinaryType`, `BinFileVersion` and `BinProductVersion` are always strings: old and new hive formats use incompatible source types for these names, so normalization permits indexing both in the same Elasticsearch index. CSV values and formatting are preserved.

Use the [example Elasticsearch mapping](docs/elasticsearch-mapping.json) to map `RecordType` and the three normalized fields as `keyword`, temporal fields as `date`, and `Usn` as `unsigned_long`. The mapping disables automatic date detection for other strings, so version strings or names that resemble dates are not accidentally mapped as dates. Field names retain their CSV capitalization, including the distinction between `ProgramID` and `ProgramId`.

This is evidence JSON Lines. For the Elasticsearch Bulk API, the ingestion step must add an action line before each evidence record.

### Validation

Run `dotnet test AmcacheParser.sln --configuration Release` with .NET 9. The tests use small synthetic old-format and new-format hives under `AmcacheParser.Tests/Fixtures`; regenerate them with `python3 AmcacheParser.Tests/Fixtures/create_hives.py`.

Set `AMCACHE_ELASTICSEARCH_URL` to a disposable local Elasticsearch instance to enable the optional live indexing test. The test creates and removes a uniquely named test index, checks mixed-format ingestion, aggregates by `RecordType`, and verifies a millisecond date range query. Without this setting, the live test is skipped.

[Registry Explorer and RECmd 1.2.0.0 released!](https://binaryforay.blogspot.com/2019/01/registry-explorer-and-recmd-1200.html)

[(Am)cache still rules everything around me (part 2 of 1)](https://binaryforay.blogspot.com/2017/10/amcache-still-rules-everything-around.html)

[Updates to the left of me, updates to the right of me, version 1 releases are here (for the most part)](https://binaryforay.blogspot.com/2018/03/updates-to-left-of-me-updates-to-right.html)

[AmcacheParser: Reducing the noise, finding the signal](https://binaryforay.blogspot.com/2015/07/amcacheparser-reducing-noise-finding.html)

[Everything gets an update, Sept 2018 edition](https://binaryforay.blogspot.com/2018/09/everything-gets-update-sept-2018-edition.html)

[Locked file support added to AmcacheParser, AppCompatCacheParser, MFTECmd, ShellBags Explorer (and SBECmd), and Registry Explorer (and RECmd)](https://binaryforay.blogspot.com/2019/01/locked-file-support-added-to.html)

# Download Eric Zimmerman's Tools

All of Eric Zimmerman's tools can be downloaded [here](https://ericzimmerman.github.io/#!index.md). 

# Special Thanks

Open Source Development funding and support provided by the following contributors: [SANS Institute](http://sans.org/) and [SANS DFIR](http://dfir.sans.org/).
