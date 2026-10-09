using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using Amcache;
using Amcache.Classes;
using CsvHelper;
using CsvHelper.TypeConversion;
using Exceptionless;
using Serilog;
using Serilog.Core;
using Serilog.Events;


namespace AmcacheParser;

internal class Program
{
    private static readonly string _preciseTimeFormat = "yyyy-MM-dd HH:mm:ss.fffffff";

    private static readonly string Header = $"AmcacheParser version {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}" +
                                            "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                                            "\r\nhttps://github.com/EricZimmerman/AmcacheParser";

    private static readonly string  Footer = @"Examples: AmcacheParser.exe -f ""C:\Temp\amcache\AmcacheWin10.hve"" --csv C:\temp" +
                                             "\r\n\t " +
                                             @"   AmcacheParser.exe -f ""C:\Temp\amcache\AmcacheWin10.hve"" -i --csv C:\temp --csvf foo.csv" + "\r\n\t " +
                                             @"   AmcacheParser.exe -f ""C:\Temp\amcache\AmcacheWin10.hve"" -w ""c:\temp\whitelist.txt"" --csv C:\temp" +
                                             "\r\n\t " +
                                             @"   AmcacheParser.exe -f ""C:\Temp\amcache\AmcacheWin10.hve"" --json C:\temp --jsonf results.json" +
                                             "\r\n\t" +
                                             "\r\n\t" +
                                             "    Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes";
        
    private static RootCommand _rootCommand;
    
    private static Stopwatch _sw;

    private static string _activeDateTimeFormat;
    private static async Task Main(string[] args)
    {
        ExceptionlessClient.Default.Startup("prIG996gFK1y6DaZEoXh3InSg8LwrHcQV4Dze2r8");

        var fOpt = new Option<string>("-f")
        {
            Description = "Amcache.hve file to parse"
        };
        
        var csvOpt = new Option<string>(
            "--csv")
        {
            Description = "Directory to save CSV formatted results to. This or --json is required"
        
        };
        
        var csvfOpt = new Option<string>(
            "--csvf")
        {
            Description   = "File name to save CSV formatted results to. When present, overrides default name\r\n"
        };

        var jsonOpt = new Option<string>("--json")
        {
            Description = "Directory to save JSON Lines results to. This or --csv is required"
        };
        var jsonfOpt = new Option<string>("--jsonf")
        {
            Description = "File name for the single JSON Lines output. Overrides the default name; requires --json"
        };
        
        var iOpt = new Option<bool>(
            "-i")
        {
            Description = "Include file entries for Programs entries",
            DefaultValueFactory = _ =>  false
        
        };
        
          
        var wOpt = new Option<string>(
            "-w")
        {
            Description = "Path to file containing SHA-1 hashes to *exclude* from the results. Blacklisting overrides whitelisting"
        
        };
        
        var bOpt = new Option<string>(
            "-b")
        {
            Description = "Path to file containing SHA-1 hashes to *include* from the results. Blacklisting overrides whitelisting"
        };
        
        var dtOpt = new Option<string>(
            "--dt"){
            Description =        "The custom date/time format for CSV and displayed timestamps. JSON always uses ISO 8601 UTC. See https://goo.gl/CNVq0k for options",
            DefaultValueFactory = _ => "yyyy-MM-dd HH:mm:ss"
        };
        
        var mpOpt = new Option<bool>("--mp")
        {
            Description = "When true, use higher precision for CSV and displayed timestamps. JSON always uses milliseconds",
            DefaultValueFactory = _ => false
        };
        
        var nlOpt = new Option<bool>("--nl")
        {
            Description = "When true, ignore transaction log files for dirty hives. Default is FALSE",
            DefaultValueFactory = _ => false
        };

        var debugOpt = new Option<bool>("--debug")
        {
            Description = "Show debug information during processing",
            DefaultValueFactory = _ => false
        };
        var traceOpt = new Option<bool>("--trace")
        {
            Description = "Show trace information during processing",
            DefaultValueFactory = _ => false
        };
        
        _rootCommand = new RootCommand
        {
            fOpt,
            iOpt,
            wOpt,
            bOpt,
            csvOpt,
            csvfOpt,
            jsonOpt,
            jsonfOpt,
            dtOpt,
            mpOpt,
            nlOpt,
            debugOpt,
            traceOpt
        };
            
        _rootCommand.Description = Header + "\r\n\r\n" +Footer;

        _rootCommand.SetAction(result => DoWork(result.GetValue(fOpt), result.GetValue(iOpt), result.GetValue(wOpt),
            result.GetValue(bOpt), result.GetValue(csvOpt), result.GetValue(csvfOpt),
            result.GetValue(jsonOpt), result.GetValue(jsonfOpt), result.GetValue(dtOpt),
            result.GetValue(mpOpt),result.GetValue(nlOpt),result.GetValue(debugOpt),result.GetValue(traceOpt)));
            

        Environment.ExitCode = await _rootCommand.Parse(args).InvokeAsync();
        
        Log.CloseAndFlush();
           
    }
    
