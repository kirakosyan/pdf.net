# Table and structured-data extraction

FP-027 has an original source profile. Complete detection, reconstruction and production qualification remain mandatory; NuGet packing/publication remain blocked.

`PdfTableExtraction.Read` captures unique ascending selected pages under one shared text/resource/parse/inference context. `PdfTableExtractionOptions` requires an explicit `Ruled` or `Borderless` mode. One candidate is inferred per selected page/optional displayed top-left region. Missing candidates return an information diagnostic; ambiguous/unsupported requested profiles reject explicitly. Source actions never run and no host font discovery occurs.

The ruled profile uses literal axis-aligned source stroke segments and explicit coordinate tolerance. A closed outer grid and horizontal/vertical rule positions define primitive cells. Missing full interior edges join adjacent cells; nonrectangular joins and partial edges reject. Paths retain construction-time transforms and source operator provenance. This is inference from source geometry, not a claim about rendered rule ink, conditional visibility, clipping or semantic table structure.

The borderless profile groups geometric horizontal text lines into left-aligned cells separated by an explicit minimum gutter. Row column counts, left starts and empty gutters must agree within tolerance. Inferred outer bounds and interior midpoints are explicit. Multiple candidates, right/center-aligned or irregular columns, complex rotated/conditional/clip content and borderless merged cells need additional profiles. Complete automatic detection and reconstruction remain mandatory.

Cells retain source page, row/column, rectangular spans, displayed bounds, original source character/operator execution records and contributing rule locations. Cell reconstruction in this source profile requires horizontal forward displayed text without right-to-left bidi runs; unsupported directions reject. Semantic text retains complete ActualText replacements and inferred geometric spaces/line breaks. An ActualText replacement crossing inferred cell boundaries rejects; no invented per-scalar ligature geometry or semantic split is returned. Scores 0.95 for accepted literal grids and 0.65 for accepted alignment profiles are documented geometric heuristics, not calibrated probabilities, semantic correctness or qualification metrics. Original diagnostics/mapping/estimated-geometry flags remain observable.

```csharp
var result = PdfTableExtraction.Read(snapshot, new[] { 0, 1 },
    new PdfTableExtractionOptions(PdfTableDetectionMode.Ruled,
        headerRows: 1, joinConsecutivePages: true, numericCulture: "de-DE"));
using var output = File.Create(selectedCsvPath);
result.WriteCsv(output, PdfMergedCellCsvPolicy.AnchorOnly,
    omitRepeatedHeaders: true, escapeSpreadsheetFormulas: true);
result.WriteJsonFile(selectedJsonPath);
```

Numeric culture is optional and explicit; null leaves every cell as text, independent of ambient culture. An explicit culture, including explicit invariant empty string, enables decimal parsing with that culture's number format. Original text is always retained; unparsed/overflowing strings remain text. Complete typed/date/boolean/schema and cultural corpora remain required. No date, locale or application schema is guessed.

Continuation is opt-in. Consecutive source pages must have equal column counts/absolute boundaries within tolerance and matching explicitly selected leading header text/spans. With zero requested header rows, only geometric continuation is inferred. Per-page tables and every source header remain retained; sequences identify the grouping. Automatic header semantics, complete multi-page/wrapped-row continuation and complete matching accuracy remain mandatory.

UTF-8 CSV requires an explicit merged-cell decision: anchor-only with covered cells empty, or repeated text. Original text is quoted/escaped with CRLF records; embedded quotes/newlines remain data. Repeated-header omission and prefix escaping for leading spreadsheet formula characters require explicit opt-in. CSV carries original cell text, not numeric type metadata. Version-one JSON retains table/cell geometry, spans, heuristic scores, text/decimal values, character/operator indices, rule source locations and sequence/header decisions. Borrowed streams remain open and output budgets apply. JSON file replacement is atomic; a failed export preserves the existing file. A borrowed stream can contain a prefix after a failed write.

The executable C#/VB consumers and source contracts cover merged/borderless grids, source provenance, explicit culture, multi-page header matching/mismatch, ActualText, CSV/JSON, malformed merges, cell/object/expansion/output/cancellation bounds and atomic files. Independent pypdf/MuPDF source observations and standard CSV/JSON parsers cross-check 32 pre-labeled cells from seven original fixtures, including one positive and one negative continuation case. Perfect scores on these small fixtures do not establish production extraction accuracy. Full labeled precision/recall/continuation corpora, all profiles and actual mandatory RID/minimum/current/fallback runtimes, exact final packages and every production gate remain mandatory.

See [source evidence](evidence/FP-027-source.json), [standards observations](evidence/FP-027-standards.json), [independent observations](evidence/FP-027-independent.json) and [production quality](production-quality.md).
