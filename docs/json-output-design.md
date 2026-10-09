# JSON Lines output design

Status: accepted and implemented following user confirmation of rounds 1 and 2.

## Established requirements

- Add `--json` (output directory) and `--jsonf` (custom output naming), following MFTECmd's CLI.
- Write one JSON object per line, rather than a JSON array.
- Represent timestamps using ISO 8601 independently of `--dt`.
- Preserve existing CSV timestamp formatting and integrations.
- Produce one combined JSON Lines file with an additional `RecordType` field on each record (user decision, round 1).
- Use the same flat fields as the CSV exports, with native JSON scalar types and no nested program/file association lists (user decision, round 1).
- Prioritize compatibility with standard Elasticsearch date fields over sub-millisecond precision (user direction, round 1).
- Output explicit `null` for missing or invalid dates, including the known invalid `LinkDate` recovery sentinel (user decision, round 1). Additional raw-date fields have not been requested.

See [ADR 0001](adr/0001-json-timestamp-contract.md).

## Verified behavior and integration constraints

Before this change, AmcacheParser required `--csv`. Both hive formats export filtered unassociated file entries; `-i` additionally exports program entries and filtered associated file entries. New-format hives also export shortcuts, driver binaries, device containers, driver packages and Plug and Play devices without requiring `-i`.

[MFTECmd's implementation](https://github.com/EricZimmerman/MFTECmd/blob/master/MFTECmd/Program.cs) writes its export records individually with `WriteLine(record.ToJson())` and enables ISO 8601 date serialization. Its `--jsonf` uses the provided basename as the output filename. The user selected one combined JSON file while retaining the existing multiple-file CSV layout.

Most temporal properties are `DateTimeOffset` or nullable `DateTimeOffset`. However, `ProgramsEntryNew.InstallDateArpLastModified`, `ProgramsEntryNew.InstallDateFromLinkFile` and `DevicePnp.DriverVerDate` are strings. `AmcacheNew` also uses `DateTimeOffset.MinValue` as a recovery sentinel for an invalid `LinkDate`. The JSON exporter normalizes these string dates separately and outputs the recovery sentinel as `null`.

Elasticsearch's [`date` type](https://www.elastic.co/docs/reference/elasticsearch/mapping-reference/date) stores millisecond resolution. Retaining finer precision in JSON does not itself retain that precision in an indexed `date` field. The choice of JSON precision and corresponding mapping must be documented.

## Accepted output contract

- `--json` is sufficient without `--csv`; both options may be supplied together. At least one output directory is required.
- `--jsonf result.json` names the single JSON Lines output `result.json`, with no category suffix. Default naming: `<yyyyMMddHHmmss>_<hiveName>_AmcacheParser.json`.
- UTF-8 JSON Lines, one compact object per line, with numbers and booleans retained as JSON scalar types except the three normalized fields below.
- `RecordType` values: `UnassociatedFileEntry`, `ProgramEntry`, `AssociatedFileEntry`, `Shortcut`, `DriverBinary`, `DeviceContainer`, `DriverPackage`, `DevicePnp`.
- Preserve existing record selection, `-i` behavior and whitelist/blacklist precedence across both hive formats.
- Format timestamp instants as UTC `yyyy-MM-ddTHH:mm:ss.fffZ`, independent of `--dt` and `--mp`. Discard sub-millisecond fractions by truncation. Convert existing offsets to UTC rather than merely appending `Z`.
- Normalize all three string date fields included by CSV auto-mapping: `DriverVerDate`, `InstallDateArpLastModified` and `InstallDateFromLinkFile`. Accepted date-only formats are `yyyy-MM-dd`, `yyyyMMdd`, `M/d/yyyy` and `M-d-yyyy`, with explicit month/day interpretation. Timestamp strings require ISO `Z` or `±HH:mm` offsets. Date-only values become ISO `yyyy-MM-dd`; empty, malformed, unsupported or timezone-unspecified timestamp strings become `null`.
- Preserve the existing CSV representation and formatting, including CSV handling of invalid-date sentinels.
- Write a fresh output file rather than append to an existing output, consistent with CSV export behavior.

## Consistent field types across hive formats

Using a single JSON Lines file does not guarantee compatibility with a single Elasticsearch index. Old-format file entries define `BinaryType`, `BinFileVersion` and `BinProductVersion` as numbers, whereas new-format entries define the same field names as strings. A `RecordType` value does not make Elasticsearch select a different mapping for the same field name.

The user confirmed normalization of these three fields to JSON strings in both hive formats, retaining field names and exact values. Other numeric and boolean fields keep their native scalar types. See [ADR 0002](adr/0002-combined-json-records.md). The [example mapping](elasticsearch-mapping.json) explicitly maps dates and keyword discriminators, disables date inference for other strings, and supports the full unsigned range of `Usn`.

## Verification coverage

- Independently parse every output line as a JSON object and verify `RecordType` and category counts.
- Cover both hive formats, all categories, `-i`, blacklist/whitelist precedence, JSON-only and simultaneous CSV/JSON export.
- Verify timestamps with nonzero offsets, fractional seconds, null values, malformed temporal strings and the invalid `LinkDate` sentinel. Verify JSON timestamp independence from `--dt`, `--mp`, local timezone and culture.
- Compare JSON fields to actual CSV headers and ensure program records contain no nested file lists.
- Verify filename override and writing a shorter replacement output without stale records.
- Check compatible field types across categories and hive formats, including the three currently known conflicts.
- Build both supported application frameworks (`net472`, `net9.0`). Run the optional live Elasticsearch indexing test with an explicit `date`/`keyword` mapping. Runtime application tests use .NET 9; .NET Framework 4.7.2 is compiled but is not executable on this macOS host.

## Validation evidence

- 36 tests passed, including live indexing on Elasticsearch 8.19.0.
- All 15 synthetic records from old and new hives indexed into one index, with eight `RecordType` buckets and the expected millisecond range-query results.
- 40 CSV files across eight baseline/current scenarios were byte-identical (both hive formats, default selection, `-i`, custom `--dt`, and `--mp`).
- All endpoint evidence in the checked-in fixtures is synthetic. Real production hives and a production Elasticsearch pipeline were not exercised.
