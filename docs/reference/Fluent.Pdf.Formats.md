# Fluent.Pdf.Formats source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Formats.IPdfIsolatedLegacyImporter
Fluent.Pdf.Formats.IPdfIsolatedLegacyImporter.EngineId.get -> string!
Fluent.Pdf.Formats.IPdfIsolatedLegacyImporter.Import(System.IO.Stream! source, Fluent.Pdf.Formats.PdfLegacyImportOptions! options, System.IO.Stream! pdfOutput, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Formats.PdfLegacyImportStatus
Fluent.Pdf.Formats.IPdfIsolatedLegacyImporter.Supports(Fluent.Pdf.Formats.PdfLegacyImportOptions! options) -> bool
Fluent.Pdf.Formats.IPdfIsolatedMetafileConverter
Fluent.Pdf.Formats.IPdfIsolatedMetafileConverter.Convert(System.IO.Stream! input, Fluent.Pdf.Formats.PdfMetafileFormat format, System.IO.Stream! pdfOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Formats.IPdfIsolatedMetafileConverter.EngineId.get -> string!
Fluent.Pdf.Formats.IPdfIsolatedMetafileConverter.Supports(Fluent.Pdf.Formats.PdfMetafileFormat format) -> bool
Fluent.Pdf.Formats.IPdfIsolatedPrinterInterpreter
Fluent.Pdf.Formats.IPdfIsolatedPrinterInterpreter.Convert(System.IO.Stream! input, Fluent.Pdf.Formats.PdfPrinterLanguageProfile profile, int maximumPages, System.IO.Stream! pdfOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Formats.IPdfIsolatedPrinterInterpreter.EngineId.get -> string!
Fluent.Pdf.Formats.IPdfIsolatedPrinterInterpreter.Supports(Fluent.Pdf.Formats.PdfPrinterLanguageProfile profile) -> bool
Fluent.Pdf.Formats.IPdfIsolatedTexTypesetter
Fluent.Pdf.Formats.IPdfIsolatedTexTypesetter.EngineId.get -> string!
Fluent.Pdf.Formats.IPdfIsolatedTexTypesetter.Supports(Fluent.Pdf.Formats.PdfTexOptions! options) -> bool
Fluent.Pdf.Formats.IPdfIsolatedTexTypesetter.Typeset(System.IO.Stream! source, Fluent.Pdf.Formats.PdfTexOptions! options, System.IO.Stream! pdfOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Formats.PdfCollectionAdapter
Fluent.Pdf.Formats.PdfCollectionAdapters
Fluent.Pdf.Formats.PdfCollectionAdapters.PdfCollectionAdapters(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string!, Fluent.Pdf.Formats.PdfCollectionAdapter!>>! adapters) -> void
Fluent.Pdf.Formats.PdfCollectionBatchResult
Fluent.Pdf.Formats.PdfCollectionBatchResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.Formats.PdfCollectionBatchResult.Outcomes.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Formats.PdfCollectionOutcome!>!
Fluent.Pdf.Formats.PdfCollectionItem
Fluent.Pdf.Formats.PdfCollectionItem.Length.get -> long
Fluent.Pdf.Formats.PdfCollectionItem.Name.get -> string!
Fluent.Pdf.Formats.PdfCollectionItem.Open() -> System.IO.Stream!
Fluent.Pdf.Formats.PdfCollectionOutcome
Fluent.Pdf.Formats.PdfCollectionOutcome.DiagnosticCode.get -> string?
Fluent.Pdf.Formats.PdfCollectionOutcome.Name.get -> string!
Fluent.Pdf.Formats.PdfCollectionOutcome.Output.get -> Fluent.Pdf.Formats.PdfCollectionItem?
Fluent.Pdf.Formats.PdfConversionOptions
Fluent.Pdf.Formats.PdfConversionOptions.Language.get -> string?
Fluent.Pdf.Formats.PdfConversionOptions.OfdIdentity.get -> Fluent.Pdf.Formats.PdfOfdIdentity?
Fluent.Pdf.Formats.PdfConversionOptions.PdfConversionOptions(Fluent.Pdf.Formats.PdfConversionTarget target, Fluent.Pdf.PdfRenderOptions? rendering = null, string? language = null, string? title = null, Fluent.Pdf.Formats.PdfOfdIdentity? ofdIdentity = null) -> void
Fluent.Pdf.Formats.PdfConversionOptions.Rendering.get -> Fluent.Pdf.PdfRenderOptions!
Fluent.Pdf.Formats.PdfConversionOptions.Target.get -> Fluent.Pdf.Formats.PdfConversionTarget
Fluent.Pdf.Formats.PdfConversionOptions.Title.get -> string?
Fluent.Pdf.Formats.PdfConversionPhase
Fluent.Pdf.Formats.PdfConversionPhase.OutputPrepared = 1 -> Fluent.Pdf.Formats.PdfConversionPhase
Fluent.Pdf.Formats.PdfConversionPhase.Starting = 0 -> Fluent.Pdf.Formats.PdfConversionPhase
Fluent.Pdf.Formats.PdfConversionTarget
Fluent.Pdf.Formats.PdfConversionTarget.Bmp = 3 -> Fluent.Pdf.Formats.PdfConversionTarget
Fluent.Pdf.Formats.PdfConversionTarget.Png = 2 -> Fluent.Pdf.Formats.PdfConversionTarget
Fluent.Pdf.Formats.PdfConversionTarget.RasterHtml = 5 -> Fluent.Pdf.Formats.PdfConversionTarget
Fluent.Pdf.Formats.PdfConversionTarget.RasterOfd = 8 -> Fluent.Pdf.Formats.PdfConversionTarget
Fluent.Pdf.Formats.PdfConversionTarget.RasterOpenXps = 7 -> Fluent.Pdf.Formats.PdfConversionTarget
Fluent.Pdf.Formats.PdfConversionTarget.RasterXps = 6 -> Fluent.Pdf.Formats.PdfConversionTarget
Fluent.Pdf.Formats.PdfConversionTarget.StaticEps = 1 -> Fluent.Pdf.Formats.PdfConversionTarget
Fluent.Pdf.Formats.PdfConversionTarget.Svg = 0 -> Fluent.Pdf.Formats.PdfConversionTarget
Fluent.Pdf.Formats.PdfConversionTarget.Tiff = 4 -> Fluent.Pdf.Formats.PdfConversionTarget
Fluent.Pdf.Formats.PdfConversions
Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Bmp = 5 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Docx = 11 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Emf = 17 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Eps = 7 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Epub = 12 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.FormattingObjects = 18 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Html = 19 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Jpeg = 3 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Ods = 13 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Ofd = 16 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.OpenXps = 15 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Pdf = 1 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Png = 2 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.PostScript = 8 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Pptx = 10 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Svg = 6 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Tiff = 4 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Unknown = 0 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Xlsx = 9 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDetectedFormat.Xps = 14 -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfDocumentCollection
Fluent.Pdf.Formats.PdfDocumentCollection.Items.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Formats.PdfCollectionItem!>!
Fluent.Pdf.Formats.PdfDocumentCollection.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.Formats.PdfDocumentCollection.Process(string! format, Fluent.Pdf.Formats.PdfCollectionAdapters! adapters, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Formats.PdfCollectionBatchResult!
Fluent.Pdf.Formats.PdfDocumentCollection.WriteArchive(System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.Formats.PdfDocumentCollection.WriteArchiveFile(string! path, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.Formats.PdfEbookLayout
Fluent.Pdf.Formats.PdfEbookLayout.FixedRaster = 0 -> Fluent.Pdf.Formats.PdfEbookLayout
Fluent.Pdf.Formats.PdfEbookLayout.ReflowText = 1 -> Fluent.Pdf.Formats.PdfEbookLayout
Fluent.Pdf.Formats.PdfEbookMetadata
Fluent.Pdf.Formats.PdfEbookMetadata.Identifier.get -> string!
Fluent.Pdf.Formats.PdfEbookMetadata.Language.get -> string!
Fluent.Pdf.Formats.PdfEbookMetadata.ModifiedUtc.get -> string!
Fluent.Pdf.Formats.PdfEbookMetadata.PdfEbookMetadata(string! identifier, string! title, string! language, string! modifiedUtc) -> void
Fluent.Pdf.Formats.PdfEbookMetadata.Title.get -> string!
Fluent.Pdf.Formats.PdfEbookOptions
Fluent.Pdf.Formats.PdfEbookOptions.Layout.get -> Fluent.Pdf.Formats.PdfEbookLayout
Fluent.Pdf.Formats.PdfEbookOptions.PdfEbookOptions(Fluent.Pdf.Formats.PdfEbookLayout layout, bool rightToLeft = false, Fluent.Pdf.PdfRenderOptions? rendering = null, Fluent.Pdf.PdfTextExtractionOptions? text = null) -> void
Fluent.Pdf.Formats.PdfEbookOptions.Rendering.get -> Fluent.Pdf.PdfRenderOptions!
Fluent.Pdf.Formats.PdfEbookOptions.RightToLeft.get -> bool
Fluent.Pdf.Formats.PdfEbookOptions.Text.get -> Fluent.Pdf.PdfTextExtractionOptions!
Fluent.Pdf.Formats.PdfEbooks
Fluent.Pdf.Formats.PdfFormatDetection
Fluent.Pdf.Formats.PdfFormatDetection.Format.get -> Fluent.Pdf.Formats.PdfDetectedFormat
Fluent.Pdf.Formats.PdfFormatDetection.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.Formats.PdfFormatTextOptions
Fluent.Pdf.Formats.PdfFormatTextOptions.Body.get -> Fluent.Pdf.PdfFlowTextStyle!
Fluent.Pdf.Formats.PdfFormatTextOptions.Layout.get -> Fluent.Pdf.PdfFlowOptions!
Fluent.Pdf.Formats.PdfFormatTextOptions.PdfFormatTextOptions(Fluent.Pdf.PdfFlowTextStyle! body, Fluent.Pdf.PdfFlowOptions? layout = null) -> void
Fluent.Pdf.Formats.PdfFormattingObjects
Fluent.Pdf.Formats.PdfHtmlExport
Fluent.Pdf.Formats.PdfHtmlExportMode
Fluent.Pdf.Formats.PdfHtmlExportMode.FixedRaster = 0 -> Fluent.Pdf.Formats.PdfHtmlExportMode
Fluent.Pdf.Formats.PdfHtmlExportMode.InferredText = 1 -> Fluent.Pdf.Formats.PdfHtmlExportMode
Fluent.Pdf.Formats.PdfHtmlExportOptions
Fluent.Pdf.Formats.PdfHtmlExportOptions.Language.get -> string!
Fluent.Pdf.Formats.PdfHtmlExportOptions.Mode.get -> Fluent.Pdf.Formats.PdfHtmlExportMode
Fluent.Pdf.Formats.PdfHtmlExportOptions.PdfHtmlExportOptions(Fluent.Pdf.Formats.PdfHtmlExportMode mode, string! title, string! language, Fluent.Pdf.PdfRenderOptions? rendering = null, Fluent.Pdf.PdfTextExtractionOptions? text = null) -> void
Fluent.Pdf.Formats.PdfHtmlExportOptions.Rendering.get -> Fluent.Pdf.PdfRenderOptions!
Fluent.Pdf.Formats.PdfHtmlExportOptions.Text.get -> Fluent.Pdf.PdfTextExtractionOptions!
Fluent.Pdf.Formats.PdfHtmlExportOptions.Title.get -> string!
Fluent.Pdf.Formats.PdfLegacyFormat
Fluent.Pdf.Formats.PdfLegacyFormat.Cdr = 1 -> Fluent.Pdf.Formats.PdfLegacyFormat
Fluent.Pdf.Formats.PdfLegacyFormat.DjVu = 0 -> Fluent.Pdf.Formats.PdfLegacyFormat
Fluent.Pdf.Formats.PdfLegacyImport
Fluent.Pdf.Formats.PdfLegacyImportOptions
Fluent.Pdf.Formats.PdfLegacyImportOptions.Format.get -> Fluent.Pdf.Formats.PdfLegacyFormat
Fluent.Pdf.Formats.PdfLegacyImportOptions.MaximumInputBytes.get -> int
Fluent.Pdf.Formats.PdfLegacyImportOptions.MaximumOutputBytes.get -> int
Fluent.Pdf.Formats.PdfLegacyImportOptions.MaximumPages.get -> int
Fluent.Pdf.Formats.PdfLegacyImportOptions.PdfLegacyImportOptions(Fluent.Pdf.Formats.PdfLegacyFormat format, string! versionProfile, int maximumPages = 1000, int maximumInputBytes = 16777216, int maximumOutputBytes = 67108864) -> void
Fluent.Pdf.Formats.PdfLegacyImportOptions.VersionProfile.get -> string!
Fluent.Pdf.Formats.PdfLegacyImportStatus
Fluent.Pdf.Formats.PdfLegacyImportStatus.Completed = 0 -> Fluent.Pdf.Formats.PdfLegacyImportStatus
Fluent.Pdf.Formats.PdfLegacyImportStatus.DamagedInput = 5 -> Fluent.Pdf.Formats.PdfLegacyImportStatus
Fluent.Pdf.Formats.PdfLegacyImportStatus.MissingCodec = 2 -> Fluent.Pdf.Formats.PdfLegacyImportStatus
Fluent.Pdf.Formats.PdfLegacyImportStatus.MissingFont = 3 -> Fluent.Pdf.Formats.PdfLegacyImportStatus
Fluent.Pdf.Formats.PdfLegacyImportStatus.UnsupportedRecord = 4 -> Fluent.Pdf.Formats.PdfLegacyImportStatus
Fluent.Pdf.Formats.PdfLegacyImportStatus.UnsupportedVersion = 1 -> Fluent.Pdf.Formats.PdfLegacyImportStatus
Fluent.Pdf.Formats.PdfMetafileFormat
Fluent.Pdf.Formats.PdfMetafileFormat.Cgm = 0 -> Fluent.Pdf.Formats.PdfMetafileFormat
Fluent.Pdf.Formats.PdfMetafileFormat.Emf = 2 -> Fluent.Pdf.Formats.PdfMetafileFormat
Fluent.Pdf.Formats.PdfMetafileFormat.EmfPlus = 3 -> Fluent.Pdf.Formats.PdfMetafileFormat
Fluent.Pdf.Formats.PdfMetafileFormat.Wmf = 1 -> Fluent.Pdf.Formats.PdfMetafileFormat
Fluent.Pdf.Formats.PdfMetafiles
Fluent.Pdf.Formats.PdfObjectXml
Fluent.Pdf.Formats.PdfOfd
Fluent.Pdf.Formats.PdfOfdIdentity
Fluent.Pdf.Formats.PdfOfdIdentity.CreationDate.get -> string!
Fluent.Pdf.Formats.PdfOfdIdentity.DocumentId.get -> string!
Fluent.Pdf.Formats.PdfOfdIdentity.PdfOfdIdentity(string! documentId, string! title, string! creationDate) -> void
Fluent.Pdf.Formats.PdfOfdIdentity.Title.get -> string!
Fluent.Pdf.Formats.PdfPresentations
Fluent.Pdf.Formats.PdfPrinterLanguageProfile
Fluent.Pdf.Formats.PdfPrinterLanguageProfile.EpsLevel2 = 2 -> Fluent.Pdf.Formats.PdfPrinterLanguageProfile
Fluent.Pdf.Formats.PdfPrinterLanguageProfile.EpsLevel3 = 3 -> Fluent.Pdf.Formats.PdfPrinterLanguageProfile
Fluent.Pdf.Formats.PdfPrinterLanguageProfile.Pcl5 = 4 -> Fluent.Pdf.Formats.PdfPrinterLanguageProfile
Fluent.Pdf.Formats.PdfPrinterLanguageProfile.Pcl5c = 6 -> Fluent.Pdf.Formats.PdfPrinterLanguageProfile
Fluent.Pdf.Formats.PdfPrinterLanguageProfile.Pcl5e = 5 -> Fluent.Pdf.Formats.PdfPrinterLanguageProfile
Fluent.Pdf.Formats.PdfPrinterLanguageProfile.PclXl = 7 -> Fluent.Pdf.Formats.PdfPrinterLanguageProfile
Fluent.Pdf.Formats.PdfPrinterLanguageProfile.PostScriptLevel2 = 0 -> Fluent.Pdf.Formats.PdfPrinterLanguageProfile
Fluent.Pdf.Formats.PdfPrinterLanguageProfile.PostScriptLevel3 = 1 -> Fluent.Pdf.Formats.PdfPrinterLanguageProfile
Fluent.Pdf.Formats.PdfPrinterLanguages
Fluent.Pdf.Formats.PdfSheetCell
Fluent.Pdf.Formats.PdfSheetCell.Column.get -> int
Fluent.Pdf.Formats.PdfSheetCell.ColumnSpan.get -> int
Fluent.Pdf.Formats.PdfSheetCell.Kind.get -> Fluent.Pdf.Formats.PdfSheetValueKind
Fluent.Pdf.Formats.PdfSheetCell.PdfSheetCell(int row, int column, string! value, Fluent.Pdf.Formats.PdfSheetValueKind kind = Fluent.Pdf.Formats.PdfSheetValueKind.Text, int rowSpan = 1, int columnSpan = 1, string? sourceFormula = null, int? sourcePage = null, Fluent.Pdf.PdfRect? sourceBounds = null) -> void
Fluent.Pdf.Formats.PdfSheetCell.Row.get -> int
Fluent.Pdf.Formats.PdfSheetCell.RowSpan.get -> int
Fluent.Pdf.Formats.PdfSheetCell.SourceBounds.get -> Fluent.Pdf.PdfRect?
Fluent.Pdf.Formats.PdfSheetCell.SourceFormula.get -> string?
Fluent.Pdf.Formats.PdfSheetCell.SourcePage.get -> int?
Fluent.Pdf.Formats.PdfSheetCell.Value.get -> string!
Fluent.Pdf.Formats.PdfSheetData
Fluent.Pdf.Formats.PdfSheetData.Cells.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Formats.PdfSheetCell!>!
Fluent.Pdf.Formats.PdfSheetData.ColumnCount.get -> int
Fluent.Pdf.Formats.PdfSheetData.Name.get -> string!
Fluent.Pdf.Formats.PdfSheetData.PdfSheetData(string! name, int rows, int columns, System.Collections.Generic.IEnumerable<Fluent.Pdf.Formats.PdfSheetCell!>! cells) -> void
Fluent.Pdf.Formats.PdfSheetData.RowCount.get -> int
Fluent.Pdf.Formats.PdfSheetValueKind
Fluent.Pdf.Formats.PdfSheetValueKind.Boolean = 2 -> Fluent.Pdf.Formats.PdfSheetValueKind
Fluent.Pdf.Formats.PdfSheetValueKind.Date = 3 -> Fluent.Pdf.Formats.PdfSheetValueKind
Fluent.Pdf.Formats.PdfSheetValueKind.Number = 1 -> Fluent.Pdf.Formats.PdfSheetValueKind
Fluent.Pdf.Formats.PdfSheetValueKind.Text = 0 -> Fluent.Pdf.Formats.PdfSheetValueKind
Fluent.Pdf.Formats.PdfSpreadsheetFormat
Fluent.Pdf.Formats.PdfSpreadsheetFormat.Csv = 0 -> Fluent.Pdf.Formats.PdfSpreadsheetFormat
Fluent.Pdf.Formats.PdfSpreadsheetFormat.Ods = 3 -> Fluent.Pdf.Formats.PdfSpreadsheetFormat
Fluent.Pdf.Formats.PdfSpreadsheetFormat.SpreadsheetMl = 1 -> Fluent.Pdf.Formats.PdfSpreadsheetFormat
Fluent.Pdf.Formats.PdfSpreadsheetFormat.Xls = 4 -> Fluent.Pdf.Formats.PdfSpreadsheetFormat
Fluent.Pdf.Formats.PdfSpreadsheetFormat.Xlsx = 2 -> Fluent.Pdf.Formats.PdfSpreadsheetFormat
Fluent.Pdf.Formats.PdfSpreadsheets
Fluent.Pdf.Formats.PdfStaticEps
Fluent.Pdf.Formats.PdfSvg
Fluent.Pdf.Formats.PdfSvgImportOptions
Fluent.Pdf.Formats.PdfSvgImportOptions.AllowDataImages.get -> bool
Fluent.Pdf.Formats.PdfSvgImportOptions.Fonts.get -> Fluent.Pdf.PdfFontFallback?
Fluent.Pdf.Formats.PdfSvgImportOptions.Images.get -> Fluent.Pdf.PdfImageReadOptions!
Fluent.Pdf.Formats.PdfSvgImportOptions.PdfSvgImportOptions(Fluent.Pdf.PdfFontFallback? fonts = null, Fluent.Pdf.PdfTextOptions? text = null, bool allowDataImages = false, Fluent.Pdf.PdfImageReadOptions? images = null) -> void
Fluent.Pdf.Formats.PdfSvgImportOptions.Text.get -> Fluent.Pdf.PdfTextOptions!
Fluent.Pdf.Formats.PdfTex
Fluent.Pdf.Formats.PdfTexOptions
Fluent.Pdf.Formats.PdfTexOptions.MaximumPages.get -> int
Fluent.Pdf.Formats.PdfTexOptions.PdfTexOptions(Fluent.Pdf.Formats.PdfTexProfile profile, System.Collections.Generic.IEnumerable<string!>? requiredPackages = null, int maximumPages = 1000) -> void
Fluent.Pdf.Formats.PdfTexOptions.Profile.get -> Fluent.Pdf.Formats.PdfTexProfile
Fluent.Pdf.Formats.PdfTexOptions.RequiredPackages.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.Formats.PdfTexProfile
Fluent.Pdf.Formats.PdfTexProfile.LuaLatex = 1 -> Fluent.Pdf.Formats.PdfTexProfile
Fluent.Pdf.Formats.PdfTexProfile.PdfLatex = 0 -> Fluent.Pdf.Formats.PdfTexProfile
Fluent.Pdf.Formats.PdfTexProfile.XeLatex = 2 -> Fluent.Pdf.Formats.PdfTexProfile
Fluent.Pdf.Formats.PdfXps
Fluent.Pdf.Formats.PdfXpsFormat
Fluent.Pdf.Formats.PdfXpsFormat.OpenXps = 1 -> Fluent.Pdf.Formats.PdfXpsFormat
Fluent.Pdf.Formats.PdfXpsFormat.Xps = 0 -> Fluent.Pdf.Formats.PdfXpsFormat
static Fluent.Pdf.Formats.PdfConversions.Detect(System.IO.Stream! input, string? extension = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Formats.PdfFormatDetection!
static Fluent.Pdf.Formats.PdfConversions.Export(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, System.IO.Stream! output, Fluent.Pdf.Formats.PdfConversionOptions! options, Fluent.Pdf.PdfProviderRegistry? providers = null, System.Action<Fluent.Pdf.Formats.PdfConversionPhase, Fluent.Pdf.PdfOperationContext!>? progress = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfConversions.ExportFile(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, string! path, Fluent.Pdf.Formats.PdfConversionOptions! options, Fluent.Pdf.PdfProviderRegistry? providers = null, System.Action<Fluent.Pdf.Formats.PdfConversionPhase, Fluent.Pdf.PdfOperationContext!>? progress = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfConversions.WriteMatrix(System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfDocumentCollection.Capture(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string!, System.IO.Stream!>>! inputs, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Formats.PdfDocumentCollection!
static Fluent.Pdf.Formats.PdfDocumentCollection.ReadArchive(System.IO.Stream! input, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Formats.PdfDocumentCollection!
static Fluent.Pdf.Formats.PdfEbooks.ImportReflowText(System.IO.Stream! input, Fluent.Pdf.Formats.PdfFormatTextOptions! text, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfEbooks.WriteEpub(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, System.IO.Stream! output, Fluent.Pdf.Formats.PdfEbookMetadata! metadata, Fluent.Pdf.Formats.PdfEbookOptions! options, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfEbooks.WriteMobiSource(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, System.IO.Stream! output, Fluent.Pdf.Formats.PdfEbookMetadata! metadata, Fluent.Pdf.PdfTextExtractionOptions? text = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfFormattingObjects.Import(System.IO.Stream! source, Fluent.Pdf.PdfFlowTextStyle! body, Fluent.Pdf.PdfProviderRegistry! providers, int maximumPages = 1000, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfFormattingObjects.Transform(System.IO.Stream! xml, System.IO.Stream! stylesheet, Fluent.Pdf.PdfFlowTextStyle! body, Fluent.Pdf.PdfProviderRegistry! providers, int maximumPages = 1000, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfHtmlExport.Write(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, System.IO.Stream! output, Fluent.Pdf.Formats.PdfHtmlExportOptions! options, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfHtmlExport.WriteFile(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, string! path, Fluent.Pdf.Formats.PdfHtmlExportOptions! options, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfLegacyImport.Read(System.IO.Stream! source, Fluent.Pdf.Formats.PdfLegacyImportOptions! options, Fluent.Pdf.Formats.IPdfIsolatedLegacyImporter? importer = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfMetafiles.Import(System.IO.Stream! input, Fluent.Pdf.Formats.PdfMetafileFormat format, Fluent.Pdf.Formats.IPdfIsolatedMetafileConverter? converter = null, int maximumPages = 1000, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfMetafiles.ImportRasterEmf(System.IO.Stream! input, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfMetafiles.WriteRasterEmf(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, System.IO.Stream! output, Fluent.Pdf.PdfRenderOptions! rendering, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfObjectXml.Export(System.IO.Stream! pdf, System.IO.Stream! xml, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfObjectXml.Import(System.IO.Stream! xml, System.IO.Stream! pdf, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfOfd.ImportRasterPages(System.IO.Stream! input, out Fluent.Pdf.Formats.PdfOfdIdentity! identity, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfOfd.WriteRasterPages(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, System.IO.Stream! output, Fluent.Pdf.Formats.PdfOfdIdentity! identity, Fluent.Pdf.PdfRenderOptions! rendering, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfPresentations.ImportRasterSlides(System.IO.Stream! input, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfPresentations.WriteRasterSlides(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, Fluent.Pdf.PdfPageSize slideSize, Fluent.Pdf.PdfRenderOptions! rendering, Fluent.Pdf.PdfProviderRegistry! providers, System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfPrinterLanguages.Import(System.IO.Stream! input, Fluent.Pdf.Formats.PdfPrinterLanguageProfile profile, Fluent.Pdf.Formats.IPdfIsolatedPrinterInterpreter? interpreter = null, int maximumPages = 1000, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfSpreadsheets.FromTable(Fluent.Pdf.PdfExtractedTable! table, string! sheetName, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Formats.PdfSheetData!
static Fluent.Pdf.Formats.PdfSpreadsheets.ImportData(Fluent.Pdf.Formats.PdfSheetData! sheet, int firstRow, int firstColumn, int rows, int columns, Fluent.Pdf.Formats.PdfFormatTextOptions! text, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfSpreadsheets.ReadData(System.IO.Stream! input, Fluent.Pdf.Formats.PdfSpreadsheetFormat format, string! sheetName, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Formats.PdfSheetData!
static Fluent.Pdf.Formats.PdfSpreadsheets.WriteData(Fluent.Pdf.Formats.PdfSheetData! sheet, Fluent.Pdf.Formats.PdfSpreadsheetFormat format, System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfSpreadsheets.WriteProvenance(Fluent.Pdf.Formats.PdfSheetData! sheet, System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfStaticEps.Import(System.IO.Stream! input, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfStaticEps.WritePage(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfStaticEps.WritePageFile(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, string! path, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfSvg.Import(System.IO.Stream! input, Fluent.Pdf.Formats.PdfSvgImportOptions? options = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfSvg.WritePage(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfSvg.WritePageFile(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, string! path, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfTex.Import(System.IO.Stream! source, Fluent.Pdf.Formats.PdfTexOptions! options, Fluent.Pdf.Formats.IPdfIsolatedTexTypesetter? typesetter = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfTex.WriteInferredText(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, System.IO.Stream! output, Fluent.Pdf.PdfTextExtractionOptions? text = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Formats.PdfXps.ImportRasterPages(System.IO.Stream! input, Fluent.Pdf.Formats.PdfXpsFormat format, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Formats.PdfXps.WriteRasterPages(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, System.IO.Stream! output, Fluent.Pdf.Formats.PdfXpsFormat format, string! language, Fluent.Pdf.PdfRenderOptions! rendering, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
```

## Member documentation

### <code>F:Fluent.Pdf.Formats.PdfConversionPhase.OutputPrepared</code>

Complete output is staged but has not been copied to the caller destination.

### <code>F:Fluent.Pdf.Formats.PdfConversionPhase.Starting</code>

Options/provider capture is starting.

### <code>F:Fluent.Pdf.Formats.PdfConversionTarget.Bmp</code>

Explicit opaque BMP page.

### <code>F:Fluent.Pdf.Formats.PdfConversionTarget.Png</code>

Explicit rendered PNG page.

### <code>F:Fluent.Pdf.Formats.PdfConversionTarget.RasterHtml</code>

Self-contained fixed raster HTML.

### <code>F:Fluent.Pdf.Formats.PdfConversionTarget.RasterOfd</code>

OFD1.0 raster pages.

### <code>F:Fluent.Pdf.Formats.PdfConversionTarget.RasterOpenXps</code>

OpenXPS1.0 raster pages.

### <code>F:Fluent.Pdf.Formats.PdfConversionTarget.RasterXps</code>

XPS2005 raster pages.

### <code>F:Fluent.Pdf.Formats.PdfConversionTarget.StaticEps</code>

Literal static EPS vector page.

### <code>F:Fluent.Pdf.Formats.PdfConversionTarget.Svg</code>

Opaque supported vector page SVG; no automatic raster fallback.

### <code>F:Fluent.Pdf.Formats.PdfConversionTarget.Tiff</code>

Explicit ordered raster TIFF pages.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Bmp</code>

BMP signature.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Docx</code>

Canonical DOCX OPC main identity.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Emf</code>

EMF header signature.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Eps</code>

EPSF PostScript header.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Epub</code>

EPUB MIME/container identity.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.FormattingObjects</code>

XSL-FO root.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Html</code>

Literal HTML5 document header; no scripts or resources are executed.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Jpeg</code>

JPEG signature.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Ods</code>

ODS MIME identity.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Ofd</code>

OFD1.0 root identity.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.OpenXps</code>

OpenXPS fixed representation.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Pdf</code>

PDF header.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Png</code>

PNG signature.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.PostScript</code>

PostScript header.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Pptx</code>

Canonical PPTX OPC main identity.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Svg</code>

Static SVG XML root.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Tiff</code>

Classic TIFF signature.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Unknown</code>

Unknown or caller-selected plain text.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Xlsx</code>

Canonical XLSX OPC main identity.

### <code>F:Fluent.Pdf.Formats.PdfDetectedFormat.Xps</code>

XPS2005 fixed representation.

### <code>F:Fluent.Pdf.Formats.PdfEbookLayout.FixedRaster</code>

Fixed-layout raster page XHTML with embedded PNG assets.

### <code>F:Fluent.Pdf.Formats.PdfEbookLayout.ReflowText</code>

Declared-loss inferred text chapters.

### <code>F:Fluent.Pdf.Formats.PdfHtmlExportMode.FixedRaster</code>

Self-contained PNG page appearances at explicit rendering DPI.

### <code>F:Fluent.Pdf.Formats.PdfHtmlExportMode.InferredText</code>

Unstyled inferred text; shared permissive policy is required for declared reconstruction losses.

### <code>F:Fluent.Pdf.Formats.PdfLegacyFormat.Cdr</code>

CDR vector documents.

### <code>F:Fluent.Pdf.Formats.PdfLegacyFormat.DjVu</code>

DjVu scanned documents.

### <code>F:Fluent.Pdf.Formats.PdfLegacyImportStatus.Completed</code>

The requested exact profile was decoded.

### <code>F:Fluent.Pdf.Formats.PdfLegacyImportStatus.DamagedInput</code>

The source is damaged.

### <code>F:Fluent.Pdf.Formats.PdfLegacyImportStatus.MissingCodec</code>

A required explicitly provisioned codec is absent.

### <code>F:Fluent.Pdf.Formats.PdfLegacyImportStatus.MissingFont</code>

A required explicitly provisioned font is absent.

### <code>F:Fluent.Pdf.Formats.PdfLegacyImportStatus.UnsupportedRecord</code>

A source record cannot be represented by this host.

### <code>F:Fluent.Pdf.Formats.PdfLegacyImportStatus.UnsupportedVersion</code>

The source version does not match the requested supported profile.

### <code>F:Fluent.Pdf.Formats.PdfMetafileFormat.Cgm</code>

Computer Graphics Metafile, with encoding/version explicit in the configured converter profile.

### <code>F:Fluent.Pdf.Formats.PdfMetafileFormat.Emf</code>

Enhanced Metafile.

### <code>F:Fluent.Pdf.Formats.PdfMetafileFormat.EmfPlus</code>

Enhanced Metafile Plus; not silently treated as its fallback records.

### <code>F:Fluent.Pdf.Formats.PdfMetafileFormat.Wmf</code>

Windows Metafile.

### <code>F:Fluent.Pdf.Formats.PdfPrinterLanguageProfile.EpsLevel2</code>

Encapsulated PostScript level2.

### <code>F:Fluent.Pdf.Formats.PdfPrinterLanguageProfile.EpsLevel3</code>

Encapsulated PostScript level3.

### <code>F:Fluent.Pdf.Formats.PdfPrinterLanguageProfile.Pcl5</code>

PCL5 monochrome.

### <code>F:Fluent.Pdf.Formats.PdfPrinterLanguageProfile.Pcl5c</code>

PCL5c color.

### <code>F:Fluent.Pdf.Formats.PdfPrinterLanguageProfile.Pcl5e</code>

PCL5e.

### <code>F:Fluent.Pdf.Formats.PdfPrinterLanguageProfile.PclXl</code>

PCL XL.

### <code>F:Fluent.Pdf.Formats.PdfPrinterLanguageProfile.PostScriptLevel2</code>

PostScript language level2.

### <code>F:Fluent.Pdf.Formats.PdfPrinterLanguageProfile.PostScriptLevel3</code>

PostScript language level3.

### <code>F:Fluent.Pdf.Formats.PdfSheetValueKind.Boolean</code>

Boolean true/false.

### <code>F:Fluent.Pdf.Formats.PdfSheetValueKind.Date</code>

ISO calendar date.

### <code>F:Fluent.Pdf.Formats.PdfSheetValueKind.Number</code>

Finite invariant IEEE double.

### <code>F:Fluent.Pdf.Formats.PdfSheetValueKind.Text</code>

Literal Unicode text.

### <code>F:Fluent.Pdf.Formats.PdfSpreadsheetFormat.Csv</code>

UTF8 RFC4180 comma-separated text.

### <code>F:Fluent.Pdf.Formats.PdfSpreadsheetFormat.Ods</code>

OpenDocument spreadsheet data.

### <code>F:Fluent.Pdf.Formats.PdfSpreadsheetFormat.SpreadsheetMl</code>

Office 2003 XML data.

### <code>F:Fluent.Pdf.Formats.PdfSpreadsheetFormat.Xls</code>

Binary XLS; unavailable in this managed profile.

### <code>F:Fluent.Pdf.Formats.PdfSpreadsheetFormat.Xlsx</code>

Open XML workbook data.

### <code>F:Fluent.Pdf.Formats.PdfTexProfile.LuaLatex</code>

LuaTeX with the LaTeX format; embedded Lua must remain isolated by the host.

### <code>F:Fluent.Pdf.Formats.PdfTexProfile.PdfLatex</code>

pdfTeX with the LaTeX format.

### <code>F:Fluent.Pdf.Formats.PdfTexProfile.XeLatex</code>

XeTeX with the LaTeX format.

### <code>F:Fluent.Pdf.Formats.PdfXpsFormat.OpenXps</code>

OpenXPS1.0 package.

### <code>F:Fluent.Pdf.Formats.PdfXpsFormat.Xps</code>

Original XPS2005 package.

### <code>M:Fluent.Pdf.Formats.IPdfIsolatedLegacyImporter.Import(System.IO.Stream,Fluent.Pdf.Formats.PdfLegacyImportOptions,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Imports in a fresh bounded OS-isolated job. Deny ambient file/network access and external document references; charge resources, use actual worker scopes and report every fidelity loss. Borrowed leases expire at return. No source actions may execute.

### <code>M:Fluent.Pdf.Formats.IPdfIsolatedLegacyImporter.Supports(Fluent.Pdf.Formats.PdfLegacyImportOptions)</code>

Checks the exact format/version and local codec/font prerequisites before input capture. No downloads or ambient discovery are allowed.

### <code>M:Fluent.Pdf.Formats.IPdfIsolatedMetafileConverter.Convert(System.IO.Stream,Fluent.Pdf.Formats.PdfMetafileFormat,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Converts in a bounded separately qualified OS-isolated job with no ambient files/network/GUI dependencies. Preserve vector/text/raster geometry where supported and report every unsupported record through the shared context. Leases expire at return.

### <code>M:Fluent.Pdf.Formats.IPdfIsolatedMetafileConverter.Supports(Fluent.Pdf.Formats.PdfMetafileFormat)</code>

Reports exact current-host/profile availability, including record encodings and local resources.

### <code>M:Fluent.Pdf.Formats.IPdfIsolatedPrinterInterpreter.Convert(System.IO.Stream,Fluent.Pdf.Formats.PdfPrinterLanguageProfile,System.Int32,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Converts in a fresh bounded OS-isolated job using explicit local engine/font/procset assets and the shared context. Arbitrary host files, processes, network, shell execution and discovery must be denied. Caller leases expire at return. Strict unsupported commands/substitutions must report through the context.

### <code>M:Fluent.Pdf.Formats.IPdfIsolatedPrinterInterpreter.Supports(Fluent.Pdf.Formats.PdfPrinterLanguageProfile)</code>

Reports exact language/version availability before input capture; no automatic installation or fallback is permitted.

### <code>M:Fluent.Pdf.Formats.IPdfIsolatedTexTypesetter.Supports(Fluent.Pdf.Formats.PdfTexOptions)</code>

Checks the exact engine and all local package/font/format prerequisites before source input is read.

### <code>M:Fluent.Pdf.Formats.IPdfIsolatedTexTypesetter.Typeset(System.IO.Stream,Fluent.Pdf.Formats.PdfTexOptions,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Typesets in a fresh bounded OS-isolated job with shell escape, ambient files/network, child processes and runtime installation denied. Lua and TeX file primitives need the same isolation. Leases expire at return; all phases use this context and actual worker scopes.

### <code>M:Fluent.Pdf.Formats.PdfCollectionAdapters.#ctor(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{System.String,Fluent.Pdf.Formats.PdfCollectionAdapter}})</code>

Copies at most 128 distinct ordinal format IDs and explicit trusted adapters.

### <code>M:Fluent.Pdf.Formats.PdfCollectionItem.Open</code>

Opens an independent read-only stream owned by the caller.

### <code>M:Fluent.Pdf.Formats.PdfConversionOptions.#ctor(Fluent.Pdf.Formats.PdfConversionTarget,Fluent.Pdf.PdfRenderOptions,System.String,System.String,Fluent.Pdf.Formats.PdfOfdIdentity)</code>

Captures an exact profile and its literal metadata/rendering requirements; does not install or discover providers.

### <code>M:Fluent.Pdf.Formats.PdfConversions.Detect(System.IO.Stream,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures up to64 MiB content, checks family identities and rejects ambiguous packages. An extension never overrides bytes; unknown text requires caller selection. Detection is not a complete decoder/validator.

### <code>M:Fluent.Pdf.Formats.PdfConversions.Export(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},System.IO.Stream,Fluent.Pdf.Formats.PdfConversionOptions,Fluent.Pdf.PdfProviderRegistry,System.Action{Fluent.Pdf.Formats.PdfConversionPhase,Fluent.Pdf.PdfOperationContext},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages exact selected pages under one shared context, then copies to a borrowed stream. Progress callbacks run only before publication. No empty document or format/provider fallback succeeds.

### <code>M:Fluent.Pdf.Formats.PdfConversions.ExportFile(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},System.String,Fluent.Pdf.Formats.PdfConversionOptions,Fluent.Pdf.PdfProviderRegistry,System.Action{Fluent.Pdf.Formats.PdfConversionPhase,Fluent.Pdf.PdfOperationContext},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages output before atomic file replacement; callbacks share the same context and can cancel before replacement.

### <code>M:Fluent.Pdf.Formats.PdfConversions.WriteMatrix(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes the exact machine-readable source export matrix. All entries use the managed library targets on Windows/Linux/macOS; renderer entries additionally need a qualified provider for the actual host.

### <code>M:Fluent.Pdf.Formats.PdfDocumentCollection.Capture(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{System.String,System.IO.Stream}},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Copies 1–4096 borrowed inputs, at most 16 MiB each and 64 MiB in total. Names must be distinct portable NFC leaf names.

### <code>M:Fluent.Pdf.Formats.PdfDocumentCollection.Process(System.String,Fluent.Pdf.Formats.PdfCollectionAdapters,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Runs one explicitly registered trusted adapter sequentially. Ordinary item failures are reported in place; cancellation, latched limits and leaked scopes abort the batch.

### <code>M:Fluent.Pdf.Formats.PdfDocumentCollection.ReadArchive(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads a bounded ZIP into memory, rejecting unsafe paths, collisions, links and corrupt data before returning any entries.

### <code>M:Fluent.Pdf.Formats.PdfDocumentCollection.WriteArchive(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes a deterministic UTF-8 ZIP with stored entries. Caller streams stay open; I/O failure may leave partial stream output.

### <code>M:Fluent.Pdf.Formats.PdfDocumentCollection.WriteArchiveFile(System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages and atomically commits an archive file after every check succeeds.

### <code>M:Fluent.Pdf.Formats.PdfEbookMetadata.#ctor(System.String,System.String,System.String,System.String)</code>

Captures literal identifier/title, a bounded modern BCP47 language tag and exact UTC modification time (yyyy-MM-ddTHH:mm:ssZ).

### <code>M:Fluent.Pdf.Formats.PdfEbookOptions.#ctor(Fluent.Pdf.Formats.PdfEbookLayout,System.Boolean,Fluent.Pdf.PdfRenderOptions,Fluent.Pdf.PdfTextExtractionOptions)</code>

Captures a finite layout profile. Rendering requires whole pages. Reflow text needs permissive loss policy.

### <code>M:Fluent.Pdf.Formats.PdfEbooks.ImportReflowText(System.IO.Stream,Fluent.Pdf.Formats.PdfFormatTextOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reconstructs EPUB3 text-only spine content with explicit caller typography. Encrypted/obfuscated resources, fixed layouts, active markup, external resources and unsupported elements reject; declared reconstruction loss requires permissive policy.

### <code>M:Fluent.Pdf.Formats.PdfEbooks.WriteEpub(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},System.IO.Stream,Fluent.Pdf.Formats.PdfEbookMetadata,Fluent.Pdf.Formats.PdfEbookOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages an EPUB3 package with original manifest/spine/navigation, an uncompressed first mimetype entry, explicit metadata and PNG or escaped text chapters in exact caller page order.

### <code>M:Fluent.Pdf.Formats.PdfEbooks.WriteMobiSource(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},System.IO.Stream,Fluent.Pdf.Formats.PdfEbookMetadata,Fluent.Pdf.PdfTextExtractionOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes escaped text-only MobiXML source with literal metadata and explicit page breaks. This does not compile a Mobi/KF8 book, bypass DRM or assert fixed-layout/typographic fidelity.

### <code>M:Fluent.Pdf.Formats.PdfFormatTextOptions.#ctor(Fluent.Pdf.PdfFlowTextStyle,Fluent.Pdf.PdfFlowOptions)</code>

Captures an explicit body style and bounded page layout. Source data conversion does not promise original spreadsheet/book/typesetting geometry.

### <code>M:Fluent.Pdf.Formats.PdfFormattingObjects.Import(System.IO.Stream,Fluent.Pdf.PdfFlowTextStyle,Fluent.Pdf.PdfProviderRegistry,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads one simple page master/sequence/body with text blocks and explicit tables. Fonts and shaping are caller supplied; unsupported formatting always rejects.

### <code>M:Fluent.Pdf.Formats.PdfFormattingObjects.Transform(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfFlowTextStyle,Fluent.Pdf.PdfProviderRegistry,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Applies a closed XSLT1.0 data profile: one root template, literal FO, text, value-of and for-each over finite literal child paths. Includes/imports/functions/recursion/scripts/URI access and unknown formatting reject before layout.

### <code>M:Fluent.Pdf.Formats.PdfHtmlExport.Write(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},System.IO.Stream,Fluent.Pdf.Formats.PdfHtmlExportOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages self-contained HTML5 before copying to a borrowed stream. Fixed raster export requires an explicit renderer; inferred text requires permissive loss policy.

### <code>M:Fluent.Pdf.Formats.PdfHtmlExport.WriteFile(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},System.String,Fluent.Pdf.Formats.PdfHtmlExportOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages complete HTML before atomic file replacement.

### <code>M:Fluent.Pdf.Formats.PdfHtmlExportOptions.#ctor(Fluent.Pdf.Formats.PdfHtmlExportMode,System.String,System.String,Fluent.Pdf.PdfRenderOptions,Fluent.Pdf.PdfTextExtractionOptions)</code>

Captures one profile, a literal title and explicit language. No asset, font, script or network discovery occurs.

### <code>M:Fluent.Pdf.Formats.PdfLegacyImport.Read(System.IO.Stream,Fluent.Pdf.Formats.PdfLegacyImportOptions,Fluent.Pdf.Formats.IPdfIsolatedLegacyImporter,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures source and stages/reparses all PDF output under one operation context. Failure returns no partial document; caller streams and host remain owned by the caller.

### <code>M:Fluent.Pdf.Formats.PdfLegacyImportOptions.#ctor(Fluent.Pdf.Formats.PdfLegacyFormat,System.String,System.Int32,System.Int32,System.Int32)</code>

Requires a literal host-defined version/profile identity; no format version is inferred or advertised by the SDK wrapper.

### <code>M:Fluent.Pdf.Formats.PdfMetafiles.Import(System.IO.Stream,Fluent.Pdf.Formats.PdfMetafileFormat,Fluent.Pdf.Formats.IPdfIsolatedMetafileConverter,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Uses an exact explicitly configured converter before input capture. All CGM/WMF/EMF+/broader EMF profiles need this separately qualified optional boundary.

### <code>M:Fluent.Pdf.Formats.PdfMetafiles.ImportRasterEmf(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads the exact single-image EMF24 source profile without executing GDI, text/font discovery or comment/EMF+ records. Every unknown record rejects.

### <code>M:Fluent.Pdf.Formats.PdfMetafiles.WriteRasterEmf(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,System.IO.Stream,Fluent.Pdf.PdfRenderOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes one explicit opaque rendered page as an original EMF1.0 header, SRCCOPY STRETCHDIBITS and EOF. Frame quantization requires declared loss authority; no vector reconstruction is claimed.

### <code>M:Fluent.Pdf.Formats.PdfObjectXml.Export(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Exports an unencrypted, unsigned PDF as a bounded typed object graph. Byte-valued names and strings use base64; indirect identities and exact decimal tokens are retained.

### <code>M:Fluent.Pdf.Formats.PdfObjectXml.Import(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Imports the strict version-one object vocabulary and writes a full PDF revision after validation. Unknown XML, encrypted trailers and signature byte ranges reject.

### <code>M:Fluent.Pdf.Formats.PdfOfd.ImportRasterPages(System.IO.Stream,Fluent.Pdf.Formats.PdfOfdIdentity@,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads the closed PNG/page/resource profile and returns the literal source identity separately. Signed OFD packages reject rather than asserting signature preservation on converted bytes.

### <code>M:Fluent.Pdf.Formats.PdfOfd.WriteRasterPages(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},System.IO.Stream,Fluent.Pdf.Formats.PdfOfdIdentity,Fluent.Pdf.PdfRenderOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages an OFD1.0 ZIP with ordered PNG pages, physical mm geometry, explicit metadata and document-owned resource identities. No source signature is copied or asserted valid.

### <code>M:Fluent.Pdf.Formats.PdfOfdIdentity.#ctor(System.String,System.String,System.String)</code>

Captures a32-hex-digit document ID, literal title and ISO calendar creation date.

### <code>M:Fluent.Pdf.Formats.PdfPresentations.ImportRasterSlides(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Imports the documented explicit picture-only PPTX profile, preserving slide order and common geometry. Text, inherited master shapes, effects, animation, external media and picture cropping reject.

### <code>M:Fluent.Pdf.Formats.PdfPresentations.WriteRasterSlides(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},Fluent.Pdf.PdfPageSize,Fluent.Pdf.PdfRenderOptions,Fluent.Pdf.PdfProviderRegistry,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Renders selected pages once in exact order into PNG pictures on an explicit common slide size. Aspect-preserving contain and opaque white margins are explicit; cropping and alpha backgrounds reject.

### <code>M:Fluent.Pdf.Formats.PdfPrinterLanguages.Import(System.IO.Stream,Fluent.Pdf.Formats.PdfPrinterLanguageProfile,Fluent.Pdf.Formats.IPdfIsolatedPrinterInterpreter,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Requires exact interpreter support before reading; captures at most8 MiB input, stages at most64 MiB complete PDF and imports under the same context. Missing/failed/empty output cannot return a substitute document.

### <code>M:Fluent.Pdf.Formats.PdfSheetCell.#ctor(System.Int32,System.Int32,System.String,Fluent.Pdf.Formats.PdfSheetValueKind,System.Int32,System.Int32,System.String,System.Nullable{System.Int32},System.Nullable{Fluent.Pdf.PdfRect})</code>

Captures a bounded explicit scalar. Formula records are observational cached-value provenance, never executable output.

### <code>M:Fluent.Pdf.Formats.PdfSheetData.#ctor(System.String,System.Int32,System.Int32,System.Collections.Generic.IEnumerable{Fluent.Pdf.Formats.PdfSheetCell})</code>

Captures a valid explicit sheet name and at most 100000 grid positions/1024 columns, with non-overlapping in-bounds spans.

### <code>M:Fluent.Pdf.Formats.PdfSpreadsheets.FromTable(Fluent.Pdf.PdfExtractedTable,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures an inferred PDF table with typed values and original page/bounds. Numeric typing comes only from prior explicit-culture extraction.

### <code>M:Fluent.Pdf.Formats.PdfSpreadsheets.ImportData(Fluent.Pdf.Formats.PdfSheetData,System.Int32,System.Int32,System.Int32,System.Int32,Fluent.Pdf.Formats.PdfFormatTextOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reconstructs an explicit zero-based rectangular range as a flow table with caller typography. Ranges crossing merged cells reject; original worksheet print layout is not asserted.

### <code>M:Fluent.Pdf.Formats.PdfSpreadsheets.ReadData(System.IO.Stream,Fluent.Pdf.Formats.PdfSpreadsheetFormat,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads one explicitly named sheet as data. Formula cells require cached values; original formulas are passive provenance. Binary XLS rejects before reading input.

### <code>M:Fluent.Pdf.Formats.PdfSpreadsheets.WriteData(Fluent.Pdf.Formats.PdfSheetData,Fluent.Pdf.Formats.PdfSpreadsheetFormat,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes one data sheet with original rectangular merges. All formulas remain data; dangerous untyped CSV text rejects. Output is staged before caller writes.

### <code>M:Fluent.Pdf.Formats.PdfSpreadsheets.WriteProvenance(Fluent.Pdf.Formats.PdfSheetData,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes a separate passive XML provenance ledger, including cached source formulas and PDF coordinates. It is never an executable spreadsheet part.

### <code>M:Fluent.Pdf.Formats.PdfStaticEps.Import(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Imports one explicitly bounded DSC EPS page using literal numeric drawing/state operators only. No PostScript VM, host I/O, native interpreter or resource discovery executes.

### <code>M:Fluent.Pdf.Formats.PdfStaticEps.WritePage(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Exports one opaque Gray/RGB vector/Form page as literal level2 EPS data. Text, images, effects, annotations and unsupported color/hairline profiles reject; no raster fallback occurs.

### <code>M:Fluent.Pdf.Formats.PdfStaticEps.WritePageFile(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages complete EPS before atomic file replacement.

### <code>M:Fluent.Pdf.Formats.PdfSvg.Import(System.IO.Stream,Fluent.Pdf.Formats.PdfSvgImportOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Imports opaque paths/rectangles/polylines, transforms/user-space clips, explicit black-fill shaped text and authorized PNG/JPEG data images. CSS/resources/scripts outside the documented matrix reject.

### <code>M:Fluent.Pdf.Formats.PdfSvg.WritePage(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Exports the documented opaque RGB/Gray vector page/Form profile as editable SVG. Text, images, annotations, layers, patterns and unsupported effects reject; no raster substitution occurs.

### <code>M:Fluent.Pdf.Formats.PdfSvg.WritePageFile(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages vector SVG before atomic file replacement.

### <code>M:Fluent.Pdf.Formats.PdfSvgImportOptions.#ctor(Fluent.Pdf.PdfFontFallback,Fluent.Pdf.PdfTextOptions,System.Boolean,Fluent.Pdf.PdfImageReadOptions)</code>

Captures caller-selected fonts/text policy and optional bounded PNG/JPEG data-image authority. Font selection explicitly overrides source family names.

### <code>M:Fluent.Pdf.Formats.PdfTex.Import(System.IO.Stream,Fluent.Pdf.Formats.PdfTexOptions,Fluent.Pdf.Formats.IPdfIsolatedTexTypesetter,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Requires exact prerequisites before capture; stages at most8 MiB source and64 MiB PDF, validates nonempty PDF and preserves caller ownership.

### <code>M:Fluent.Pdf.Formats.PdfTex.WriteInferredText(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},System.IO.Stream,Fluent.Pdf.PdfTextExtractionOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages an escaped ASCII LaTeX article in selected page order. Explicit permissive authority is required for text/layout/math reconstruction loss. No source string can introduce a TeX command.

### <code>M:Fluent.Pdf.Formats.PdfTexOptions.#ctor(Fluent.Pdf.Formats.PdfTexProfile,System.Collections.Generic.IEnumerable{System.String},System.Int32)</code>

Captures at most128 distinct ASCII package identities and an exact engine profile.

### <code>M:Fluent.Pdf.Formats.PdfXps.ImportRasterPages(System.IO.Stream,Fluent.Pdf.Formats.PdfXpsFormat,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Imports the documented PNG-brush full-page profile with exact namespace/version and page order. Print tickets, glyphs, vector/effect/resource-dictionary constructs, external URIs and cropped/partial image brushes reject before return.

### <code>M:Fluent.Pdf.Formats.PdfXps.WriteRasterPages(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},System.IO.Stream,Fluent.Pdf.Formats.PdfXpsFormat,System.String,Fluent.Pdf.PdfRenderOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Renders selected whole pages in explicit order and writes an original OPC fixed-layout package with canonical part names and lossless PNG brushes. Density is normalized to96-DPI image-space defaults; integral96-DPI page dimensions retain source points; fractional dimensions require explicit permissive loss authority.

### <code>P:Fluent.Pdf.Formats.IPdfIsolatedLegacyImporter.EngineId</code>

Gets the pinned nonsecret engine/configuration identity.

### <code>P:Fluent.Pdf.Formats.IPdfIsolatedMetafileConverter.EngineId</code>

Gets a nonsecret ASCII pinned converter/configuration identity.

### <code>P:Fluent.Pdf.Formats.IPdfIsolatedPrinterInterpreter.EngineId</code>

Gets a stable nonsecret ASCII engine/configuration identity. Do not return payloads, paths or credentials.

### <code>P:Fluent.Pdf.Formats.IPdfIsolatedTexTypesetter.EngineId</code>

Gets a stable nonsecret ASCII pinned engine/configuration identity.

### <code>P:Fluent.Pdf.Formats.PdfCollectionBatchResult.OperationReport</code>

Gets the shared operation report.

### <code>P:Fluent.Pdf.Formats.PdfCollectionBatchResult.Outcomes</code>

Gets all attempted outcomes in input order.

### <code>P:Fluent.Pdf.Formats.PdfCollectionItem.Length</code>

Gets the byte length.

### <code>P:Fluent.Pdf.Formats.PdfCollectionItem.Name</code>

Gets its canonical portable leaf name.

### <code>P:Fluent.Pdf.Formats.PdfCollectionOutcome.DiagnosticCode</code>

Gets a stable failure code, or null on success.

### <code>P:Fluent.Pdf.Formats.PdfCollectionOutcome.Name</code>

Gets the original name.

### <code>P:Fluent.Pdf.Formats.PdfCollectionOutcome.Output</code>

Gets successful output, or null when this item failed.

### <code>P:Fluent.Pdf.Formats.PdfConversionOptions.Language</code>

Gets explicit language, when used.

### <code>P:Fluent.Pdf.Formats.PdfConversionOptions.OfdIdentity</code>

Gets explicit OFD identity, when used.

### <code>P:Fluent.Pdf.Formats.PdfConversionOptions.Rendering</code>

Gets explicit raster choices.

### <code>P:Fluent.Pdf.Formats.PdfConversionOptions.Target</code>

Gets exact export profile.

### <code>P:Fluent.Pdf.Formats.PdfConversionOptions.Title</code>

Gets literal HTML title, when used.

### <code>P:Fluent.Pdf.Formats.PdfDocumentCollection.Items</code>

Gets entries in capture or central-directory order.

### <code>P:Fluent.Pdf.Formats.PdfDocumentCollection.OperationReport</code>

Gets the capture operation report.

### <code>P:Fluent.Pdf.Formats.PdfEbookMetadata.Identifier</code>

Gets literal publication identifier.

### <code>P:Fluent.Pdf.Formats.PdfEbookMetadata.Language</code>

Gets explicit language.

### <code>P:Fluent.Pdf.Formats.PdfEbookMetadata.ModifiedUtc</code>

Gets exact caller UTC modification timestamp.

### <code>P:Fluent.Pdf.Formats.PdfEbookMetadata.Title</code>

Gets title.

### <code>P:Fluent.Pdf.Formats.PdfEbookOptions.Layout</code>

Gets selected layout.

### <code>P:Fluent.Pdf.Formats.PdfEbookOptions.Rendering</code>

Gets whole-page rendering policy.

### <code>P:Fluent.Pdf.Formats.PdfEbookOptions.RightToLeft</code>

Gets explicit page progression and XHTML direction.

### <code>P:Fluent.Pdf.Formats.PdfEbookOptions.Text</code>

Gets passive text selection/grouping policy.

### <code>P:Fluent.Pdf.Formats.PdfFormatDetection.Format</code>

Gets the observed family. Consumers must still validate its exact supported content profile.

### <code>P:Fluent.Pdf.Formats.PdfFormatDetection.OperationReport</code>

Gets detection diagnostics, including a literal extension mismatch when supplied.

### <code>P:Fluent.Pdf.Formats.PdfFormatTextOptions.Body</code>

Gets explicit text style and font bindings.

### <code>P:Fluent.Pdf.Formats.PdfFormatTextOptions.Layout</code>

Gets explicit page flow policy.

### <code>P:Fluent.Pdf.Formats.PdfHtmlExportOptions.Language</code>

Gets explicit language tag.

### <code>P:Fluent.Pdf.Formats.PdfHtmlExportOptions.Mode</code>

Gets selected profile.

### <code>P:Fluent.Pdf.Formats.PdfHtmlExportOptions.Rendering</code>

Gets whole-page raster policy.

### <code>P:Fluent.Pdf.Formats.PdfHtmlExportOptions.Text</code>

Gets passive text/layer grouping policy.

### <code>P:Fluent.Pdf.Formats.PdfHtmlExportOptions.Title</code>

Gets literal caller title.

### <code>P:Fluent.Pdf.Formats.PdfLegacyImportOptions.Format</code>

Gets the explicit source format.

### <code>P:Fluent.Pdf.Formats.PdfLegacyImportOptions.MaximumInputBytes</code>

Gets the source capture byte limit.

### <code>P:Fluent.Pdf.Formats.PdfLegacyImportOptions.MaximumOutputBytes</code>

Gets the staged PDF byte limit.

### <code>P:Fluent.Pdf.Formats.PdfLegacyImportOptions.MaximumPages</code>

Gets the maximum nonempty output page count.

### <code>P:Fluent.Pdf.Formats.PdfLegacyImportOptions.VersionProfile</code>

Gets the exact separately documented host version/compression/font profile.

### <code>P:Fluent.Pdf.Formats.PdfOfdIdentity.CreationDate</code>

Gets ISO calendar creation date.

### <code>P:Fluent.Pdf.Formats.PdfOfdIdentity.DocumentId</code>

Gets literal source identifier.

### <code>P:Fluent.Pdf.Formats.PdfOfdIdentity.Title</code>

Gets literal title.

### <code>P:Fluent.Pdf.Formats.PdfSheetCell.Column</code>

Gets zero-based column.

### <code>P:Fluent.Pdf.Formats.PdfSheetCell.ColumnSpan</code>

Gets horizontal span.

### <code>P:Fluent.Pdf.Formats.PdfSheetCell.Kind</code>

Gets explicit type.

### <code>P:Fluent.Pdf.Formats.PdfSheetCell.Row</code>

Gets zero-based row.

### <code>P:Fluent.Pdf.Formats.PdfSheetCell.RowSpan</code>

Gets vertical span.

### <code>P:Fluent.Pdf.Formats.PdfSheetCell.SourceBounds</code>

Gets optional original displayed PDF bounds.

### <code>P:Fluent.Pdf.Formats.PdfSheetCell.SourceFormula</code>

Gets passive source formula, never executed or emitted as a formula.

### <code>P:Fluent.Pdf.Formats.PdfSheetCell.SourcePage</code>

Gets optional original PDF page.

### <code>P:Fluent.Pdf.Formats.PdfSheetCell.Value</code>

Gets canonical scalar data.

### <code>P:Fluent.Pdf.Formats.PdfSheetData.Cells</code>

Gets ordered grid-origin cells.

### <code>P:Fluent.Pdf.Formats.PdfSheetData.ColumnCount</code>

Gets column count.

### <code>P:Fluent.Pdf.Formats.PdfSheetData.Name</code>

Gets explicit name.

### <code>P:Fluent.Pdf.Formats.PdfSheetData.RowCount</code>

Gets row count.

### <code>P:Fluent.Pdf.Formats.PdfSvgImportOptions.AllowDataImages</code>

Gets local data-image authority; network/file URLs always reject.

### <code>P:Fluent.Pdf.Formats.PdfSvgImportOptions.Fonts</code>

Gets explicit font bindings; text rejects if missing.

### <code>P:Fluent.Pdf.Formats.PdfSvgImportOptions.Images</code>

Gets explicit PNG/JPEG color/orientation/lossless-conversion policy.

### <code>P:Fluent.Pdf.Formats.PdfSvgImportOptions.Text</code>

Gets explicit text shaping policy.

### <code>P:Fluent.Pdf.Formats.PdfTexOptions.MaximumPages</code>

Gets the nonempty output page bound.

### <code>P:Fluent.Pdf.Formats.PdfTexOptions.Profile</code>

Gets exact engine/format profile.

### <code>P:Fluent.Pdf.Formats.PdfTexOptions.RequiredPackages</code>

Gets required locally pinned packages. Font, format and transitive package assets are explicit host prerequisites too.

### <code>T:Fluent.Pdf.Formats.IPdfIsolatedLegacyImporter</code>

Caller-owned trusted adapter for an independently qualified offline isolated decoder. Implementing this interface does not supply a sandbox or codec.

### <code>T:Fluent.Pdf.Formats.IPdfIsolatedMetafileConverter</code>

Explicit trusted optional offline isolated converter. Implementations declare exact version/OS/font/record support through their pinned configuration; platform restrictions fail before capture.

### <code>T:Fluent.Pdf.Formats.IPdfIsolatedPrinterInterpreter</code>

Trusted optional host contract for a separately qualified, offline, OS-isolated printer-language interpreter. Merely implementing this interface is not isolation/engine qualification.

### <code>T:Fluent.Pdf.Formats.IPdfIsolatedTexTypesetter</code>

Trusted caller-owned host for a separately qualified offline OS-isolated typesetter. This contract alone does not supply or qualify a sandbox.

### <code>T:Fluent.Pdf.Formats.PdfCollectionAdapter</code>

A trusted synchronous adapter. It must use the supplied context, keep streams borrowed and leave process state unchanged. This delegate is not a sandbox.

### <code>T:Fluent.Pdf.Formats.PdfCollectionAdapters</code>

An immutable instance-owned registry. No process-wide registration or implicit plugin discovery occurs.

### <code>T:Fluent.Pdf.Formats.PdfCollectionBatchResult</code>

An immutable batch result; a completed batch may contain individual failures.

### <code>T:Fluent.Pdf.Formats.PdfCollectionItem</code>

An immutable named byte sequence. Opening an entry never executes its content.

### <code>T:Fluent.Pdf.Formats.PdfCollectionOutcome</code>

A detached item outcome in original input order.

### <code>T:Fluent.Pdf.Formats.PdfConversionOptions</code>

Explicit per-export format options. Language/title/identity are required by the profiles that use them.

### <code>T:Fluent.Pdf.Formats.PdfConversionPhase</code>

Precommit progress; callback failure/cancellation prevents caller output publication.

### <code>T:Fluent.Pdf.Formats.PdfConversionTarget</code>

Exact export profiles available through this source orchestration API.

### <code>T:Fluent.Pdf.Formats.PdfConversions</code>

Content discovery and explicit source-profile export orchestration. No advertised entry denotes complete standards or production qualification.

### <code>T:Fluent.Pdf.Formats.PdfDetectedFormat</code>

Content family observation, not complete validation or an inferred plain-text format.

### <code>T:Fluent.Pdf.Formats.PdfDocumentCollection</code>

A bounded ordered collection with no filesystem extraction or implicit format conversion.

### <code>T:Fluent.Pdf.Formats.PdfEbookLayout</code>

Explicit EPUB layout intent.

### <code>T:Fluent.Pdf.Formats.PdfEbookMetadata</code>

Explicit publication identity; identifiers and timestamps are never generated from ambient state.

### <code>T:Fluent.Pdf.Formats.PdfEbookOptions</code>

Explicit EPUB layout, page progression and rendering/extraction policy.

### <code>T:Fluent.Pdf.Formats.PdfEbooks</code>

Original bounded EPUB3 generation, passive reflow-text import and documented MobiXML source output. No DRM circumvention or book compilation occurs.

### <code>T:Fluent.Pdf.Formats.PdfFormatDetection</code>

Immutable content identification and shared operation report.

### <code>T:Fluent.Pdf.Formats.PdfFormatTextOptions</code>

Caller-owned explicit typography and flow geometry for data/text reconstruction. No font or locale discovery occurs.

### <code>T:Fluent.Pdf.Formats.PdfFormattingObjects</code>

Closed, passive XSL-FO block/table layout and finite declarative XML transformation. No general XSLT evaluator or external resolver executes.

### <code>T:Fluent.Pdf.Formats.PdfHtmlExport</code>

Bounded offline HTML export with static CSP, escaped source text, explicit rasterization and declared text reconstruction losses.

### <code>T:Fluent.Pdf.Formats.PdfHtmlExportMode</code>

Explicit reconstruction intent; neither profile infers source accessibility structure.

### <code>T:Fluent.Pdf.Formats.PdfHtmlExportOptions</code>

Explicit HTML document identity, rendering and passive extraction choices.

### <code>T:Fluent.Pdf.Formats.PdfLegacyFormat</code>

Explicit read-only source format for a separately provisioned import host.

### <code>T:Fluent.Pdf.Formats.PdfLegacyImport</code>

Bounded, explicit DjVu/CDR host integration. No built-in decoder or supported source version is supplied.

### <code>T:Fluent.Pdf.Formats.PdfLegacyImportOptions</code>

Immutable exact decoder profile and finite source, output and page bounds.

### <code>T:Fluent.Pdf.Formats.PdfLegacyImportStatus</code>

Stable import outcomes. A completed decode must still return a nonempty valid PDF.

### <code>T:Fluent.Pdf.Formats.PdfMetafileFormat</code>

Exact caller-selected metafile family; no format guessing or desktop-session fallback.

### <code>T:Fluent.Pdf.Formats.PdfMetafiles</code>

Managed EMF24 raster-page interchange and explicit optional full metafile conversion.

### <code>T:Fluent.Pdf.Formats.PdfObjectXml</code>

Versioned typed XML interchange for the active PDF object graph, including opaque stream bytes. It does not execute actions or preserve revision history, encryption or signatures.

### <code>T:Fluent.Pdf.Formats.PdfOfd</code>

Original OFD1.0 raster-page package interchange. No font/vector/signature reconstruction or standard-wide conformance claim.

### <code>T:Fluent.Pdf.Formats.PdfOfdIdentity</code>

Explicit OFD document identity. No random identifier, ambient date or metadata invention occurs.

### <code>T:Fluent.Pdf.Formats.PdfPresentations</code>

Explicit original image-based PPTX interchange. No editable text/vector reconstruction or automatic raster fallback is asserted.

### <code>T:Fluent.Pdf.Formats.PdfPrinterLanguageProfile</code>

Exact caller-selected language/version. No language or engine substitution occurs.

### <code>T:Fluent.Pdf.Formats.PdfPrinterLanguages</code>

Explicit bounded import through a caller-owned optional isolated interpreter. The managed static EPS subset has a separate non-executing API.

### <code>T:Fluent.Pdf.Formats.PdfSheetCell</code>

Immutable cell data, rectangular span and optional passive source provenance.

### <code>T:Fluent.Pdf.Formats.PdfSheetData</code>

Immutable bounded data grid. This profile reconstructs data, without original print styling or charts.

### <code>T:Fluent.Pdf.Formats.PdfSheetValueKind</code>

Explicit scalar type; numeric/date inference from styles or locale never occurs.

### <code>T:Fluent.Pdf.Formats.PdfSpreadsheetFormat</code>

Explicit spreadsheet data interchange families. Binary XLS requires a separate qualified adapter.

### <code>T:Fluent.Pdf.Formats.PdfSpreadsheets</code>

Original bounded spreadsheet data interchange. No formula execution, ambient locale or binary XLS fallback occurs.

### <code>T:Fluent.Pdf.Formats.PdfStaticEps</code>

Original non-executing static EPS drawing-data subset. Procedures, text/fonts/images, file/process operators and unknown commands reject.

### <code>T:Fluent.Pdf.Formats.PdfSvg</code>

Original bounded static SVG import and vector-page SVG export. Unsupported constructs reject without raster substitution.

### <code>T:Fluent.Pdf.Formats.PdfSvgImportOptions</code>

Explicit fonts/shaping and local embedded-image authority for the documented SVG profile. No font, URI or stylesheet discovery occurs.

### <code>T:Fluent.Pdf.Formats.PdfTex</code>

Explicit optional typesetting import and passive ASCII LaTeX text reconstruction.

### <code>T:Fluent.Pdf.Formats.PdfTexOptions</code>

Immutable, explicit engine and local package prerequisites. No package installation/discovery is permitted.

### <code>T:Fluent.Pdf.Formats.PdfTexProfile</code>

Explicit TeX engine/format identities. Availability never implies permission for shell escape or ambient file/network access.

### <code>T:Fluent.Pdf.Formats.PdfXps</code>

Original bounded raster-page XPS/OpenXPS interchange. General glyph/vector/resource-dictionary and print-ticket processing remain separate mandatory work.

### <code>T:Fluent.Pdf.Formats.PdfXpsFormat</code>

Explicit fixed-layout namespace/package version.