    class DateTimeOffsetFormatter : IFormatProvider, ICustomFormatter
    {
        private readonly IFormatProvider _innerFormatProvider;

        public DateTimeOffsetFormatter(IFormatProvider innerFormatProvider)
        {
            _innerFormatProvider = innerFormatProvider;
        }

        public object GetFormat(Type formatType)
        {
            return formatType == typeof(ICustomFormatter) ? this : _innerFormatProvider.GetFormat(formatType);
        }

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (arg is DateTimeOffset dateTimeOffset)
            {
                return dateTimeOffset.ToString(_activeDateTimeFormat);
            }

            if (arg is IFormattable formattable)
            {
                return formattable.ToString(format, _innerFormatProvider);
            }

            return arg.ToString();
        }
    }

    private static int DoWork(string f,bool i,string w,string b, string csv,string csvf,string json,string jsonf,string dt,bool mp,bool nl,bool debug,bool trace)
    {
        var levelSwitch = new LoggingLevelSwitch();

        _activeDateTimeFormat = dt;
        
        if (mp)
        {
            _activeDateTimeFormat = _preciseTimeFormat;
        }
        
        var formatter  =
            new DateTimeOffsetFormatter(CultureInfo.CurrentCulture);
        

        var template = "{Message:lj}{NewLine}{Exception}";

        if (debug)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Debug;
            template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        }

        if (trace)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Verbose;
            template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        }
        
        var conf = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: template,formatProvider: formatter)
            .MinimumLevel.ControlledBy(levelSwitch);
      
        Log.Logger = conf.CreateLogger();
        
        if (string.IsNullOrEmpty(f))
        {
            var aaa = new CustomHelpAction(new HelpAction());
            aaa.Invoke(_rootCommand.Parse("-f is required. Exiting"));

            return 1;
        }
        
        if (string.IsNullOrEmpty(csv) && string.IsNullOrEmpty(json))
        {
            var aaa = new CustomHelpAction(new HelpAction());
            aaa.Invoke(_rootCommand.Parse("--csv or --json is required. Exiting"));

            return 1;
        }

        if (!string.IsNullOrEmpty(jsonf) && string.IsNullOrEmpty(json))
        {
            Log.Error("--jsonf requires --json. Exiting");
            return 1;
        }
        
        if (!File.Exists(f))
        {
            Log.Error("{F} not found. Exiting",f);
            Console.WriteLine();
            return 1;
        }

        Log.Information("{Header}",Header);
        Console.WriteLine();
        Log.Information("Command line: {Args}",string.Join(" ",Environment.GetCommandLineArgs().Skip(1)));
        Console.WriteLine();

        if (mp)
        {
            dt = _preciseTimeFormat;
        }

        if (IsAdministrator() == false)
        {
            Log.Warning("Warning: Administrator privileges not found!");
            Console.WriteLine();
        }

        _sw = new Stopwatch();
        _sw.Start();
        
        var ts1 = DateTime.Now.ToString("yyyyMMddHHmmss");

        f = Path.GetFullPath(f);

        try
        {
            if (Helper.IsNewFormat(f, nl))
            {
                Log.Debug($"Processing new format hive");

                var amNew = new AmcacheNew(f,
                    true, nl);

                if (amNew.ProgramsEntries.Count == 0 && amNew.UnassociatedFileEntries.Count == 0)
                {
                    Log.Warning("Hive did not contain program entries nor file entries");
                }

                _sw.Stop();

                var whitelistHashes1 = new HashSet<string>();

                var useBlacklist2 = false;

                if (b?.Length > 0)
                {
                    if (File.Exists(b))
                    {
                        foreach (var readLine in File.ReadLines(b))
                        {
                            whitelistHashes1.Add(readLine.ToLowerInvariant());
                        }

                        useBlacklist2 = true;
                    }
                    else
                    {
                        Log.Warning("{B} does not exist",b);
                    }
                }
                else if (w?.Length > 0)
                {
                    if (File.Exists(w))
                    {
                        foreach (var readLine in File.ReadLines(w))
                        {
                            whitelistHashes1.Add(readLine.ToLowerInvariant());
                        }
                    }
                    else
                    {
                        Log.Warning("{W} does not exist",w);
                    }
                }

                var cleanList2 =
                    amNew.UnassociatedFileEntries.Where(t => whitelistHashes1.Contains(t.SHA1) == useBlacklist2)
                        .ToList();
                var totalProgramFileEntries2 = 0;

                if (!string.IsNullOrEmpty(csv) && Directory.Exists(csv) == false)
                {
                    try
                    {
                        Directory.CreateDirectory(csv);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex,"There was an error creating directory {Csv}. Error: {Message} Exiting",csv,ex.Message);
                        Console.WriteLine();
                        return 1;
                    }
                }

                foreach (var pe in amNew.ProgramsEntries)
                {
                    var cleanList22 =
                        pe.FileEntries.Where(t => whitelistHashes1.Contains(t.SHA1) == useBlacklist2).ToList();
                    totalProgramFileEntries2 += cleanList22.Count;
                }

                
                if (!string.IsNullOrEmpty(csv))
                {
                    var hiveName1 = Path.GetFileNameWithoutExtension(f);

                    var outbase1 = $"{ts1}_{hiveName1}_UnassociatedFileEntries.csv";

                    if (string.IsNullOrEmpty(csvf) == false)
                    {
                        outbase1 =
                            $"{Path.GetFileNameWithoutExtension(csvf)}_UnassociatedFileEntries{Path.GetExtension(csvf)}";
                    }

                    var outFile1 = Path.Combine(csv, outbase1);

                    using (var sw = new StreamWriter(outFile1))
                    {
                        sw.AutoFlush = true;

                        var csvWriter = new CsvWriter(sw,CultureInfo.InvariantCulture);

                        var o = new TypeConverterOptions
                        {
                            DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                        };
                        csvWriter.Context.TypeConverterOptionsCache.AddOptions<FileEntryNew>(o);

                        var foo = ExportMaps.NewUnassociatedFile(csvWriter.Context, dt);
                        csvWriter.Context.RegisterClassMap(foo);

                        csvWriter.WriteHeader<FileEntryNew>();
                        csvWriter.NextRecord();
                        csvWriter.WriteRecords(cleanList2);
                    }

                    if (i)
                    {
                        outbase1 = $"{ts1}_{hiveName1}_ProgramEntries.csv";

                        if (string.IsNullOrEmpty(csvf) == false)
                        {
                            outbase1 =
                                $"{Path.GetFileNameWithoutExtension(csvf)}_ProgramEntries{Path.GetExtension(csvf)}";
                        }

                        outFile1 = Path.Combine(csv, outbase1);

                        using (var sw = new StreamWriter(outFile1))
                        {
                            sw.AutoFlush = true;

                            var csvWriter = new CsvWriter(sw,CultureInfo.InvariantCulture);

                            var o = new TypeConverterOptions
                            {
                                DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                            };
                            csvWriter.Context.TypeConverterOptionsCache.AddOptions<ProgramsEntryNew>(o);

                            var foo = ExportMaps.NewProgram(csvWriter.Context, dt);
                            csvWriter.Context.RegisterClassMap(foo);

                            csvWriter.WriteHeader<ProgramsEntryNew>();
                            csvWriter.NextRecord();
                            csvWriter.WriteRecords(amNew.ProgramsEntries);
                        }

                        outbase1 = $"{ts1}_{hiveName1}_AssociatedFileEntries.csv";

                        if (string.IsNullOrEmpty(csvf) == false)
                        {
                            outbase1 =
                                $"{Path.GetFileNameWithoutExtension(csvf)}_AssociatedFileEntries{Path.GetExtension(csvf)}";
                        }

                        outFile1 = Path.Combine(csv, outbase1);

                        using (var sw = new StreamWriter(outFile1))
                        {
                            var csvWriter = new CsvWriter(sw,CultureInfo.InvariantCulture);

                            var o = new TypeConverterOptions
                            {
                                DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                            };
                            csvWriter.Context.TypeConverterOptionsCache.AddOptions<FileEntryNew>(o);

                            var foo = ExportMaps.NewAssociatedFile(csvWriter.Context, dt);
                            csvWriter.Context.RegisterClassMap(foo);

                            csvWriter.WriteHeader<FileEntryNew>();
                            csvWriter.NextRecord();

                            sw.AutoFlush = true;

                            foreach (var pe in amNew.ProgramsEntries)
                            {
                                var cleanList22 =
                                    pe.FileEntries.Where(t => whitelistHashes1.Contains(t.SHA1) == useBlacklist2)
                                        .ToList();

                                csvWriter.WriteRecords(cleanList22);
                            }
                        }
                    }


                    outbase1 = $"{ts1}_{hiveName1}_ShortCuts.csv";

                    if (string.IsNullOrEmpty(csvf) == false)
                    {
                        outbase1 =
                            $"{Path.GetFileNameWithoutExtension(csvf)}_ShortCuts{Path.GetExtension(csvf)}";
                    }

                    outFile1 = Path.Combine(csv, outbase1);

                    using (var sw = new StreamWriter(outFile1))
                    {
                        var csvWriter = new CsvWriter(sw,CultureInfo.InvariantCulture);

                        var o = new TypeConverterOptions
                        {
                            DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                        };
                        csvWriter.Context.TypeConverterOptionsCache.AddOptions<Shortcut>(o);

                        var foo = ExportMaps.Shortcut(csvWriter.Context, dt);
                        csvWriter.Context.RegisterClassMap(foo);

                        csvWriter.WriteHeader<Shortcut>();
                        csvWriter.NextRecord();
                        sw.AutoFlush = true;

                        foreach (var sc in amNew.ShortCuts)
                        {
                            csvWriter.WriteRecord(sc);
                            csvWriter.NextRecord();
                        }
                    }

                    outbase1 = $"{ts1}_{hiveName1}_DriverBinaries.csv";

                    if (string.IsNullOrEmpty(csvf) == false)
                    {
                        outbase1 =
                            $"{Path.GetFileNameWithoutExtension(csvf)}_DriverBinaries{Path.GetExtension(csvf)}";
                    }

                    outFile1 = Path.Combine(csv, outbase1);

                    using (var sw = new StreamWriter(outFile1))
                    {
                        var csvWriter = new CsvWriter(sw,CultureInfo.InvariantCulture);


                        var o = new TypeConverterOptions
                        {
                            DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                        };
                        csvWriter.Context.TypeConverterOptionsCache.AddOptions<DriverBinary>(o);

                        var foo = ExportMaps.DriverBinary(csvWriter.Context, dt);
                        csvWriter.Context.RegisterClassMap(foo);

                        csvWriter.WriteHeader<DriverBinary>();
                        csvWriter.NextRecord();
                        sw.AutoFlush = true;

                        foreach (var sc in amNew.DriveBinaries)
                        {
                            csvWriter.WriteRecord(sc);
                            csvWriter.NextRecord();
                        }
                    }


                    outbase1 = $"{ts1}_{hiveName1}_DeviceContainers.csv";

                    if (string.IsNullOrEmpty(csvf) == false)
                    {
                        outbase1 =
                            $"{Path.GetFileNameWithoutExtension(csvf)}_DeviceContainers{Path.GetExtension(csvf)}";
                    }

                    outFile1 = Path.Combine(csv, outbase1);

                    using (var sw = new StreamWriter(outFile1))
                    {
                        var csvWriter = new CsvWriter(sw,CultureInfo.InvariantCulture);


                        var o = new TypeConverterOptions
                        {
                            DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                        };
                        csvWriter.Context.TypeConverterOptionsCache.AddOptions<DeviceContainer>(o);

                        var foo = ExportMaps.DeviceContainer(csvWriter.Context, dt);
                        csvWriter.Context.RegisterClassMap(foo);

                        csvWriter.WriteHeader<DeviceContainer>();
                        csvWriter.NextRecord();
                        sw.AutoFlush = true;

                        foreach (var sc in amNew.DeviceContainers)
                        {
                            csvWriter.WriteRecord(sc);
                            csvWriter.NextRecord();
                        }
                    }

                    outbase1 = $"{ts1}_{hiveName1}_DriverPackages.csv";

                    if (string.IsNullOrEmpty(csvf) == false)
                    {
                        outbase1 =
                            $"{Path.GetFileNameWithoutExtension(csvf)}_DriverPackages{Path.GetExtension(csvf)}";
                    }

                    outFile1 = Path.Combine(csv, outbase1);

                    using (var sw = new StreamWriter(outFile1))
                    {
                        var csvWriter = new CsvWriter(sw,CultureInfo.InvariantCulture);

                        var o = new TypeConverterOptions
                        {
                            DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                        };
                        csvWriter.Context.TypeConverterOptionsCache.AddOptions<DriverPackage>(o);

                        var foo = ExportMaps.DriverPackage(csvWriter.Context, dt);
                        csvWriter.Context.RegisterClassMap(foo);

                        csvWriter.WriteHeader<DriverPackage>();
                        csvWriter.NextRecord();
                        sw.AutoFlush = true;

                        foreach (var sc in amNew.DriverPackages)
                        {
                            csvWriter.WriteRecord(sc);
                            csvWriter.NextRecord();
                        }
                    }

                    outbase1 = $"{ts1}_{hiveName1}_DevicePnps.csv";

                    if (string.IsNullOrEmpty(csvf) == false)
                    {
                        outbase1 =
                            $"{Path.GetFileNameWithoutExtension(csvf)}_DevicePnps{Path.GetExtension(csvf)}";
                    }


                    outFile1 = Path.Combine(csv, outbase1);

                    using (var sw = new StreamWriter(outFile1))
                    {
                        var csvWriter = new CsvWriter(sw,CultureInfo.InvariantCulture);

                        var o = new TypeConverterOptions
                        {
                            DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                        };
                        csvWriter.Context.TypeConverterOptionsCache.AddOptions<DevicePnp>(o);

                        var foo = ExportMaps.DevicePnp(csvWriter.Context, dt);
                        csvWriter.Context.RegisterClassMap(foo);

                        csvWriter.WriteHeader<DevicePnp>();
                        csvWriter.NextRecord();
                        sw.AutoFlush = true;

                        foreach (var sc in amNew.DevicePnps)
                        {
                            csvWriter.WriteRecord(sc);
                            csvWriter.NextRecord();
                        }
                    }
                }

                if (!string.IsNullOrEmpty(json))
                {
                    var jsonPath = JsonExport.GetPath(json, jsonf, f, ts1);
                    JsonExport.Write(jsonPath, amNew, cleanList2, i, whitelistHashes1, useBlacklist2);
                    Log.Information("JSON Lines results saved to: {JsonPath}", jsonPath);
                }

                Console.WriteLine();
                Log.Information("{F} is in new format!",f);

                Console.WriteLine();

                Log.Information("Total file entries found: {TotalFileEntries:N0}",amNew.TotalFileEntries);
                if (amNew.ShortCuts.Count > 0)
                {
                    Log.Information("Total shortcuts found: {Count:N0}",amNew.ShortCuts.Count);
                }

                if (amNew.DeviceContainers.Count > 0)
                {
                    Log.Information("Total device containers found: {Count:N0}",amNew.DeviceContainers.Count);
                }

                if (amNew.DevicePnps.Count > 0)
                {
                    Log.Information("Total device PnPs found: {Count:N0}",amNew.DevicePnps.Count);
                }

                if (amNew.DriveBinaries.Count > 0)
                {
                    Log.Information("Total drive binaries found: {Count:N0}",amNew.DriveBinaries.Count);
                }

                if (amNew.DriverPackages.Count > 0)
                {
                    Log.Information("Total driver packages found: {Count:N0}",amNew.DriverPackages.Count);
                }

                if (amNew.UnassociatedFileEntries.Count == 1)
                {
                    Console.WriteLine();
                    if (i)
                    {
                        Log.Information("Found {Count:N0} unassociated file entry and {TotalProgramFileEntries2:N0} program file entries (across {ProgramsEntries.Count:N0} program entries)",cleanList2.Count,totalProgramFileEntries2,amNew.ProgramsEntries.Count);
                    }
                    else
                    {
                        Log.Information("Found {Count:N0} unassociated file entry",cleanList2.Count);
                    }
                }
                else
                {
                    Console.WriteLine();
                    if (i)
                    {
                        Log.Information("Found {Count:N0} unassociated file entry and {TotalProgramFileEntries2:N0} program file entries (across {ProgramsEntriesCount:N0} program entries)",cleanList2.Count,totalProgramFileEntries2,amNew.ProgramsEntries.Count);
                    }
                    else
                    {
                        Log.Information("Found {Count:N0} unassociated file entry",cleanList2.Count);    
                    }
                }

                if (whitelistHashes1.Count > 0)
                {
                    var per = (double) (totalProgramFileEntries2 + cleanList2.Count) / amNew.TotalFileEntries;

                    Console.WriteLine();

                    var list = "whitelist";
                    if (b?.Length > 0)
                    {
                        list = "blacklist";
                    }

                    Log.Information("{List} hash count: {Count:N0}",UppercaseFirst(list),whitelistHashes1.Count);

                    Console.WriteLine();

                    Log.Information("Percentage of total shown based on {List}: {Per:P3} ({Per2:P3} savings)",list,per,1-per);
                }

                Console.WriteLine();

                if (!string.IsNullOrEmpty(csv))
                {
                    Log.Information("Results saved to: {Csv}",csv);
                }


                Console.WriteLine();
                Log.Information(
                    "Total parsing time: {TotalSeconds:N3} seconds",_sw.Elapsed.TotalSeconds);
                Console.WriteLine();


                return 0;
            }

            Log.Debug($"Processing old format hive");

            var am = new AmcacheOld(f,
                true, nl);


            if (am.ProgramsEntries.Count == 0 && am.UnassociatedFileEntries.Count == 0)
            {
                Log.Warning("Hive did not contain program entries nor file entries. Exiting");
                Console.WriteLine();
                return 0;
            }

            _sw.Stop();

            var whitelistHashes = new HashSet<string>();

            var useBlacklist = false;

            if (b?.Length > 0)
            {
                if (File.Exists(b))
                {
                    foreach (var readLine in File.ReadLines(b))
                    {
                        whitelistHashes.Add(readLine.ToLowerInvariant());
                    }

                    useBlacklist = true;
                }
                else
                {
                    Log.Warning("{B} does not exist",b);
                }
            }
            else if (w?.Length > 0)
            {
                if (File.Exists(w))
                {
                    foreach (var readLine in File.ReadLines(w))
                    {
                        whitelistHashes.Add(readLine.ToLowerInvariant());
                    }
                }
                else
                {
                    Log.Warning("{W} does not exist",w);
                }
            }

            var cleanList =
                am.UnassociatedFileEntries.Where(t => whitelistHashes.Contains(t.SHA1) == useBlacklist).ToList();
            var totalProgramFileEntries = 0;

            if (!string.IsNullOrEmpty(csv) && Directory.Exists(csv) == false)
            {
                try
                {
                    Directory.CreateDirectory(csv);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "There was an error creating directory {Csv}. Error: {Message} Exiting",csv,ex.Message);
                    Console.WriteLine();
                    return 1;
                }
            }


            foreach (var pe in am.ProgramsEntries)
            {
                var cleanList2 =
                    pe.FileEntries.Where(t => whitelistHashes.Contains(t.SHA1) == useBlacklist).ToList();
                totalProgramFileEntries += cleanList2.Count;
            }

            if (!string.IsNullOrEmpty(csv))
            {
                //var ts = DateTime.Now.ToString("yyyyMMddHHmmss");
                var hiveName = Path.GetFileNameWithoutExtension(f);

                var outbase = $"{ts1}_{hiveName}_UnassociatedFileEntries.csv";

                if (string.IsNullOrEmpty(csvf) == false)
                {
                    outbase =
                        $"{Path.GetFileNameWithoutExtension(csvf)}_UnassociatedFileEntries{Path.GetExtension(csvf)}";
                }

                var outFile = Path.Combine(csv, outbase);

                using (var sw = new StreamWriter(outFile))
                {
                    sw.AutoFlush = true;

                    var csvWriter = new CsvWriter(sw,CultureInfo.InvariantCulture);

                    var o = new TypeConverterOptions
                    {
                        DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                    };
                    csvWriter.Context.TypeConverterOptionsCache.AddOptions<FileEntryOld>(o);

                    var foo = ExportMaps.OldUnassociatedFile(csvWriter.Context, dt);
                    csvWriter.Context.RegisterClassMap(foo);

                    csvWriter.WriteHeader<FileEntryOld>();
                    csvWriter.NextRecord();
                    csvWriter.WriteRecords(cleanList);
                }

                if (i)
                {
                    outbase = $"{ts1}_{hiveName}_ProgramEntries.csv";

                    if (string.IsNullOrEmpty(csvf) == false)
                    {
                        outbase =
                            $"{Path.GetFileNameWithoutExtension(csvf)}_ProgramEntries{Path.GetExtension(csvf)}";
                    }

                    outFile = Path.Combine(csv, outbase);

                    using (var sw = new StreamWriter(outFile))
                    {
                        sw.AutoFlush = true;

                        var csvWriter = new CsvWriter(sw,CultureInfo.InvariantCulture);

                        var o = new TypeConverterOptions
                        {
                            DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                        };
                        csvWriter.Context.TypeConverterOptionsCache.AddOptions<ProgramsEntryOld>(o);

                        var foo = ExportMaps.OldProgram(csvWriter.Context, dt);
                        csvWriter.Context.RegisterClassMap(foo);

                        csvWriter.WriteHeader<ProgramsEntryOld>();
                        csvWriter.NextRecord();
                        csvWriter.WriteRecords(am.ProgramsEntries);
                    }

                    outbase = $"{ts1}_{hiveName}_AssociatedFileEntries.csv";

                    if (string.IsNullOrEmpty(csvf) == false)
                    {
                        outbase =
                            $"{Path.GetFileNameWithoutExtension(csvf)}_AssociatedFileEntries{Path.GetExtension(csvf)}";
                    }

                    outFile = Path.Combine(csv, outbase);

                    using (var sw = new StreamWriter(outFile))
                    {
                        var csvWriter = new CsvWriter(sw,CultureInfo.InvariantCulture);

                        var o = new TypeConverterOptions
                        {
                            DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                        };
                        csvWriter.Context.TypeConverterOptionsCache.AddOptions<FileEntryOld>(o);

                        var foo = ExportMaps.OldAssociatedFile(csvWriter.Context, dt);
                        csvWriter.Context.RegisterClassMap(foo);

                        csvWriter.WriteHeader<FileEntryOld>();
                        csvWriter.NextRecord();

                        sw.AutoFlush = true;

                        foreach (var pe in am.ProgramsEntries)
                        {
                            var cleanList2 =
                                pe.FileEntries.Where(t => whitelistHashes.Contains(t.SHA1) == useBlacklist).ToList();

                            csvWriter.WriteRecords(cleanList2);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(json))
            {
                var jsonPath = JsonExport.GetPath(json, jsonf, f, ts1);
                JsonExport.Write(jsonPath, am, cleanList, i, whitelistHashes, useBlacklist);
                Log.Information("JSON Lines results saved to: {JsonPath}", jsonPath);
            }

            Console.WriteLine();
            Log.Information("{F} is in old format!",f);

            Console.WriteLine();

            Log.Information("Total file entries found: {TotalFileEntries:N0}",am.TotalFileEntries);

            if (am.UnassociatedFileEntries.Count == 1)
            {
                if (i)
                {
                    Log.Information("Found {Count:N0} unassociated file entries and {TotalProgramFileEntries:N0} program file entries (across {ProgramsEntriesCount:N0} program entries)",cleanList.Count,totalProgramFileEntries,am.ProgramsEntries.Count);
                }
                else
                {
                    Log.Information("Found {Count:N0} unassociated file entry",cleanList.Count);    
                }
                
            }
            else
            {
                if (i)
                {
                    Log.Information("Found {Count:N0} unassociated file entries and {TotalProgramFileEntries:N0} program file entries (across {ProgramsEntriesCount:N0} program entries)",cleanList.Count,totalProgramFileEntries,am.ProgramsEntries.Count);
                }
                else
                {
                    Log.Information("Found {Count:N0} unassociated file entries",cleanList.Count);
                }
                    
            }

            if (whitelistHashes.Count > 0)
            {
                var per = (double) (totalProgramFileEntries + cleanList.Count) / am.TotalFileEntries;

                Console.WriteLine();

                var list = "whitelist";
                if (b?.Length > 0)
                {
                    list = "blacklist";
                }

                Log.Information("{List} hash count: {Count:N0}",UppercaseFirst(list),whitelistHashes.Count);

                Console.WriteLine();

                Log.Information("Percentage of total shown based on {List}: {Per:P3} ({Per2:P3} savings)",list,per,1-per);
            }

            Console.WriteLine();

            if (!string.IsNullOrEmpty(csv))
            {
                Log.Information("Results saved to: {Csv}",csv);
            }


            Console.WriteLine();
            Log.Information("Total parsing time: {TotalSeconds:N3} seconds",_sw.Elapsed.TotalSeconds);
            Console.WriteLine();
            return 0;
        }
        catch (Exception ex)
        {
            if (!ex.Message.Contains(
                    "Sequence numbers do not match and transaction logs were not found in the same directory as the hive. Abort")
               )
            {
                if (ex.Message.Contains("Administrator privileges not found"))
                {
                    Log.Fatal("Could not access {F} because it is in use",f);
                    Console.WriteLine();
                    Log.Fatal("Rerun the program with Administrator privileges to try again");
                    Console.WriteLine();
                }
                else
                {
                    Log.Error(ex,"There was an error: {Message}",ex.Message);
                    Console.WriteLine();
                    Log.Error("Please send {F} to {Email} in order to fix the issue",f,"saericzimmerman@gmail.com");                        
                }

            }

            return 1;
        }
    }
    
    private class CustomHelpAction : SynchronousCommandLineAction
    {
        private readonly HelpAction _defaultHelp;

        public CustomHelpAction(HelpAction action)
        {
            _defaultHelp = action;
        }

        public override int Invoke(ParseResult parseResult)
        {
            var result = _defaultHelp.Invoke(parseResult);

            Log.Warning("{Msg}", string.Join(" ",parseResult.Tokens));

            return result;
        }
    }

    private static string UppercaseFirst(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        var a = s.ToCharArray();
        a[0] = char.ToUpper(a[0]);
        return new string(a);
    }

    private static bool IsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return true;
        }
            
        Log.Debug("Checking for admin rights");
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}



internal class ApplicationArguments
{
    public string File { get; set; }

    //       public string Extension { get; set; } = string.Empty;
    public string Whitelist { get; set; } = string.Empty;

    public string Blacklist { get; set; } = string.Empty;
    public string CsvDirectory { get; set; } = string.Empty;
    public string CsvName { get; set; } = string.Empty;
    public bool IncludeLinked { get; set; } = false;
    public bool RecoverDeleted { get; set; } = false;
    public bool NoTransLogs { get; set; } = false;
    public string DateTimeFormat { get; set; }
    public bool PreciseTimestamps { get; set; }
    public bool Debug { get; set; }
    public bool Trace { get; set; }
}
