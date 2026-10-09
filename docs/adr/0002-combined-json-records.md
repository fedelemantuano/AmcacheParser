---
status: accepted
---

# Combine flat evidence records with consistent Elasticsearch field types

JSON Lines output combines all selected categories in one file, with a `RecordType` discriminator and the same flat fields as the corresponding CSV exports. Serializing the underlying program objects would duplicate associated file evidence through nested lists; shared CSV maps instead define the exported fields, including automatically mapped columns.

`BinaryType`, `BinFileVersion` and `BinProductVersion` have numeric types in old-format hives and string types in new-format hives. The user accepted emitting these three fields as JSON strings in both formats, retaining names and exact values, so records can share an Elasticsearch index without field-type conflicts. Other numeric and boolean fields keep native JSON types, and CSV formatting is preserved.
