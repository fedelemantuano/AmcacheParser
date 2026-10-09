using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CsvHelper.Configuration;
using ServiceStack.Text;

namespace AmcacheParser;

internal sealed class JsonLinesWriter : IDisposable
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd", "yyyyMMdd", "M/d/yyyy", "M-d-yyyy"
    };
    private static readonly string[] TimestampFormats =
    {
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:ss'Z'"
    };
    private readonly StreamWriter _writer;

    internal JsonLinesWriter(string path)
    {
        _writer = new StreamWriter(path, false, new UTF8Encoding(false));
    }

    internal void WriteRecords<T>(IEnumerable<T> records, string recordType, ClassMap<T> map)
    {
        // Read original values from the shared CSV map, bypassing its display converters.
        // Enumerable association lists are not member maps and are never serialized here.
        var properties = map.MemberMaps
            .Where(m => !m.Data.Ignore)
            .Select(m => (Property: (PropertyInfo)m.Data.Member, Name: m.Data.Names[0]))
            .ToArray();

        using var scope = JsConfig.With(new Config
        {
            IncludeNullValues = true,
            IncludeNullValuesInDictionaries = true,
            ExcludeTypeInfo = true,
            TextCase = TextCase.Default
        });

        foreach (var record in records)
        {
            var values = new Dictionary<string, object> { ["RecordType"] = recordType };
            foreach (var property in properties)
            {
                values[property.Name] = Normalize(property.Property.Name, property.Property.GetValue(record));
            }

            _writer.WriteLine(JsonSerializer.SerializeToString(values));
        }
    }

    private static object Normalize(string name, object value)
    {
        if (value is DateTimeOffset timestamp)
        {
            // AmcacheNew uses this value to recover an invalid LinkDate.
            return timestamp == DateTimeOffset.MinValue
                ? null
                : timestamp.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        }

        if (name is "DriverVerDate" or "InstallDateArpLastModified" or "InstallDateFromLinkFile")
        {
            var text = value as string;
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            // A date string without an explicit timezone is not treated as a UTC instant.
            if (DateTimeOffset.TryParseExact(text, TimestampFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var instant))
            {
                return instant == DateTimeOffset.MinValue
                    ? null
                    : instant.ToString(TimestampFormat, CultureInfo.InvariantCulture);
            }

            return null;
        }

        if (name is "BinaryType" or "BinFileVersion" or "BinProductVersion")
        {
            // These names are numeric in old hives and strings in new hives.
            return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return value;
    }

    public void Dispose() => _writer.Dispose();
}
