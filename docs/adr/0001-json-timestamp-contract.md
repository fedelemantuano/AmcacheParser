---
status: accepted
---

# JSON timestamps use ISO 8601 independently of display formatting

AmcacheParser's new JSON output will use JSON Lines: one evidence record per line. Timestamp values in this output must use ISO 8601 independently of `--dt`; existing CSV date formatting remains compatible with current integrations. Sharing the configurable CSV format with JSON would allow human-oriented date strings that break downstream Elasticsearch ingestion, so the JSON timestamp representation is a separate output contract.

The CLI exposes `--json` for the output directory and `--jsonf` for custom naming, following [MFTECmd](https://github.com/EricZimmerman/MFTECmd). The user selected a single combined JSON Lines file, flat fields equivalent to the CSV exports, an additional `RecordType` discriminator, and `null` for missing or invalid dates. Elasticsearch compatibility takes priority over retaining sub-millisecond timestamp precision: timestamp instants use UTC with three fractional-second digits for its standard `date` mapping. Date-only source strings retain an ISO date without an invented time or timezone.
