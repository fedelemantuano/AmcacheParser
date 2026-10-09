using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amcache.Classes;
using CsvHelper;
using CsvHelper.Configuration;
using NUnit.Framework;

namespace AmcacheParser.Tests;

public class JsonOutputTests
{
    private string _directory;
    private static readonly DateTimeOffset Timestamp =
        new DateTimeOffset(2026, 10, 9, 12, 15, 30, TimeSpan.FromHours(2)).AddTicks(1234567);

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "amcache-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, true);

    [TestCase("new", false, 7)]
    [TestCase("new", true, 10)]
    [TestCase("old", false, 2)]
    [TestCase("old", true, 5)]
    public void JsonOnlyExportsExpectedCategories(string format, bool includePrograms, int count)
    {
        var arguments = new List<string> { "--json", _directory, "--jsonf", "results.json" };
        if (includePrograms) arguments.Add("-i");
        RunSuccess(format, arguments.ToArray());
        var path = Path.Combine(_directory, "results.json");
        var records = ReadRecords(path);
        Assert.That(records.Length, Is.EqualTo(count));
        Assert.That(Directory.GetFiles(_directory).Select(Path.GetFileName), Is.EqualTo(new[] { "results.json" }));
        Assert.That(File.ReadAllBytes(path).Take(3), Is.Not.EqualTo(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.That(records.Count(r => r.GetProperty("RecordType").GetString() == "ProgramEntry"), Is.EqualTo(includePrograms ? 1 : 0));
        Assert.That(records.Count(r => r.GetProperty("RecordType").GetString() == "AssociatedFileEntry"), Is.EqualTo(includePrograms ? 2 : 0));
        foreach (var record in records)
        {
            Assert.That(record.EnumerateObject().All(p => p.Value.ValueKind is not JsonValueKind.Array and not JsonValueKind.Object));
        }
        if (format == "new")
        {
            var types = records.Select(r => r.GetProperty("RecordType").GetString()).ToArray();
            foreach (var type in new[] { "Shortcut", "DriverBinary", "DeviceContainer", "DriverPackage", "DevicePnp" })
                Assert.That(types, Does.Contain(type));
        }
    }

    [TestCase("old")]
    [TestCase("new")]
    public void JsonFieldsMatchActualCsvHeaders(string format)
    {
        RunSuccess(format, "--json", _directory, "--jsonf", "results.json", "--csv", _directory, "--csvf", "results.csv", "-i");
        var categories = new Dictionary<string, string>
        {
            ["UnassociatedFileEntries"] = "UnassociatedFileEntry", ["AssociatedFileEntries"] = "AssociatedFileEntry",
            ["ProgramEntries"] = "ProgramEntry", ["ShortCuts"] = "Shortcut", ["DriverBinaries"] = "DriverBinary",
            ["DeviceContainers"] = "DeviceContainer", ["DriverPackages"] = "DriverPackage", ["DevicePnps"] = "DevicePnp"
        };
        var records = ReadRecords(Path.Combine(_directory, "results.json"));
        foreach (var csvPath in Directory.GetFiles(_directory, "*.csv"))
        {
            var category = Path.GetFileNameWithoutExtension(csvPath).Substring("results_".Length);
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            var matching = records.Where(r => r.GetProperty("RecordType").GetString() == categories[category]).ToArray();
            var count = 0;
            while (csv.Read()) count++;
            Assert.That(matching.Length, Is.EqualTo(count), category);
            foreach (var record in matching)
                Assert.That(record.EnumerateObject().Where(p => p.Name != "RecordType").Select(p => p.Name), Is.EquivalentTo(csv.HeaderRecord), category);
        }
    }

    [TestCase("old")]
    [TestCase("new")]
    public void JsonIgnoresDisplayDateFormatAndPrecision(string format)
    {
        RunSuccess(format, "--json", _directory, "--jsonf", "default.json", "-i");
        RunSuccess(format, "--json", _directory, "--jsonf", "custom.json", "-i", "--dt", "dd/MM/yyyy HH:mm:ss");
        RunSuccess(format, "--json", _directory, "--jsonf", "precise.json", "-i", "--mp");
        Assert.That(File.ReadAllText(Path.Combine(_directory, "custom.json")), Is.EqualTo(File.ReadAllText(Path.Combine(_directory, "default.json"))));
        Assert.That(File.ReadAllText(Path.Combine(_directory, "precise.json")), Is.EqualTo(File.ReadAllText(Path.Combine(_directory, "default.json"))));
    }

    [TestCase("old", false)]
    [TestCase("old", true)]
    [TestCase("new", false)]
    [TestCase("new", true)]
    public void FileHashFiltersPreserveBlacklistPrecedence(string format, bool both)
    {
        var included = new string('a', 40);
        var blacklist = Path.Combine(_directory, "blacklist.txt");
        var whitelist = Path.Combine(_directory, "whitelist.txt");
        File.WriteAllText(blacklist, included.ToUpperInvariant());
        File.WriteAllText(whitelist, new string('b', 40));
        var args = new List<string> { "--json", _directory, "--jsonf", "filtered.json", "-i", "-b", blacklist };
        if (both) args.AddRange(new[] { "-w", whitelist });
        RunSuccess(format, args.ToArray());
        var records = ReadRecords(Path.Combine(_directory, "filtered.json"));
        var files = records.Where(r => r.TryGetProperty("SHA1", out _)).ToArray();
        Assert.That(files.Length, Is.EqualTo(2));
        Assert.That(files.All(r => r.GetProperty("SHA1").GetString() == included));
        Assert.That(records.Length, Is.EqualTo(format == "new" ? 8 : 3));
    }

    [TestCase("old")]
    [TestCase("new")]
    public void WhitelistExcludesMatchingFileHashes(string format)
    {
        var path = Path.Combine(_directory, "whitelist.txt");
        File.WriteAllText(path, new string('a', 40));
        RunSuccess(format, "--json", _directory, "--jsonf", "filtered.json", "-i", "-w", path);
        var files = ReadRecords(Path.Combine(_directory, "filtered.json")).Where(r => r.TryGetProperty("SHA1", out _)).ToArray();
        Assert.That(files.Length, Is.EqualTo(2));
        Assert.That(files.All(r => r.GetProperty("SHA1").GetString() == new string('b', 40)));
    }

    [Test]
    public void MissingAndRecoveredInvalidDatesAreExplicitNulls()
    {
        RunSuccess("new", "--json", _directory, "--jsonf", "dates.json", "-i");
        var records = ReadRecords(Path.Combine(_directory, "dates.json"));
        Assert.That(records[0].GetProperty("LinkDate").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(records[1].GetProperty("LinkDate").GetString(), Is.EqualTo("2026-10-09T10:15:30.123Z"));
        var program = records.Single(r => r.GetProperty("RecordType").GetString() == "ProgramEntry");
        Assert.That(program.GetProperty("InstallDateMsi").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(program.GetProperty("InstallDateFromLinkFile").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(program.GetProperty("InstallDateArpLastModified").GetString(), Is.EqualTo("2026-10-09"));
    }

    [TestCase("10/09/2026", "2026-10-09")]
    [TestCase("10-09-2026", "2026-10-09")]
    [TestCase("20261009", "2026-10-09")]
    [TestCase("2026-10-09", "2026-10-09")]
    [TestCase("2026-10-09T12:15:30.1234567+02:00", "2026-10-09T10:15:30.123Z")]
    [TestCase("2026-10-09T10:15:30Z", "2026-10-09T10:15:30.000Z")]
    [TestCase("0001-01-01T00:00:00Z", null)]
    [TestCase("00/00/0000", null)]
    [TestCase("bad date", null)]
    [TestCase("2026-10-09T10:15:30", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void StringDatesHaveAnExplicitInvariantContract(string input, string expected)
    {
        var record = Create<DevicePnp>(new Dictionary<string, object> { ["driverVerDate"] = input });
        var result = WriteOne(record, ExportMaps.DevicePnp(Context(), "dd/MM/yyyy"));
        Assert.That(result.GetProperty("DriverVerDate").GetString(), Is.EqualTo(expected));
    }

    [TestCase("it-IT")]
    [TestCase("ar-SA")]
    public void TimestampOffsetsAndFractionsAreNormalizedIndependentlyOfCulture(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            var record = Create<FileEntryOld>(new Dictionary<string, object> { ["lastWrite"] = Timestamp, ["created"] = Timestamp.ToOffset(TimeSpan.FromHours(-7)) });
            var result = WriteOne(record, ExportMaps.OldUnassociatedFile(Context(), "dd/MM/yyyy"));
            Assert.That(result.GetProperty("FileIDLastWriteTimestamp").GetString(), Is.EqualTo("2026-10-09T10:15:30.123Z"));
            Assert.That(result.GetProperty("Created").GetString(), Is.EqualTo("2026-10-09T10:15:30.123Z"));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Test]
    public void ConflictingLegacyFieldsAreStringsWithoutLosingIntegerPrecision()
    {
        var record = Create<FileEntryOld>(new Dictionary<string, object> { ["binFileVer"] = ulong.MaxValue, ["binProdVer"] = long.MaxValue, ["binType"] = 2, ["fileSize"] = 123 });
        var result = WriteOne(record, ExportMaps.OldUnassociatedFile(Context(), "O"));
        Assert.That(result.GetProperty("BinFileVersion").GetString(), Is.EqualTo("18446744073709551615"));
        Assert.That(result.GetProperty("BinProductVersion").GetString(), Is.EqualTo("9223372036854775807"));
        Assert.That(result.GetProperty("BinaryType").GetString(), Is.EqualTo("2"));
        Assert.That(result.GetProperty("FileSize").GetInt32(), Is.EqualTo(123));
    }

    [Test]
    public void JsonEscapesEmbeddedNewlinesAndRetainsNativeBooleans()
    {
        var record = Create<FileEntryNew>(new Dictionary<string, object> { ["description"] = "café\n\"quoted\"\r\nnext", ["isPe"] = true, ["size"] = 123L });
        var result = WriteOne(record, ExportMaps.NewUnassociatedFile(Context(), "O"));
        Assert.That(File.ReadAllLines(Path.Combine(_directory, "unit.json")).Length, Is.EqualTo(1));
        Assert.That(result.GetProperty("Description").GetString(), Is.EqualTo("café\n\"quoted\"\r\nnext"));
        Assert.That(result.GetProperty("IsPeFile").GetBoolean());
        Assert.That(result.GetProperty("Size").GetInt64(), Is.EqualTo(123));
    }

    [Test]
    public void ReplacingAnOutputTruncatesAllOldRecords()
    {
        var record = Create<Shortcut>();
        var path = Path.Combine(_directory, "unit.json");
        using (var writer = new JsonLinesWriter(path))
            writer.WriteRecords(Enumerable.Repeat(record, 100), "Shortcut", ExportMaps.Shortcut(Context(), "O"));
        WriteOne(record, ExportMaps.Shortcut(Context(), "O"));
        Assert.That(ReadRecords(path).Length, Is.EqualTo(1));
    }

    [Test]
    public void DefaultFilenameIdentifiesTheHiveAndCustomFilenameIsExact()
    {
        RunSuccess("old", "--json", _directory);
        Assert.That(Path.GetFileName(Directory.GetFiles(_directory).Single()), Does.Match(@"^\d{14}_old_AmcacheParser\.json$"));
        Assert.That(JsonExport.GetPath(_directory, "ignored/custom.json", "old.hve", "123"), Is.EqualTo(Path.Combine(_directory, "custom.json")));
    }

    [Test]
    public void CsvOnlyStillWorks()
    {
        RunSuccess("old", "--csv", _directory, "--csvf", "results.csv", "-i", "--dt", "dd/MM/yyyy HH:mm:ss");
        Assert.That(Directory.GetFiles(_directory, "*.csv").Length, Is.EqualTo(3));
        Assert.That(Directory.GetFiles(_directory, "*.json"), Is.Empty);
    }

    [Test]
    public void InvalidOutputArgumentsAndWriteFailuresReturnNonzeroExitCodes()
    {
        Assert.That(Run("old").ExitCode, Is.Not.Zero);
        Assert.That(Run("old", "--csv", _directory, "--jsonf", "results.json").ExitCode, Is.Not.Zero);
        var blocked = Path.Combine(_directory, "file-not-directory");
        File.WriteAllText(blocked, "existing file");
        Assert.That(Run("old", "--json", blocked).ExitCode, Is.Not.Zero);
    }

    [Test]
    public async Task ElasticsearchAcceptsBothHiveFormatsInOneIndex()
    {
        var url = Environment.GetEnvironmentVariable("AMCACHE_ELASTICSEARCH_URL");
        if (string.IsNullOrEmpty(url)) Assert.Ignore("Set AMCACHE_ELASTICSEARCH_URL to run live indexing validation.");
        RunSuccess("old", "--json", _directory, "--jsonf", "old.json", "-i");
        RunSuccess("new", "--json", _directory, "--jsonf", "new.json", "-i");
        using var client = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(20) };
        var index = "amcache-json-test-" + Guid.NewGuid().ToString("N");
        var mapping = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "elasticsearch-mapping.json"));
        using var created = await client.PutAsync(index, new StringContent(mapping, Encoding.UTF8, "application/json"));
        created.EnsureSuccessStatusCode();
        try
        {
            var bulk = new StringBuilder();
            foreach (var name in new[] { "old.json", "new.json" })
                foreach (var line in File.ReadLines(Path.Combine(_directory, name)))
                {
                    bulk.AppendLine("{\"index\":{\"_index\":\"" + index + "\"}}");
                    bulk.AppendLine(line);
                }
            using var indexed = await client.PostAsync("_bulk?refresh=true", new StringContent(bulk.ToString(), Encoding.UTF8, "application/x-ndjson"));
            indexed.EnsureSuccessStatusCode();
            var bulkResponse = await indexed.Content.ReadAsStringAsync();
            using var result = JsonDocument.Parse(bulkResponse);
            Assert.That(result.RootElement.GetProperty("errors").GetBoolean(), Is.False, bulkResponse);
            Assert.That(result.RootElement.GetProperty("items").GetArrayLength(), Is.EqualTo(15));

            using var search = await client.PostAsync(index + "/_search", new StringContent(
                "{\"size\":0,\"aggs\":{\"types\":{\"terms\":{\"field\":\"RecordType\",\"size\":20}}}}", Encoding.UTF8, "application/json"));
            search.EnsureSuccessStatusCode();
            using var searched = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
            Assert.That(searched.RootElement.GetProperty("hits").GetProperty("total").GetProperty("value").GetInt32(), Is.EqualTo(15));
            Assert.That(searched.RootElement.GetProperty("aggregations").GetProperty("types").GetProperty("buckets").GetArrayLength(), Is.EqualTo(8));

            using var dates = await client.PostAsync(index + "/_search", new StringContent(
                "{\"query\":{\"range\":{\"LinkDate\":{\"gte\":\"2026-10-09T10:15:30.123Z\",\"lte\":\"2026-10-09T10:15:30.123Z\"}}},\"docvalue_fields\":[\"LinkDate\"]}", Encoding.UTF8, "application/json"));
            dates.EnsureSuccessStatusCode();
            using var matched = JsonDocument.Parse(await dates.Content.ReadAsStringAsync());
            Assert.That(matched.RootElement.GetProperty("hits").GetProperty("total").GetProperty("value").GetInt32(), Is.EqualTo(3));
            foreach (var hit in matched.RootElement.GetProperty("hits").GetProperty("hits").EnumerateArray())
                Assert.That(hit.GetProperty("fields").GetProperty("LinkDate")[0].GetString(), Is.EqualTo("2026-10-09T10:15:30.123Z"));
        }
        finally
        {
            using var deleted = await client.DeleteAsync(index);
            deleted.EnsureSuccessStatusCode();
        }
    }

    private void RunSuccess(string format, params string[] args)
    {
        var result = Run(format, args);
        Assert.That(result.ExitCode, Is.Zero, result.Output);
    }

    private (int ExitCode, string Output) Run(string format, params string[] args)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add("-f");
        start.ArgumentList.Add(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", format + ".hve"));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(20_000))
        {
            process.Kill(true);
            Assert.Fail("Parser exceeded its subprocess timeout.");
        }
        return (process.ExitCode, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
    }

    private JsonElement WriteOne<T>(T record, ClassMap<T> map)
    {
        var path = Path.Combine(_directory, "unit.json");
        using (var writer = new JsonLinesWriter(path)) writer.WriteRecords(new[] { record }, "Test", map);
        return ReadRecords(path).Single();
    }

    private static JsonElement[] ReadRecords(string path) => File.ReadAllLines(path).Select(line =>
    {
        using var document = JsonDocument.Parse(line);
        Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
        return document.RootElement.Clone();
    }).ToArray();

    private static CsvContext Context() => new(new CsvConfiguration(CultureInfo.InvariantCulture));

    private static T Create<T>(Dictionary<string, object> overrides = null)
    {
        var constructor = typeof(T).GetConstructors().Single();
        var args = constructor.GetParameters().Select(p =>
        {
            if (overrides != null && overrides.TryGetValue(p.Name, out var value)) return value;
            if (p.ParameterType == typeof(string)) return p.Name == "keyName" ? "00000001" : "";
            if (p.ParameterType == typeof(DateTimeOffset)) return (object)Timestamp;
            if (p.ParameterType == typeof(byte[])) return Array.Empty<byte>();
            return p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
        }).ToArray();
        return (T)constructor.Invoke(args);
    }
}
