using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Amcache;
using Amcache.Classes;
using CsvHelper;
using CsvHelper.Configuration;

namespace AmcacheParser;

internal static class JsonExport
{
    // Maps provide schema only; their date display converters are not used by JSON.
    private const string MapDateFormat = "O";

    internal static string GetPath(string directory, string name, string hivePath, string timestamp)
    {
        var fileName = string.IsNullOrEmpty(name)
            ? $"{timestamp}_{Path.GetFileNameWithoutExtension(hivePath)}_AmcacheParser.json"
            : Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("--jsonf must specify a file name.", nameof(name));
        }

        return Path.Combine(directory, fileName);
    }

    internal static void Write(string path, AmcacheNew hive, IEnumerable<FileEntryNew> unassociated,
        bool includePrograms, HashSet<string> hashes, bool useBlacklist)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        using var writer = new JsonLinesWriter(path);
        var context = new CsvContext(new CsvConfiguration(CultureInfo.InvariantCulture));

        writer.WriteRecords(unassociated, "UnassociatedFileEntry", ExportMaps.NewUnassociatedFile(context, MapDateFormat));
        if (includePrograms)
        {
            writer.WriteRecords(hive.ProgramsEntries, "ProgramEntry", ExportMaps.NewProgram(context, MapDateFormat));
            writer.WriteRecords(hive.ProgramsEntries.SelectMany(p => p.FileEntries)
                    .Where(f => hashes.Contains(f.SHA1) == useBlacklist),
                "AssociatedFileEntry", ExportMaps.NewAssociatedFile(context, MapDateFormat));
        }

        writer.WriteRecords(hive.ShortCuts, "Shortcut", ExportMaps.Shortcut(context, MapDateFormat));
        writer.WriteRecords(hive.DriveBinaries, "DriverBinary", ExportMaps.DriverBinary(context, MapDateFormat));
        writer.WriteRecords(hive.DeviceContainers, "DeviceContainer", ExportMaps.DeviceContainer(context, MapDateFormat));
        writer.WriteRecords(hive.DriverPackages, "DriverPackage", ExportMaps.DriverPackage(context, MapDateFormat));
        writer.WriteRecords(hive.DevicePnps, "DevicePnp", ExportMaps.DevicePnp(context, MapDateFormat));
    }

    internal static void Write(string path, AmcacheOld hive, IEnumerable<FileEntryOld> unassociated,
        bool includePrograms, HashSet<string> hashes, bool useBlacklist)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        using var writer = new JsonLinesWriter(path);
        var context = new CsvContext(new CsvConfiguration(CultureInfo.InvariantCulture));

        writer.WriteRecords(unassociated, "UnassociatedFileEntry", ExportMaps.OldUnassociatedFile(context, MapDateFormat));
        if (includePrograms)
        {
            writer.WriteRecords(hive.ProgramsEntries, "ProgramEntry", ExportMaps.OldProgram(context, MapDateFormat));
            writer.WriteRecords(hive.ProgramsEntries.SelectMany(p => p.FileEntries)
                    .Where(f => hashes.Contains(f.SHA1) == useBlacklist),
                "AssociatedFileEntry", ExportMaps.OldAssociatedFile(context, MapDateFormat));
        }
    }
}
