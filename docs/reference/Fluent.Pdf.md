# Fluent.Pdf source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.IPdfAccessibilityValidator
Fluent.Pdf.IPdfAccessibilityValidator.Validate(Fluent.Pdf.PdfAccessibilityProfile profile, System.IO.Stream! input, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfAccessibilityValidation!
Fluent.Pdf.IPdfArchivalValidator
Fluent.Pdf.IPdfArchivalValidator.Validate(Fluent.Pdf.PdfArchivalProfile profile, System.IO.Stream! input, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfArchivalValidation!
Fluent.Pdf.IPdfAsyncSignatureProvider
Fluent.Pdf.IPdfAsyncSignatureProvider.SignAsync(System.IO.Stream! signedBytes, System.IO.Stream! cmsOutput, Fluent.Pdf.PdfOperationContext! context) -> System.Threading.Tasks.Task!
Fluent.Pdf.IPdfBarcodeDecoder
Fluent.Pdf.IPdfBarcodeDecoder.Decode(Fluent.Pdf.PdfOcrRaster! raster, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfBarcodeObservation?
Fluent.Pdf.IPdfBarcodeDecoder.Supports(Fluent.Pdf.PdfBarcodeSymbology symbology) -> bool
Fluent.Pdf.IPdfCertificateStatusProvider
Fluent.Pdf.IPdfCertificateStatusProvider.Check(System.IO.Stream! certificate, System.DateTimeOffset evaluationTime, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfRevocationStatus
Fluent.Pdf.IPdfCidFontProgram
Fluent.Pdf.IPdfCidFontProgram.MapCid(int cid, Fluent.Pdf.PdfOperationContext! context) -> int
Fluent.Pdf.IPdfCmsEnvelopeProvider
Fluent.Pdf.IPdfCmsEnvelopeProvider.Encrypt(System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfCertificateRecipient!>! recipients, System.IO.Stream! plaintext, System.IO.Stream! envelope, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfCmsEnvelopeProvider.TryDecrypt(Fluent.Pdf.PdfCertificateCredential! credential, bool allowLegacyKeyTransport, System.IO.Stream! envelope, System.IO.Stream! plaintext, Fluent.Pdf.PdfOperationContext! context) -> bool
Fluent.Pdf.IPdfColorManagedPageRenderer
Fluent.Pdf.IPdfColorManagedPageRenderer.Render(Fluent.Pdf.PdfRasterRequest! request, Fluent.Pdf.PdfColorRenderOptions! colorManagement, System.IO.Stream! bgraOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfDocumentReader
Fluent.Pdf.IPdfDocumentReader.Read(System.IO.Stream! input, Fluent.Pdf.PdfDocument! target, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfDocumentReader.RequiresSeekableStream.get -> bool
Fluent.Pdf.IPdfDocumentWriter
Fluent.Pdf.IPdfDocumentWriter.RequiresSeekableStream.get -> bool
Fluent.Pdf.IPdfDocumentWriter.Write(Fluent.Pdf.PdfDocumentSnapshot! document, System.IO.Stream! output, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfExternalSigningKey
Fluent.Pdf.IPdfExternalSigningKey.OpenSessionAsync(Fluent.Pdf.PdfOperationContext! context) -> System.Threading.Tasks.Task<Fluent.Pdf.IPdfExternalSigningSession!>!
Fluent.Pdf.IPdfExternalSigningSession
Fluent.Pdf.IPdfExternalSigningSession.SignHashAsync(System.IO.Stream! sha256Digest, System.IO.Stream! signatureOutput, Fluent.Pdf.PdfOperationContext! context) -> System.Threading.Tasks.Task!
Fluent.Pdf.IPdfFontEncodingMetricsProvider
Fluent.Pdf.IPdfFontEncodingMetricsProvider.GetSimpleAdvances(Fluent.Pdf.PdfValue! encoding, string! baseFont, Fluent.Pdf.PdfOperationContext! context) -> System.Collections.Generic.IReadOnlyList<double?>!
Fluent.Pdf.IPdfFontEncodingProvider
Fluent.Pdf.IPdfFontEncodingProvider.DecodeSimple(Fluent.Pdf.PdfValue! encoding, Fluent.Pdf.PdfValue! encodedText, string! baseFont, Fluent.Pdf.PdfOperationContext! context) -> string!
Fluent.Pdf.IPdfFontProgram
Fluent.Pdf.IPdfFontProgram.GetAdvance(int glyph, Fluent.Pdf.PdfOperationContext! context) -> double
Fluent.Pdf.IPdfFontProgram.GetGlyphName(int glyph, Fluent.Pdf.PdfOperationContext! context) -> string!
Fluent.Pdf.IPdfFontProgram.Info.get -> Fluent.Pdf.PdfFontInfo!
Fluent.Pdf.IPdfFontProgram.MapGlyph(int unicodeScalar, Fluent.Pdf.PdfOperationContext! context) -> int
Fluent.Pdf.IPdfFontProgram.Write(System.IO.Stream! output, System.Collections.Generic.IReadOnlyList<int>! glyphs, bool subset, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfFontProgramWriteResult!
Fluent.Pdf.IPdfFontProvider
Fluent.Pdf.IPdfFontProvider.Read(System.IO.Stream! input, int faceIndex, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.IPdfFontProgram!
Fluent.Pdf.IPdfFontProvider.Standard(string! postScriptName, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.IPdfFontProgram!
Fluent.Pdf.IPdfFontShapingSource
Fluent.Pdf.IPdfFontShapingSource.CopyShapingSource(System.IO.Stream! output, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfFontShapingSource.ShapingFaceIndex.get -> int
Fluent.Pdf.IPdfHtmlRenderer
Fluent.Pdf.IPdfHtmlRenderer.Render(Fluent.Pdf.PdfHtmlSource! source, Fluent.Pdf.PdfHtmlOptions! options, System.IO.Stream! pdfOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfImageCodecProvider
Fluent.Pdf.IPdfImageCodecProvider.Decode(Fluent.Pdf.PdfImageCodec codec, Fluent.Pdf.PdfImageInfo! info, Fluent.Pdf.PdfValue! imageDictionary, Fluent.Pdf.PdfValue! decodeParameters, System.IO.Stream! encodedInput, System.IO.Stream! sampleOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfImageCodecProvider.Encode(Fluent.Pdf.PdfImageCodec codec, Fluent.Pdf.PdfImageInfo! info, Fluent.Pdf.PdfValue! parameters, System.IO.Stream! sampleInput, System.IO.Stream! encodedOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfImageCodecProvider.Inspect(Fluent.Pdf.PdfImageCodec codec, System.IO.Stream! encodedInput, Fluent.Pdf.PdfValue! imageDictionary, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfImageInfo!
Fluent.Pdf.IPdfImageCodecProvider.Supports(Fluent.Pdf.PdfImageCodec codec, bool encoding) -> bool
Fluent.Pdf.IPdfInvoiceProducer
Fluent.Pdf.IPdfInvoiceProducer.Convert(System.IO.Stream! sourcePdf, System.IO.Stream! invoiceXml, Fluent.Pdf.PdfInvoiceData! invoice, System.IO.Stream! candidatePdf, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfInvoiceProducer.EngineId.get -> string!
Fluent.Pdf.IPdfInvoiceProducer.Supports(Fluent.Pdf.PdfInvoiceProfile profile) -> bool
Fluent.Pdf.IPdfInvoiceValidator
Fluent.Pdf.IPdfInvoiceValidator.EngineId.get -> string!
Fluent.Pdf.IPdfInvoiceValidator.Supports(Fluent.Pdf.PdfInvoiceProfile profile) -> bool
Fluent.Pdf.IPdfInvoiceValidator.Validate(System.IO.Stream! pdf, System.IO.Stream! xml, Fluent.Pdf.PdfInvoiceData! invoice, string! requestSha256, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfInvoiceValidation!
Fluent.Pdf.IPdfLinearizationProducer
Fluent.Pdf.IPdfLinearizationProducer.EngineId.get -> string!
Fluent.Pdf.IPdfLinearizationProducer.Linearize(System.IO.Stream! source, System.IO.Stream! candidate, Fluent.Pdf.PdfLinearizationOptions! options, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfLinearizationProducer.Supports(Fluent.Pdf.PdfLinearizationOptions! options) -> bool
Fluent.Pdf.IPdfLinearizationValidator
Fluent.Pdf.IPdfLinearizationValidator.EngineId.get -> string!
Fluent.Pdf.IPdfLinearizationValidator.Supports(Fluent.Pdf.PdfLinearizationOptions! options) -> bool
Fluent.Pdf.IPdfLinearizationValidator.Validate(System.IO.Stream! candidate, Fluent.Pdf.PdfLinearizationOptions! options, string! requestSha256, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfLinearizationValidation!
Fluent.Pdf.IPdfOcrProvider
Fluent.Pdf.IPdfOcrProvider.Recognize(Fluent.Pdf.PdfOcrRaster! raster, Fluent.Pdf.PdfOcrOptions! options, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfOcrResult!
Fluent.Pdf.IPdfPadesProvider
Fluent.Pdf.IPdfPadesProvider.Upgrade(System.IO.Stream! source, System.IO.Stream! output, Fluent.Pdf.PdfPadesProfile profile, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfPadesValidator
Fluent.Pdf.IPdfPadesValidator.Validate(System.IO.Stream! source, Fluent.Pdf.PdfPadesProfile profile, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfPadesValidation!
Fluent.Pdf.IPdfPageRenderer
Fluent.Pdf.IPdfPageRenderer.Render(Fluent.Pdf.PdfRasterRequest! request, System.IO.Stream! bgraOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfPrintProvider
Fluent.Pdf.IPdfPrintProvider.Cancel(string! printer, int jobId, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfPrintJob!
Fluent.Pdf.IPdfPrintProvider.GetStatus(string! printer, int jobId, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfPrintJob!
Fluent.Pdf.IPdfPrintProvider.Submit(System.IO.Stream! pdf, int pageCount, Fluent.Pdf.PdfPrintOptions! options, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfPrintJob!
Fluent.Pdf.IPdfProfileProducer
Fluent.Pdf.IPdfProfileProducer.Convert(System.IO.Stream! source, Fluent.Pdf.PdfProfileOptions! options, System.IO.Stream! candidate, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfProfileProducer.EngineId.get -> string!
Fluent.Pdf.IPdfProfileProducer.Supports(Fluent.Pdf.PdfProfileOptions! options) -> bool
Fluent.Pdf.IPdfProfileValidator
Fluent.Pdf.IPdfProfileValidator.EngineId.get -> string!
Fluent.Pdf.IPdfProfileValidator.Supports(Fluent.Pdf.PdfConformanceProfile profile) -> bool
Fluent.Pdf.IPdfProfileValidator.Validate(System.IO.Stream! input, Fluent.Pdf.PdfProfileOptions! options, string! requestSha256, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfProfileValidation!
Fluent.Pdf.IPdfProvider
Fluent.Pdf.IPdfProvider.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.IPdfRangeSource
Fluent.Pdf.IPdfRangeSource.GetIdentity(Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfRangeIdentity!
Fluent.Pdf.IPdfRangeSource.OpenRange(long offset, int length, Fluent.Pdf.PdfRangeIdentity! expectedIdentity, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfRangeResponse!
Fluent.Pdf.IPdfRasterDocumentDecoder
Fluent.Pdf.IPdfRasterDocumentDecoder.DecodeFrame(System.IO.Stream! file, Fluent.Pdf.PdfRasterFileFormat format, int frame, Fluent.Pdf.PdfRasterFrameInfo! info, System.IO.Stream! rgbaOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfRasterDocumentDecoder.Inspect(System.IO.Stream! file, Fluent.Pdf.PdfRasterFileFormat format, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfRasterFileInfo!
Fluent.Pdf.IPdfRasterDocumentDecoder.Supports(Fluent.Pdf.PdfRasterFileFormat format) -> bool
Fluent.Pdf.IPdfRevisionWriter
Fluent.Pdf.IPdfRevisionWriter.Supports(Fluent.Pdf.PdfWriteOptions! options) -> bool
Fluent.Pdf.IPdfSignatureProvider
Fluent.Pdf.IPdfSignatureProvider.Sign(System.IO.Stream! signedBytes, System.IO.Stream! cmsOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfSignatureProvider.Verify(System.IO.Stream! signedBytes, System.IO.Stream! cms, Fluent.Pdf.PdfOperationContext! context) -> bool
Fluent.Pdf.IPdfSignatureTrustProvider
Fluent.Pdf.IPdfSignatureTrustProvider.Evaluate(System.IO.Stream! cms, Fluent.Pdf.PdfSignatureTrustOptions! options, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfSignatureTrustDecision!
Fluent.Pdf.IPdfTemplateHtmlProvider
Fluent.Pdf.IPdfTemplateHtmlProvider.Render(string! html, Fluent.Pdf.PdfPageSize viewport, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfDrawing!
Fluent.Pdf.IPdfTextShaper
Fluent.Pdf.IPdfTextShaper.Shape(Fluent.Pdf.PdfShapingRequest! request, Fluent.Pdf.PdfOperationContext! context) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfShapedGlyph>!
Fluent.Pdf.IPdfVerticalFontProgram
Fluent.Pdf.IPdfVerticalFontProgram.GetVerticalMetrics(int glyph, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfFontVerticalMetrics
Fluent.Pdf.IPdfVerticalFontProgram.SupportsVerticalMetrics.get -> bool
Fluent.Pdf.IPdfWordConverter
Fluent.Pdf.IPdfWordConverter.Export(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfWordFormat format, System.IO.Stream! output, Fluent.Pdf.PdfWordConversionOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfWordConverter.Import(System.IO.Stream! source, Fluent.Pdf.PdfWordFormat format, System.IO.Stream! pdfOutput, Fluent.Pdf.PdfWordConversionOptions! options, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.IPdfWordConverter.Supports(Fluent.Pdf.PdfWordFormat format, bool import) -> bool
Fluent.Pdf.ManagedPdfProvider
Fluent.Pdf.ManagedPdfProvider.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.ManagedPdfProvider.ManagedPdfProvider() -> void
Fluent.Pdf.ManagedPdfProvider.Read(System.IO.Stream! input, Fluent.Pdf.PdfDocument! target, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.ManagedPdfProvider.RequiresSeekableStream.get -> bool
Fluent.Pdf.ManagedPdfProvider.Supports(Fluent.Pdf.PdfWriteOptions! options) -> bool
Fluent.Pdf.ManagedPdfProvider.Write(Fluent.Pdf.PdfDocumentSnapshot! document, System.IO.Stream! output, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Pdf
Fluent.Pdf.PdfAccessibility
Fluent.Pdf.PdfAccessibilityFinding
Fluent.Pdf.PdfAccessibilityFinding.Location.get -> string!
Fluent.Pdf.PdfAccessibilityFinding.Message.get -> string!
Fluent.Pdf.PdfAccessibilityFinding.PdfAccessibilityFinding(string! rule, string! location, string! message) -> void
Fluent.Pdf.PdfAccessibilityFinding.Rule.get -> string!
Fluent.Pdf.PdfAccessibilityHumanReview
Fluent.Pdf.PdfAccessibilityHumanReview.PdfAccessibilityHumanReview(Fluent.Pdf.PdfAccessibilityProfile profile, string! sourceSha256, string! reviewer, string! reportId, bool readingOrderReviewed, bool semanticsReviewed) -> void
Fluent.Pdf.PdfAccessibilityHumanReview.Profile.get -> Fluent.Pdf.PdfAccessibilityProfile
Fluent.Pdf.PdfAccessibilityHumanReview.ReadingOrderReviewed.get -> bool
Fluent.Pdf.PdfAccessibilityHumanReview.ReportId.get -> string!
Fluent.Pdf.PdfAccessibilityHumanReview.Reviewer.get -> string!
Fluent.Pdf.PdfAccessibilityHumanReview.SemanticsReviewed.get -> bool
Fluent.Pdf.PdfAccessibilityHumanReview.SourceSha256.get -> string!
Fluent.Pdf.PdfAccessibilityInspection
Fluent.Pdf.PdfAccessibilityInspection.Content.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfStructureContent!>!
Fluent.Pdf.PdfAccessibilityInspection.Elements.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfStructureElementInfo!>!
Fluent.Pdf.PdfAccessibilityInspection.Findings.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfAccessibilityFinding!>!
Fluent.Pdf.PdfAccessibilityInspection.HumanReviewRequired.get -> bool
Fluent.Pdf.PdfAccessibilityInspection.Language.get -> string?
Fluent.Pdf.PdfAccessibilityInspection.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfAccessibilityInspection.Profile.get -> Fluent.Pdf.PdfAccessibilityProfile
Fluent.Pdf.PdfAccessibilityInspection.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfAccessibilityProfile
Fluent.Pdf.PdfAccessibilityProfile.Ua1 = 0 -> Fluent.Pdf.PdfAccessibilityProfile
Fluent.Pdf.PdfAccessibilityProfile.Ua2 = 1 -> Fluent.Pdf.PdfAccessibilityProfile
Fluent.Pdf.PdfAccessibilityValidation
Fluent.Pdf.PdfAccessibilityValidation.Completed.get -> bool
Fluent.Pdf.PdfAccessibilityValidation.Findings.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfAccessibilityFinding!>!
Fluent.Pdf.PdfAccessibilityValidation.HumanReview.get -> Fluent.Pdf.PdfAccessibilityHumanReview?
Fluent.Pdf.PdfAccessibilityValidation.IsCompliant.get -> bool
Fluent.Pdf.PdfAccessibilityValidation.OperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfAccessibilityValidation.PdfAccessibilityValidation(Fluent.Pdf.PdfAccessibilityProfile profile, string! sourceSha256, bool completed, bool isCompliant, Fluent.Pdf.PdfAccessibilityFinding![]! findings) -> void
Fluent.Pdf.PdfAccessibilityValidation.Profile.get -> Fluent.Pdf.PdfAccessibilityProfile
Fluent.Pdf.PdfAccessibilityValidation.SourceSha256.get -> string!
Fluent.Pdf.PdfAction
Fluent.Pdf.PdfAction.Then(params Fluent.Pdf.PdfAction![]! actions) -> Fluent.Pdf.PdfAction!
Fluent.Pdf.PdfActionPolicy
Fluent.Pdf.PdfActionPolicy.AllowJavaScript.get -> bool
Fluent.Pdf.PdfActionPolicy.AllowLaunch.get -> bool
Fluent.Pdf.PdfActionPolicy.AllowLocalGoTo.get -> bool
Fluent.Pdf.PdfActionPolicy.AllowOtherActions.get -> bool
Fluent.Pdf.PdfActionPolicy.AllowRemote.get -> bool
Fluent.Pdf.PdfActionPolicy.AllowUri.get -> bool
Fluent.Pdf.PdfActionPolicy.AllowedUriSchemes.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfActionPolicy.PdfActionPolicy(bool allowLocalGoTo = true, bool allowRemote = false, bool allowLaunch = false, bool allowJavaScript = false, bool allowOtherActions = false, bool allowUri = true, params string![]! allowedUriSchemes) -> void
Fluent.Pdf.PdfActionRecord
Fluent.Pdf.PdfActionRecord.Definition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfActionRecord.IsAllowed.get -> bool
Fluent.Pdf.PdfActionRecord.IsAttachment.get -> bool
Fluent.Pdf.PdfActionRecord.Location.get -> string!
Fluent.Pdf.PdfActionRecord.Subtype.get -> string!
Fluent.Pdf.PdfAnnotation
Fluent.Pdf.PdfAnnotation.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfAnnotation.Identifier.get -> string!
Fluent.Pdf.PdfAnnotation.Kind.get -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotation.Metadata.get -> Fluent.Pdf.PdfAnnotationMetadata!
Fluent.Pdf.PdfAnnotation.MinimumVersion.get -> string!
Fluent.Pdf.PdfAnnotation.NormalAppearance.get -> Fluent.Pdf.PdfGraphicsResource!
Fluent.Pdf.PdfAnnotation.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfAnnotation.Properties.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.Caret = 13 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.Circle = 8 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.FileAttachment = 14 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.FreeText = 12 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.Highlight = 1 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.Ink = 5 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.Line = 9 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.PolyLine = 11 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.Polygon = 10 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.RichMedia = 15 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.Square = 7 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.Squiggly = 4 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.Stamp = 6 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.StrikeOut = 3 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.Text = 0 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationKind.Underline = 2 -> Fluent.Pdf.PdfAnnotationKind
Fluent.Pdf.PdfAnnotationMetadata
Fluent.Pdf.PdfAnnotationMetadata.Author.get -> string!
Fluent.Pdf.PdfAnnotationMetadata.Contents.get -> string!
Fluent.Pdf.PdfAnnotationMetadata.Created.get -> System.DateTimeOffset?
Fluent.Pdf.PdfAnnotationMetadata.GroupReply.get -> bool
Fluent.Pdf.PdfAnnotationMetadata.Modified.get -> System.DateTimeOffset?
Fluent.Pdf.PdfAnnotationMetadata.PdfAnnotationMetadata(string! contents = "", string! author = "", string! subject = "", System.DateTimeOffset? created = null, System.DateTimeOffset? modified = null, string? state = null, string? stateModel = null, string? replyTo = null, bool groupReply = false) -> void
Fluent.Pdf.PdfAnnotationMetadata.ReplyTo.get -> string?
Fluent.Pdf.PdfAnnotationMetadata.State.get -> string?
Fluent.Pdf.PdfAnnotationMetadata.StateModel.get -> string?
Fluent.Pdf.PdfAnnotationMetadata.Subject.get -> string!
Fluent.Pdf.PdfAnnotationRecord
Fluent.Pdf.PdfAnnotationRecord.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfAnnotationRecord.Definition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfAnnotationRecord.Identifier.get -> string?
Fluent.Pdf.PdfAnnotationRecord.Index.get -> int
Fluent.Pdf.PdfAnnotationRecord.ObjectId.get -> Fluent.Pdf.PdfObjectId?
Fluent.Pdf.PdfAnnotationRecord.PageIndex.get -> int
Fluent.Pdf.PdfAnnotationRecord.PageOrdinal.get -> int
Fluent.Pdf.PdfAnnotationRecord.Subtype.get -> string!
Fluent.Pdf.PdfAnnotationRecord.TextProperties.get -> System.Collections.Generic.IReadOnlyDictionary<string!, string!>!
Fluent.Pdf.PdfAnnotationSet
Fluent.Pdf.PdfAnnotationSet.AddPopup(int annotationIndex, Fluent.Pdf.PdfRect bounds, bool open = false, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfAnnotationSet.Annotations.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfAnnotationRecord!>!
Fluent.Pdf.PdfAnnotationSet.Delete(System.Collections.Generic.IEnumerable<int>! annotationIndices, bool cascade = true, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfAnnotationSet.Flatten(System.Collections.Generic.IEnumerable<int>! annotationIndices, Fluent.Pdf.PdfAnnotationView view = Fluent.Pdf.PdfAnnotationView.Screen, bool cascade = true, bool allowRichMediaPosterOnly = false, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfAnnotationSet.ImportTo(Fluent.Pdf.PdfDocumentSnapshot! target, int targetPageIndex, System.Collections.Generic.IEnumerable<int>! annotationIndices, bool includeRelated = true, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfAnnotationSet.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfAnnotationSet.SetReply(int annotationIndex, int? targetIndex, bool grouped = false, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfAnnotationSet.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfAnnotationSet.Update(int annotationIndex, Fluent.Pdf.PdfValue! properties, Fluent.Pdf.PdfRect? bounds = null, Fluent.Pdf.PdfGraphicsResource? normalAppearance = null, string![]? removeProperties = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfAnnotationSet.WriteXfdf(System.IO.Stream! output, System.Collections.Generic.IEnumerable<int>? annotationIndices = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfAnnotationView
Fluent.Pdf.PdfAnnotationView.Print = 1 -> Fluent.Pdf.PdfAnnotationView
Fluent.Pdf.PdfAnnotationView.Screen = 0 -> Fluent.Pdf.PdfAnnotationView
Fluent.Pdf.PdfAnnotations
Fluent.Pdf.PdfArchival
Fluent.Pdf.PdfArchivalConversion
Fluent.Pdf.PdfArchivalConversion.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfArchivalConversion.Validation.get -> Fluent.Pdf.PdfArchivalValidation!
Fluent.Pdf.PdfArchivalFinding
Fluent.Pdf.PdfArchivalFinding.Location.get -> string!
Fluent.Pdf.PdfArchivalFinding.Message.get -> string!
Fluent.Pdf.PdfArchivalFinding.PdfArchivalFinding(string! rule, string! location, string! message) -> void
Fluent.Pdf.PdfArchivalFinding.Rule.get -> string!
Fluent.Pdf.PdfArchivalInspection
Fluent.Pdf.PdfArchivalInspection.Findings.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfArchivalFinding!>!
Fluent.Pdf.PdfArchivalInspection.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfArchivalOptions
Fluent.Pdf.PdfArchivalOptions.AllowSignatureInvalidation.get -> bool
Fluent.Pdf.PdfArchivalOptions.OutputColorSpace.get -> Fluent.Pdf.PdfColorSpace?
Fluent.Pdf.PdfArchivalOptions.OutputCondition.get -> string!
Fluent.Pdf.PdfArchivalOptions.PdfArchivalOptions(Fluent.Pdf.PdfArchivalProfile profile, Fluent.Pdf.PdfColorSpace? outputColorSpace = null, string! outputCondition = "Explicit output condition", bool replaceMetadata = false, bool allowSignatureInvalidation = false) -> void
Fluent.Pdf.PdfArchivalOptions.Profile.get -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalOptions.ReplaceMetadata.get -> bool
Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalProfile.A1A = 0 -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalProfile.A1B = 1 -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalProfile.A2A = 2 -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalProfile.A2B = 3 -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalProfile.A2U = 4 -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalProfile.A3A = 5 -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalProfile.A3B = 6 -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalProfile.A3U = 7 -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalProfile.A4 = 8 -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalProfile.A4E = 9 -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalProfile.A4F = 10 -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalValidation
Fluent.Pdf.PdfArchivalValidation.Completed.get -> bool
Fluent.Pdf.PdfArchivalValidation.Findings.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfArchivalFinding!>!
Fluent.Pdf.PdfArchivalValidation.IsCompliant.get -> bool
Fluent.Pdf.PdfArchivalValidation.OperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfArchivalValidation.PdfArchivalValidation(Fluent.Pdf.PdfArchivalProfile profile, string! sourceSha256, bool completed, bool isCompliant, Fluent.Pdf.PdfArchivalFinding![]! findings) -> void
Fluent.Pdf.PdfArchivalValidation.Profile.get -> Fluent.Pdf.PdfArchivalProfile
Fluent.Pdf.PdfArchivalValidation.SourceSha256.get -> string!
Fluent.Pdf.PdfAttachment
Fluent.Pdf.PdfAttachment.Description.get -> string?
Fluent.Pdf.PdfAttachment.FileName.get -> string!
Fluent.Pdf.PdfAttachment.Id.get -> Fluent.Pdf.PdfObjectId?
Fluent.Pdf.PdfAttachment.Name.get -> string?
Fluent.Pdf.PdfAttachment.Relationship.get -> string?
Fluent.Pdf.PdfAttachment.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfAttachment.Specification.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfAttachment.Variants.get -> System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfValue!>!
Fluent.Pdf.PdfAttachmentOptions
Fluent.Pdf.PdfAttachmentOptions.CreationTime.get -> System.DateTimeOffset?
Fluent.Pdf.PdfAttachmentOptions.Description.get -> string?
Fluent.Pdf.PdfAttachmentOptions.FileName.get -> string!
Fluent.Pdf.PdfAttachmentOptions.MimeType.get -> string!
Fluent.Pdf.PdfAttachmentOptions.ModificationTime.get -> System.DateTimeOffset?
Fluent.Pdf.PdfAttachmentOptions.PdfAttachmentOptions(string! fileName, string! mimeType = "application/octet-stream", string? description = null, System.DateTimeOffset? creationTime = null, System.DateTimeOffset? modificationTime = null, string? relationship = null) -> void
Fluent.Pdf.PdfAttachmentOptions.Relationship.get -> string?
Fluent.Pdf.PdfAttachmentSet
Fluent.Pdf.PdfAttachmentSet.Collection.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfAttachmentSet.EmbeddedStreams.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfValue!>!
Fluent.Pdf.PdfAttachmentSet.Items.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfAttachment!>!
Fluent.Pdf.PdfAttachmentSet.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfAttachmentSet.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfAttachments
Fluent.Pdf.PdfAuthenticationRole
Fluent.Pdf.PdfAuthenticationRole.Certificate = 2 -> Fluent.Pdf.PdfAuthenticationRole
Fluent.Pdf.PdfAuthenticationRole.CreatedProtection = 3 -> Fluent.Pdf.PdfAuthenticationRole
Fluent.Pdf.PdfAuthenticationRole.Owner = 1 -> Fluent.Pdf.PdfAuthenticationRole
Fluent.Pdf.PdfAuthenticationRole.User = 0 -> Fluent.Pdf.PdfAuthenticationRole
Fluent.Pdf.PdfBarcodeDetection
Fluent.Pdf.PdfBarcodeDetection.Observation.get -> Fluent.Pdf.PdfBarcodeObservation?
Fluent.Pdf.PdfBarcodeDetection.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfBarcodeDetection.PageIndex.get -> int?
Fluent.Pdf.PdfBarcodeErrorCorrection
Fluent.Pdf.PdfBarcodeErrorCorrection.High = 4 -> Fluent.Pdf.PdfBarcodeErrorCorrection
Fluent.Pdf.PdfBarcodeErrorCorrection.Low = 1 -> Fluent.Pdf.PdfBarcodeErrorCorrection
Fluent.Pdf.PdfBarcodeErrorCorrection.Medium = 2 -> Fluent.Pdf.PdfBarcodeErrorCorrection
Fluent.Pdf.PdfBarcodeErrorCorrection.None = 0 -> Fluent.Pdf.PdfBarcodeErrorCorrection
Fluent.Pdf.PdfBarcodeErrorCorrection.Quartile = 3 -> Fluent.Pdf.PdfBarcodeErrorCorrection
Fluent.Pdf.PdfBarcodeObservation
Fluent.Pdf.PdfBarcodeObservation.ErrorCorrection.get -> Fluent.Pdf.PdfBarcodeErrorCorrection
Fluent.Pdf.PdfBarcodeObservation.PdfBarcodeObservation(Fluent.Pdf.PdfBarcodeSymbology symbology, string! text, Fluent.Pdf.PdfBarcodeErrorCorrection errorCorrection, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfPoint>! points) -> void
Fluent.Pdf.PdfBarcodeObservation.Points.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfPoint>!
Fluent.Pdf.PdfBarcodeObservation.Symbology.get -> Fluent.Pdf.PdfBarcodeSymbology
Fluent.Pdf.PdfBarcodeObservation.Text.get -> string!
Fluent.Pdf.PdfBarcodeOptions
Fluent.Pdf.PdfBarcodeOptions.BarHeight.get -> int
Fluent.Pdf.PdfBarcodeOptions.ErrorCorrection.get -> Fluent.Pdf.PdfBarcodeErrorCorrection
Fluent.Pdf.PdfBarcodeOptions.PdfBarcodeOptions(Fluent.Pdf.PdfBarcodeSymbology symbology = Fluent.Pdf.PdfBarcodeSymbology.QrCode, Fluent.Pdf.PdfBarcodeErrorCorrection errorCorrection = Fluent.Pdf.PdfBarcodeErrorCorrection.Medium, int qrVersion = 0, int quietZone = 0, int barHeight = 40) -> void
Fluent.Pdf.PdfBarcodeOptions.QrVersion.get -> int
Fluent.Pdf.PdfBarcodeOptions.QuietZone.get -> int
Fluent.Pdf.PdfBarcodeOptions.Symbology.get -> Fluent.Pdf.PdfBarcodeSymbology
Fluent.Pdf.PdfBarcodeSymbol
Fluent.Pdf.PdfBarcodeSymbol.Drawing.get -> Fluent.Pdf.PdfDrawing!
Fluent.Pdf.PdfBarcodeSymbol.Height.get -> int
Fluent.Pdf.PdfBarcodeSymbol.Mask.get -> int
Fluent.Pdf.PdfBarcodeSymbol.OpenVerificationPixels() -> System.IO.Stream!
Fluent.Pdf.PdfBarcodeSymbol.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfBarcodeSymbol.Options.get -> Fluent.Pdf.PdfBarcodeOptions!
Fluent.Pdf.PdfBarcodeSymbol.QrVersion.get -> int
Fluent.Pdf.PdfBarcodeSymbol.Text.get -> string!
Fluent.Pdf.PdfBarcodeSymbol.VerificationRaster.get -> Fluent.Pdf.PdfOcrRaster!
Fluent.Pdf.PdfBarcodeSymbol.VerifierIdentity.get -> string!
Fluent.Pdf.PdfBarcodeSymbol.Width.get -> int
Fluent.Pdf.PdfBarcodeSymbology
Fluent.Pdf.PdfBarcodeSymbology.Code128 = 1 -> Fluent.Pdf.PdfBarcodeSymbology
Fluent.Pdf.PdfBarcodeSymbology.QrCode = 0 -> Fluent.Pdf.PdfBarcodeSymbology
Fluent.Pdf.PdfBarcodes
Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.AL = 2 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.AN = 6 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.B = 10 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.BN = 9 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.CS = 7 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.EN = 3 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.ES = 4 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.ET = 5 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.FSI = 21 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.L = 0 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.LRE = 14 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.LRI = 19 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.LRO = 15 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.NSM = 8 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.ON = 13 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.PDF = 18 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.PDI = 22 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.R = 1 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.RLE = 16 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.RLI = 20 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.RLO = 17 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.S = 11 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiClass.WS = 12 -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfBidiParagraph
Fluent.Pdf.PdfBidiParagraph.EmbeddingLevel.get -> int
Fluent.Pdf.PdfBidiParagraph.FirstScalar.get -> int
Fluent.Pdf.PdfBidiParagraph.PdfBidiParagraph() -> void
Fluent.Pdf.PdfBidiParagraph.ScalarCount.get -> int
Fluent.Pdf.PdfBookmark
Fluent.Pdf.PdfBookmark.Action.get -> Fluent.Pdf.PdfAction?
Fluent.Pdf.PdfBookmark.Bold.get -> bool
Fluent.Pdf.PdfBookmark.Children.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfBookmark!>!
Fluent.Pdf.PdfBookmark.Color.get -> Fluent.Pdf.PdfColor?
Fluent.Pdf.PdfBookmark.Destination.get -> Fluent.Pdf.PdfDestination?
Fluent.Pdf.PdfBookmark.IsOpen.get -> bool
Fluent.Pdf.PdfBookmark.Italic.get -> bool
Fluent.Pdf.PdfBookmark.PdfBookmark(string! title, Fluent.Pdf.PdfDestination? destination = null, Fluent.Pdf.PdfAction? action = null, bool isOpen = true, bool bold = false, bool italic = false, Fluent.Pdf.PdfColor? color = null, params Fluent.Pdf.PdfBookmark![]! children) -> void
Fluent.Pdf.PdfBookmark.Title.get -> string!
Fluent.Pdf.PdfBookmark.WithChildren(params Fluent.Pdf.PdfBookmark![]! children) -> Fluent.Pdf.PdfBookmark!
Fluent.Pdf.PdfCalculationResult
Fluent.Pdf.PdfCalculationResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfCalculationResult.Value.get -> decimal
Fluent.Pdf.PdfCanvas
Fluent.Pdf.PdfCanvas.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfCanvas.Clip(Fluent.Pdf.PdfPath! path, bool evenOdd = false) -> void
Fluent.Pdf.PdfCanvas.DrawDrawing(Fluent.Pdf.PdfDrawing! drawing, Fluent.Pdf.PdfRect sourceBounds, Fluent.Pdf.PdfRect destination, Fluent.Pdf.PdfCanvasFit fit = Fluent.Pdf.PdfCanvasFit.Contain, Fluent.Pdf.PdfCanvasAnchor anchor = Fluent.Pdf.PdfCanvasAnchor.Center, int zIndex = 0) -> Fluent.Pdf.PdfCanvasPlacement!
Fluent.Pdf.PdfCanvas.DrawForm(Fluent.Pdf.PdfGraphicsResource! form, Fluent.Pdf.PdfRect destination, Fluent.Pdf.PdfCanvasFit fit = Fluent.Pdf.PdfCanvasFit.Contain, Fluent.Pdf.PdfCanvasAnchor anchor = Fluent.Pdf.PdfCanvasAnchor.Center, int zIndex = 0) -> Fluent.Pdf.PdfCanvasPlacement!
Fluent.Pdf.PdfCanvas.DrawImage(Fluent.Pdf.PdfGraphicsResource! image, Fluent.Pdf.PdfRect destination, Fluent.Pdf.PdfCanvasFit fit = Fluent.Pdf.PdfCanvasFit.Contain, Fluent.Pdf.PdfCanvasAnchor anchor = Fluent.Pdf.PdfCanvasAnchor.Center, int zIndex = 0) -> Fluent.Pdf.PdfCanvasPlacement!
Fluent.Pdf.PdfCanvas.DrawImageAsset(Fluent.Pdf.PdfImageAsset! image, Fluent.Pdf.PdfRect destination, Fluent.Pdf.PdfCanvasFit fit = Fluent.Pdf.PdfCanvasFit.Contain, Fluent.Pdf.PdfCanvasAnchor anchor = Fluent.Pdf.PdfCanvasAnchor.Center, int zIndex = 0) -> Fluent.Pdf.PdfCanvasPlacement!
Fluent.Pdf.PdfCanvas.DrawPath(Fluent.Pdf.PdfPath! path, Fluent.Pdf.PdfCanvasStyle? style = null, int zIndex = 0) -> Fluent.Pdf.PdfCanvasPlacement!
Fluent.Pdf.PdfCanvas.DrawText(Fluent.Pdf.PdfShapedText! text, Fluent.Pdf.PdfPoint baseline, double fontSizePoints, double lineHeight = 1.2, bool subset = true, int zIndex = 0) -> Fluent.Pdf.PdfCanvasPlacement!
Fluent.Pdf.PdfCanvas.Geometry.get -> Fluent.Pdf.PdfPageGeometry!
Fluent.Pdf.PdfCanvas.Options.get -> Fluent.Pdf.PdfCanvasOptions!
Fluent.Pdf.PdfCanvas.Rotate(double degrees) -> void
Fluent.Pdf.PdfCanvas.Save() -> System.IDisposable!
Fluent.Pdf.PdfCanvas.Scale(double x, double y) -> void
Fluent.Pdf.PdfCanvas.ToUser.get -> Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfCanvas.Transform(Fluent.Pdf.PdfMatrix matrix) -> void
Fluent.Pdf.PdfCanvas.Translate(double x, double y) -> void
Fluent.Pdf.PdfCanvasAnchor
Fluent.Pdf.PdfCanvasAnchor.Bottom = 7 -> Fluent.Pdf.PdfCanvasAnchor
Fluent.Pdf.PdfCanvasAnchor.BottomLeft = 6 -> Fluent.Pdf.PdfCanvasAnchor
Fluent.Pdf.PdfCanvasAnchor.BottomRight = 8 -> Fluent.Pdf.PdfCanvasAnchor
Fluent.Pdf.PdfCanvasAnchor.Center = 4 -> Fluent.Pdf.PdfCanvasAnchor
Fluent.Pdf.PdfCanvasAnchor.Left = 3 -> Fluent.Pdf.PdfCanvasAnchor
Fluent.Pdf.PdfCanvasAnchor.Right = 5 -> Fluent.Pdf.PdfCanvasAnchor
Fluent.Pdf.PdfCanvasAnchor.Top = 1 -> Fluent.Pdf.PdfCanvasAnchor
Fluent.Pdf.PdfCanvasAnchor.TopLeft = 0 -> Fluent.Pdf.PdfCanvasAnchor
Fluent.Pdf.PdfCanvasAnchor.TopRight = 2 -> Fluent.Pdf.PdfCanvasAnchor
Fluent.Pdf.PdfCanvasApplyMode
Fluent.Pdf.PdfCanvasApplyMode.Overlay = 0 -> Fluent.Pdf.PdfCanvasApplyMode
Fluent.Pdf.PdfCanvasApplyMode.Replace = 2 -> Fluent.Pdf.PdfCanvasApplyMode
Fluent.Pdf.PdfCanvasApplyMode.Underlay = 1 -> Fluent.Pdf.PdfCanvasApplyMode
Fluent.Pdf.PdfCanvasFit
Fluent.Pdf.PdfCanvasFit.Contain = 1 -> Fluent.Pdf.PdfCanvasFit
Fluent.Pdf.PdfCanvasFit.Cover = 2 -> Fluent.Pdf.PdfCanvasFit
Fluent.Pdf.PdfCanvasFit.Stretch = 0 -> Fluent.Pdf.PdfCanvasFit
Fluent.Pdf.PdfCanvasOptions
Fluent.Pdf.PdfCanvasOptions.Box.get -> Fluent.Pdf.PdfPageBox
Fluent.Pdf.PdfCanvasOptions.ClipToContent.get -> bool
Fluent.Pdf.PdfCanvasOptions.Dpi.get -> double
Fluent.Pdf.PdfCanvasOptions.Margins.get -> Fluent.Pdf.PdfMargins
Fluent.Pdf.PdfCanvasOptions.Origin.get -> Fluent.Pdf.PdfCanvasOrigin
Fluent.Pdf.PdfCanvasOptions.PdfCanvasOptions(Fluent.Pdf.PdfLengthUnit unit = Fluent.Pdf.PdfLengthUnit.Point, Fluent.Pdf.PdfCanvasOrigin origin = Fluent.Pdf.PdfCanvasOrigin.TopLeft, Fluent.Pdf.PdfPageBox box = Fluent.Pdf.PdfPageBox.Crop, Fluent.Pdf.PdfMargins margins = default(Fluent.Pdf.PdfMargins), bool clipToContent = true, double dpi = 96) -> void
Fluent.Pdf.PdfCanvasOptions.Unit.get -> Fluent.Pdf.PdfLengthUnit
Fluent.Pdf.PdfCanvasOrigin
Fluent.Pdf.PdfCanvasOrigin.BottomLeft = 1 -> Fluent.Pdf.PdfCanvasOrigin
Fluent.Pdf.PdfCanvasOrigin.TopLeft = 0 -> Fluent.Pdf.PdfCanvasOrigin
Fluent.Pdf.PdfCanvasPageLayer
Fluent.Pdf.PdfCanvasPageLayer.Content.get -> Fluent.Pdf.PdfContentProgram!
Fluent.Pdf.PdfCanvasPageLayer.Mode.get -> Fluent.Pdf.PdfCanvasApplyMode
Fluent.Pdf.PdfCanvasPageLayer.ResourceName.get -> string!
Fluent.Pdf.PdfCanvasPlacement
Fluent.Pdf.PdfCanvasPlacement.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfCanvasPlacement.HitTest(Fluent.Pdf.PdfPoint point, double tolerance = 1E-09, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> bool
Fluent.Pdf.PdfCanvasPlacement.Index.get -> int
Fluent.Pdf.PdfCanvasPlacement.LocalBounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfCanvasPlacement.Quad.get -> Fluent.Pdf.PdfQuad
Fluent.Pdf.PdfCanvasPlacement.Resource.get -> Fluent.Pdf.PdfGraphicsResource!
Fluent.Pdf.PdfCanvasPlacement.Transform.get -> Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfCanvasPlacement.UserBounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfCanvasPlacement.VisibleBounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfCanvasPlacement.ZIndex.get -> int
Fluent.Pdf.PdfCanvasResult
Fluent.Pdf.PdfCanvasResult.ApplyTo(Fluent.Pdf.PdfPage! page, Fluent.Pdf.PdfCanvasApplyMode mode = Fluent.Pdf.PdfCanvasApplyMode.Overlay, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfCanvasResult.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfCanvasResult.Drawing.get -> Fluent.Pdf.PdfDrawing!
Fluent.Pdf.PdfCanvasResult.Geometry.get -> Fluent.Pdf.PdfPageGeometry!
Fluent.Pdf.PdfCanvasResult.HitTest(Fluent.Pdf.PdfPoint point, bool topmostOnly = true, double tolerance = 1E-09, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfCanvasPlacement!>!
Fluent.Pdf.PdfCanvasResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfCanvasResult.Options.get -> Fluent.Pdf.PdfCanvasOptions!
Fluent.Pdf.PdfCanvasResult.Placements.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfCanvasPlacement!>!
Fluent.Pdf.PdfCanvasResult.ToUser.get -> Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfCanvasStyle
Fluent.Pdf.PdfCanvasStyle.Dash.get -> System.Collections.Generic.IReadOnlyList<double>!
Fluent.Pdf.PdfCanvasStyle.DashPhase.get -> double
Fluent.Pdf.PdfCanvasStyle.EvenOdd.get -> bool
Fluent.Pdf.PdfCanvasStyle.Fill.get -> Fluent.Pdf.PdfPaint?
Fluent.Pdf.PdfCanvasStyle.LineCap.get -> Fluent.Pdf.PdfLineCap
Fluent.Pdf.PdfCanvasStyle.LineJoin.get -> Fluent.Pdf.PdfLineJoin
Fluent.Pdf.PdfCanvasStyle.LineWidth.get -> double
Fluent.Pdf.PdfCanvasStyle.MiterLimit.get -> double
Fluent.Pdf.PdfCanvasStyle.PdfCanvasStyle(Fluent.Pdf.PdfPaint? fill = null, Fluent.Pdf.PdfPaint? stroke = null, double lineWidth = 1, Fluent.Pdf.PdfLineCap lineCap = Fluent.Pdf.PdfLineCap.Butt, Fluent.Pdf.PdfLineJoin lineJoin = Fluent.Pdf.PdfLineJoin.Miter, double miterLimit = 10, double[]? dash = null, double dashPhase = 0, bool evenOdd = false, Fluent.Pdf.PdfGraphicsState? state = null) -> void
Fluent.Pdf.PdfCanvasStyle.State.get -> Fluent.Pdf.PdfGraphicsState?
Fluent.Pdf.PdfCanvasStyle.Stroke.get -> Fluent.Pdf.PdfPaint?
Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.Barcodes = 19 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.CmsEnvelopes = 8 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.ConvertWord = 10 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.DecodeRasterDocuments = 18 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.Fonts = 6 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.ImageCodecs = 5 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.Pades = 14 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.PrintDocument = 17 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.ReadDocument = 0 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.RecognizeText = 4 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.RenderColorManaged = 16 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.RenderHtml = 3 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.RenderPages = 2 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.ShapeText = 7 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.SignatureTrust = 13 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.Signatures = 12 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.ValidateAccessibility = 11 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.ValidateArchival = 9 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.ValidatePades = 15 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCapability.WriteDocument = 1 -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfCertificateCredential
Fluent.Pdf.PdfCertificateCredential.OpenCertificate() -> System.IO.Stream!
Fluent.Pdf.PdfCertificateCredential.PdfCertificateCredential(System.Security.Cryptography.X509Certificates.X509Certificate2! certificate, System.Security.Cryptography.RSA! privateKey) -> void
Fluent.Pdf.PdfCertificateCredential.PrivateKey.get -> System.Security.Cryptography.RSA!
Fluent.Pdf.PdfCertificateProtection
Fluent.Pdf.PdfCertificateProtection.Cipher.get -> Fluent.Pdf.PdfPublicKeyCipher
Fluent.Pdf.PdfCertificateProtection.EncryptMetadata.get -> bool
Fluent.Pdf.PdfCertificateProtection.PdfCertificateProtection(Fluent.Pdf.PdfCertificateRecipient![]! recipients, Fluent.Pdf.PdfPublicKeyCipher cipher = Fluent.Pdf.PdfPublicKeyCipher.Aes256, bool encryptMetadata = true) -> void
Fluent.Pdf.PdfCertificateProtection.Recipients.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfCertificateRecipient!>!
Fluent.Pdf.PdfCertificateRecipient
Fluent.Pdf.PdfCertificateRecipient.KeyTransport.get -> Fluent.Pdf.PdfCmsKeyTransport
Fluent.Pdf.PdfCertificateRecipient.OpenCertificate() -> System.IO.Stream!
Fluent.Pdf.PdfCertificateRecipient.OwnerAuthority.get -> bool
Fluent.Pdf.PdfCertificateRecipient.PdfCertificateRecipient(System.Security.Cryptography.X509Certificates.X509Certificate2! certificate, Fluent.Pdf.PdfDocumentPermissions permissions = Fluent.Pdf.PdfDocumentPermissions.All, bool ownerAuthority = false, Fluent.Pdf.PdfCmsKeyTransport keyTransport = Fluent.Pdf.PdfCmsKeyTransport.OaepSha256) -> void
Fluent.Pdf.PdfCertificateRecipient.Permissions.get -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfCertificationPermission
Fluent.Pdf.PdfCertificationPermission.FormsAndSignatures = 2 -> Fluent.Pdf.PdfCertificationPermission
Fluent.Pdf.PdfCertificationPermission.FormsSignaturesAndAnnotations = 3 -> Fluent.Pdf.PdfCertificationPermission
Fluent.Pdf.PdfCertificationPermission.NoChanges = 1 -> Fluent.Pdf.PdfCertificationPermission
Fluent.Pdf.PdfCertificationPermission.None = 0 -> Fluent.Pdf.PdfCertificationPermission
Fluent.Pdf.PdfCmsKeyTransport
Fluent.Pdf.PdfCmsKeyTransport.LegacyPkcs1 = 1 -> Fluent.Pdf.PdfCmsKeyTransport
Fluent.Pdf.PdfCmsKeyTransport.OaepSha256 = 0 -> Fluent.Pdf.PdfCmsKeyTransport
Fluent.Pdf.PdfColor
Fluent.Pdf.PdfColor.ColorSpace.get -> Fluent.Pdf.PdfColorSpace!
Fluent.Pdf.PdfColor.Components.get -> System.Collections.Generic.IReadOnlyList<double>!
Fluent.Pdf.PdfColor.PdfColor(Fluent.Pdf.PdfColorSpace! colorSpace, params double[]! components) -> void
Fluent.Pdf.PdfColorRenderOptions
Fluent.Pdf.PdfColorRenderOptions.Intent.get -> Fluent.Pdf.PdfColorRenderingIntent
Fluent.Pdf.PdfColorRenderOptions.OutputProfile.get -> Fluent.Pdf.PdfColorSpace!
Fluent.Pdf.PdfColorRenderOptions.PdfColorRenderOptions(Fluent.Pdf.PdfColorSpace! outputProfile, Fluent.Pdf.PdfColorRenderingIntent intent = Fluent.Pdf.PdfColorRenderingIntent.RelativeColorimetric, bool simulateOverprint = false, Fluent.Pdf.PdfSpotColorPolicy spotColors = Fluent.Pdf.PdfSpotColorPolicy.Reject) -> void
Fluent.Pdf.PdfColorRenderOptions.SimulateOverprint.get -> bool
Fluent.Pdf.PdfColorRenderOptions.SpotColors.get -> Fluent.Pdf.PdfSpotColorPolicy
Fluent.Pdf.PdfColorRendering
Fluent.Pdf.PdfColorRenderingIntent
Fluent.Pdf.PdfColorRenderingIntent.AbsoluteColorimetric = 3 -> Fluent.Pdf.PdfColorRenderingIntent
Fluent.Pdf.PdfColorRenderingIntent.Perceptual = 0 -> Fluent.Pdf.PdfColorRenderingIntent
Fluent.Pdf.PdfColorRenderingIntent.RelativeColorimetric = 1 -> Fluent.Pdf.PdfColorRenderingIntent
Fluent.Pdf.PdfColorRenderingIntent.Saturation = 2 -> Fluent.Pdf.PdfColorRenderingIntent
Fluent.Pdf.PdfColorSpace
Fluent.Pdf.PdfColorSpace.AsResource(Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfGraphicsResource!
Fluent.Pdf.PdfColorSpace.Components.get -> int
Fluent.Pdf.PdfColorSpace.Definition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfColorSpace.IsPattern.get -> bool
Fluent.Pdf.PdfColorSpace.MinimumVersion.get -> string!
Fluent.Pdf.PdfColorSpace.OperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfColorSpace.Ranges.get -> System.Collections.Generic.IReadOnlyList<double>!
Fluent.Pdf.PdfColorTransformation
Fluent.Pdf.PdfColorTransformation.Color.get -> Fluent.Pdf.PdfColor!
Fluent.Pdf.PdfColorTransformation.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfComparison
Fluent.Pdf.PdfComparisonOptions
Fluent.Pdf.PdfComparisonOptions.Geometry.get -> bool
Fluent.Pdf.PdfComparisonOptions.GeometryTolerance.get -> double
Fluent.Pdf.PdfComparisonOptions.IgnoredInformationKeys.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfComparisonOptions.Metadata.get -> bool
Fluent.Pdf.PdfComparisonOptions.PdfComparisonOptions(bool text = true, bool geometry = true, bool metadata = true, bool visual = false, double geometryTolerance = 0.5, byte pixelTolerance = 0, string![]? ignoredInformationKeys = null, Fluent.Pdf.PdfRenderOptions? rendering = null) -> void
Fluent.Pdf.PdfComparisonOptions.PixelTolerance.get -> byte
Fluent.Pdf.PdfComparisonOptions.Rendering.get -> Fluent.Pdf.PdfRenderOptions!
Fluent.Pdf.PdfComparisonOptions.Text.get -> bool
Fluent.Pdf.PdfComparisonOptions.Visual.get -> bool
Fluent.Pdf.PdfComparisonResult
Fluent.Pdf.PdfComparisonResult.CreateAnnotatedDocument(Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfComparisonResult.Differences.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfDifference!>!
Fluent.Pdf.PdfComparisonResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfComparisonResult.Options.get -> Fluent.Pdf.PdfComparisonOptions!
Fluent.Pdf.PdfComparisonResult.WriteReport(System.IO.Stream! output, bool html = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfCompatibility
Fluent.Pdf.PdfCompatibilityInspection
Fluent.Pdf.PdfCompatibilityInspection.CatalogAssociatedFiles.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfCompatibilityInspection.EffectiveVersion.get -> string!
Fluent.Pdf.PdfCompatibilityInspection.Extensions.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfCompatibilityInspection.HeaderVersion.get -> string!
Fluent.Pdf.PdfCompatibilityInspection.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfCompatibilityInspection.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfCompatibilityInspection.StructureNamespaces.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfConformanceProfile
Fluent.Pdf.PdfConformanceProfile.E12008 = 5 -> Fluent.Pdf.PdfConformanceProfile
Fluent.Pdf.PdfConformanceProfile.X1A2001 = 0 -> Fluent.Pdf.PdfConformanceProfile
Fluent.Pdf.PdfConformanceProfile.X1A2003 = 1 -> Fluent.Pdf.PdfConformanceProfile
Fluent.Pdf.PdfConformanceProfile.X32002 = 2 -> Fluent.Pdf.PdfConformanceProfile
Fluent.Pdf.PdfConformanceProfile.X32003 = 3 -> Fluent.Pdf.PdfConformanceProfile
Fluent.Pdf.PdfConformanceProfile.X42010 = 4 -> Fluent.Pdf.PdfConformanceProfile
Fluent.Pdf.PdfContentKind
Fluent.Pdf.PdfContentKind.Page = 0 -> Fluent.Pdf.PdfContentKind
Fluent.Pdf.PdfContentKind.Type3Glyph = 1 -> Fluent.Pdf.PdfContentKind
Fluent.Pdf.PdfContentOperation
Fluent.Pdf.PdfContentOperation.InlineImageDictionary.get -> Fluent.Pdf.PdfValue?
Fluent.Pdf.PdfContentOperation.InlineImageLength.get -> long
Fluent.Pdf.PdfContentOperation.MinimumVersion.get -> string!
Fluent.Pdf.PdfContentOperation.OpenInlineImageData() -> System.IO.Stream!
Fluent.Pdf.PdfContentOperation.Operands.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfValue!>!
Fluent.Pdf.PdfContentOperation.Operation.get -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfContentOperation.PdfContentOperation(Fluent.Pdf.PdfOperator operation, params Fluent.Pdf.PdfValue![]! operands) -> void
Fluent.Pdf.PdfContentOperation.Token.get -> string!
Fluent.Pdf.PdfContentProgram
Fluent.Pdf.PdfContentProgram.Edit(int index, int removeCount, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfContentOperation!>! replacement, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfContentProgram!
Fluent.Pdf.PdfContentProgram.Kind.get -> Fluent.Pdf.PdfContentKind
Fluent.Pdf.PdfContentProgram.MinimumVersion.get -> string!
Fluent.Pdf.PdfContentProgram.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfContentProgram.Operations.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfContentOperation!>!
Fluent.Pdf.PdfContentProgram.Write(System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfContentsEntry
Fluent.Pdf.PdfContentsEntry.DestinationId.get -> string!
Fluent.Pdf.PdfContentsEntry.PageIndex.get -> int
Fluent.Pdf.PdfContentsEntry.Point.get -> Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfContentsEntry.Title.get -> string!
Fluent.Pdf.PdfCrossReference
Fluent.Pdf.PdfCrossReference.ContainerObjectNumber.get -> int
Fluent.Pdf.PdfCrossReference.Id.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfCrossReference.Kind.get -> Fluent.Pdf.PdfCrossReferenceKind
Fluent.Pdf.PdfCrossReference.MemberIndex.get -> int
Fluent.Pdf.PdfCrossReference.Offset.get -> long
Fluent.Pdf.PdfCrossReferenceFormat
Fluent.Pdf.PdfCrossReferenceFormat.Auto = 0 -> Fluent.Pdf.PdfCrossReferenceFormat
Fluent.Pdf.PdfCrossReferenceFormat.Stream = 2 -> Fluent.Pdf.PdfCrossReferenceFormat
Fluent.Pdf.PdfCrossReferenceFormat.Table = 1 -> Fluent.Pdf.PdfCrossReferenceFormat
Fluent.Pdf.PdfCrossReferenceKind
Fluent.Pdf.PdfCrossReferenceKind.Compressed = 2 -> Fluent.Pdf.PdfCrossReferenceKind
Fluent.Pdf.PdfCrossReferenceKind.Free = 0 -> Fluent.Pdf.PdfCrossReferenceKind
Fluent.Pdf.PdfCrossReferenceKind.Offset = 1 -> Fluent.Pdf.PdfCrossReferenceKind
Fluent.Pdf.PdfDataGeneration
Fluent.Pdf.PdfDecodedData
Fluent.Pdf.PdfDecodedData.Length.get -> long
Fluent.Pdf.PdfDecodedData.OpenRead() -> System.IO.Stream!
Fluent.Pdf.PdfDecodedData.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfDecodedText
Fluent.Pdf.PdfDecodedText.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfDecodedText.Text.get -> string!
Fluent.Pdf.PdfDestination
Fluent.Pdf.PdfDestination.Coordinates.get -> System.Collections.Generic.IReadOnlyList<double?>!
Fluent.Pdf.PdfDestination.PageIndex.get -> int
Fluent.Pdf.PdfDestination.PdfDestination(int pageIndex, Fluent.Pdf.PdfDestinationView view = Fluent.Pdf.PdfDestinationView.Fit, params double?[]! coordinates) -> void
Fluent.Pdf.PdfDestination.View.get -> Fluent.Pdf.PdfDestinationView
Fluent.Pdf.PdfDestinationView
Fluent.Pdf.PdfDestinationView.Fit = 1 -> Fluent.Pdf.PdfDestinationView
Fluent.Pdf.PdfDestinationView.FitB = 5 -> Fluent.Pdf.PdfDestinationView
Fluent.Pdf.PdfDestinationView.FitBH = 6 -> Fluent.Pdf.PdfDestinationView
Fluent.Pdf.PdfDestinationView.FitBV = 7 -> Fluent.Pdf.PdfDestinationView
Fluent.Pdf.PdfDestinationView.FitH = 2 -> Fluent.Pdf.PdfDestinationView
Fluent.Pdf.PdfDestinationView.FitR = 4 -> Fluent.Pdf.PdfDestinationView
Fluent.Pdf.PdfDestinationView.FitV = 3 -> Fluent.Pdf.PdfDestinationView
Fluent.Pdf.PdfDestinationView.XYZ = 0 -> Fluent.Pdf.PdfDestinationView
Fluent.Pdf.PdfDiagnostic
Fluent.Pdf.PdfDiagnostic.Capability.get -> Fluent.Pdf.PdfCapability?
Fluent.Pdf.PdfDiagnostic.Code.get -> string!
Fluent.Pdf.PdfDiagnostic.LossCategory.get -> Fluent.Pdf.PdfLossCategory
Fluent.Pdf.PdfDiagnostic.Message.get -> string!
Fluent.Pdf.PdfDiagnostic.ObjectId.get -> long?
Fluent.Pdf.PdfDiagnostic.Operation.get -> string?
Fluent.Pdf.PdfDiagnostic.PageIndex.get -> int?
Fluent.Pdf.PdfDiagnostic.PdfDiagnostic(string! code, string! message, Fluent.Pdf.PdfCapability? capability = null, string? providerId = null, Fluent.Pdf.PdfDiagnosticSeverity severity = Fluent.Pdf.PdfDiagnosticSeverity.Error, Fluent.Pdf.PdfLossCategory lossCategory = Fluent.Pdf.PdfLossCategory.None, string? operation = null, int? pageIndex = null, long? objectId = null, string? remediation = null) -> void
Fluent.Pdf.PdfDiagnostic.ProviderId.get -> string?
Fluent.Pdf.PdfDiagnostic.Remediation.get -> string?
Fluent.Pdf.PdfDiagnostic.Severity.get -> Fluent.Pdf.PdfDiagnosticSeverity
Fluent.Pdf.PdfDiagnosticSeverity
Fluent.Pdf.PdfDiagnosticSeverity.Error = 2 -> Fluent.Pdf.PdfDiagnosticSeverity
Fluent.Pdf.PdfDiagnosticSeverity.Information = 0 -> Fluent.Pdf.PdfDiagnosticSeverity
Fluent.Pdf.PdfDiagnosticSeverity.Warning = 1 -> Fluent.Pdf.PdfDiagnosticSeverity
Fluent.Pdf.PdfDifference
Fluent.Pdf.PdfDifference.After.get -> string!
Fluent.Pdf.PdfDifference.AfterObject.get -> Fluent.Pdf.PdfObjectId?
Fluent.Pdf.PdfDifference.Before.get -> string!
Fluent.Pdf.PdfDifference.BeforeObject.get -> Fluent.Pdf.PdfObjectId?
Fluent.Pdf.PdfDifference.Kind.get -> Fluent.Pdf.PdfDifferenceKind
Fluent.Pdf.PdfDifference.PageIndex.get -> int?
Fluent.Pdf.PdfDifferenceKind
Fluent.Pdf.PdfDifferenceKind.Geometry = 2 -> Fluent.Pdf.PdfDifferenceKind
Fluent.Pdf.PdfDifferenceKind.Metadata = 3 -> Fluent.Pdf.PdfDifferenceKind
Fluent.Pdf.PdfDifferenceKind.Page = 0 -> Fluent.Pdf.PdfDifferenceKind
Fluent.Pdf.PdfDifferenceKind.Text = 1 -> Fluent.Pdf.PdfDifferenceKind
Fluent.Pdf.PdfDifferenceKind.Visual = 4 -> Fluent.Pdf.PdfDifferenceKind
Fluent.Pdf.PdfDocument
Fluent.Pdf.PdfDocument.AddPage() -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfDocument.AddPage(Fluent.Pdf.PdfPageGeometry! geometry) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfDocument.AddPage(Fluent.Pdf.PdfPageSize size) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfDocument.CreateSnapshot() -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfDocument.Dispose() -> void
Fluent.Pdf.PdfDocument.IsDisposed.get -> bool
Fluent.Pdf.PdfDocument.LastOperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfDocument.Options.get -> Fluent.Pdf.PdfOptions!
Fluent.Pdf.PdfDocument.Pages.get -> Fluent.Pdf.PdfPageCollection!
Fluent.Pdf.PdfDocument.Providers.get -> Fluent.Pdf.PdfProviderRegistry!
Fluent.Pdf.PdfDocument.Save(System.IO.Stream! output, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), Fluent.Pdf.PdfWriteOptions? writeOptions = null) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfDocument.SaveFile(string! path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), Fluent.Pdf.PdfWriteOptions? writeOptions = null) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfDocument.SourceDocument.get -> Fluent.Pdf.PdfSyntaxDocument?
Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfDocumentPermissions.All = Fluent.Pdf.PdfDocumentPermissions.Print | Fluent.Pdf.PdfDocumentPermissions.Modify | Fluent.Pdf.PdfDocumentPermissions.Extract | Fluent.Pdf.PdfDocumentPermissions.Annotate | Fluent.Pdf.PdfDocumentPermissions.FillForms | Fluent.Pdf.PdfDocumentPermissions.ExtractForAccessibility | Fluent.Pdf.PdfDocumentPermissions.Assemble | Fluent.Pdf.PdfDocumentPermissions.PrintHighQuality -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfDocumentPermissions.Annotate = 32 -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfDocumentPermissions.Assemble = 1024 -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfDocumentPermissions.Extract = 16 -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfDocumentPermissions.ExtractForAccessibility = 512 -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfDocumentPermissions.FillForms = 256 -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfDocumentPermissions.Modify = 8 -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfDocumentPermissions.None = 0 -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfDocumentPermissions.Print = 4 -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfDocumentPermissions.PrintHighQuality = 2048 -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfDocumentSnapshot
Fluent.Pdf.PdfDocumentSnapshot.Options.get -> Fluent.Pdf.PdfOptions!
Fluent.Pdf.PdfDocumentSnapshot.PageNumbering.get -> Fluent.Pdf.PdfPageNumbering?
Fluent.Pdf.PdfDocumentSnapshot.Pages.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfPageSnapshot>!
Fluent.Pdf.PdfDocumentSnapshot.SourceDocument.get -> Fluent.Pdf.PdfSyntaxDocument?
Fluent.Pdf.PdfDocumentSnapshot.WriteOptions.get -> Fluent.Pdf.PdfWriteOptions!
Fluent.Pdf.PdfDrawing
Fluent.Pdf.PdfDrawing.Content.get -> Fluent.Pdf.PdfContentProgram!
Fluent.Pdf.PdfDrawing.MinimumVersion.get -> string!
Fluent.Pdf.PdfDrawing.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfDrawing.Resources.get -> Fluent.Pdf.PdfResourceSet!
Fluent.Pdf.PdfEngineering
Fluent.Pdf.PdfEngineeringContentKind
Fluent.Pdf.PdfEngineeringContentKind.EmbeddedFile = 6 -> Fluent.Pdf.PdfEngineeringContentKind
Fluent.Pdf.PdfEngineeringContentKind.Geospatial = 5 -> Fluent.Pdf.PdfEngineeringContentKind
Fluent.Pdf.PdfEngineeringContentKind.Other3D = 2 -> Fluent.Pdf.PdfEngineeringContentKind
Fluent.Pdf.PdfEngineeringContentKind.Prc = 1 -> Fluent.Pdf.PdfEngineeringContentKind
Fluent.Pdf.PdfEngineeringContentKind.RichMedia = 4 -> Fluent.Pdf.PdfEngineeringContentKind
Fluent.Pdf.PdfEngineeringContentKind.ThreeDAnnotation = 3 -> Fluent.Pdf.PdfEngineeringContentKind
Fluent.Pdf.PdfEngineeringContentKind.U3D = 0 -> Fluent.Pdf.PdfEngineeringContentKind
Fluent.Pdf.PdfEngineeringInspection
Fluent.Pdf.PdfEngineeringInspection.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfEngineeringInspection.Records.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfEngineeringRecord!>!
Fluent.Pdf.PdfEngineeringPreservation
Fluent.Pdf.PdfEngineeringPreservation.Length.get -> long
Fluent.Pdf.PdfEngineeringPreservation.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfEngineeringPreservation.Sha256.get -> string!
Fluent.Pdf.PdfEngineeringRecord
Fluent.Pdf.PdfEngineeringRecord.Definition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfEngineeringRecord.EncodedSha256.get -> string?
Fluent.Pdf.PdfEngineeringRecord.Kind.get -> Fluent.Pdf.PdfEngineeringContentKind
Fluent.Pdf.PdfEngineeringRecord.Location.get -> string!
Fluent.Pdf.PdfException
Fluent.Pdf.PdfException.Diagnostic.get -> Fluent.Pdf.PdfDiagnostic!
Fluent.Pdf.PdfException.PdfException(Fluent.Pdf.PdfDiagnostic! diagnostic, System.Exception? innerException = null) -> void
Fluent.Pdf.PdfExtensionRequirement
Fluent.Pdf.PdfExtensionRequirement.BaseVersion.get -> string!
Fluent.Pdf.PdfExtensionRequirement.DocumentationUrl.get -> string?
Fluent.Pdf.PdfExtensionRequirement.ExtensionLevel.get -> int
Fluent.Pdf.PdfExtensionRequirement.PdfExtensionRequirement(string! prefix, string! baseVersion, int extensionLevel) -> void
Fluent.Pdf.PdfExtensionRequirement.PdfExtensionRequirement(string! prefix, string! baseVersion, int extensionLevel, string! documentationUrl, string? revision = null) -> void
Fluent.Pdf.PdfExtensionRequirement.Prefix.get -> string!
Fluent.Pdf.PdfExtensionRequirement.Revision.get -> string?
Fluent.Pdf.PdfExtractedArtifact
Fluent.Pdf.PdfExtractedArtifact.EndOperationIndex.get -> int
Fluent.Pdf.PdfExtractedArtifact.OccurrenceIndices.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfExtractedArtifact.PathIndices.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfExtractedArtifact.Properties.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfExtractedArtifact.Source.get -> Fluent.Pdf.PdfResourceSource!
Fluent.Pdf.PdfExtractedCharacter
Fluent.Pdf.PdfExtractedCharacter.BaselineEnd.get -> Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfExtractedCharacter.BaselineStart.get -> Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfExtractedCharacter.Code.get -> uint
Fluent.Pdf.PdfExtractedCharacter.CodeLength.get -> int
Fluent.Pdf.PdfExtractedCharacter.Confidence.get -> Fluent.Pdf.PdfTextMappingConfidence
Fluent.Pdf.PdfExtractedCharacter.FontName.get -> string!
Fluent.Pdf.PdfExtractedCharacter.FontSize.get -> double
Fluent.Pdf.PdfExtractedCharacter.GeometryEstimated.get -> bool
Fluent.Pdf.PdfExtractedCharacter.Index.get -> int
Fluent.Pdf.PdfExtractedCharacter.LayerVisible.get -> bool
Fluent.Pdf.PdfExtractedCharacter.OperationIndex.get -> int
Fluent.Pdf.PdfExtractedCharacter.Quad.get -> Fluent.Pdf.PdfQuad
Fluent.Pdf.PdfExtractedCharacter.RenderingMode.get -> int
Fluent.Pdf.PdfExtractedCharacter.Text.get -> string!
Fluent.Pdf.PdfExtractedCharacter.UserQuad.get -> Fluent.Pdf.PdfQuad
Fluent.Pdf.PdfExtractedLine
Fluent.Pdf.PdfExtractedLine.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfExtractedLine.Words.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedWord!>!
Fluent.Pdf.PdfExtractedParagraph
Fluent.Pdf.PdfExtractedParagraph.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfExtractedParagraph.Lines.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedLine!>!
Fluent.Pdf.PdfExtractedPath
Fluent.Pdf.PdfExtractedPath.Elements.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedPathElement!>!
Fluent.Pdf.PdfExtractedPath.EndOperation.get -> Fluent.Pdf.PdfContentOperation!
Fluent.Pdf.PdfExtractedPath.EvenOddClip.get -> bool
Fluent.Pdf.PdfExtractedPath.IsArtifact.get -> bool
Fluent.Pdf.PdfExtractedPath.SetsClip.get -> bool
Fluent.Pdf.PdfExtractedPath.Source.get -> Fluent.Pdf.PdfResourceSource!
Fluent.Pdf.PdfExtractedPathElement
Fluent.Pdf.PdfExtractedPathElement.Operation.get -> Fluent.Pdf.PdfContentOperation!
Fluent.Pdf.PdfExtractedPathElement.Source.get -> Fluent.Pdf.PdfResourceSource!
Fluent.Pdf.PdfExtractedPathElement.Transform.get -> Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfExtractedResource
Fluent.Pdf.PdfExtractedResource.DeclaredSubtype.get -> string!
Fluent.Pdf.PdfExtractedResource.Definition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfExtractedResource.FontProgramIndices.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfExtractedResource.HasStream.get -> bool
Fluent.Pdf.PdfExtractedResource.Index.get -> int
Fluent.Pdf.PdfExtractedResource.Kind.get -> Fluent.Pdf.PdfExtractedResourceKind
Fluent.Pdf.PdfExtractedResource.ObjectId.get -> Fluent.Pdf.PdfObjectId?
Fluent.Pdf.PdfExtractedResource.ReadFontProgram(Fluent.Pdf.PdfProviderRegistry! providers, int faceIndex = 0, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFont!
Fluent.Pdf.PdfExtractedResource.WriteFile(string! path, bool decoded = false, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfExtractedResource.WriteStream(System.IO.Stream! output, bool decoded = false, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfExtractedResourceKind
Fluent.Pdf.PdfExtractedResourceKind.ColorSpace = 7 -> Fluent.Pdf.PdfExtractedResourceKind
Fluent.Pdf.PdfExtractedResourceKind.Font = 3 -> Fluent.Pdf.PdfExtractedResourceKind
Fluent.Pdf.PdfExtractedResourceKind.FontProgram = 4 -> Fluent.Pdf.PdfExtractedResourceKind
Fluent.Pdf.PdfExtractedResourceKind.Form = 2 -> Fluent.Pdf.PdfExtractedResourceKind
Fluent.Pdf.PdfExtractedResourceKind.GraphicsState = 8 -> Fluent.Pdf.PdfExtractedResourceKind
Fluent.Pdf.PdfExtractedResourceKind.Image = 1 -> Fluent.Pdf.PdfExtractedResourceKind
Fluent.Pdf.PdfExtractedResourceKind.Pattern = 6 -> Fluent.Pdf.PdfExtractedResourceKind
Fluent.Pdf.PdfExtractedResourceKind.Properties = 9 -> Fluent.Pdf.PdfExtractedResourceKind
Fluent.Pdf.PdfExtractedResourceKind.Shading = 5 -> Fluent.Pdf.PdfExtractedResourceKind
Fluent.Pdf.PdfExtractedResourceKind.Unknown = 0 -> Fluent.Pdf.PdfExtractedResourceKind
Fluent.Pdf.PdfExtractedSpan
Fluent.Pdf.PdfExtractedSpan.Bounds.get -> Fluent.Pdf.PdfRect?
Fluent.Pdf.PdfExtractedSpan.CharacterCount.get -> int
Fluent.Pdf.PdfExtractedSpan.FirstCharacter.get -> int
Fluent.Pdf.PdfExtractedSpan.Text.get -> string!
Fluent.Pdf.PdfExtractedTable
Fluent.Pdf.PdfExtractedTable.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfExtractedTable.Cells.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedTableCell!>!
Fluent.Pdf.PdfExtractedTable.ColumnBoundaries.get -> System.Collections.Generic.IReadOnlyList<double>!
Fluent.Pdf.PdfExtractedTable.ColumnCount.get -> int
Fluent.Pdf.PdfExtractedTable.Confidence.get -> double
Fluent.Pdf.PdfExtractedTable.Mode.get -> Fluent.Pdf.PdfTableDetectionMode
Fluent.Pdf.PdfExtractedTable.PageIndex.get -> int
Fluent.Pdf.PdfExtractedTable.RowCount.get -> int
Fluent.Pdf.PdfExtractedTableCell
Fluent.Pdf.PdfExtractedTableCell.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfExtractedTableCell.Characters.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedCharacter!>!
Fluent.Pdf.PdfExtractedTableCell.Column.get -> int
Fluent.Pdf.PdfExtractedTableCell.ColumnSpan.get -> int
Fluent.Pdf.PdfExtractedTableCell.Confidence.get -> double
Fluent.Pdf.PdfExtractedTableCell.Number.get -> decimal?
Fluent.Pdf.PdfExtractedTableCell.PageIndex.get -> int
Fluent.Pdf.PdfExtractedTableCell.Row.get -> int
Fluent.Pdf.PdfExtractedTableCell.RowSpan.get -> int
Fluent.Pdf.PdfExtractedTableCell.RuleSources.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfResourceSource!>!
Fluent.Pdf.PdfExtractedTableCell.Text.get -> string!
Fluent.Pdf.PdfExtractedTableSequence
Fluent.Pdf.PdfExtractedTableSequence.RepeatedHeaderRows.get -> int
Fluent.Pdf.PdfExtractedTableSequence.TableIndices.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfExtractedWord
Fluent.Pdf.PdfExtractedWord.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfExtractedWord.Characters.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedCharacter!>!
Fluent.Pdf.PdfExtractedWord.Index.get -> int
Fluent.Pdf.PdfExtractedWord.Text.get -> string!
Fluent.Pdf.PdfFieldCollisionPolicy
Fluent.Pdf.PdfFieldCollisionPolicy.Reject = 0 -> Fluent.Pdf.PdfFieldCollisionPolicy
Fluent.Pdf.PdfFieldCollisionPolicy.Rename = 1 -> Fluent.Pdf.PdfFieldCollisionPolicy
Fluent.Pdf.PdfFilter
Fluent.Pdf.PdfFilter.Name.get -> string!
Fluent.Pdf.PdfFilter.Parameters.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfFilter.PdfFilter(string! name, Fluent.Pdf.PdfValue? parameters = null) -> void
Fluent.Pdf.PdfFilters
Fluent.Pdf.PdfFlow
Fluent.Pdf.PdfFlowAlignment
Fluent.Pdf.PdfFlowAlignment.Center = 2 -> Fluent.Pdf.PdfFlowAlignment
Fluent.Pdf.PdfFlowAlignment.End = 1 -> Fluent.Pdf.PdfFlowAlignment
Fluent.Pdf.PdfFlowAlignment.Justify = 3 -> Fluent.Pdf.PdfFlowAlignment
Fluent.Pdf.PdfFlowAlignment.Start = 0 -> Fluent.Pdf.PdfFlowAlignment
Fluent.Pdf.PdfFlowBlock
Fluent.Pdf.PdfFlowClear
Fluent.Pdf.PdfFlowClear.Both = 3 -> Fluent.Pdf.PdfFlowClear
Fluent.Pdf.PdfFlowClear.Left = 1 -> Fluent.Pdf.PdfFlowClear
Fluent.Pdf.PdfFlowClear.None = 0 -> Fluent.Pdf.PdfFlowClear
Fluent.Pdf.PdfFlowClear.Right = 2 -> Fluent.Pdf.PdfFlowClear
Fluent.Pdf.PdfFlowColumnOrder
Fluent.Pdf.PdfFlowColumnOrder.LeftToRight = 0 -> Fluent.Pdf.PdfFlowColumnOrder
Fluent.Pdf.PdfFlowColumnOrder.RightToLeft = 1 -> Fluent.Pdf.PdfFlowColumnOrder
Fluent.Pdf.PdfFlowColumnPlacement
Fluent.Pdf.PdfFlowColumnPlacement.BlockIndex.get -> int
Fluent.Pdf.PdfFlowColumnPlacement.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfFlowColumnPlacement.LogicalColumnIndex.get -> int
Fluent.Pdf.PdfFlowColumnPlacement.PhysicalColumnIndex.get -> int
Fluent.Pdf.PdfFlowColumnPlacement.Source.get -> Fluent.Pdf.PdfFlowPage!
Fluent.Pdf.PdfFlowColumns
Fluent.Pdf.PdfFlowColumns.Content.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowBlock!>!
Fluent.Pdf.PdfFlowColumns.Count.get -> int
Fluent.Pdf.PdfFlowColumns.Gap.get -> double
Fluent.Pdf.PdfFlowColumns.Height.get -> double?
Fluent.Pdf.PdfFlowColumns.Order.get -> Fluent.Pdf.PdfFlowColumnOrder
Fluent.Pdf.PdfFlowColumns.PdfFlowColumns(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowBlock!>! content, int count = 2, double gap = 18, double? height = null, Fluent.Pdf.PdfFlowColumnOrder order = Fluent.Pdf.PdfFlowColumnOrder.LeftToRight) -> void
Fluent.Pdf.PdfFlowComponent
Fluent.Pdf.PdfFlowComponent.Content.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowBlock!>!
Fluent.Pdf.PdfFlowComponent.KeepTogether.get -> bool
Fluent.Pdf.PdfFlowComponent.PdfFlowComponent(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowBlock!>! content, bool keepTogether = false) -> void
Fluent.Pdf.PdfFlowContents
Fluent.Pdf.PdfFlowContents.LevelIndent.get -> double
Fluent.Pdf.PdfFlowContents.MaximumLevel.get -> int
Fluent.Pdf.PdfFlowContents.MinimumLevel.get -> int
Fluent.Pdf.PdfFlowContents.PageColumnWidth.get -> double
Fluent.Pdf.PdfFlowContents.PdfFlowContents(Fluent.Pdf.PdfFlowTextStyle! textStyle, int minimumLevel = 1, int maximumLevel = 6, double pageColumnWidth = 48, double levelIndent = 12) -> void
Fluent.Pdf.PdfFlowContents.TextStyle.get -> Fluent.Pdf.PdfFlowTextStyle!
Fluent.Pdf.PdfFlowFloat
Fluent.Pdf.PdfFlowFloat.Clear.get -> Fluent.Pdf.PdfFlowClear
Fluent.Pdf.PdfFlowFloat.Drawing.get -> Fluent.Pdf.PdfDrawing!
Fluent.Pdf.PdfFlowFloat.Gap.get -> double
Fluent.Pdf.PdfFlowFloat.Height.get -> double
Fluent.Pdf.PdfFlowFloat.PdfFlowFloat(Fluent.Pdf.PdfDrawing! drawing, Fluent.Pdf.PdfRect sourceBounds, double width, double height, Fluent.Pdf.PdfFlowFloatSide side = Fluent.Pdf.PdfFlowFloatSide.Left, double gap = 6, Fluent.Pdf.PdfFlowClear clear = Fluent.Pdf.PdfFlowClear.None) -> void
Fluent.Pdf.PdfFlowFloat.Side.get -> Fluent.Pdf.PdfFlowFloatSide
Fluent.Pdf.PdfFlowFloat.SourceBounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfFlowFloat.Width.get -> double
Fluent.Pdf.PdfFlowFloatPlacement
Fluent.Pdf.PdfFlowFloatPlacement.BlockIndex.get -> int
Fluent.Pdf.PdfFlowFloatPlacement.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfFlowFloatPlacement.Float.get -> Fluent.Pdf.PdfFlowFloat!
Fluent.Pdf.PdfFlowFloatSide
Fluent.Pdf.PdfFlowFloatSide.Left = 0 -> Fluent.Pdf.PdfFlowFloatSide
Fluent.Pdf.PdfFlowFloatSide.Right = 1 -> Fluent.Pdf.PdfFlowFloatSide
Fluent.Pdf.PdfFlowHeading
Fluent.Pdf.PdfFlowHeading.Id.get -> string!
Fluent.Pdf.PdfFlowHeading.Level.get -> int
Fluent.Pdf.PdfFlowHeading.Paragraph.get -> Fluent.Pdf.PdfFlowParagraph!
Fluent.Pdf.PdfFlowHeading.PdfFlowHeading(string! id, string! title, Fluent.Pdf.PdfFlowTextStyle! textStyle, int level = 1, Fluent.Pdf.PdfFlowParagraphStyle? paragraphStyle = null) -> void
Fluent.Pdf.PdfFlowHeading.Title.get -> string!
Fluent.Pdf.PdfFlowHeadingPlacement
Fluent.Pdf.PdfFlowHeadingPlacement.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfFlowHeadingPlacement.Heading.get -> Fluent.Pdf.PdfFlowHeading!
Fluent.Pdf.PdfFlowHeadingPlacement.PageIndex.get -> int
Fluent.Pdf.PdfFlowIndex
Fluent.Pdf.PdfFlowIndex.PageColumnWidth.get -> double
Fluent.Pdf.PdfFlowIndex.PdfFlowIndex(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowIndexTerm!>! terms, Fluent.Pdf.PdfFlowTextStyle! textStyle, System.Globalization.CultureInfo! culture, double pageColumnWidth = 48) -> void
Fluent.Pdf.PdfFlowIndex.Terms.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowIndexTerm!>!
Fluent.Pdf.PdfFlowIndex.TextStyle.get -> Fluent.Pdf.PdfFlowTextStyle!
Fluent.Pdf.PdfFlowIndexTerm
Fluent.Pdf.PdfFlowIndexTerm.DestinationIds.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfFlowIndexTerm.PdfFlowIndexTerm(string! text, params string![]! destinationIds) -> void
Fluent.Pdf.PdfFlowIndexTerm.Text.get -> string!
Fluent.Pdf.PdfFlowLine
Fluent.Pdf.PdfFlowLine.Advance.get -> double
Fluent.Pdf.PdfFlowLine.Baseline.get -> double
Fluent.Pdf.PdfFlowLine.BlockIndex.get -> int
Fluent.Pdf.PdfFlowLine.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfFlowLine.EndsHardBreak.get -> bool
Fluent.Pdf.PdfFlowLine.IsClipped.get -> bool
Fluent.Pdf.PdfFlowLine.ShapedText.get -> Fluent.Pdf.PdfShapedText!
Fluent.Pdf.PdfFlowLine.Text.get -> string!
Fluent.Pdf.PdfFlowLine.Utf16Length.get -> int
Fluent.Pdf.PdfFlowLine.Utf16Offset.get -> int
Fluent.Pdf.PdfFlowList
Fluent.Pdf.PdfFlowList.Counters.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfFlowList.Gap.get -> double
Fluent.Pdf.PdfFlowList.Items.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowListItem!>!
Fluent.Pdf.PdfFlowList.MarkerStyle.get -> Fluent.Pdf.PdfFlowTextStyle!
Fluent.Pdf.PdfFlowList.MarkerWidth.get -> double?
Fluent.Pdf.PdfFlowList.Markers.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfFlowList.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfFlowListItem
Fluent.Pdf.PdfFlowListItem.Content.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowBlock!>!
Fluent.Pdf.PdfFlowListItem.CounterValue.get -> int?
Fluent.Pdf.PdfFlowListItem.PdfFlowListItem(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowBlock!>! content, int? counterValue = null) -> void
Fluent.Pdf.PdfFlowListKind
Fluent.Pdf.PdfFlowListKind.Bullet = 0 -> Fluent.Pdf.PdfFlowListKind
Fluent.Pdf.PdfFlowListKind.Decimal = 1 -> Fluent.Pdf.PdfFlowListKind
Fluent.Pdf.PdfFlowListKind.LowerAlpha = 2 -> Fluent.Pdf.PdfFlowListKind
Fluent.Pdf.PdfFlowListKind.LowerRoman = 4 -> Fluent.Pdf.PdfFlowListKind
Fluent.Pdf.PdfFlowListKind.UpperAlpha = 3 -> Fluent.Pdf.PdfFlowListKind
Fluent.Pdf.PdfFlowListKind.UpperRoman = 5 -> Fluent.Pdf.PdfFlowListKind
Fluent.Pdf.PdfFlowNote
Fluent.Pdf.PdfFlowNote.Content.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowBlock!>!
Fluent.Pdf.PdfFlowNote.Id.get -> string!
Fluent.Pdf.PdfFlowNote.Label.get -> string!
Fluent.Pdf.PdfFlowNote.MarkerStyle.get -> Fluent.Pdf.PdfFlowTextStyle!
Fluent.Pdf.PdfFlowNote.PdfFlowNote(string! id, string! label, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowBlock!>! content, Fluent.Pdf.PdfFlowTextStyle! markerStyle) -> void
Fluent.Pdf.PdfFlowNotePlacement
Fluent.Pdf.PdfFlowNotePlacement.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfFlowNotePlacement.IsEndnote.get -> bool
Fluent.Pdf.PdfFlowNotePlacement.Note.get -> Fluent.Pdf.PdfFlowNote!
Fluent.Pdf.PdfFlowNotePlacement.ReferenceBlockIndices.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfFlowNoteReference
Fluent.Pdf.PdfFlowNoteReference.IsEndnote.get -> bool
Fluent.Pdf.PdfFlowNoteReference.Note.get -> Fluent.Pdf.PdfFlowNote!
Fluent.Pdf.PdfFlowNoteReference.Paragraph.get -> Fluent.Pdf.PdfFlowParagraph!
Fluent.Pdf.PdfFlowNoteReference.PdfFlowNoteReference(Fluent.Pdf.PdfFlowParagraph! paragraph, Fluent.Pdf.PdfFlowNote! note, bool endnote = false) -> void
Fluent.Pdf.PdfFlowOptions
Fluent.Pdf.PdfFlowOptions.Frame.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfFlowOptions.Geometry.get -> Fluent.Pdf.PdfPageGeometry!
Fluent.Pdf.PdfFlowOptions.Margins.get -> Fluent.Pdf.PdfMargins
Fluent.Pdf.PdfFlowOptions.MaximumPages.get -> int
Fluent.Pdf.PdfFlowOptions.Overflow.get -> Fluent.Pdf.PdfFlowOverflow
Fluent.Pdf.PdfFlowOptions.PdfFlowOptions(Fluent.Pdf.PdfPageGeometry! geometry, Fluent.Pdf.PdfMargins? margins = null, Fluent.Pdf.PdfFlowOverflow overflow = Fluent.Pdf.PdfFlowOverflow.Error, int maximumPages = 10000, double tabInterval = 36, double[]? tabStops = null) -> void
Fluent.Pdf.PdfFlowOptions.TabInterval.get -> double
Fluent.Pdf.PdfFlowOptions.TabStops.get -> System.Collections.Generic.IReadOnlyList<double>!
Fluent.Pdf.PdfFlowOverflow
Fluent.Pdf.PdfFlowOverflow.BreakGrapheme = 1 -> Fluent.Pdf.PdfFlowOverflow
Fluent.Pdf.PdfFlowOverflow.Clip = 2 -> Fluent.Pdf.PdfFlowOverflow
Fluent.Pdf.PdfFlowOverflow.Error = 0 -> Fluent.Pdf.PdfFlowOverflow
Fluent.Pdf.PdfFlowPage
Fluent.Pdf.PdfFlowPage.Canvas.get -> Fluent.Pdf.PdfCanvasResult!
Fluent.Pdf.PdfFlowPage.Columns.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowColumnPlacement!>!
Fluent.Pdf.PdfFlowPage.ContentHeight.get -> double
Fluent.Pdf.PdfFlowPage.Floats.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowFloatPlacement!>!
Fluent.Pdf.PdfFlowPage.Headings.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowHeadingPlacement!>!
Fluent.Pdf.PdfFlowPage.Index.get -> int
Fluent.Pdf.PdfFlowPage.Lines.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowLine!>!
Fluent.Pdf.PdfFlowPage.Notes.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowNotePlacement!>!
Fluent.Pdf.PdfFlowPage.Tables.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfTableFragment!>!
Fluent.Pdf.PdfFlowPageBreak
Fluent.Pdf.PdfFlowPageBreak.PdfFlowPageBreak() -> void
Fluent.Pdf.PdfFlowParagraph
Fluent.Pdf.PdfFlowParagraph.PdfFlowParagraph(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowRun!>! runs) -> void
Fluent.Pdf.PdfFlowParagraph.PdfFlowParagraph(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowRun!>! runs, Fluent.Pdf.PdfFlowParagraphStyle? style) -> void
Fluent.Pdf.PdfFlowParagraph.PdfFlowParagraph(string! text, Fluent.Pdf.PdfFlowTextStyle! textStyle, Fluent.Pdf.PdfFlowParagraphStyle? style = null) -> void
Fluent.Pdf.PdfFlowParagraph.Runs.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowRun!>!
Fluent.Pdf.PdfFlowParagraph.Style.get -> Fluent.Pdf.PdfFlowParagraphStyle!
Fluent.Pdf.PdfFlowParagraphStyle
Fluent.Pdf.PdfFlowParagraphStyle.Alignment.get -> Fluent.Pdf.PdfFlowAlignment
Fluent.Pdf.PdfFlowParagraphStyle.Clear.get -> Fluent.Pdf.PdfFlowClear
Fluent.Pdf.PdfFlowParagraphStyle.FirstLineIndent.get -> double
Fluent.Pdf.PdfFlowParagraphStyle.KeepTogether.get -> bool
Fluent.Pdf.PdfFlowParagraphStyle.KeepWithNext.get -> bool
Fluent.Pdf.PdfFlowParagraphStyle.LeftIndent.get -> double
Fluent.Pdf.PdfFlowParagraphStyle.LineHeight.get -> double
Fluent.Pdf.PdfFlowParagraphStyle.OrphanLines.get -> int
Fluent.Pdf.PdfFlowParagraphStyle.PageBreakBefore.get -> bool
Fluent.Pdf.PdfFlowParagraphStyle.PdfFlowParagraphStyle(Fluent.Pdf.PdfFlowAlignment alignment = Fluent.Pdf.PdfFlowAlignment.Start, double lineHeight = 1.2, double spaceBefore = 0, double spaceAfter = 0, double leftIndent = 0, double rightIndent = 0, double firstLineIndent = 0, int orphanLines = 2, int widowLines = 2, bool keepTogether = false, bool keepWithNext = false, bool pageBreakBefore = false, Fluent.Pdf.PdfFlowClear clear = Fluent.Pdf.PdfFlowClear.None) -> void
Fluent.Pdf.PdfFlowParagraphStyle.RightIndent.get -> double
Fluent.Pdf.PdfFlowParagraphStyle.SpaceAfter.get -> double
Fluent.Pdf.PdfFlowParagraphStyle.SpaceBefore.get -> double
Fluent.Pdf.PdfFlowParagraphStyle.WidowLines.get -> int
Fluent.Pdf.PdfFlowResult
Fluent.Pdf.PdfFlowResult.CreateDocument(Fluent.Pdf.PdfOptions? options = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfFlowResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfFlowResult.Options.get -> Fluent.Pdf.PdfFlowOptions!
Fluent.Pdf.PdfFlowResult.Pages.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowPage!>!
Fluent.Pdf.PdfFlowRun
Fluent.Pdf.PdfFlowRun.PdfFlowRun(string! text, Fluent.Pdf.PdfFlowTextStyle! style) -> void
Fluent.Pdf.PdfFlowRun.Style.get -> Fluent.Pdf.PdfFlowTextStyle!
Fluent.Pdf.PdfFlowRun.Text.get -> string!
Fluent.Pdf.PdfFlowSectionBreak
Fluent.Pdf.PdfFlowSectionBreak.PdfFlowSectionBreak(Fluent.Pdf.PdfFlowSectionStart start = Fluent.Pdf.PdfFlowSectionStart.NextPage) -> void
Fluent.Pdf.PdfFlowSectionBreak.Start.get -> Fluent.Pdf.PdfFlowSectionStart
Fluent.Pdf.PdfFlowSectionStart
Fluent.Pdf.PdfFlowSectionStart.Continuous = 0 -> Fluent.Pdf.PdfFlowSectionStart
Fluent.Pdf.PdfFlowSectionStart.NextEvenPage = 3 -> Fluent.Pdf.PdfFlowSectionStart
Fluent.Pdf.PdfFlowSectionStart.NextOddPage = 2 -> Fluent.Pdf.PdfFlowSectionStart
Fluent.Pdf.PdfFlowSectionStart.NextPage = 1 -> Fluent.Pdf.PdfFlowSectionStart
Fluent.Pdf.PdfFlowTextStyle
Fluent.Pdf.PdfFlowTextStyle.BaselineShift.get -> double
Fluent.Pdf.PdfFlowTextStyle.Color.get -> Fluent.Pdf.PdfColor!
Fluent.Pdf.PdfFlowTextStyle.FontSize.get -> double
Fluent.Pdf.PdfFlowTextStyle.Fonts.get -> Fluent.Pdf.PdfFontFallback!
Fluent.Pdf.PdfFlowTextStyle.PdfFlowTextStyle(Fluent.Pdf.PdfFontFallback! fonts, double fontSize = 12, Fluent.Pdf.PdfTextOptions? textOptions = null, Fluent.Pdf.PdfColor? color = null, double baselineShift = 0) -> void
Fluent.Pdf.PdfFlowTextStyle.TextOptions.get -> Fluent.Pdf.PdfTextOptions!
Fluent.Pdf.PdfFont
Fluent.Pdf.PdfFont.CopyShapingSource(System.IO.Stream! output, Fluent.Pdf.PdfOperationContext! context) -> int
Fluent.Pdf.PdfFont.GetGlyph(int unicodeScalar, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFontGlyph!
Fluent.Pdf.PdfFont.Info.get -> Fluent.Pdf.PdfFontInfo!
Fluent.Pdf.PdfFont.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfFont.ProviderId.get -> string!
Fluent.Pdf.PdfFont.WriteProgram(System.IO.Stream! output, System.Collections.Generic.IEnumerable<int>! glyphs, bool subset = true, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFontWriteResult!
Fluent.Pdf.PdfFontEmbeddingRights
Fluent.Pdf.PdfFontEmbeddingRights.BitmapOnly = 512 -> Fluent.Pdf.PdfFontEmbeddingRights
Fluent.Pdf.PdfFontEmbeddingRights.Editable = 8 -> Fluent.Pdf.PdfFontEmbeddingRights
Fluent.Pdf.PdfFontEmbeddingRights.Installable = 0 -> Fluent.Pdf.PdfFontEmbeddingRights
Fluent.Pdf.PdfFontEmbeddingRights.NoSubsetting = 256 -> Fluent.Pdf.PdfFontEmbeddingRights
Fluent.Pdf.PdfFontEmbeddingRights.PreviewAndPrint = 4 -> Fluent.Pdf.PdfFontEmbeddingRights
Fluent.Pdf.PdfFontEmbeddingRights.Restricted = 2 -> Fluent.Pdf.PdfFontEmbeddingRights
Fluent.Pdf.PdfFontFallback
Fluent.Pdf.PdfFontFallback.Fonts.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFont!>!
Fluent.Pdf.PdfFontFallback.PdfFontFallback(params Fluent.Pdf.PdfFont![]! fonts) -> void
Fluent.Pdf.PdfFontFallback.Resolve(int unicodeScalar, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFontGlyph!
Fluent.Pdf.PdfFontFormat
Fluent.Pdf.PdfFontFormat.Cff = 2 -> Fluent.Pdf.PdfFontFormat
Fluent.Pdf.PdfFontFormat.Standard = 0 -> Fluent.Pdf.PdfFontFormat
Fluent.Pdf.PdfFontFormat.TrueType = 1 -> Fluent.Pdf.PdfFontFormat
Fluent.Pdf.PdfFontFormat.Type1 = 3 -> Fluent.Pdf.PdfFontFormat
Fluent.Pdf.PdfFontFormat.Type3 = 4 -> Fluent.Pdf.PdfFontFormat
Fluent.Pdf.PdfFontGlyph
Fluent.Pdf.PdfFontGlyph.Advance.get -> double
Fluent.Pdf.PdfFontGlyph.Font.get -> Fluent.Pdf.PdfFont!
Fluent.Pdf.PdfFontGlyph.GlyphIndex.get -> int
Fluent.Pdf.PdfFontGlyph.GlyphName.get -> string!
Fluent.Pdf.PdfFontGlyph.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfFontGlyph.UnicodeScalar.get -> int
Fluent.Pdf.PdfFontGlyph.VerticalAdvance.get -> double?
Fluent.Pdf.PdfFontInfo
Fluent.Pdf.PdfFontInfo.EmbeddingRights.get -> Fluent.Pdf.PdfFontEmbeddingRights
Fluent.Pdf.PdfFontInfo.Format.get -> Fluent.Pdf.PdfFontFormat
Fluent.Pdf.PdfFontInfo.GlyphCount.get -> int
Fluent.Pdf.PdfFontInfo.Metrics.get -> Fluent.Pdf.PdfFontMetrics
Fluent.Pdf.PdfFontInfo.PdfFontInfo(string! postScriptName, Fluent.Pdf.PdfFontFormat format, int glyphCount, Fluent.Pdf.PdfFontMetrics metrics, string! sha256, Fluent.Pdf.PdfFontEmbeddingRights embeddingRights = Fluent.Pdf.PdfFontEmbeddingRights.Installable, bool symbolic = false) -> void
Fluent.Pdf.PdfFontInfo.PostScriptName.get -> string!
Fluent.Pdf.PdfFontInfo.Sha256.get -> string!
Fluent.Pdf.PdfFontInfo.Symbolic.get -> bool
Fluent.Pdf.PdfFontMetrics
Fluent.Pdf.PdfFontMetrics.Ascender.get -> int
Fluent.Pdf.PdfFontMetrics.Bottom.get -> int
Fluent.Pdf.PdfFontMetrics.CapHeight.get -> int
Fluent.Pdf.PdfFontMetrics.Descender.get -> int
Fluent.Pdf.PdfFontMetrics.FixedPitch.get -> bool
Fluent.Pdf.PdfFontMetrics.ItalicAngle.get -> double
Fluent.Pdf.PdfFontMetrics.Left.get -> int
Fluent.Pdf.PdfFontMetrics.PdfFontMetrics() -> void
Fluent.Pdf.PdfFontMetrics.PdfFontMetrics(int unitsPerEm, int ascender, int descender, int capHeight, int left, int bottom, int right, int top, bool fixedPitch = false, double italicAngle = 0) -> void
Fluent.Pdf.PdfFontMetrics.Right.get -> int
Fluent.Pdf.PdfFontMetrics.Top.get -> int
Fluent.Pdf.PdfFontMetrics.UnitsPerEm.get -> int
Fluent.Pdf.PdfFontProgramWriteResult
Fluent.Pdf.PdfFontProgramWriteResult.CidKeyed.get -> bool
Fluent.Pdf.PdfFontProgramWriteResult.FontFileSubtype.get -> string?
Fluent.Pdf.PdfFontProgramWriteResult.Length1.get -> long
Fluent.Pdf.PdfFontProgramWriteResult.Length2.get -> long
Fluent.Pdf.PdfFontProgramWriteResult.Length3.get -> long
Fluent.Pdf.PdfFontProgramWriteResult.PdfFontProgramWriteResult(string! postScriptName, string? fontFileSubtype = null, long length1 = 0, long length2 = 0, long length3 = 0, bool cidKeyed = false) -> void
Fluent.Pdf.PdfFontProgramWriteResult.PostScriptName.get -> string!
Fluent.Pdf.PdfFontResource
Fluent.Pdf.PdfFontResource.EncodedText.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfFontResource.Font.get -> Fluent.Pdf.PdfFont!
Fluent.Pdf.PdfFontResource.GlyphMappings.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfGlyphMapping!>!
Fluent.Pdf.PdfFontResource.MinimumVersion.get -> string!
Fluent.Pdf.PdfFontResource.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfFontResource.Vertical.get -> bool
Fluent.Pdf.PdfFontVerticalMetrics
Fluent.Pdf.PdfFontVerticalMetrics.Advance.get -> double
Fluent.Pdf.PdfFontVerticalMetrics.OriginX.get -> double
Fluent.Pdf.PdfFontVerticalMetrics.OriginY.get -> double
Fluent.Pdf.PdfFontVerticalMetrics.PdfFontVerticalMetrics() -> void
Fluent.Pdf.PdfFontVerticalMetrics.PdfFontVerticalMetrics(double advance, double originX, double originY) -> void
Fluent.Pdf.PdfFontWriteResult
Fluent.Pdf.PdfFontWriteResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfFontWriteResult.Program.get -> Fluent.Pdf.PdfFontProgramWriteResult!
Fluent.Pdf.PdfFormChoice
Fluent.Pdf.PdfFormChoice.DisplayValue.get -> string!
Fluent.Pdf.PdfFormChoice.ExportValue.get -> string!
Fluent.Pdf.PdfFormChoice.PdfFormChoice(string! exportValue, string? displayValue = null) -> void
Fluent.Pdf.PdfFormData
Fluent.Pdf.PdfFormData.Fields.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFormDataField!>!
Fluent.Pdf.PdfFormData.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfFormData.Write(System.IO.Stream! output, Fluent.Pdf.PdfFormDataFormat format, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfFormDataField
Fluent.Pdf.PdfFormDataField.Name.get -> string!
Fluent.Pdf.PdfFormDataField.PdfFormDataField(string! name, Fluent.Pdf.PdfValue! value) -> void
Fluent.Pdf.PdfFormDataField.Value.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfFormDataFormat
Fluent.Pdf.PdfFormDataFormat.Fdf = 0 -> Fluent.Pdf.PdfFormDataFormat
Fluent.Pdf.PdfFormDataFormat.Json = 3 -> Fluent.Pdf.PdfFormDataFormat
Fluent.Pdf.PdfFormDataFormat.Xfdf = 1 -> Fluent.Pdf.PdfFormDataFormat
Fluent.Pdf.PdfFormDataFormat.Xml = 2 -> Fluent.Pdf.PdfFormDataFormat
Fluent.Pdf.PdfFormField
Fluent.Pdf.PdfFormField.DefaultValue.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfFormField.Flags.get -> uint
Fluent.Pdf.PdfFormField.Inherited.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfFormField.Kind.get -> Fluent.Pdf.PdfFormFieldKind
Fluent.Pdf.PdfFormField.Name.get -> string!
Fluent.Pdf.PdfFormField.Representations.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfObjectId>!
Fluent.Pdf.PdfFormField.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfFormField.Value.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfFormField.Widgets.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFormWidget!>!
Fluent.Pdf.PdfFormFieldDefinition
Fluent.Pdf.PdfFormFieldDefinition.AlternateName.get -> string?
Fluent.Pdf.PdfFormFieldDefinition.Choices.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFormChoice!>!
Fluent.Pdf.PdfFormFieldDefinition.DefaultValue.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfFormFieldDefinition.EditingFont.get -> Fluent.Pdf.PdfFontResource?
Fluent.Pdf.PdfFormFieldDefinition.Flags.get -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldDefinition.FontSize.get -> double
Fluent.Pdf.PdfFormFieldDefinition.Kind.get -> Fluent.Pdf.PdfFormFieldKind
Fluent.Pdf.PdfFormFieldDefinition.MappingName.get -> string?
Fluent.Pdf.PdfFormFieldDefinition.MaximumLength.get -> int?
Fluent.Pdf.PdfFormFieldDefinition.Name.get -> string!
Fluent.Pdf.PdfFormFieldDefinition.PdfFormFieldDefinition(string! name, Fluent.Pdf.PdfFormFieldKind kind, Fluent.Pdf.PdfFormWidgetDefinition![]! widgets, Fluent.Pdf.PdfValue? value = null, Fluent.Pdf.PdfValue? defaultValue = null, Fluent.Pdf.PdfFormFieldFlags flags = Fluent.Pdf.PdfFormFieldFlags.None, Fluent.Pdf.PdfFormChoice![]? choices = null, int? maximumLength = null, string? alternateName = null, string? mappingName = null, Fluent.Pdf.PdfFontResource? editingFont = null, double fontSize = 12) -> void
Fluent.Pdf.PdfFormFieldDefinition.Value.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfFormFieldDefinition.Widgets.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFormWidgetDefinition!>!
Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.Comb = 16777216 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.CommitOnSelectionChange = 67108864 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.DoNotScroll = 8388608 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.DoNotSpellCheck = 4194304 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.Edit = 262144 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.FileSelect = 1048576 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.MultiSelect = 2097152 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.Multiline = 4096 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.NoExport = 4 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.NoToggleToOff = 16384 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.None = 0 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.Password = 8192 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.RadiosInUnison = 33554432 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.ReadOnly = 1 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.Required = 2 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldFlags.Sort = 524288 -> Fluent.Pdf.PdfFormFieldFlags
Fluent.Pdf.PdfFormFieldKind
Fluent.Pdf.PdfFormFieldKind.CheckBox = 2 -> Fluent.Pdf.PdfFormFieldKind
Fluent.Pdf.PdfFormFieldKind.ComboBox = 4 -> Fluent.Pdf.PdfFormFieldKind
Fluent.Pdf.PdfFormFieldKind.ListBox = 5 -> Fluent.Pdf.PdfFormFieldKind
Fluent.Pdf.PdfFormFieldKind.PushButton = 6 -> Fluent.Pdf.PdfFormFieldKind
Fluent.Pdf.PdfFormFieldKind.RadioButton = 3 -> Fluent.Pdf.PdfFormFieldKind
Fluent.Pdf.PdfFormFieldKind.Signature = 7 -> Fluent.Pdf.PdfFormFieldKind
Fluent.Pdf.PdfFormFieldKind.Text = 1 -> Fluent.Pdf.PdfFormFieldKind
Fluent.Pdf.PdfFormFieldKind.Unknown = 0 -> Fluent.Pdf.PdfFormFieldKind
Fluent.Pdf.PdfFormSet
Fluent.Pdf.PdfFormSet.Dictionary.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfFormSet.Fields.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFormField!>!
Fluent.Pdf.PdfFormSet.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfFormSet.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfFormTabOrder
Fluent.Pdf.PdfFormTabOrder.Columns = 1 -> Fluent.Pdf.PdfFormTabOrder
Fluent.Pdf.PdfFormTabOrder.Rows = 0 -> Fluent.Pdf.PdfFormTabOrder
Fluent.Pdf.PdfFormTabOrder.Structure = 2 -> Fluent.Pdf.PdfFormTabOrder
Fluent.Pdf.PdfFormTextStyle
Fluent.Pdf.PdfFormTextStyle.FontSize.get -> double
Fluent.Pdf.PdfFormTextStyle.Fonts.get -> Fluent.Pdf.PdfFontFallback!
Fluent.Pdf.PdfFormTextStyle.Padding.get -> double
Fluent.Pdf.PdfFormTextStyle.PasswordMask.get -> string?
Fluent.Pdf.PdfFormTextStyle.PdfFormTextStyle(Fluent.Pdf.PdfFontFallback! fonts, Fluent.Pdf.PdfProviderRegistry! providers, double fontSize = 12, double padding = 2, Fluent.Pdf.PdfTextOptions? textOptions = null, string? passwordMask = null) -> void
Fluent.Pdf.PdfFormTextStyle.Providers.get -> Fluent.Pdf.PdfProviderRegistry!
Fluent.Pdf.PdfFormTextStyle.TextOptions.get -> Fluent.Pdf.PdfTextOptions!
Fluent.Pdf.PdfFormUpdate
Fluent.Pdf.PdfFormUpdate.Appearances.get -> System.Collections.Generic.IReadOnlyDictionary<Fluent.Pdf.PdfObjectId, Fluent.Pdf.PdfGraphicsResource!>!
Fluent.Pdf.PdfFormUpdate.DefaultValue.get -> Fluent.Pdf.PdfValue?
Fluent.Pdf.PdfFormUpdate.Name.get -> string!
Fluent.Pdf.PdfFormUpdate.PdfFormUpdate(string! name, Fluent.Pdf.PdfValue! value, Fluent.Pdf.PdfFormTextStyle? textStyle = null, System.Collections.Generic.IReadOnlyDictionary<Fluent.Pdf.PdfObjectId, Fluent.Pdf.PdfGraphicsResource!>? appearances = null, Fluent.Pdf.PdfValue? defaultValue = null) -> void
Fluent.Pdf.PdfFormUpdate.TextStyle.get -> Fluent.Pdf.PdfFormTextStyle?
Fluent.Pdf.PdfFormUpdate.Value.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfFormWidget
Fluent.Pdf.PdfFormWidget.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfFormWidget.Definition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfFormWidget.Id.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfFormWidget.PageIndex.get -> int?
Fluent.Pdf.PdfFormWidgetDefinition
Fluent.Pdf.PdfFormWidgetDefinition.AnnotationFlags.get -> int
Fluent.Pdf.PdfFormWidgetDefinition.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfFormWidgetDefinition.ExportValue.get -> string?
Fluent.Pdf.PdfFormWidgetDefinition.NormalAppearance.get -> Fluent.Pdf.PdfGraphicsResource!
Fluent.Pdf.PdfFormWidgetDefinition.OffAppearance.get -> Fluent.Pdf.PdfGraphicsResource?
Fluent.Pdf.PdfFormWidgetDefinition.PageIndex.get -> int
Fluent.Pdf.PdfFormWidgetDefinition.PdfFormWidgetDefinition(int pageIndex, Fluent.Pdf.PdfRect bounds, Fluent.Pdf.PdfGraphicsResource! normalAppearance, string? exportValue = null, Fluent.Pdf.PdfGraphicsResource? offAppearance = null, int annotationFlags = 4) -> void
Fluent.Pdf.PdfForms
Fluent.Pdf.PdfFunction
Fluent.Pdf.PdfFunction.Definition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfFunction.Domain.get -> System.Collections.Generic.IReadOnlyList<double>!
Fluent.Pdf.PdfFunction.InputComponents.get -> int
Fluent.Pdf.PdfFunction.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfFunction.OutputComponents.get -> int
Fluent.Pdf.PdfFunction.Range.get -> System.Collections.Generic.IReadOnlyList<double>!
Fluent.Pdf.PdfGenerationContext
Fluent.Pdf.PdfGenerationContext.Add(Fluent.Pdf.PdfFlowBlock! block) -> void
Fluent.Pdf.PdfGenerationContext.Add(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowBlock!>! blocks) -> void
Fluent.Pdf.PdfGenerationContext.AddTable<T>(System.Collections.Generic.IEnumerable<T>! source, Fluent.Pdf.PdfTableDataColumn<T>![]! columns, int maximumRows = 100000, Fluent.Pdf.PdfTableRow![]? headerRows = null, bool allowRowSplit = true) -> void
Fluent.Pdf.PdfGenerationContext.Apply<T>(Fluent.Pdf.PdfGenerationTemplate<T>! template, T record) -> void
Fluent.Pdf.PdfGenerationContext.BlockCount.get -> int
Fluent.Pdf.PdfGenerationContext.Culture.get -> System.Globalization.CultureInfo!
Fluent.Pdf.PdfGenerationContext.Format(object? value, string? format = null, string! nullText = "") -> string!
Fluent.Pdf.PdfGenerationContext.OperationContext.get -> Fluent.Pdf.PdfOperationContext!
Fluent.Pdf.PdfGenerationContext.Paragraph(string! text, Fluent.Pdf.PdfFlowTextStyle! style, Fluent.Pdf.PdfFlowParagraphStyle? paragraphStyle = null) -> void
Fluent.Pdf.PdfGenerationContext.Providers.get -> Fluent.Pdf.PdfProviderRegistry!
Fluent.Pdf.PdfGenerationContext.RecordIndex.get -> int
Fluent.Pdf.PdfGenerationContext.RecordsPrepared.get -> int
Fluent.Pdf.PdfGenerationEvent
Fluent.Pdf.PdfGenerationEvent.BlockCount.get -> int
Fluent.Pdf.PdfGenerationEvent.Kind.get -> Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationEvent.OperationContext.get -> Fluent.Pdf.PdfOperationContext!
Fluent.Pdf.PdfGenerationEvent.Ordinal.get -> int
Fluent.Pdf.PdfGenerationEvent.Page.get -> Fluent.Pdf.PdfGenerationPage?
Fluent.Pdf.PdfGenerationEvent.PageCount.get -> int
Fluent.Pdf.PdfGenerationEvent.PageIndex.get -> int
Fluent.Pdf.PdfGenerationEvent.Pages.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfGenerationPage!>!
Fluent.Pdf.PdfGenerationEvent.RecordIndex.get -> int
Fluent.Pdf.PdfGenerationEvent.RecordsPrepared.get -> int
Fluent.Pdf.PdfGenerationEvent.TemplateDepth.get -> int
Fluent.Pdf.PdfGenerationEvent.TemplateIdentifier.get -> string?
Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationEventKind.LayoutPrepared = 6 -> Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationEventKind.LayoutStarting = 5 -> Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationEventKind.PagePrepared = 7 -> Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationEventKind.Prepared = 10 -> Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationEventKind.RecordPrepared = 4 -> Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationEventKind.RecordStarting = 1 -> Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationEventKind.SavePrepared = 9 -> Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationEventKind.SaveStarting = 8 -> Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationEventKind.Starting = 0 -> Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationEventKind.TemplatePrepared = 3 -> Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationEventKind.TemplateStarting = 2 -> Fluent.Pdf.PdfGenerationEventKind
Fluent.Pdf.PdfGenerationOptions
Fluent.Pdf.PdfGenerationOptions.Culture.get -> System.Globalization.CultureInfo!
Fluent.Pdf.PdfGenerationOptions.DocumentOptions.get -> Fluent.Pdf.PdfOptions!
Fluent.Pdf.PdfGenerationOptions.Layout.get -> Fluent.Pdf.PdfFlowOptions!
Fluent.Pdf.PdfGenerationOptions.MaximumBlocks.get -> int
Fluent.Pdf.PdfGenerationOptions.MaximumEvents.get -> int
Fluent.Pdf.PdfGenerationOptions.MaximumRecords.get -> int
Fluent.Pdf.PdfGenerationOptions.Observer.get -> System.Action<Fluent.Pdf.PdfGenerationEvent!>?
Fluent.Pdf.PdfGenerationOptions.PageTemplate.get -> Fluent.Pdf.PdfPageTemplate?
Fluent.Pdf.PdfGenerationOptions.PdfGenerationOptions(Fluent.Pdf.PdfFlowOptions! layout, string! cultureName, Fluent.Pdf.PdfPageTemplate? pageTemplate = null, Fluent.Pdf.PdfOptions? documentOptions = null, System.Action<Fluent.Pdf.PdfGenerationEvent!>? observer = null, int maximumRecords = 100000, int maximumBlocks = 100000, int maximumEvents = 1000000) -> void
Fluent.Pdf.PdfGenerationPage
Fluent.Pdf.PdfGenerationPage.ColumnCount.get -> int
Fluent.Pdf.PdfGenerationPage.ContentHeight.get -> double
Fluent.Pdf.PdfGenerationPage.Geometry.get -> Fluent.Pdf.PdfPageGeometry!
Fluent.Pdf.PdfGenerationPage.Index.get -> int
Fluent.Pdf.PdfGenerationPage.LineCount.get -> int
Fluent.Pdf.PdfGenerationPage.NoteCount.get -> int
Fluent.Pdf.PdfGenerationPage.TableFragmentCount.get -> int
Fluent.Pdf.PdfGenerationTemplate<T>
Fluent.Pdf.PdfGenerationTemplate<T>.Identifier.get -> string!
Fluent.Pdf.PdfGenerationTemplate<T>.PdfGenerationTemplate(string! identifier, System.Action<Fluent.Pdf.PdfGenerationContext!, T>! render) -> void
Fluent.Pdf.PdfGlyphMapping
Fluent.Pdf.PdfGlyphMapping.GlyphIndex.get -> int
Fluent.Pdf.PdfGlyphMapping.PdfGlyphMapping(int glyphIndex, string! unicodeText) -> void
Fluent.Pdf.PdfGlyphMapping.UnicodeText.get -> string!
Fluent.Pdf.PdfGraphemeCluster
Fluent.Pdf.PdfGraphemeCluster.FirstScalar.get -> int
Fluent.Pdf.PdfGraphemeCluster.PdfGraphemeCluster() -> void
Fluent.Pdf.PdfGraphemeCluster.ScalarCount.get -> int
Fluent.Pdf.PdfGraphemeCluster.Utf16Length.get -> int
Fluent.Pdf.PdfGraphemeCluster.Utf16Offset.get -> int
Fluent.Pdf.PdfGraphicsObjects
Fluent.Pdf.PdfGraphicsOccurrence
Fluent.Pdf.PdfGraphicsOccurrence.Definition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfGraphicsOccurrence.Kind.get -> Fluent.Pdf.PdfGraphicsOccurrenceKind
Fluent.Pdf.PdfGraphicsOccurrence.OperationIndex.get -> int
Fluent.Pdf.PdfGraphicsOccurrence.Transform.get -> Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfGraphicsOccurrenceKind
Fluent.Pdf.PdfGraphicsOccurrenceKind.Form = 2 -> Fluent.Pdf.PdfGraphicsOccurrenceKind
Fluent.Pdf.PdfGraphicsOccurrenceKind.Image = 0 -> Fluent.Pdf.PdfGraphicsOccurrenceKind
Fluent.Pdf.PdfGraphicsOccurrenceKind.InlineImage = 1 -> Fluent.Pdf.PdfGraphicsOccurrenceKind
Fluent.Pdf.PdfGraphicsResource
Fluent.Pdf.PdfGraphicsResource.Category.get -> Fluent.Pdf.PdfGraphicsResourceCategory
Fluent.Pdf.PdfGraphicsResource.MinimumVersion.get -> string!
Fluent.Pdf.PdfGraphicsResource.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfGraphicsResourceCategory
Fluent.Pdf.PdfGraphicsResourceCategory.ColorSpace = 1 -> Fluent.Pdf.PdfGraphicsResourceCategory
Fluent.Pdf.PdfGraphicsResourceCategory.ExtGState = 4 -> Fluent.Pdf.PdfGraphicsResourceCategory
Fluent.Pdf.PdfGraphicsResourceCategory.Font = 0 -> Fluent.Pdf.PdfGraphicsResourceCategory
Fluent.Pdf.PdfGraphicsResourceCategory.Pattern = 2 -> Fluent.Pdf.PdfGraphicsResourceCategory
Fluent.Pdf.PdfGraphicsResourceCategory.Shading = 3 -> Fluent.Pdf.PdfGraphicsResourceCategory
Fluent.Pdf.PdfGraphicsResourceCategory.XObject = 5 -> Fluent.Pdf.PdfGraphicsResourceCategory
Fluent.Pdf.PdfGraphicsState
Fluent.Pdf.PdfGraphicsState.BlendMode.get -> string!
Fluent.Pdf.PdfGraphicsState.FillAlpha.get -> double
Fluent.Pdf.PdfGraphicsState.Resource.get -> Fluent.Pdf.PdfGraphicsResource!
Fluent.Pdf.PdfGraphicsState.SoftMask.get -> Fluent.Pdf.PdfSoftMask?
Fluent.Pdf.PdfGraphicsState.StrokeAlpha.get -> double
Fluent.Pdf.PdfHtml
Fluent.Pdf.PdfHtmlMedia
Fluent.Pdf.PdfHtmlMedia.Print = 0 -> Fluent.Pdf.PdfHtmlMedia
Fluent.Pdf.PdfHtmlMedia.Screen = 1 -> Fluent.Pdf.PdfHtmlMedia
Fluent.Pdf.PdfHtmlOptions
Fluent.Pdf.PdfHtmlOptions.EnableJavaScript.get -> bool
Fluent.Pdf.PdfHtmlOptions.Locale.get -> string!
Fluent.Pdf.PdfHtmlOptions.MaximumPages.get -> int
Fluent.Pdf.PdfHtmlOptions.Media.get -> Fluent.Pdf.PdfHtmlMedia
Fluent.Pdf.PdfHtmlOptions.PageSize.get -> Fluent.Pdf.PdfPageSize
Fluent.Pdf.PdfHtmlOptions.PdfHtmlOptions(Fluent.Pdf.PdfPageSize? pageSize = null, int viewportWidth = 1280, int viewportHeight = 720, bool enableJavaScript = false, bool printBackground = true, int maximumPages = 1000, Fluent.Pdf.PdfHtmlReadinessOptions? readiness = null, string! locale = "en-US", string! timeZone = "UTC", Fluent.Pdf.PdfHtmlMedia media = Fluent.Pdf.PdfHtmlMedia.Print) -> void
Fluent.Pdf.PdfHtmlOptions.PdfHtmlOptions(Fluent.Pdf.PdfPageSize? pageSize, int viewportWidth, int viewportHeight, bool enableJavaScript, bool printBackground, int maximumPages) -> void
Fluent.Pdf.PdfHtmlOptions.Print.get -> Fluent.Pdf.PdfHtmlPrintOptions!
Fluent.Pdf.PdfHtmlOptions.PrintBackground.get -> bool
Fluent.Pdf.PdfHtmlOptions.Readiness.get -> Fluent.Pdf.PdfHtmlReadinessOptions!
Fluent.Pdf.PdfHtmlOptions.TimeZone.get -> string!
Fluent.Pdf.PdfHtmlOptions.ViewportHeight.get -> int
Fluent.Pdf.PdfHtmlOptions.ViewportWidth.get -> int
Fluent.Pdf.PdfHtmlOptions.WithPrint(Fluent.Pdf.PdfHtmlPrintOptions! print) -> Fluent.Pdf.PdfHtmlOptions!
Fluent.Pdf.PdfHtmlPrintOptions
Fluent.Pdf.PdfHtmlPrintOptions.FooterHtml.get -> string?
Fluent.Pdf.PdfHtmlPrintOptions.HeaderHtml.get -> string?
Fluent.Pdf.PdfHtmlPrintOptions.Landscape.get -> bool
Fluent.Pdf.PdfHtmlPrintOptions.MarginBottom.get -> double
Fluent.Pdf.PdfHtmlPrintOptions.MarginLeft.get -> double
Fluent.Pdf.PdfHtmlPrintOptions.MarginRight.get -> double
Fluent.Pdf.PdfHtmlPrintOptions.MarginTop.get -> double
Fluent.Pdf.PdfHtmlPrintOptions.PdfHtmlPrintOptions(bool preferCssPageSize = false, bool landscape = false, double scale = 1, double marginTop = 0, double marginBottom = 0, double marginLeft = 0, double marginRight = 0, string? headerHtml = null, string? footerHtml = null, bool tagged = false) -> void
Fluent.Pdf.PdfHtmlPrintOptions.PreferCssPageSize.get -> bool
Fluent.Pdf.PdfHtmlPrintOptions.Scale.get -> double
Fluent.Pdf.PdfHtmlPrintOptions.Tagged.get -> bool
Fluent.Pdf.PdfHtmlReadinessOptions
Fluent.Pdf.PdfHtmlReadinessOptions.JavaScriptPromise.get -> string?
Fluent.Pdf.PdfHtmlReadinessOptions.MaximumInFlightRequests.get -> int
Fluent.Pdf.PdfHtmlReadinessOptions.NetworkQuietPeriod.get -> System.TimeSpan
Fluent.Pdf.PdfHtmlReadinessOptions.PdfHtmlReadinessOptions(System.TimeSpan? timeout = null, System.Collections.Generic.IEnumerable<string!>? selectors = null, string? javaScriptPromise = null, bool waitForDocumentComplete = true, bool waitForFonts = true, bool waitForImages = true, System.TimeSpan? networkQuietPeriod = null, int maximumInFlightRequests = 0, System.TimeSpan? pollingInterval = null) -> void
Fluent.Pdf.PdfHtmlReadinessOptions.PollingInterval.get -> System.TimeSpan
Fluent.Pdf.PdfHtmlReadinessOptions.Selectors.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfHtmlReadinessOptions.Timeout.get -> System.TimeSpan
Fluent.Pdf.PdfHtmlReadinessOptions.WaitForDocumentComplete.get -> bool
Fluent.Pdf.PdfHtmlReadinessOptions.WaitForFonts.get -> bool
Fluent.Pdf.PdfHtmlReadinessOptions.WaitForImages.get -> bool
Fluent.Pdf.PdfHtmlResource
Fluent.Pdf.PdfHtmlResource.Address.get -> System.Uri!
Fluent.Pdf.PdfHtmlResource.Length.get -> long
Fluent.Pdf.PdfHtmlResource.MediaType.get -> string!
Fluent.Pdf.PdfHtmlResource.OpenContent() -> System.IO.Stream!
Fluent.Pdf.PdfHtmlSource
Fluent.Pdf.PdfHtmlSource.Address.get -> System.Uri!
Fluent.Pdf.PdfHtmlSource.Markup.get -> string?
Fluent.Pdf.PdfHtmlSource.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfHtmlSource.Resources.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfHtmlResource!>!
Fluent.Pdf.PdfImageAsset
Fluent.Pdf.PdfImageAsset.Definition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfImageAsset.DisplayAspect.get -> double
Fluent.Pdf.PdfImageAsset.HasOriginalFile.get -> bool
Fluent.Pdf.PdfImageAsset.Info.get -> Fluent.Pdf.PdfImageInfo!
Fluent.Pdf.PdfImageAsset.IsStencil.get -> bool
Fluent.Pdf.PdfImageAsset.MediaType.get -> string?
Fluent.Pdf.PdfImageAsset.Metadata.get -> Fluent.Pdf.PdfImageMetadata!
Fluent.Pdf.PdfImageAsset.NaturalSize.get -> Fluent.Pdf.PdfPageSize?
Fluent.Pdf.PdfImageAsset.OpenSamples(Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.IO.Stream!
Fluent.Pdf.PdfImageAsset.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfImageAsset.Resource.get -> Fluent.Pdf.PdfGraphicsResource!
Fluent.Pdf.PdfImageAsset.SoftMask.get -> Fluent.Pdf.PdfImageAsset?
Fluent.Pdf.PdfImageAsset.WriteEncoded(System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfImageAsset.WriteOriginalFile(System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfImageCodec
Fluent.Pdf.PdfImageCodec.Dct = 0 -> Fluent.Pdf.PdfImageCodec
Fluent.Pdf.PdfImageCodec.Jbig2 = 2 -> Fluent.Pdf.PdfImageCodec
Fluent.Pdf.PdfImageCodec.Jpx = 1 -> Fluent.Pdf.PdfImageCodec
Fluent.Pdf.PdfImageDeduplicationResult
Fluent.Pdf.PdfImageDeduplicationResult.Images.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfImageAsset!>!
Fluent.Pdf.PdfImageDeduplicationResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfImageDeduplicationResult.UniqueCount.get -> int
Fluent.Pdf.PdfImageInfo
Fluent.Pdf.PdfImageInfo.BitsPerComponent.get -> int
Fluent.Pdf.PdfImageInfo.ByteLength.get -> long
Fluent.Pdf.PdfImageInfo.Components.get -> int
Fluent.Pdf.PdfImageInfo.Height.get -> int
Fluent.Pdf.PdfImageInfo.PdfImageInfo(int width, int height, int components, int bitsPerComponent) -> void
Fluent.Pdf.PdfImageInfo.RowBytes.get -> long
Fluent.Pdf.PdfImageInfo.Width.get -> int
Fluent.Pdf.PdfImageMetadata
Fluent.Pdf.PdfImageMetadata.HorizontalDpi.get -> double
Fluent.Pdf.PdfImageMetadata.Orientation.get -> Fluent.Pdf.PdfImageOrientation
Fluent.Pdf.PdfImageMetadata.PdfImageMetadata(Fluent.Pdf.PdfImageOrientation orientation = Fluent.Pdf.PdfImageOrientation.Normal, double horizontalDpi = 0, double verticalDpi = 0, double pixelAspectRatio = 1) -> void
Fluent.Pdf.PdfImageMetadata.PixelAspectRatio.get -> double
Fluent.Pdf.PdfImageMetadata.VerticalDpi.get -> double
Fluent.Pdf.PdfImageOrientation
Fluent.Pdf.PdfImageOrientation.MirrorHorizontal = 2 -> Fluent.Pdf.PdfImageOrientation
Fluent.Pdf.PdfImageOrientation.MirrorVertical = 4 -> Fluent.Pdf.PdfImageOrientation
Fluent.Pdf.PdfImageOrientation.Normal = 1 -> Fluent.Pdf.PdfImageOrientation
Fluent.Pdf.PdfImageOrientation.Rotate180 = 3 -> Fluent.Pdf.PdfImageOrientation
Fluent.Pdf.PdfImageOrientation.Rotate270 = 8 -> Fluent.Pdf.PdfImageOrientation
Fluent.Pdf.PdfImageOrientation.Rotate90 = 6 -> Fluent.Pdf.PdfImageOrientation
Fluent.Pdf.PdfImageOrientation.Transpose = 5 -> Fluent.Pdf.PdfImageOrientation
Fluent.Pdf.PdfImageOrientation.Transverse = 7 -> Fluent.Pdf.PdfImageOrientation
Fluent.Pdf.PdfImageReadOptions
Fluent.Pdf.PdfImageReadOptions.AllowLosslessTranscode.get -> bool
Fluent.Pdf.PdfImageReadOptions.ApplyExifOrientation.get -> bool
Fluent.Pdf.PdfImageReadOptions.ColorSpaceOverride.get -> Fluent.Pdf.PdfColorSpace?
Fluent.Pdf.PdfImageReadOptions.PdfImageReadOptions(bool allowLosslessTranscode = false, bool applyExifOrientation = true, Fluent.Pdf.PdfColorSpace? colorSpaceOverride = null) -> void
Fluent.Pdf.PdfImages
Fluent.Pdf.PdfInputConversion
Fluent.Pdf.PdfInputConversionOptions
Fluent.Pdf.PdfInputConversionOptions.Generation.get -> Fluent.Pdf.PdfGenerationOptions!
Fluent.Pdf.PdfInputConversionOptions.MaximumInputBytes.get -> int
Fluent.Pdf.PdfInputConversionOptions.MaximumNodes.get -> int
Fluent.Pdf.PdfInputConversionOptions.PdfInputConversionOptions(Fluent.Pdf.PdfGenerationOptions! generation, System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfFlowTextStyle!>! styles, System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfInputDrawing!>? resources = null, int maximumInputBytes = 4194304, int maximumNodes = 100000) -> void
Fluent.Pdf.PdfInputConversionOptions.Resources.get -> System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfInputDrawing!>!
Fluent.Pdf.PdfInputConversionOptions.Styles.get -> System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfFlowTextStyle!>!
Fluent.Pdf.PdfInputDrawing
Fluent.Pdf.PdfInputDrawing.Drawing.get -> Fluent.Pdf.PdfDrawing!
Fluent.Pdf.PdfInputDrawing.Height.get -> double
Fluent.Pdf.PdfInputDrawing.PdfInputDrawing(Fluent.Pdf.PdfDrawing! drawing, Fluent.Pdf.PdfRect sourceBounds, double width, double height) -> void
Fluent.Pdf.PdfInputDrawing.SourceBounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfInputDrawing.Width.get -> double
Fluent.Pdf.PdfInputFormat
Fluent.Pdf.PdfInputFormat.Json = 2 -> Fluent.Pdf.PdfInputFormat
Fluent.Pdf.PdfInputFormat.Markdown = 1 -> Fluent.Pdf.PdfInputFormat
Fluent.Pdf.PdfInputFormat.Text = 0 -> Fluent.Pdf.PdfInputFormat
Fluent.Pdf.PdfInputFormat.Xml = 3 -> Fluent.Pdf.PdfInputFormat
Fluent.Pdf.PdfInvoiceData
Fluent.Pdf.PdfInvoiceData.Buyer.get -> string!
Fluent.Pdf.PdfInvoiceData.Currency.get -> string!
Fluent.Pdf.PdfInvoiceData.GrandTotal.get -> decimal
Fluent.Pdf.PdfInvoiceData.Identifier.get -> string!
Fluent.Pdf.PdfInvoiceData.IssueTime.get -> System.DateTimeOffset
Fluent.Pdf.PdfInvoiceData.LineNetAmounts.get -> System.Collections.Generic.IReadOnlyList<decimal>!
Fluent.Pdf.PdfInvoiceData.Lines.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfInvoiceLine!>!
Fluent.Pdf.PdfInvoiceData.MinorUnits.get -> int
Fluent.Pdf.PdfInvoiceData.NetTotal.get -> decimal
Fluent.Pdf.PdfInvoiceData.PdfInvoiceData(Fluent.Pdf.PdfInvoiceProfile profile, string! identifier, string! seller, string! buyer, System.DateTimeOffset issueTime, string! currency, System.MidpointRounding rounding, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfInvoiceLine!>! lines) -> void
Fluent.Pdf.PdfInvoiceData.Profile.get -> Fluent.Pdf.PdfInvoiceProfile
Fluent.Pdf.PdfInvoiceData.Rounding.get -> System.MidpointRounding
Fluent.Pdf.PdfInvoiceData.Seller.get -> string!
Fluent.Pdf.PdfInvoiceData.TaxCategories.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfInvoiceTaxCategory!>!
Fluent.Pdf.PdfInvoiceData.TaxTotal.get -> decimal
Fluent.Pdf.PdfInvoiceLine
Fluent.Pdf.PdfInvoiceLine.Description.get -> string!
Fluent.Pdf.PdfInvoiceLine.PdfInvoiceLine(string! description, decimal quantity, decimal unitPrice, decimal taxRate) -> void
Fluent.Pdf.PdfInvoiceLine.Quantity.get -> decimal
Fluent.Pdf.PdfInvoiceLine.TaxRate.get -> decimal
Fluent.Pdf.PdfInvoiceLine.UnitPrice.get -> decimal
Fluent.Pdf.PdfInvoiceProfile
Fluent.Pdf.PdfInvoiceProfile.FacturX107Minimum = 0 -> Fluent.Pdf.PdfInvoiceProfile
Fluent.Pdf.PdfInvoiceTaxCategory
Fluent.Pdf.PdfInvoiceTaxCategory.TaxAmount.get -> decimal
Fluent.Pdf.PdfInvoiceTaxCategory.TaxRate.get -> decimal
Fluent.Pdf.PdfInvoiceTaxCategory.TaxableAmount.get -> decimal
Fluent.Pdf.PdfInvoiceValidation
Fluent.Pdf.PdfInvoiceValidation.ArchivalValidation.get -> Fluent.Pdf.PdfArchivalValidation?
Fluent.Pdf.PdfInvoiceValidation.Completed.get -> bool
Fluent.Pdf.PdfInvoiceValidation.Findings.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfInvoiceValidation.IsCompliant.get -> bool
Fluent.Pdf.PdfInvoiceValidation.OperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfInvoiceValidation.PdfInvoiceValidation(Fluent.Pdf.PdfInvoiceProfile profile, string! pdfSha256, string! xmlSha256, string! requestSha256, bool completed, bool isCompliant, System.Collections.Generic.IEnumerable<string!>! findings) -> void
Fluent.Pdf.PdfInvoiceValidation.PdfSha256.get -> string!
Fluent.Pdf.PdfInvoiceValidation.Profile.get -> Fluent.Pdf.PdfInvoiceProfile
Fluent.Pdf.PdfInvoiceValidation.RequestSha256.get -> string!
Fluent.Pdf.PdfInvoiceValidation.XmlSha256.get -> string!
Fluent.Pdf.PdfInvoices
Fluent.Pdf.PdfLayerGroup
Fluent.Pdf.PdfLayerGroup.Definition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfLayerGroup.Id.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfLayerGroup.Name.get -> string!
Fluent.Pdf.PdfLayerGroup.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfLayerSet
Fluent.Pdf.PdfLayerSet.Configurations.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfValue!>!
Fluent.Pdf.PdfLayerSet.Groups.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfLayerGroup!>!
Fluent.Pdf.PdfLayerSet.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfLayerSet.Properties.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfLayerSet.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfLayerUsage
Fluent.Pdf.PdfLayerUsage.Configured = 3 -> Fluent.Pdf.PdfLayerUsage
Fluent.Pdf.PdfLayerUsage.Export = 2 -> Fluent.Pdf.PdfLayerUsage
Fluent.Pdf.PdfLayerUsage.Print = 1 -> Fluent.Pdf.PdfLayerUsage
Fluent.Pdf.PdfLayerUsage.View = 0 -> Fluent.Pdf.PdfLayerUsage
Fluent.Pdf.PdfLayerVisibility
Fluent.Pdf.PdfLayerVisibility.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfLayerVisibility.Visible.get -> bool
Fluent.Pdf.PdfLayers
Fluent.Pdf.PdfLengthUnit
Fluent.Pdf.PdfLengthUnit.Centimeter = 3 -> Fluent.Pdf.PdfLengthUnit
Fluent.Pdf.PdfLengthUnit.Inch = 1 -> Fluent.Pdf.PdfLengthUnit
Fluent.Pdf.PdfLengthUnit.Millimeter = 2 -> Fluent.Pdf.PdfLengthUnit
Fluent.Pdf.PdfLengthUnit.Pica = 4 -> Fluent.Pdf.PdfLengthUnit
Fluent.Pdf.PdfLengthUnit.Pixel = 5 -> Fluent.Pdf.PdfLengthUnit
Fluent.Pdf.PdfLengthUnit.Point = 0 -> Fluent.Pdf.PdfLengthUnit
Fluent.Pdf.PdfLineCap
Fluent.Pdf.PdfLineCap.Butt = 0 -> Fluent.Pdf.PdfLineCap
Fluent.Pdf.PdfLineCap.Round = 1 -> Fluent.Pdf.PdfLineCap
Fluent.Pdf.PdfLineCap.Square = 2 -> Fluent.Pdf.PdfLineCap
Fluent.Pdf.PdfLineJoin
Fluent.Pdf.PdfLineJoin.Bevel = 2 -> Fluent.Pdf.PdfLineJoin
Fluent.Pdf.PdfLineJoin.Miter = 0 -> Fluent.Pdf.PdfLineJoin
Fluent.Pdf.PdfLineJoin.Round = 1 -> Fluent.Pdf.PdfLineJoin
Fluent.Pdf.PdfLinearization
Fluent.Pdf.PdfLinearizationInfo
Fluent.Pdf.PdfLinearizationInfo.FileLength.get -> long
Fluent.Pdf.PdfLinearizationInfo.FirstPageEnd.get -> long
Fluent.Pdf.PdfLinearizationInfo.FirstPageIndex.get -> int
Fluent.Pdf.PdfLinearizationInfo.FirstPageObjectNumber.get -> int
Fluent.Pdf.PdfLinearizationInfo.HintLength.get -> long
Fluent.Pdf.PdfLinearizationInfo.HintOffset.get -> long
Fluent.Pdf.PdfLinearizationInfo.MainCrossReferenceOffset.get -> long
Fluent.Pdf.PdfLinearizationInfo.PageCount.get -> int
Fluent.Pdf.PdfLinearizationOptions
Fluent.Pdf.PdfLinearizationOptions.AllowSignatureInvalidation.get -> bool
Fluent.Pdf.PdfLinearizationOptions.FirstPageIndex.get -> int
Fluent.Pdf.PdfLinearizationOptions.PdfLinearizationOptions(int firstPageIndex = 0, bool allowSignatureInvalidation = false) -> void
Fluent.Pdf.PdfLinearizationValidation
Fluent.Pdf.PdfLinearizationValidation.Completed.get -> bool
Fluent.Pdf.PdfLinearizationValidation.Findings.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfLinearizationValidation.IsValid.get -> bool
Fluent.Pdf.PdfLinearizationValidation.OperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfLinearizationValidation.PdfLinearizationValidation(string! sourceSha256, string! requestSha256, bool completed, bool isValid, System.Collections.Generic.IEnumerable<string!>! findings) -> void
Fluent.Pdf.PdfLinearizationValidation.RequestSha256.get -> string!
Fluent.Pdf.PdfLinearizationValidation.SourceSha256.get -> string!
Fluent.Pdf.PdfLossCategory
Fluent.Pdf.PdfLossCategory.FidelityReduction = 4 -> Fluent.Pdf.PdfLossCategory
Fluent.Pdf.PdfLossCategory.None = 0 -> Fluent.Pdf.PdfLossCategory
Fluent.Pdf.PdfLossCategory.OmittedContent = 2 -> Fluent.Pdf.PdfLossCategory
Fluent.Pdf.PdfLossCategory.SubstitutedContent = 3 -> Fluent.Pdf.PdfLossCategory
Fluent.Pdf.PdfLossCategory.UnsupportedContent = 1 -> Fluent.Pdf.PdfLossCategory
Fluent.Pdf.PdfMailMerge
Fluent.Pdf.PdfMailMergeOptions
Fluent.Pdf.PdfMailMergeOptions.Evaluation.get -> Fluent.Pdf.PdfMergeEvaluationOptions!
Fluent.Pdf.PdfMailMergeOptions.Generation.get -> Fluent.Pdf.PdfGenerationOptions!
Fluent.Pdf.PdfMailMergeOptions.PdfMailMergeOptions(Fluent.Pdf.PdfGenerationOptions! generation, System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfFlowTextStyle!>! styles, Fluent.Pdf.PdfMergeMissingField missingFields = Fluent.Pdf.PdfMergeMissingField.Error, int maximumVisits = 100000, int maximumRecords = 100000) -> void
Fluent.Pdf.PdfMailMergeOptions.Styles.get -> System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfFlowTextStyle!>!
Fluent.Pdf.PdfMargins
Fluent.Pdf.PdfMargins.Bottom.get -> double
Fluent.Pdf.PdfMargins.Left.get -> double
Fluent.Pdf.PdfMargins.PdfMargins() -> void
Fluent.Pdf.PdfMargins.PdfMargins(double left, double top, double right, double bottom) -> void
Fluent.Pdf.PdfMargins.Right.get -> double
Fluent.Pdf.PdfMargins.Top.get -> double
Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfMatrix.A.get -> double
Fluent.Pdf.PdfMatrix.B.get -> double
Fluent.Pdf.PdfMatrix.C.get -> double
Fluent.Pdf.PdfMatrix.D.get -> double
Fluent.Pdf.PdfMatrix.E.get -> double
Fluent.Pdf.PdfMatrix.F.get -> double
Fluent.Pdf.PdfMatrix.Invert() -> Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfMatrix.Multiply(Fluent.Pdf.PdfMatrix right) -> Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfMatrix.PdfMatrix() -> void
Fluent.Pdf.PdfMatrix.PdfMatrix(double a, double b, double c, double d, double e, double f) -> void
Fluent.Pdf.PdfMatrix.Transform(Fluent.Pdf.PdfPoint point) -> Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfMatrixIccTransform
Fluent.Pdf.PdfMergeBlock
Fluent.Pdf.PdfMergeCondition
Fluent.Pdf.PdfMergeCondition.Path.get -> string!
Fluent.Pdf.PdfMergeCondition.PdfMergeCondition(string! path, Fluent.Pdf.PdfMergeBlock![]! whenTrue, Fluent.Pdf.PdfMergeBlock![]? whenFalse = null) -> void
Fluent.Pdf.PdfMergeCondition.WhenFalse.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfMergeBlock!>!
Fluent.Pdf.PdfMergeCondition.WhenTrue.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfMergeBlock!>!
Fluent.Pdf.PdfMergeEvaluationOptions
Fluent.Pdf.PdfMergeEvaluationOptions.Culture.get -> System.Globalization.CultureInfo!
Fluent.Pdf.PdfMergeEvaluationOptions.MaximumRecords.get -> int
Fluent.Pdf.PdfMergeEvaluationOptions.MaximumVisits.get -> int
Fluent.Pdf.PdfMergeEvaluationOptions.MissingFields.get -> Fluent.Pdf.PdfMergeMissingField
Fluent.Pdf.PdfMergeEvaluationOptions.PdfMergeEvaluationOptions(string! cultureName, Fluent.Pdf.PdfMergeMissingField missingFields = Fluent.Pdf.PdfMergeMissingField.Error, int maximumVisits = 100000, int maximumRecords = 100000) -> void
Fluent.Pdf.PdfMergeImage
Fluent.Pdf.PdfMergeImage.Height.get -> double
Fluent.Pdf.PdfMergeImage.Width.get -> double
Fluent.Pdf.PdfMergeImageBlock
Fluent.Pdf.PdfMergeImageBlock.Path.get -> string!
Fluent.Pdf.PdfMergeImageBlock.PdfMergeImageBlock(string! path) -> void
Fluent.Pdf.PdfMergeMissingField
Fluent.Pdf.PdfMergeMissingField.Empty = 1 -> Fluent.Pdf.PdfMergeMissingField
Fluent.Pdf.PdfMergeMissingField.Error = 0 -> Fluent.Pdf.PdfMergeMissingField
Fluent.Pdf.PdfMergeMissingField.PreserveScalar = 2 -> Fluent.Pdf.PdfMergeMissingField
Fluent.Pdf.PdfMergeParagraph
Fluent.Pdf.PdfMergeParagraph.PdfMergeParagraph(Fluent.Pdf.PdfMergeRun![]! runs, Fluent.Pdf.PdfFlowParagraphStyle? style = null) -> void
Fluent.Pdf.PdfMergeParagraph.Runs.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfMergeRun!>!
Fluent.Pdf.PdfMergeParagraph.Style.get -> Fluent.Pdf.PdfFlowParagraphStyle!
Fluent.Pdf.PdfMergeRecord
Fluent.Pdf.PdfMergeRecord.Fields.get -> System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfMergeValue!>!
Fluent.Pdf.PdfMergeRecord.PdfMergeRecord(System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfMergeValue!>! fields) -> void
Fluent.Pdf.PdfMergeRepeat
Fluent.Pdf.PdfMergeRepeat.Children.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfMergeBlock!>!
Fluent.Pdf.PdfMergeRepeat.Path.get -> string!
Fluent.Pdf.PdfMergeRepeat.PdfMergeRepeat(string! path, params Fluent.Pdf.PdfMergeBlock![]! children) -> void
Fluent.Pdf.PdfMergeRun
Fluent.Pdf.PdfMergeRun.Format.get -> string?
Fluent.Pdf.PdfMergeRun.IsField.get -> bool
Fluent.Pdf.PdfMergeRun.Style.get -> string!
Fluent.Pdf.PdfMergeRun.Value.get -> string!
Fluent.Pdf.PdfMergeTable
Fluent.Pdf.PdfMergeTable.Columns.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfMergeTableColumn!>!
Fluent.Pdf.PdfMergeTable.Path.get -> string!
Fluent.Pdf.PdfMergeTable.PdfMergeTable(string! path, params Fluent.Pdf.PdfMergeTableColumn![]! columns) -> void
Fluent.Pdf.PdfMergeTableColumn
Fluent.Pdf.PdfMergeTableColumn.Column.get -> Fluent.Pdf.PdfTableColumn!
Fluent.Pdf.PdfMergeTableColumn.Header.get -> string?
Fluent.Pdf.PdfMergeTableColumn.PdfMergeTableColumn(Fluent.Pdf.PdfTableColumn! column, Fluent.Pdf.PdfMergeRun![]! runs, string? header = null) -> void
Fluent.Pdf.PdfMergeTableColumn.Runs.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfMergeRun!>!
Fluent.Pdf.PdfMergeTemplate
Fluent.Pdf.PdfMergeTemplate.Blocks.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfMergeBlock!>!
Fluent.Pdf.PdfMergeTemplate.Identifier.get -> string!
Fluent.Pdf.PdfMergeTemplate.PdfMergeTemplate(string! identifier, params Fluent.Pdf.PdfMergeBlock![]! blocks) -> void
Fluent.Pdf.PdfMergeValue
Fluent.Pdf.PdfMergedCellCsvPolicy
Fluent.Pdf.PdfMergedCellCsvPolicy.AnchorOnly = 0 -> Fluent.Pdf.PdfMergedCellCsvPolicy
Fluent.Pdf.PdfMergedCellCsvPolicy.RepeatText = 1 -> Fluent.Pdf.PdfMergedCellCsvPolicy
Fluent.Pdf.PdfMetadata
Fluent.Pdf.PdfMetadataEditor
Fluent.Pdf.PdfMetadataEditor.GetInformation(string! name) -> string?
Fluent.Pdf.PdfMetadataEditor.RemoveIdentity() -> Fluent.Pdf.PdfMetadataEditor!
Fluent.Pdf.PdfMetadataEditor.SetCreationTime(System.DateTimeOffset? value) -> Fluent.Pdf.PdfMetadataEditor!
Fluent.Pdf.PdfMetadataEditor.SetIdentity(byte[]! permanent, byte[]? revision = null) -> Fluent.Pdf.PdfMetadataEditor!
Fluent.Pdf.PdfMetadataEditor.SetInformation(string! name, string? value) -> Fluent.Pdf.PdfMetadataEditor!
Fluent.Pdf.PdfMetadataEditor.SetInformationText(string! name, Fluent.Pdf.PdfTextString! text) -> Fluent.Pdf.PdfMetadataEditor!
Fluent.Pdf.PdfMetadataEditor.SetModificationTime(System.DateTimeOffset? value) -> Fluent.Pdf.PdfMetadataEditor!
Fluent.Pdf.PdfMetadataEditor.SetXmp(string? xml) -> Fluent.Pdf.PdfMetadataEditor!
Fluent.Pdf.PdfMetadataEditor.SetXmpProperty(string! namespaceUri, string! localName, string? value) -> Fluent.Pdf.PdfMetadataEditor!
Fluent.Pdf.PdfMetadataEditor.Synchronize(Fluent.Pdf.PdfMetadataSynchronization authority) -> Fluent.Pdf.PdfMetadataEditor!
Fluent.Pdf.PdfMetadataEmbeddedFilePolicy
Fluent.Pdf.PdfMetadataEmbeddedFilePolicy.Reject = 0 -> Fluent.Pdf.PdfMetadataEmbeddedFilePolicy
Fluent.Pdf.PdfMetadataEmbeddedFilePolicy.Retain = 1 -> Fluent.Pdf.PdfMetadataEmbeddedFilePolicy
Fluent.Pdf.PdfMetadataRemovalOptions
Fluent.Pdf.PdfMetadataRemovalOptions.EmbeddedFiles.get -> Fluent.Pdf.PdfMetadataEmbeddedFilePolicy
Fluent.Pdf.PdfMetadataRemovalOptions.PdfMetadataRemovalOptions(bool removeIdentifiers = true, Fluent.Pdf.PdfMetadataEmbeddedFilePolicy embeddedFiles = Fluent.Pdf.PdfMetadataEmbeddedFilePolicy.Reject) -> void
Fluent.Pdf.PdfMetadataRemovalOptions.RemoveIdentifiers.get -> bool
Fluent.Pdf.PdfMetadataSet
Fluent.Pdf.PdfMetadataSet.Identity.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfMetadataSet.Information.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfMetadataSet.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfMetadataSet.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfMetadataSet.Text.get -> System.Collections.Generic.IReadOnlyDictionary<string!, string!>!
Fluent.Pdf.PdfMetadataSet.XmpXml.get -> string?
Fluent.Pdf.PdfMetadataSynchronization
Fluent.Pdf.PdfMetadataSynchronization.InformationToXmp = 0 -> Fluent.Pdf.PdfMetadataSynchronization
Fluent.Pdf.PdfMetadataSynchronization.PreferNewest = 2 -> Fluent.Pdf.PdfMetadataSynchronization
Fluent.Pdf.PdfMetadataSynchronization.XmpToInformation = 1 -> Fluent.Pdf.PdfMetadataSynchronization
Fluent.Pdf.PdfMissingDestinationPolicy
Fluent.Pdf.PdfMissingDestinationPolicy.OmitWithDiagnostic = 1 -> Fluent.Pdf.PdfMissingDestinationPolicy
Fluent.Pdf.PdfMissingDestinationPolicy.Reject = 0 -> Fluent.Pdf.PdfMissingDestinationPolicy
Fluent.Pdf.PdfNamedDestination
Fluent.Pdf.PdfNamedDestination.Definition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfNamedDestination.Name.get -> string!
Fluent.Pdf.PdfNamedDestination.PageIndex.get -> int
Fluent.Pdf.PdfNativeAsset
Fluent.Pdf.PdfNativeAsset.FileName.get -> string!
Fluent.Pdf.PdfNativeAsset.IsNativeBinary.get -> bool
Fluent.Pdf.PdfNativeAsset.Length.get -> long
Fluent.Pdf.PdfNativeAsset.PdfNativeAsset(string! fileName, long length, string! sha256, bool isNativeBinary = true) -> void
Fluent.Pdf.PdfNativeAsset.Sha256.get -> string!
Fluent.Pdf.PdfNativeDeployment
Fluent.Pdf.PdfNativeDeployment.Abi.get -> string!
Fluent.Pdf.PdfNativeDeployment.Assets.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfNativeAsset!>!
Fluent.Pdf.PdfNativeDeployment.EngineId.get -> string!
Fluent.Pdf.PdfNativeDeployment.EngineVersion.get -> string!
Fluent.Pdf.PdfNativeDeployment.MaximumTotalBytes.get -> long
Fluent.Pdf.PdfNativeDeployment.PdfNativeDeployment(string! rootDirectory, string! runtimeIdentifier, string! engineId, string! engineVersion, string! abi, long maximumTotalBytes, params Fluent.Pdf.PdfNativeAsset![]! assets) -> void
Fluent.Pdf.PdfNativeDeployment.RootDirectory.get -> string!
Fluent.Pdf.PdfNativeDeployment.RuntimeIdentifier.get -> string!
Fluent.Pdf.PdfNativeDeploymentInspector
Fluent.Pdf.PdfNativeDeploymentReport
Fluent.Pdf.PdfNativeDeploymentReport.AssetsVerified.get -> bool
Fluent.Pdf.PdfNativeDeploymentReport.CurrentRuntimeIdentifier.get -> string?
Fluent.Pdf.PdfNativeDeploymentReport.Deployment.get -> Fluent.Pdf.PdfNativeDeployment!
Fluent.Pdf.PdfNativeDeploymentReport.Diagnostics.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfDiagnostic!>!
Fluent.Pdf.PdfNativeDeploymentReport.VerifiedAssetCount.get -> int
Fluent.Pdf.PdfNavigation
Fluent.Pdf.PdfNavigationSet
Fluent.Pdf.PdfNavigationSet.Actions.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfActionRecord!>!
Fluent.Pdf.PdfNavigationSet.NamedDestinations.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfNamedDestination!>!
Fluent.Pdf.PdfNavigationSet.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfNavigationSet.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfObjectId.Equals(Fluent.Pdf.PdfObjectId other) -> bool
Fluent.Pdf.PdfObjectId.Generation.get -> int
Fluent.Pdf.PdfObjectId.Number.get -> int
Fluent.Pdf.PdfObjectId.PdfObjectId() -> void
Fluent.Pdf.PdfObjectId.PdfObjectId(int number, int generation = 0) -> void
Fluent.Pdf.PdfOcr
Fluent.Pdf.PdfOcrAccuracy
Fluent.Pdf.PdfOcrAccuracyGroup
Fluent.Pdf.PdfOcrAccuracyGroup.CharacterErrorRate.get -> double?
Fluent.Pdf.PdfOcrAccuracyGroup.CharacterErrors.get -> long
Fluent.Pdf.PdfOcrAccuracyGroup.Language.get -> string!
Fluent.Pdf.PdfOcrAccuracyGroup.ModelIdentity.get -> string!
Fluent.Pdf.PdfOcrAccuracyGroup.ReferenceCharacters.get -> long
Fluent.Pdf.PdfOcrAccuracyGroup.ReferenceWords.get -> long
Fluent.Pdf.PdfOcrAccuracyGroup.Samples.get -> int
Fluent.Pdf.PdfOcrAccuracyGroup.Thresholds.get -> Fluent.Pdf.PdfOcrAccuracyThresholds!
Fluent.Pdf.PdfOcrAccuracyGroup.ThresholdsMet.get -> bool
Fluent.Pdf.PdfOcrAccuracyGroup.WordErrorRate.get -> double?
Fluent.Pdf.PdfOcrAccuracyGroup.WordErrors.get -> long
Fluent.Pdf.PdfOcrAccuracyOptions
Fluent.Pdf.PdfOcrAccuracyOptions.MaximumCharactersPerSample.get -> int
Fluent.Pdf.PdfOcrAccuracyOptions.MaximumComparisonCells.get -> long
Fluent.Pdf.PdfOcrAccuracyOptions.MaximumSamples.get -> int
Fluent.Pdf.PdfOcrAccuracyOptions.PdfOcrAccuracyOptions(System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfOcrAccuracyThresholds!>! thresholds, int maximumSamples = 10000, long maximumComparisonCells = 16000000, int maximumCharactersPerSample = 1000000) -> void
Fluent.Pdf.PdfOcrAccuracyOptions.Thresholds.get -> System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfOcrAccuracyThresholds!>!
Fluent.Pdf.PdfOcrAccuracyReport
Fluent.Pdf.PdfOcrAccuracyReport.ComparisonCells.get -> long
Fluent.Pdf.PdfOcrAccuracyReport.CorpusIdentifier.get -> string!
Fluent.Pdf.PdfOcrAccuracyReport.CorpusVersion.get -> string!
Fluent.Pdf.PdfOcrAccuracyReport.Groups.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfOcrAccuracyGroup!>!
Fluent.Pdf.PdfOcrAccuracyReport.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfOcrAccuracyReport.RecognitionSha256.get -> string!
Fluent.Pdf.PdfOcrAccuracyReport.ReferenceSha256.get -> string!
Fluent.Pdf.PdfOcrAccuracySample
Fluent.Pdf.PdfOcrAccuracySample.Identifier.get -> string!
Fluent.Pdf.PdfOcrAccuracySample.Language.get -> string!
Fluent.Pdf.PdfOcrAccuracySample.ModelIdentity.get -> string!
Fluent.Pdf.PdfOcrAccuracySample.PdfOcrAccuracySample(string! identifier, string! language, string! modelIdentity, string! referenceText, string! recognizedText) -> void
Fluent.Pdf.PdfOcrAccuracySample.RecognizedText.get -> string!
Fluent.Pdf.PdfOcrAccuracySample.ReferenceText.get -> string!
Fluent.Pdf.PdfOcrAccuracyThresholds
Fluent.Pdf.PdfOcrAccuracyThresholds.MaximumCharacterErrorRate.get -> double
Fluent.Pdf.PdfOcrAccuracyThresholds.MaximumWordErrorRate.get -> double
Fluent.Pdf.PdfOcrAccuracyThresholds.PdfOcrAccuracyThresholds(double maximumCharacterErrorRate, double maximumWordErrorRate) -> void
Fluent.Pdf.PdfOcrExistingTextPolicy
Fluent.Pdf.PdfOcrExistingTextPolicy.AllowDuplicates = 2 -> Fluent.Pdf.PdfOcrExistingTextPolicy
Fluent.Pdf.PdfOcrExistingTextPolicy.RejectOverlap = 1 -> Fluent.Pdf.PdfOcrExistingTextPolicy
Fluent.Pdf.PdfOcrExistingTextPolicy.SkipOverlapping = 0 -> Fluent.Pdf.PdfOcrExistingTextPolicy
Fluent.Pdf.PdfOcrLanguageModel
Fluent.Pdf.PdfOcrLanguageModel.ByteLength.get -> long
Fluent.Pdf.PdfOcrLanguageModel.FileName.get -> string!
Fluent.Pdf.PdfOcrLanguageModel.Identifier.get -> string!
Fluent.Pdf.PdfOcrLanguageModel.Languages.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfOcrLanguageModel.License.get -> string!
Fluent.Pdf.PdfOcrLanguageModel.PdfOcrLanguageModel(string! identifier, string! version, string! sha256, long byteLength, string! license, string! sourceId, string![]! scripts, string![]! languages) -> void
Fluent.Pdf.PdfOcrLanguageModel.Scripts.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfOcrLanguageModel.Sha256.get -> string!
Fluent.Pdf.PdfOcrLanguageModel.SourceId.get -> string!
Fluent.Pdf.PdfOcrLanguageModel.Version.get -> string!
Fluent.Pdf.PdfOcrModelCatalog
Fluent.Pdf.PdfOcrModelCatalog.Models.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfOcrLanguageModel!>!
Fluent.Pdf.PdfOcrModelCatalog.PdfOcrModelCatalog(params Fluent.Pdf.PdfOcrLanguageModel![]! models) -> void
Fluent.Pdf.PdfOcrModelCatalog.Select(string! language, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOcrModelSelection!
Fluent.Pdf.PdfOcrModelSelection
Fluent.Pdf.PdfOcrModelSelection.Identity.get -> string!
Fluent.Pdf.PdfOcrModelSelection.Language.get -> string!
Fluent.Pdf.PdfOcrModelSelection.Models.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfOcrLanguageModel!>!
Fluent.Pdf.PdfOcrModelSelection.OperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfOcrOptions
Fluent.Pdf.PdfOcrOptions.Language.get -> string!
Fluent.Pdf.PdfOcrOptions.MaximumCharacters.get -> int
Fluent.Pdf.PdfOcrOptions.MaximumWords.get -> int
Fluent.Pdf.PdfOcrOptions.PdfOcrOptions(string! language, Fluent.Pdf.PdfOcrSegmentation segmentation = Fluent.Pdf.PdfOcrSegmentation.Automatic, int maximumCharacters = 100000, int maximumWords = 100000) -> void
Fluent.Pdf.PdfOcrOptions.Segmentation.get -> Fluent.Pdf.PdfOcrSegmentation
Fluent.Pdf.PdfOcrOutput
Fluent.Pdf.PdfOcrOutputFormat
Fluent.Pdf.PdfOcrOutputFormat.AltoXml = 3 -> Fluent.Pdf.PdfOcrOutputFormat
Fluent.Pdf.PdfOcrOutputFormat.HOcr = 2 -> Fluent.Pdf.PdfOcrOutputFormat
Fluent.Pdf.PdfOcrOutputFormat.Json = 1 -> Fluent.Pdf.PdfOcrOutputFormat
Fluent.Pdf.PdfOcrOutputFormat.PlainText = 0 -> Fluent.Pdf.PdfOcrOutputFormat
Fluent.Pdf.PdfOcrOverlayOptions
Fluent.Pdf.PdfOcrOverlayOptions.ExistingTextPolicy.get -> Fluent.Pdf.PdfOcrExistingTextPolicy
Fluent.Pdf.PdfOcrOverlayOptions.MaximumOverlapComparisons.get -> long
Fluent.Pdf.PdfOcrOverlayOptions.MaximumWords.get -> int
Fluent.Pdf.PdfOcrOverlayOptions.OverlapCoverage.get -> double
Fluent.Pdf.PdfOcrOverlayOptions.PdfOcrOverlayOptions(Fluent.Pdf.PdfOcrExistingTextPolicy existingTextPolicy = Fluent.Pdf.PdfOcrExistingTextPolicy.SkipOverlapping, double overlapCoverage = 0.8, int maximumWords = 10000, long maximumOverlapComparisons = 1000000) -> void
Fluent.Pdf.PdfOcrPageFrame
Fluent.Pdf.PdfOcrPageFrame.DisplayedRegion.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfOcrPageFrame.Geometry.get -> Fluent.Pdf.PdfPageGeometry!
Fluent.Pdf.PdfOcrPageFrame.PdfOcrPageFrame(Fluent.Pdf.PdfPageGeometry! geometry, int rasterWidth, int rasterHeight, Fluent.Pdf.PdfRect? displayedRegion = null) -> void
Fluent.Pdf.PdfOcrPageFrame.PixelToUser.get -> Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfOcrPageFrame.RasterHeight.get -> int
Fluent.Pdf.PdfOcrPageFrame.RasterWidth.get -> int
Fluent.Pdf.PdfOcrPixelFormat
Fluent.Pdf.PdfOcrPixelFormat.Bgra32 = 2 -> Fluent.Pdf.PdfOcrPixelFormat
Fluent.Pdf.PdfOcrPixelFormat.Gray8 = 0 -> Fluent.Pdf.PdfOcrPixelFormat
Fluent.Pdf.PdfOcrPixelFormat.Rgb24 = 1 -> Fluent.Pdf.PdfOcrPixelFormat
Fluent.Pdf.PdfOcrRaster
Fluent.Pdf.PdfOcrRaster.ByteLength.get -> long
Fluent.Pdf.PdfOcrRaster.Dpi.get -> int
Fluent.Pdf.PdfOcrRaster.Height.get -> int
Fluent.Pdf.PdfOcrRaster.OpenPixels(Fluent.Pdf.PdfOperationContext! context) -> System.IO.Stream!
Fluent.Pdf.PdfOcrRaster.OperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfOcrRaster.PixelFormat.get -> Fluent.Pdf.PdfOcrPixelFormat
Fluent.Pdf.PdfOcrRaster.Stride.get -> int
Fluent.Pdf.PdfOcrRaster.Width.get -> int
Fluent.Pdf.PdfOcrResult
Fluent.Pdf.PdfOcrResult.EngineVersion.get -> string!
Fluent.Pdf.PdfOcrResult.Language.get -> string!
Fluent.Pdf.PdfOcrResult.MeanConfidence.get -> double?
Fluent.Pdf.PdfOcrResult.ModelIdentity.get -> string!
Fluent.Pdf.PdfOcrResult.Models.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfOcrLanguageModel!>!
Fluent.Pdf.PdfOcrResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfOcrResult.PdfOcrResult(string! text, Fluent.Pdf.PdfOcrWord![]! words, string! language, string! engineVersion, string! modelIdentity, double? meanConfidence = null) -> void
Fluent.Pdf.PdfOcrResult.Text.get -> string!
Fluent.Pdf.PdfOcrResult.Words.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfOcrWord!>!
Fluent.Pdf.PdfOcrSearchable
Fluent.Pdf.PdfOcrSegmentation
Fluent.Pdf.PdfOcrSegmentation.Automatic = 0 -> Fluent.Pdf.PdfOcrSegmentation
Fluent.Pdf.PdfOcrSegmentation.SingleBlock = 1 -> Fluent.Pdf.PdfOcrSegmentation
Fluent.Pdf.PdfOcrSegmentation.SingleLine = 2 -> Fluent.Pdf.PdfOcrSegmentation
Fluent.Pdf.PdfOcrSegmentation.SparseText = 3 -> Fluent.Pdf.PdfOcrSegmentation
Fluent.Pdf.PdfOcrWord
Fluent.Pdf.PdfOcrWord.Block.get -> int
Fluent.Pdf.PdfOcrWord.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfOcrWord.Confidence.get -> double?
Fluent.Pdf.PdfOcrWord.Line.get -> int
Fluent.Pdf.PdfOcrWord.Paragraph.get -> int
Fluent.Pdf.PdfOcrWord.PdfOcrWord(string! text, Fluent.Pdf.PdfRect bounds, double? confidence, int block, int paragraph, int line, int word) -> void
Fluent.Pdf.PdfOcrWord.Text.get -> string!
Fluent.Pdf.PdfOcrWord.Word.get -> int
Fluent.Pdf.PdfOperationContext
Fluent.Pdf.PdfOperationContext.BorrowExpandedInput(System.IO.Stream! decodedInput) -> System.IO.Stream!
Fluent.Pdf.PdfOperationContext.CancellationToken.get -> System.Threading.CancellationToken
Fluent.Pdf.PdfOperationContext.Capability.get -> Fluent.Pdf.PdfCapability
Fluent.Pdf.PdfOperationContext.Checkpoint() -> void
Fluent.Pdf.PdfOperationContext.Consume(Fluent.Pdf.PdfResourceKind resource, long amount) -> void
Fluent.Pdf.PdfOperationContext.ConsumePixels(long width, long height) -> void
Fluent.Pdf.PdfOperationContext.EnterScope(Fluent.Pdf.PdfResourceKind resource) -> System.IDisposable!
Fluent.Pdf.PdfOperationContext.EnterTimeLimit(System.TimeSpan maximumElapsedTime) -> System.IDisposable!
Fluent.Pdf.PdfOperationContext.Operation.get -> string!
Fluent.Pdf.PdfOperationContext.Policy.get -> Fluent.Pdf.PdfOperationPolicy!
Fluent.Pdf.PdfOperationContext.ProviderId.get -> string!
Fluent.Pdf.PdfOperationContext.Report(Fluent.Pdf.PdfDiagnostic! diagnostic) -> void
Fluent.Pdf.PdfOperationPolicy
Fluent.Pdf.PdfOperationPolicy.Limits.get -> Fluent.Pdf.PdfResourceLimits!
Fluent.Pdf.PdfOperationPolicy.Mode.get -> Fluent.Pdf.PdfProcessingMode
Fluent.Pdf.PdfOperationPolicy.PdfOperationPolicy(Fluent.Pdf.PdfProcessingMode mode = Fluent.Pdf.PdfProcessingMode.Strict, Fluent.Pdf.PdfResourceLimits? limits = null) -> void
Fluent.Pdf.PdfOperationReport
Fluent.Pdf.PdfOperationReport.Diagnostics.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfDiagnostic!>!
Fluent.Pdf.PdfOperationReport.ElapsedTime.get -> System.TimeSpan
Fluent.Pdf.PdfOperationReport.Failure.get -> Fluent.Pdf.PdfDiagnostic?
Fluent.Pdf.PdfOperationReport.Operation.get -> string!
Fluent.Pdf.PdfOperationReport.ProviderId.get -> string!
Fluent.Pdf.PdfOperationReport.Resources.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfResourceUsage!>!
Fluent.Pdf.PdfOperationReport.Succeeded.get -> bool
Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.BeginCompatibility = 69 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.BeginMarkedContent = 66 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.BeginMarkedContentProperties = 67 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.BeginText = 31 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.CharacterSpacing = 33 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.Clip = 29 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.ClipEvenOdd = 30 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.CloseFillStroke = 26 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.CloseFillStrokeEvenOdd = 27 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.ClosePath = 17 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.CloseStroke = 20 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.CurveFromCurrent = 15 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.CurveTo = 14 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.CurveToEndpoint = 16 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.Dash = 8 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.DrawObject = 61 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.EndCompatibility = 70 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.EndMarkedContent = 68 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.EndPath = 28 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.EndText = 32 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.ExtendedState = 11 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.Fill = 21 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.FillCmyk = 59 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.FillColor = 52 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.FillColorExtended = 53 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.FillColorSpace = 49 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.FillEvenOdd = 23 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.FillGray = 55 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.FillRgb = 57 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.FillStroke = 24 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.FillStrokeEvenOdd = 25 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.Flatness = 10 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.Font = 37 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.GlyphBounds = 63 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.GlyphWidth = 62 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.HorizontalScale = 35 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.InlineImage = 71 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.Leading = 36 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.LegacyFill = 22 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.LineCap = 5 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.LineJoin = 6 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.LineTo = 13 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.LineWidth = 4 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.MarkPoint = 64 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.MarkPointProperties = 65 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.MiterLimit = 7 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.MoveText = 40 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.MoveTextLeading = 41 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.MoveTo = 12 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.NextLineShowText = 46 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.NextTextLine = 43 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.Rectangle = 18 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.RenderingIntent = 9 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.RestoreState = 2 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.SaveState = 1 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.Shading = 60 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.ShowPositionedText = 45 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.ShowText = 44 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.SpacedNextLineShowText = 47 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.Stroke = 19 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.StrokeCmyk = 58 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.StrokeColor = 50 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.StrokeColorExtended = 51 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.StrokeColorSpace = 48 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.StrokeGray = 54 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.StrokeRgb = 56 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.TextMatrix = 42 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.TextRenderingMode = 38 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.TextRise = 39 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.Transform = 3 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.Unknown = 0 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOperator.WordSpacing = 34 -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfOptimization
Fluent.Pdf.PdfOptimizationOptions
Fluent.Pdf.PdfOptimizationOptions.AllowSignatureInvalidation.get -> bool
Fluent.Pdf.PdfOptimizationOptions.CleanupObjects.get -> bool
Fluent.Pdf.PdfOptimizationOptions.CompressStreams.get -> bool
Fluent.Pdf.PdfOptimizationOptions.DeduplicateResources.get -> bool
Fluent.Pdf.PdfOptimizationOptions.MaximumImageDimension.get -> int
Fluent.Pdf.PdfOptimizationOptions.PdfOptimizationOptions(Fluent.Pdf.PdfOptimizationPreset preset = Fluent.Pdf.PdfOptimizationPreset.Lossless, bool cleanupObjects = true, bool compressStreams = true, bool deduplicateResources = true, bool removeUnusedResources = false, bool allowSignatureInvalidation = false, int maximumImageDimension = 0) -> void
Fluent.Pdf.PdfOptimizationOptions.Preset.get -> Fluent.Pdf.PdfOptimizationPreset
Fluent.Pdf.PdfOptimizationOptions.RemoveUnusedResources.get -> bool
Fluent.Pdf.PdfOptimizationPreset
Fluent.Pdf.PdfOptimizationPreset.CompactImages = 1 -> Fluent.Pdf.PdfOptimizationPreset
Fluent.Pdf.PdfOptimizationPreset.Lossless = 0 -> Fluent.Pdf.PdfOptimizationPreset
Fluent.Pdf.PdfOptimizationResult
Fluent.Pdf.PdfOptimizationResult.CompressedStreams.get -> int
Fluent.Pdf.PdfOptimizationResult.DownsampledImages.get -> int
Fluent.Pdf.PdfOptimizationResult.OpenPdfStream() -> System.IO.Stream!
Fluent.Pdf.PdfOptimizationResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfOptimizationResult.OutputBytes.get -> long
Fluent.Pdf.PdfOptimizationResult.OutputSha256.get -> string!
Fluent.Pdf.PdfOptimizationResult.RemovedObjects.get -> int
Fluent.Pdf.PdfOptimizationResult.RemovedResourceBindings.get -> int
Fluent.Pdf.PdfOptimizationResult.SavedBytes.get -> long
Fluent.Pdf.PdfOptimizationResult.SharedStreams.get -> int
Fluent.Pdf.PdfOptimizationResult.SourceBytes.get -> long
Fluent.Pdf.PdfOptimizationResult.SourceSha256.get -> string!
Fluent.Pdf.PdfOptions
Fluent.Pdf.PdfOptions.DefaultPageSize.get -> Fluent.Pdf.PdfPageSize
Fluent.Pdf.PdfOptions.MaximumPages.get -> int
Fluent.Pdf.PdfOptions.OperationPolicy.get -> Fluent.Pdf.PdfOperationPolicy!
Fluent.Pdf.PdfOptions.PdfOptions(Fluent.Pdf.PdfPageSize? defaultPageSize = null, int maximumPages = 10000, Fluent.Pdf.PdfOperationPolicy? operationPolicy = null) -> void
Fluent.Pdf.PdfOrganizationOptions
Fluent.Pdf.PdfOrganizationOptions.AllowSignatureInvalidation.get -> bool
Fluent.Pdf.PdfOrganizationOptions.FieldCollisions.get -> Fluent.Pdf.PdfFieldCollisionPolicy
Fluent.Pdf.PdfOrganizationOptions.MaximumPages.get -> int
Fluent.Pdf.PdfOrganizationOptions.MissingDestinations.get -> Fluent.Pdf.PdfMissingDestinationPolicy
Fluent.Pdf.PdfOrganizationOptions.PdfOrganizationOptions(bool allowSignatureInvalidation = false, Fluent.Pdf.PdfMissingDestinationPolicy missingDestinations = Fluent.Pdf.PdfMissingDestinationPolicy.Reject, Fluent.Pdf.PdfFieldCollisionPolicy fieldCollisions = Fluent.Pdf.PdfFieldCollisionPolicy.Reject, int maximumPages = 100000) -> void
Fluent.Pdf.PdfOutlineRecord
Fluent.Pdf.PdfOutlineRecord.Children.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfOutlineRecord!>!
Fluent.Pdf.PdfOutlineRecord.Definition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfOutlineRecord.IsOpen.get -> bool
Fluent.Pdf.PdfOutlineRecord.ObjectId.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfOutlineRecord.TargetPageIndex.get -> int?
Fluent.Pdf.PdfOutlineRecord.Title.get -> string!
Fluent.Pdf.PdfPades
Fluent.Pdf.PdfPadesProfile
Fluent.Pdf.PdfPadesProfile.B = 0 -> Fluent.Pdf.PdfPadesProfile
Fluent.Pdf.PdfPadesProfile.LT = 2 -> Fluent.Pdf.PdfPadesProfile
Fluent.Pdf.PdfPadesProfile.LTA = 3 -> Fluent.Pdf.PdfPadesProfile
Fluent.Pdf.PdfPadesProfile.T = 1 -> Fluent.Pdf.PdfPadesProfile
Fluent.Pdf.PdfPadesValidation
Fluent.Pdf.PdfPadesValidation.Completed.get -> bool
Fluent.Pdf.PdfPadesValidation.Compliant.get -> bool
Fluent.Pdf.PdfPadesValidation.PdfPadesValidation(string! sourceSha256, Fluent.Pdf.PdfPadesProfile profile, bool completed, bool compliant) -> void
Fluent.Pdf.PdfPadesValidation.Profile.get -> Fluent.Pdf.PdfPadesProfile
Fluent.Pdf.PdfPadesValidation.SourceSha256.get -> string!
Fluent.Pdf.PdfPage
Fluent.Pdf.PdfPage.CanvasLayers.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfCanvasPageLayer!>!
Fluent.Pdf.PdfPage.Content.get -> Fluent.Pdf.PdfContentProgram?
Fluent.Pdf.PdfPage.Content.set -> void
Fluent.Pdf.PdfPage.Geometry.get -> Fluent.Pdf.PdfPageGeometry!
Fluent.Pdf.PdfPage.Geometry.set -> void
Fluent.Pdf.PdfPage.GraphicsResources.get -> Fluent.Pdf.PdfResourceSet!
Fluent.Pdf.PdfPage.ReadContent(Fluent.Pdf.PdfUnknownOperatorPolicy unknownOperators = Fluent.Pdf.PdfUnknownOperatorPolicy.Reject, string? unknownOperatorVersion = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfContentProgram!
Fluent.Pdf.PdfPage.ReadFonts(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfPageFont!>!
Fluent.Pdf.PdfPage.RemoveFont(string! resourceName) -> bool
Fluent.Pdf.PdfPage.RemoveResource(Fluent.Pdf.PdfGraphicsResourceCategory category, string! resourceName) -> bool
Fluent.Pdf.PdfPage.Rotation.get -> Fluent.Pdf.PdfPageRotation
Fluent.Pdf.PdfPage.Rotation.set -> void
Fluent.Pdf.PdfPage.SetFont(string! resourceName, Fluent.Pdf.PdfFontResource! font) -> void
Fluent.Pdf.PdfPage.SetResource(string! resourceName, Fluent.Pdf.PdfGraphicsResource! resource) -> void
Fluent.Pdf.PdfPage.Size.get -> Fluent.Pdf.PdfPageSize
Fluent.Pdf.PdfPage.Size.set -> void
Fluent.Pdf.PdfPage.Source.get -> Fluent.Pdf.PdfPageSource?
Fluent.Pdf.PdfPageBox
Fluent.Pdf.PdfPageBox.Art = 4 -> Fluent.Pdf.PdfPageBox
Fluent.Pdf.PdfPageBox.Bleed = 2 -> Fluent.Pdf.PdfPageBox
Fluent.Pdf.PdfPageBox.Crop = 1 -> Fluent.Pdf.PdfPageBox
Fluent.Pdf.PdfPageBox.Media = 0 -> Fluent.Pdf.PdfPageBox
Fluent.Pdf.PdfPageBox.Trim = 3 -> Fluent.Pdf.PdfPageBox
Fluent.Pdf.PdfPageBoxes
Fluent.Pdf.PdfPageBoxes.Art.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfPageBoxes.Bleed.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfPageBoxes.Crop.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfPageBoxes.Equals(Fluent.Pdf.PdfPageBoxes? other) -> bool
Fluent.Pdf.PdfPageBoxes.Media.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfPageBoxes.PdfPageBoxes(Fluent.Pdf.PdfRect media, Fluent.Pdf.PdfRect? crop = null, Fluent.Pdf.PdfRect? bleed = null, Fluent.Pdf.PdfRect? trim = null, Fluent.Pdf.PdfRect? art = null) -> void
Fluent.Pdf.PdfPageBoxes.Trim.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfPageBoxes.this[Fluent.Pdf.PdfPageBox box].get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfPageCollection
Fluent.Pdf.PdfPageCollection.Add() -> Fluent.Pdf.PdfPage!
Fluent.Pdf.PdfPageCollection.Add(Fluent.Pdf.PdfPageGeometry! geometry) -> Fluent.Pdf.PdfPage!
Fluent.Pdf.PdfPageCollection.Add(Fluent.Pdf.PdfPageSize size) -> Fluent.Pdf.PdfPage!
Fluent.Pdf.PdfPageCollection.Clear() -> void
Fluent.Pdf.PdfPageCollection.Count.get -> int
Fluent.Pdf.PdfPageCollection.GetEnumerator() -> System.Collections.Generic.IEnumerator<Fluent.Pdf.PdfPage!>!
Fluent.Pdf.PdfPageCollection.Move(int oldIndex, int newIndex) -> void
Fluent.Pdf.PdfPageCollection.RemoveAt(int index) -> void
Fluent.Pdf.PdfPageCollection.this[int index].get -> Fluent.Pdf.PdfPage!
Fluent.Pdf.PdfPageDecoration
Fluent.Pdf.PdfPageFont
Fluent.Pdf.PdfPageFont.BaseFont.get -> string!
Fluent.Pdf.PdfPageFont.DecodeText(Fluent.Pdf.PdfValue! encodedText, System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfUnicodeMap!>? inheritedMaps = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDecodedText!
Fluent.Pdf.PdfPageFont.ExtractProgram(Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDecodedData!
Fluent.Pdf.PdfPageFont.Load(Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFont!
Fluent.Pdf.PdfPageFont.ReadToUnicode(System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfUnicodeMap!>? inheritedMaps = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfUnicodeMap!
Fluent.Pdf.PdfPageFont.ResourceName.get -> string!
Fluent.Pdf.PdfPageFont.SourceDictionary.get -> Fluent.Pdf.PdfValue?
Fluent.Pdf.PdfPageFont.Subtype.get -> string!
Fluent.Pdf.PdfPageGeometry
Fluent.Pdf.PdfPageGeometry.Boxes.get -> Fluent.Pdf.PdfPageBoxes!
Fluent.Pdf.PdfPageGeometry.DisplayBounds(Fluent.Pdf.PdfPageBox box = Fluent.Pdf.PdfPageBox.Crop) -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfPageGeometry.DisplayToUser(Fluent.Pdf.PdfPageBox box = Fluent.Pdf.PdfPageBox.Crop) -> Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfPageGeometry.Equals(Fluent.Pdf.PdfPageGeometry? other) -> bool
Fluent.Pdf.PdfPageGeometry.PdfPageGeometry(Fluent.Pdf.PdfPageBoxes! boxes, Fluent.Pdf.PdfPageRotation rotation = Fluent.Pdf.PdfPageRotation.None, double userUnit = 1) -> void
Fluent.Pdf.PdfPageGeometry.Resize(Fluent.Pdf.PdfPageSize size) -> Fluent.Pdf.PdfPageGeometry!
Fluent.Pdf.PdfPageGeometry.Rotation.get -> Fluent.Pdf.PdfPageRotation
Fluent.Pdf.PdfPageGeometry.Size.get -> Fluent.Pdf.PdfPageSize
Fluent.Pdf.PdfPageGeometry.UserToDisplay(Fluent.Pdf.PdfPageBox box = Fluent.Pdf.PdfPageBox.Crop) -> Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfPageGeometry.UserUnit.get -> double
Fluent.Pdf.PdfPageGeometry.WithRotation(Fluent.Pdf.PdfPageRotation rotation) -> Fluent.Pdf.PdfPageGeometry!
Fluent.Pdf.PdfPageGraphics
Fluent.Pdf.PdfPageGraphics.ExtractImage(int occurrenceIndex, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfImageAsset!
Fluent.Pdf.PdfPageGraphics.ExtractResource(int occurrenceIndex, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfGraphicsResource!
Fluent.Pdf.PdfPageGraphics.Occurrences.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfGraphicsOccurrence!>!
Fluent.Pdf.PdfPageGraphics.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfPageGraphics.PageIndex.get -> int
Fluent.Pdf.PdfPageGraphics.Remove(int occurrenceIndex, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfPageGraphics.ReplaceImage(int occurrenceIndex, Fluent.Pdf.PdfImageAsset! replacement, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfPageGraphics.ReplaceResource(int occurrenceIndex, Fluent.Pdf.PdfGraphicsResource! replacement, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfPageGraphics.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfPageImposition
Fluent.Pdf.PdfPageInput
Fluent.Pdf.PdfPageInput.Document.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfPageInput.PdfPageInput(Fluent.Pdf.PdfDocumentSnapshot! document, Fluent.Pdf.PdfPageSelection? selection = null) -> void
Fluent.Pdf.PdfPageInput.Selection.get -> Fluent.Pdf.PdfPageSelection?
Fluent.Pdf.PdfPageLabelStyle
Fluent.Pdf.PdfPageLabelStyle.Decimal = 1 -> Fluent.Pdf.PdfPageLabelStyle
Fluent.Pdf.PdfPageLabelStyle.LowerLetters = 4 -> Fluent.Pdf.PdfPageLabelStyle
Fluent.Pdf.PdfPageLabelStyle.LowerRoman = 2 -> Fluent.Pdf.PdfPageLabelStyle
Fluent.Pdf.PdfPageLabelStyle.None = 0 -> Fluent.Pdf.PdfPageLabelStyle
Fluent.Pdf.PdfPageLabelStyle.UpperLetters = 5 -> Fluent.Pdf.PdfPageLabelStyle
Fluent.Pdf.PdfPageLabelStyle.UpperRoman = 3 -> Fluent.Pdf.PdfPageLabelStyle
Fluent.Pdf.PdfPageLayout
Fluent.Pdf.PdfPageLayout.OneColumn = 1 -> Fluent.Pdf.PdfPageLayout
Fluent.Pdf.PdfPageLayout.SinglePage = 0 -> Fluent.Pdf.PdfPageLayout
Fluent.Pdf.PdfPageLayout.TwoColumnLeft = 2 -> Fluent.Pdf.PdfPageLayout
Fluent.Pdf.PdfPageLayout.TwoColumnRight = 3 -> Fluent.Pdf.PdfPageLayout
Fluent.Pdf.PdfPageLayout.TwoPageLeft = 4 -> Fluent.Pdf.PdfPageLayout
Fluent.Pdf.PdfPageLayout.TwoPageRight = 5 -> Fluent.Pdf.PdfPageLayout
Fluent.Pdf.PdfPageLinkSnapshot
Fluent.Pdf.PdfPageLinkSnapshot.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfPageLinkSnapshot.PdfPageLinkSnapshot() -> void
Fluent.Pdf.PdfPageLinkSnapshot.TargetPageIndex.get -> int
Fluent.Pdf.PdfPageLinkSnapshot.TargetPoint.get -> Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfPageLinkSnapshot.Uri.get -> string?
Fluent.Pdf.PdfPageMode
Fluent.Pdf.PdfPageMode.FullScreen = 3 -> Fluent.Pdf.PdfPageMode
Fluent.Pdf.PdfPageMode.UseAttachments = 5 -> Fluent.Pdf.PdfPageMode
Fluent.Pdf.PdfPageMode.UseNone = 0 -> Fluent.Pdf.PdfPageMode
Fluent.Pdf.PdfPageMode.UseOC = 4 -> Fluent.Pdf.PdfPageMode
Fluent.Pdf.PdfPageMode.UseOutlines = 1 -> Fluent.Pdf.PdfPageMode
Fluent.Pdf.PdfPageMode.UseThumbs = 2 -> Fluent.Pdf.PdfPageMode
Fluent.Pdf.PdfPageNumberRange
Fluent.Pdf.PdfPageNumberRange.FirstNumber.get -> int
Fluent.Pdf.PdfPageNumberRange.PdfPageNumberRange(int startPageIndex, Fluent.Pdf.PdfPageLabelStyle style = Fluent.Pdf.PdfPageLabelStyle.Decimal, int firstNumber = 1, string! prefix = "") -> void
Fluent.Pdf.PdfPageNumberRange.Prefix.get -> string!
Fluent.Pdf.PdfPageNumberRange.StartPageIndex.get -> int
Fluent.Pdf.PdfPageNumberRange.Style.get -> Fluent.Pdf.PdfPageLabelStyle
Fluent.Pdf.PdfPageNumbering
Fluent.Pdf.PdfPageNumbering.GetLabel(int pageIndex) -> string!
Fluent.Pdf.PdfPageNumbering.PdfPageNumbering(params Fluent.Pdf.PdfPageNumberRange![]! ranges) -> void
Fluent.Pdf.PdfPageNumbering.Ranges.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfPageNumberRange!>!
Fluent.Pdf.PdfPageOrganization
Fluent.Pdf.PdfPagePresentationRecord
Fluent.Pdf.PdfPagePresentationRecord.DisplayDuration.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfPagePresentationRecord.PageIndex.get -> int
Fluent.Pdf.PdfPagePresentationRecord.Transition.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfPageResourceManifest
Fluent.Pdf.PdfPageResourceManifest.Artifacts.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedArtifact!>!
Fluent.Pdf.PdfPageResourceManifest.Content.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfResourceContent!>!
Fluent.Pdf.PdfPageResourceManifest.Declarations.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfResourceDeclaration!>!
Fluent.Pdf.PdfPageResourceManifest.Occurrences.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfResourceOccurrence!>!
Fluent.Pdf.PdfPageResourceManifest.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfPageResourceManifest.PageIndex.get -> int
Fluent.Pdf.PdfPageResourceManifest.Paths.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedPath!>!
Fluent.Pdf.PdfPageResourceManifest.Resources.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedResource!>!
Fluent.Pdf.PdfPageResourceManifest.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfPageResourceManifest.Stamps.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfStampInfo!>!
Fluent.Pdf.PdfPageRotation
Fluent.Pdf.PdfPageRotation.Clockwise180 = 180 -> Fluent.Pdf.PdfPageRotation
Fluent.Pdf.PdfPageRotation.Clockwise270 = 270 -> Fluent.Pdf.PdfPageRotation
Fluent.Pdf.PdfPageRotation.Clockwise90 = 90 -> Fluent.Pdf.PdfPageRotation
Fluent.Pdf.PdfPageRotation.None = 0 -> Fluent.Pdf.PdfPageRotation
Fluent.Pdf.PdfPageSelection
Fluent.Pdf.PdfPageSelection.Indices.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfPageSize
Fluent.Pdf.PdfPageSize.Equals(Fluent.Pdf.PdfPageSize other) -> bool
Fluent.Pdf.PdfPageSize.Height.get -> double
Fluent.Pdf.PdfPageSize.IsValid.get -> bool
Fluent.Pdf.PdfPageSize.PdfPageSize() -> void
Fluent.Pdf.PdfPageSize.PdfPageSize(double width, double height) -> void
Fluent.Pdf.PdfPageSize.Width.get -> double
Fluent.Pdf.PdfPageSnapshot
Fluent.Pdf.PdfPageSnapshot.CanvasLayers.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfCanvasPageLayer!>!
Fluent.Pdf.PdfPageSnapshot.Content.get -> Fluent.Pdf.PdfContentProgram?
Fluent.Pdf.PdfPageSnapshot.FontResources.get -> System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfFontResource!>!
Fluent.Pdf.PdfPageSnapshot.Geometry.get -> Fluent.Pdf.PdfPageGeometry!
Fluent.Pdf.PdfPageSnapshot.GraphicsResources.get -> Fluent.Pdf.PdfResourceSet!
Fluent.Pdf.PdfPageSnapshot.Links.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfPageLinkSnapshot>!
Fluent.Pdf.PdfPageSnapshot.PdfPageSnapshot() -> void
Fluent.Pdf.PdfPageSnapshot.Rotation.get -> Fluent.Pdf.PdfPageRotation
Fluent.Pdf.PdfPageSnapshot.Size.get -> Fluent.Pdf.PdfPageSize
Fluent.Pdf.PdfPageSnapshot.Source.get -> Fluent.Pdf.PdfPageSource?
Fluent.Pdf.PdfPageSnapshot.StructureRoles.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfPageSource
Fluent.Pdf.PdfPageSource.Dictionary.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfPageSource.Document.get -> Fluent.Pdf.PdfSyntaxDocument!
Fluent.Pdf.PdfPageSource.EffectiveAttributes.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfPageSource.Geometry.get -> Fluent.Pdf.PdfPageGeometry!
Fluent.Pdf.PdfPageSource.ObjectId.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfPageSource.Rotation.get -> Fluent.Pdf.PdfPageRotation
Fluent.Pdf.PdfPageSource.Size.get -> Fluent.Pdf.PdfPageSize
Fluent.Pdf.PdfPageTemplate
Fluent.Pdf.PdfPageTemplate.Footer.get -> System.Func<Fluent.Pdf.PdfPageTemplateContext!, Fluent.Pdf.PdfPageDecoration?>?
Fluent.Pdf.PdfPageTemplate.FooterHeight.get -> double
Fluent.Pdf.PdfPageTemplate.Gap.get -> double
Fluent.Pdf.PdfPageTemplate.Header.get -> System.Func<Fluent.Pdf.PdfPageTemplateContext!, Fluent.Pdf.PdfPageDecoration?>?
Fluent.Pdf.PdfPageTemplate.HeaderHeight.get -> double
Fluent.Pdf.PdfPageTemplate.MaximumPasses.get -> int
Fluent.Pdf.PdfPageTemplate.Numbering.get -> Fluent.Pdf.PdfPageNumbering!
Fluent.Pdf.PdfPageTemplate.PdfPageTemplate(double headerHeight = 0, double footerHeight = 0, double gap = 6, System.Func<Fluent.Pdf.PdfPageTemplateContext!, Fluent.Pdf.PdfPageDecoration?>? header = null, System.Func<Fluent.Pdf.PdfPageTemplateContext!, Fluent.Pdf.PdfPageDecoration?>? footer = null, Fluent.Pdf.PdfPageNumbering? numbering = null, int maximumPasses = 8) -> void
Fluent.Pdf.PdfPageTemplateContext
Fluent.Pdf.PdfPageTemplateContext.Body.get -> Fluent.Pdf.PdfFlowPage!
Fluent.Pdf.PdfPageTemplateContext.IsFirstPage.get -> bool
Fluent.Pdf.PdfPageTemplateContext.IsOddPage.get -> bool
Fluent.Pdf.PdfPageTemplateContext.OperationContext.get -> Fluent.Pdf.PdfOperationContext!
Fluent.Pdf.PdfPageTemplateContext.PageIndex.get -> int
Fluent.Pdf.PdfPageTemplateContext.PageLabel.get -> string!
Fluent.Pdf.PdfPageTemplateContext.PageNumber.get -> int
Fluent.Pdf.PdfPageTemplateContext.TotalPages.get -> int
Fluent.Pdf.PdfPageTemplateResult
Fluent.Pdf.PdfPageTemplateResult.Body.get -> Fluent.Pdf.PdfFlowResult!
Fluent.Pdf.PdfPageTemplateResult.CreateDocument(Fluent.Pdf.PdfOptions? options = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfPageTemplateResult.Decorations.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfCanvasResult!>!
Fluent.Pdf.PdfPageTemplateResult.Entries.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfContentsEntry!>!
Fluent.Pdf.PdfPageTemplateResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfPageTemplateResult.Passes.get -> int
Fluent.Pdf.PdfPageTemplateResult.Template.get -> Fluent.Pdf.PdfPageTemplate!
Fluent.Pdf.PdfPageTransition
Fluent.Pdf.PdfPageTransition.Dimension.get -> Fluent.Pdf.PdfTransitionDimension?
Fluent.Pdf.PdfPageTransition.Direction.get -> int?
Fluent.Pdf.PdfPageTransition.Duration.get -> double
Fluent.Pdf.PdfPageTransition.Motion.get -> Fluent.Pdf.PdfTransitionMotion?
Fluent.Pdf.PdfPageTransition.PdfPageTransition(Fluent.Pdf.PdfTransitionStyle style, double duration = 1, Fluent.Pdf.PdfTransitionDimension? dimension = null, Fluent.Pdf.PdfTransitionMotion? motion = null, int? direction = null, double? scale = null, bool? rectangular = null) -> void
Fluent.Pdf.PdfPageTransition.Rectangular.get -> bool?
Fluent.Pdf.PdfPageTransition.Scale.get -> double?
Fluent.Pdf.PdfPageTransition.Style.get -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfPaint
Fluent.Pdf.PdfPaint.Color.get -> Fluent.Pdf.PdfColor?
Fluent.Pdf.PdfPaint.Pattern.get -> Fluent.Pdf.PdfPattern?
Fluent.Pdf.PdfParagraphDirection
Fluent.Pdf.PdfParagraphDirection.Auto = 0 -> Fluent.Pdf.PdfParagraphDirection
Fluent.Pdf.PdfParagraphDirection.LeftToRight = 1 -> Fluent.Pdf.PdfParagraphDirection
Fluent.Pdf.PdfParagraphDirection.RightToLeft = 2 -> Fluent.Pdf.PdfParagraphDirection
Fluent.Pdf.PdfPassword
Fluent.Pdf.PdfPassword.Dispose() -> void
Fluent.Pdf.PdfPassword.PdfPassword(byte[]! preparedPasswordBytes) -> void
Fluent.Pdf.PdfPasswordCipher
Fluent.Pdf.PdfPasswordCipher.Aes128 = 1 -> Fluent.Pdf.PdfPasswordCipher
Fluent.Pdf.PdfPasswordCipher.Aes256 = 0 -> Fluent.Pdf.PdfPasswordCipher
Fluent.Pdf.PdfPasswordProtection
Fluent.Pdf.PdfPasswordProtection.Cipher.get -> Fluent.Pdf.PdfPasswordCipher
Fluent.Pdf.PdfPasswordProtection.EncryptMetadata.get -> bool
Fluent.Pdf.PdfPasswordProtection.OwnerPassword.get -> Fluent.Pdf.PdfPassword!
Fluent.Pdf.PdfPasswordProtection.PdfPasswordProtection(Fluent.Pdf.PdfPassword! userPassword, Fluent.Pdf.PdfPassword! ownerPassword, Fluent.Pdf.PdfDocumentPermissions permissions = Fluent.Pdf.PdfDocumentPermissions.All, Fluent.Pdf.PdfPasswordCipher cipher = Fluent.Pdf.PdfPasswordCipher.Aes256, bool encryptMetadata = true) -> void
Fluent.Pdf.PdfPasswordProtection.Permissions.get -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfPasswordProtection.UserPassword.get -> Fluent.Pdf.PdfPassword!
Fluent.Pdf.PdfPath
Fluent.Pdf.PdfPath.Contains(Fluent.Pdf.PdfPoint point, bool evenOdd = false, double tolerance = 1E-09, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> bool
Fluent.Pdf.PdfPath.Edit(int index, int removeCount, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfPathSegment!>! replacement, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPath!
Fluent.Pdf.PdfPath.GetBounds(Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfPath.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfPath.Operations.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfContentOperation!>!
Fluent.Pdf.PdfPath.Segments.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfPathSegment!>!
Fluent.Pdf.PdfPathSegment
Fluent.Pdf.PdfPathSegment.Coordinates.get -> System.Collections.Generic.IReadOnlyList<double>!
Fluent.Pdf.PdfPathSegment.Operation.get -> Fluent.Pdf.PdfOperator
Fluent.Pdf.PdfPattern
Fluent.Pdf.PdfPattern.Colored.get -> bool
Fluent.Pdf.PdfPattern.Resource.get -> Fluent.Pdf.PdfGraphicsResource!
Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfPoint.PdfPoint() -> void
Fluent.Pdf.PdfPoint.PdfPoint(double x, double y) -> void
Fluent.Pdf.PdfPoint.X.get -> double
Fluent.Pdf.PdfPoint.Y.get -> double
Fluent.Pdf.PdfPreflight
Fluent.Pdf.PdfPreflightFinding
Fluent.Pdf.PdfPreflightFinding.Code.get -> string!
Fluent.Pdf.PdfPreflightFinding.Location.get -> string!
Fluent.Pdf.PdfPreflightFinding.LossDescription.get -> string?
Fluent.Pdf.PdfPreflightFinding.RemediationCode.get -> string?
Fluent.Pdf.PdfPreflightFinding.Severity.get -> Fluent.Pdf.PdfDiagnosticSeverity
Fluent.Pdf.PdfPreflightReport
Fluent.Pdf.PdfPreflightReport.AvailableCapabilities.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfCapability>!
Fluent.Pdf.PdfPreflightReport.ConformanceValidated.get -> bool
Fluent.Pdf.PdfPreflightReport.Findings.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfPreflightFinding!>!
Fluent.Pdf.PdfPreflightReport.HasSignatures.get -> bool
Fluent.Pdf.PdfPreflightReport.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfPreflightReport.SourceSha256.get -> string!
Fluent.Pdf.PdfPresentation
Fluent.Pdf.PdfPresentationSet
Fluent.Pdf.PdfPresentationSet.Catalog.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfPresentationSet.Numbering.get -> Fluent.Pdf.PdfPageNumbering?
Fluent.Pdf.PdfPresentationSet.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfPresentationSet.Outlines.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfOutlineRecord!>!
Fluent.Pdf.PdfPresentationSet.PageLabels.get -> System.Collections.Generic.IReadOnlyDictionary<int, Fluent.Pdf.PdfValue!>!
Fluent.Pdf.PdfPresentationSet.Pages.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfPagePresentationRecord!>!
Fluent.Pdf.PdfPresentationSet.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfPrintDuplex
Fluent.Pdf.PdfPrintDuplex.LongEdge = 1 -> Fluent.Pdf.PdfPrintDuplex
Fluent.Pdf.PdfPrintDuplex.ShortEdge = 2 -> Fluent.Pdf.PdfPrintDuplex
Fluent.Pdf.PdfPrintDuplex.Simplex = 0 -> Fluent.Pdf.PdfPrintDuplex
Fluent.Pdf.PdfPrintJob
Fluent.Pdf.PdfPrintJob.JobId.get -> int
Fluent.Pdf.PdfPrintJob.PdfPrintJob(string! printer, int jobId, Fluent.Pdf.PdfPrintJobState state) -> void
Fluent.Pdf.PdfPrintJob.Printer.get -> string!
Fluent.Pdf.PdfPrintJob.State.get -> Fluent.Pdf.PdfPrintJobState
Fluent.Pdf.PdfPrintJobState
Fluent.Pdf.PdfPrintJobState.Aborted = 6 -> Fluent.Pdf.PdfPrintJobState
Fluent.Pdf.PdfPrintJobState.Canceled = 5 -> Fluent.Pdf.PdfPrintJobState
Fluent.Pdf.PdfPrintJobState.CancellationRequested = 8 -> Fluent.Pdf.PdfPrintJobState
Fluent.Pdf.PdfPrintJobState.Completed = 7 -> Fluent.Pdf.PdfPrintJobState
Fluent.Pdf.PdfPrintJobState.Held = 2 -> Fluent.Pdf.PdfPrintJobState
Fluent.Pdf.PdfPrintJobState.Pending = 1 -> Fluent.Pdf.PdfPrintJobState
Fluent.Pdf.PdfPrintJobState.Processing = 3 -> Fluent.Pdf.PdfPrintJobState
Fluent.Pdf.PdfPrintJobState.Stopped = 4 -> Fluent.Pdf.PdfPrintJobState
Fluent.Pdf.PdfPrintJobState.Unknown = 0 -> Fluent.Pdf.PdfPrintJobState
Fluent.Pdf.PdfPrintOptions
Fluent.Pdf.PdfPrintOptions.Copies.get -> int
Fluent.Pdf.PdfPrintOptions.Duplex.get -> Fluent.Pdf.PdfPrintDuplex
Fluent.Pdf.PdfPrintOptions.FirstPage.get -> int
Fluent.Pdf.PdfPrintOptions.LastPage.get -> int
Fluent.Pdf.PdfPrintOptions.NumberUp.get -> int
Fluent.Pdf.PdfPrintOptions.PdfPrintOptions(string! printer, int copies = 1, Fluent.Pdf.PdfPrintDuplex duplex = Fluent.Pdf.PdfPrintDuplex.Simplex, Fluent.Pdf.PdfPrintScaling scaling = Fluent.Pdf.PdfPrintScaling.Fit, int numberUp = 1, int firstPage = 1, int lastPage = 0) -> void
Fluent.Pdf.PdfPrintOptions.Printer.get -> string!
Fluent.Pdf.PdfPrintOptions.Scaling.get -> Fluent.Pdf.PdfPrintScaling
Fluent.Pdf.PdfPrintScaling
Fluent.Pdf.PdfPrintScaling.Fit = 0 -> Fluent.Pdf.PdfPrintScaling
Fluent.Pdf.PdfPrintScaling.None = 1 -> Fluent.Pdf.PdfPrintScaling
Fluent.Pdf.PdfPrinting
Fluent.Pdf.PdfProcessingJob
Fluent.Pdf.PdfProcessingJob.Context.get -> Fluent.Pdf.PdfOperationContext!
Fluent.Pdf.PdfProcessingJob.Document.get -> Fluent.Pdf.PdfDocument!
Fluent.Pdf.PdfProcessingJob.Input.get -> System.IO.Stream!
Fluent.Pdf.PdfProcessingJob.ReadDocument() -> void
Fluent.Pdf.PdfProcessingJob.Save(System.IO.Stream! output, Fluent.Pdf.PdfWriteOptions? writeOptions = null) -> void
Fluent.Pdf.PdfProcessingJob.SaveFile(string! path, Fluent.Pdf.PdfWriteOptions? writeOptions = null) -> void
Fluent.Pdf.PdfProcessingMode
Fluent.Pdf.PdfProcessingMode.Permissive = 1 -> Fluent.Pdf.PdfProcessingMode
Fluent.Pdf.PdfProcessingMode.Strict = 0 -> Fluent.Pdf.PdfProcessingMode
Fluent.Pdf.PdfProcessingOptions
Fluent.Pdf.PdfProcessingOptions.MaximumQueuedJobs.get -> int
Fluent.Pdf.PdfProcessingOptions.MaximumResidentInputBytes.get -> long
Fluent.Pdf.PdfProcessingOptions.MaximumWorkers.get -> int
Fluent.Pdf.PdfProcessingOptions.PdfProcessingOptions(int maximumWorkers = 4, int maximumQueuedJobs = 16, long maximumResidentInputBytes = 67108864) -> void
Fluent.Pdf.PdfProcessingPool
Fluent.Pdf.PdfProcessingPool.CompleteAsync() -> System.Threading.Tasks.Task!
Fluent.Pdf.PdfProcessingPool.Completion.get -> System.Threading.Tasks.Task!
Fluent.Pdf.PdfProcessingPool.Dispose() -> void
Fluent.Pdf.PdfProcessingPool.Options.get -> Fluent.Pdf.PdfProcessingOptions!
Fluent.Pdf.PdfProcessingPool.PdfProcessingPool(Fluent.Pdf.PdfProcessingOptions? options = null, Fluent.Pdf.PdfOptions? documentOptions = null, Fluent.Pdf.PdfProviderRegistry? providers = null) -> void
Fluent.Pdf.PdfProcessingPool.Progress.get -> Fluent.Pdf.PdfProcessingProgress!
Fluent.Pdf.PdfProcessingPool.ShutdownAsync() -> System.Threading.Tasks.Task!
Fluent.Pdf.PdfProcessingPool.TrySubmit(byte[]! input, System.Action<Fluent.Pdf.PdfProcessingJob!>! action, out System.Threading.Tasks.Task<Fluent.Pdf.PdfOperationReport!>? completion, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> bool
Fluent.Pdf.PdfProcessingProgress
Fluent.Pdf.PdfProcessingProgress.Canceled.get -> long
Fluent.Pdf.PdfProcessingProgress.Failed.get -> long
Fluent.Pdf.PdfProcessingProgress.Queued.get -> int
Fluent.Pdf.PdfProcessingProgress.ResidentInputBytes.get -> long
Fluent.Pdf.PdfProcessingProgress.Running.get -> int
Fluent.Pdf.PdfProcessingProgress.Succeeded.get -> long
Fluent.Pdf.PdfProfileConversions
Fluent.Pdf.PdfProfileOptions
Fluent.Pdf.PdfProfileOptions.AllowAdvancedContentLoss.get -> bool
Fluent.Pdf.PdfProfileOptions.AllowSignatureInvalidation.get -> bool
Fluent.Pdf.PdfProfileOptions.OutputColorSpace.get -> Fluent.Pdf.PdfColorSpace?
Fluent.Pdf.PdfProfileOptions.OutputCondition.get -> string!
Fluent.Pdf.PdfProfileOptions.PdfProfileOptions(Fluent.Pdf.PdfConformanceProfile profile, Fluent.Pdf.PdfColorSpace? outputColorSpace = null, string? outputCondition = null, bool allowSignatureInvalidation = false, bool allowAdvancedContentLoss = false) -> void
Fluent.Pdf.PdfProfileOptions.Profile.get -> Fluent.Pdf.PdfConformanceProfile
Fluent.Pdf.PdfProfileValidation
Fluent.Pdf.PdfProfileValidation.Completed.get -> bool
Fluent.Pdf.PdfProfileValidation.Findings.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfProfileValidation.IsCompliant.get -> bool
Fluent.Pdf.PdfProfileValidation.OperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfProfileValidation.PdfProfileValidation(Fluent.Pdf.PdfConformanceProfile profile, string! sourceSha256, string! requestSha256, bool completed, bool isCompliant, System.Collections.Generic.IEnumerable<string!>! findings) -> void
Fluent.Pdf.PdfProfileValidation.Profile.get -> Fluent.Pdf.PdfConformanceProfile
Fluent.Pdf.PdfProfileValidation.RequestSha256.get -> string!
Fluent.Pdf.PdfProfileValidation.SourceSha256.get -> string!
Fluent.Pdf.PdfProgressiveDocument
Fluent.Pdf.PdfProgressiveDocument.DecodeStream(Fluent.Pdf.PdfObjectId id, Fluent.Pdf.PdfProviderRegistry? providers = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDecodedData!
Fluent.Pdf.PdfProgressiveDocument.Dispose() -> void
Fluent.Pdf.PdfProgressiveDocument.InspectLinearization(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfLinearizationInfo?
Fluent.Pdf.PdfProgressiveDocument.PdfProgressiveDocument(Fluent.Pdf.IPdfRangeSource! source, Fluent.Pdf.PdfProgressiveOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null) -> void
Fluent.Pdf.PdfProgressiveDocument.ReadObject(Fluent.Pdf.PdfObjectId id, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfProgressiveDocument.ReadPage(int pageIndex, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfProgressivePage!
Fluent.Pdf.PdfProgressiveOptions
Fluent.Pdf.PdfProgressiveOptions.BlockSize.get -> int
Fluent.Pdf.PdfProgressiveOptions.MaximumCacheBytes.get -> int
Fluent.Pdf.PdfProgressiveOptions.PdfProgressiveOptions(int blockSize = 4096, int maximumCacheBytes = 1048576) -> void
Fluent.Pdf.PdfProgressivePage
Fluent.Pdf.PdfProgressivePage.Dictionary.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfProgressivePage.ObjectId.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfProgressivePage.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfProgressivePage.PageCount.get -> int
Fluent.Pdf.PdfProgressivePage.PageIndex.get -> int
Fluent.Pdf.PdfProviderDescriptor
Fluent.Pdf.PdfProviderDescriptor.Capabilities.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfCapability>!
Fluent.Pdf.PdfProviderDescriptor.Id.get -> string!
Fluent.Pdf.PdfProviderDescriptor.PdfProviderDescriptor(string! id, string! version, params Fluent.Pdf.PdfCapability[]! capabilities) -> void
Fluent.Pdf.PdfProviderDescriptor.Version.get -> string!
Fluent.Pdf.PdfProviderRegistry
Fluent.Pdf.PdfProviderRegistry.Capabilities.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfCapability>!
Fluent.Pdf.PdfProviderRegistry.Descriptors.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfProviderDescriptor!>!
Fluent.Pdf.PdfProviderRegistry.GetRequired<T>(Fluent.Pdf.PdfCapability capability) -> T!
Fluent.Pdf.PdfProviderRegistry.PdfProviderRegistry(params Fluent.Pdf.IPdfProvider![]! providers) -> void
Fluent.Pdf.PdfProviderRegistry.Supports(Fluent.Pdf.PdfCapability capability) -> bool
Fluent.Pdf.PdfProviderUnavailableException
Fluent.Pdf.PdfProviderUnavailableException.PdfProviderUnavailableException(Fluent.Pdf.PdfCapability capability) -> void
Fluent.Pdf.PdfPublicKeyCipher
Fluent.Pdf.PdfPublicKeyCipher.Aes128 = 1 -> Fluent.Pdf.PdfPublicKeyCipher
Fluent.Pdf.PdfPublicKeyCipher.Aes256 = 0 -> Fluent.Pdf.PdfPublicKeyCipher
Fluent.Pdf.PdfQuad
Fluent.Pdf.PdfQuad.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfQuad.Contains(Fluent.Pdf.PdfPoint point) -> bool
Fluent.Pdf.PdfQuad.P0.get -> Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfQuad.P1.get -> Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfQuad.P2.get -> Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfQuad.P3.get -> Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfQuad.PdfQuad() -> void
Fluent.Pdf.PdfQuad.PdfQuad(Fluent.Pdf.PdfPoint p0, Fluent.Pdf.PdfPoint p1, Fluent.Pdf.PdfPoint p2, Fluent.Pdf.PdfPoint p3) -> void
Fluent.Pdf.PdfRangeIdentity
Fluent.Pdf.PdfRangeIdentity.Length.get -> long
Fluent.Pdf.PdfRangeIdentity.PdfRangeIdentity(long length, string! version) -> void
Fluent.Pdf.PdfRangeIdentity.Version.get -> string!
Fluent.Pdf.PdfRangeResponse
Fluent.Pdf.PdfRangeResponse.Body.get -> System.IO.Stream!
Fluent.Pdf.PdfRangeResponse.Dispose() -> void
Fluent.Pdf.PdfRangeResponse.Identity.get -> Fluent.Pdf.PdfRangeIdentity!
Fluent.Pdf.PdfRangeResponse.Length.get -> long
Fluent.Pdf.PdfRangeResponse.Offset.get -> long
Fluent.Pdf.PdfRangeResponse.PdfRangeResponse(long offset, long length, Fluent.Pdf.PdfRangeIdentity! identity, System.IO.Stream! body) -> void
Fluent.Pdf.PdfRaster
Fluent.Pdf.PdfRaster.OpenSamples() -> System.IO.Stream!
Fluent.Pdf.PdfRaster.Report.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfRaster.Request.get -> Fluent.Pdf.PdfRasterRequest!
Fluent.Pdf.PdfRasterColor
Fluent.Pdf.PdfRasterColor.Gray = 1 -> Fluent.Pdf.PdfRasterColor
Fluent.Pdf.PdfRasterColor.Rgb = 0 -> Fluent.Pdf.PdfRasterColor
Fluent.Pdf.PdfRasterDocuments
Fluent.Pdf.PdfRasterExport
Fluent.Pdf.PdfRasterExportOptions
Fluent.Pdf.PdfRasterExportOptions.BitsPerComponent.get -> int
Fluent.Pdf.PdfRasterExportOptions.ColorManagement.get -> Fluent.Pdf.PdfColorRenderOptions?
Fluent.Pdf.PdfRasterExportOptions.Format.get -> Fluent.Pdf.PdfRasterFormat
Fluent.Pdf.PdfRasterExportOptions.PdfRasterExportOptions(Fluent.Pdf.PdfRasterFormat format, Fluent.Pdf.PdfRenderOptions? rendering = null, Fluent.Pdf.PdfColorRenderOptions? colorManagement = null) -> void
Fluent.Pdf.PdfRasterExportOptions.Rendering.get -> Fluent.Pdf.PdfRenderOptions!
Fluent.Pdf.PdfRasterFileFormat
Fluent.Pdf.PdfRasterFileFormat.Bmp = 2 -> Fluent.Pdf.PdfRasterFileFormat
Fluent.Pdf.PdfRasterFileFormat.Gif = 3 -> Fluent.Pdf.PdfRasterFileFormat
Fluent.Pdf.PdfRasterFileFormat.Heif = 6 -> Fluent.Pdf.PdfRasterFileFormat
Fluent.Pdf.PdfRasterFileFormat.Jpeg = 1 -> Fluent.Pdf.PdfRasterFileFormat
Fluent.Pdf.PdfRasterFileFormat.Png = 0 -> Fluent.Pdf.PdfRasterFileFormat
Fluent.Pdf.PdfRasterFileFormat.Tiff = 4 -> Fluent.Pdf.PdfRasterFileFormat
Fluent.Pdf.PdfRasterFileFormat.WebP = 5 -> Fluent.Pdf.PdfRasterFileFormat
Fluent.Pdf.PdfRasterFileInfo
Fluent.Pdf.PdfRasterFileInfo.Frames.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfRasterFrameInfo!>!
Fluent.Pdf.PdfRasterFileInfo.IsAnimation.get -> bool
Fluent.Pdf.PdfRasterFileInfo.PdfRasterFileInfo(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfRasterFrameInfo!>! frames, bool isAnimation = false) -> void
Fluent.Pdf.PdfRasterFormat
Fluent.Pdf.PdfRasterFormat.Bmp = 1 -> Fluent.Pdf.PdfRasterFormat
Fluent.Pdf.PdfRasterFormat.Png = 0 -> Fluent.Pdf.PdfRasterFormat
Fluent.Pdf.PdfRasterFormat.Tiff = 2 -> Fluent.Pdf.PdfRasterFormat
Fluent.Pdf.PdfRasterFrameInfo
Fluent.Pdf.PdfRasterFrameInfo.ColorSpace.get -> Fluent.Pdf.PdfColorSpace!
Fluent.Pdf.PdfRasterFrameInfo.Metadata.get -> Fluent.Pdf.PdfImageMetadata!
Fluent.Pdf.PdfRasterFrameInfo.PdfRasterFrameInfo(int width, int height, Fluent.Pdf.PdfImageMetadata? metadata = null, Fluent.Pdf.PdfColorSpace? colorSpace = null) -> void
Fluent.Pdf.PdfRasterFrameInfo.Samples.get -> Fluent.Pdf.PdfImageInfo!
Fluent.Pdf.PdfRasterImportOptions
Fluent.Pdf.PdfRasterImportOptions.FallbackDpi.get -> double
Fluent.Pdf.PdfRasterImportOptions.ImageOptions.get -> Fluent.Pdf.PdfImageReadOptions!
Fluent.Pdf.PdfRasterImportOptions.Margin.get -> double
Fluent.Pdf.PdfRasterImportOptions.MaximumPages.get -> int
Fluent.Pdf.PdfRasterImportOptions.PageSize.get -> Fluent.Pdf.PdfPageSize?
Fluent.Pdf.PdfRasterImportOptions.PdfRasterImportOptions(Fluent.Pdf.PdfPageSize? pageSize = null, double fallbackDpi = 72, double margin = 0, int maximumPages = 1000, Fluent.Pdf.PdfImageReadOptions? imageOptions = null) -> void
Fluent.Pdf.PdfRasterInput
Fluent.Pdf.PdfRasterInput.AllowAnimationFrames.get -> bool
Fluent.Pdf.PdfRasterInput.Format.get -> Fluent.Pdf.PdfRasterFileFormat
Fluent.Pdf.PdfRasterInput.Frames.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfRasterInput.PdfRasterInput(System.IO.Stream! input, Fluent.Pdf.PdfRasterFileFormat format, System.Collections.Generic.IEnumerable<int>! frames, bool allowAnimationFrames = false) -> void
Fluent.Pdf.PdfRasterRequest
Fluent.Pdf.PdfRasterRequest.ByteLength.get -> long
Fluent.Pdf.PdfRasterRequest.EffectiveDpiX.get -> double
Fluent.Pdf.PdfRasterRequest.EffectiveDpiY.get -> double
Fluent.Pdf.PdfRasterRequest.FullHeight.get -> int
Fluent.Pdf.PdfRasterRequest.FullWidth.get -> int
Fluent.Pdf.PdfRasterRequest.Height.get -> int
Fluent.Pdf.PdfRasterRequest.Options.get -> Fluent.Pdf.PdfRenderOptions!
Fluent.Pdf.PdfRasterRequest.Page.get -> Fluent.Pdf.PdfPageSource!
Fluent.Pdf.PdfRasterRequest.PageIndex.get -> int
Fluent.Pdf.PdfRasterRequest.Region.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfRasterRequest.Stride.get -> int
Fluent.Pdf.PdfRasterRequest.Width.get -> int
Fluent.Pdf.PdfRasterRequest.X.get -> int
Fluent.Pdf.PdfRasterRequest.Y.get -> int
Fluent.Pdf.PdfRect
Fluent.Pdf.PdfRect.Height.get -> double
Fluent.Pdf.PdfRect.PdfRect() -> void
Fluent.Pdf.PdfRect.PdfRect(double x, double y, double width, double height) -> void
Fluent.Pdf.PdfRect.Right.get -> double
Fluent.Pdf.PdfRect.Top.get -> double
Fluent.Pdf.PdfRect.Width.get -> double
Fluent.Pdf.PdfRect.X.get -> double
Fluent.Pdf.PdfRect.Y.get -> double
Fluent.Pdf.PdfRedaction
Fluent.Pdf.PdfRedactionAudit
Fluent.Pdf.PdfRedactionAudit.Coverage.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfRedactionCoverage!>!
Fluent.Pdf.PdfRedactionAudit.FullRewrite.get -> bool
Fluent.Pdf.PdfRedactionAudit.MaskedPixels.get -> long
Fluent.Pdf.PdfRedactionAudit.OriginalGraphDiscarded.get -> bool
Fluent.Pdf.PdfRedactionAudit.OutputSha256.get -> string!
Fluent.Pdf.PdfRedactionAudit.PageCount.get -> int
Fluent.Pdf.PdfRedactionAudit.SourceHadSignatures.get -> bool
Fluent.Pdf.PdfRedactionAudit.SourceRevisionCount.get -> long
Fluent.Pdf.PdfRedactionCoverage
Fluent.Pdf.PdfRedactionCoverage.PageIndex.get -> int
Fluent.Pdf.PdfRedactionCoverage.PixelBounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfRedactionCoverage.RequestedBounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfRedactionRasterOptions
Fluent.Pdf.PdfRedactionRasterOptions.AllowSignatureInvalidation.get -> bool
Fluent.Pdf.PdfRedactionRasterOptions.BackgroundRgb.get -> uint
Fluent.Pdf.PdfRedactionRasterOptions.Dpi.get -> int
Fluent.Pdf.PdfRedactionRasterOptions.FillRgb.get -> uint
Fluent.Pdf.PdfRedactionRasterOptions.IncludeAnnotationAppearances.get -> bool
Fluent.Pdf.PdfRedactionRasterOptions.MaximumMaskWork.get -> long
Fluent.Pdf.PdfRedactionRasterOptions.MaximumRasterBytes.get -> long
Fluent.Pdf.PdfRedactionRasterOptions.MaximumRegions.get -> int
Fluent.Pdf.PdfRedactionRasterOptions.PaddingPixels.get -> int
Fluent.Pdf.PdfRedactionRasterOptions.PdfRedactionRasterOptions(int dpi, uint fillRgb = 0, uint backgroundRgb = 16777215, int paddingPixels = 1, bool includeAnnotationAppearances = false, bool allowSignatureInvalidation = false, int maximumRegions = 10000, long maximumRasterBytes = 67108864, long maximumMaskWork = 100000000) -> void
Fluent.Pdf.PdfRedactionRegion
Fluent.Pdf.PdfRedactionRegion.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfRedactionRegion.PageIndex.get -> int
Fluent.Pdf.PdfRedactionRegion.PdfRedactionRegion(int pageIndex, Fluent.Pdf.PdfRect bounds) -> void
Fluent.Pdf.PdfRedactionResult
Fluent.Pdf.PdfRedactionResult.Audit.get -> Fluent.Pdf.PdfRedactionAudit!
Fluent.Pdf.PdfRedactionResult.ByteLength.get -> long
Fluent.Pdf.PdfRedactionResult.OpenPdfStream() -> System.IO.Stream!
Fluent.Pdf.PdfRedactionResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfRedactionResult.Save(System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfRedactionResult.SaveFile(string! path, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfRemediationAudit
Fluent.Pdf.PdfRemediationAudit.After.get -> Fluent.Pdf.PdfPreflightReport!
Fluent.Pdf.PdfRemediationAudit.Before.get -> Fluent.Pdf.PdfPreflightReport!
Fluent.Pdf.PdfRemediationAudit.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfRemediationAudit.Plan.get -> Fluent.Pdf.PdfRemediationPlan!
Fluent.Pdf.PdfRemediationPlan
Fluent.Pdf.PdfRemediationPlan.AllowSignatureInvalidation.get -> bool
Fluent.Pdf.PdfRemediationPlan.Fixes.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfRemediationPlan.PdfRemediationPlan(Fluent.Pdf.PdfPreflightReport! source, System.Collections.Generic.IEnumerable<string!>! fixes, bool allowSignatureInvalidation = false) -> void
Fluent.Pdf.PdfRemediationPlan.SourceSha256.get -> string!
Fluent.Pdf.PdfRenderOptions
Fluent.Pdf.PdfRenderOptions.Annotations.get -> bool
Fluent.Pdf.PdfRenderOptions.BackgroundRgb.get -> uint?
Fluent.Pdf.PdfRenderOptions.Color.get -> Fluent.Pdf.PdfRasterColor
Fluent.Pdf.PdfRenderOptions.DpiX.get -> double
Fluent.Pdf.PdfRenderOptions.DpiY.get -> double
Fluent.Pdf.PdfRenderOptions.PdfRenderOptions(double dpiX = 96, double dpiY = 96, Fluent.Pdf.PdfRect? region = null, uint? backgroundRgb = 16777215, Fluent.Pdf.PdfRasterColor color = Fluent.Pdf.PdfRasterColor.Rgb, bool annotations = true, bool print = false) -> void
Fluent.Pdf.PdfRenderOptions.Print.get -> bool
Fluent.Pdf.PdfRenderOptions.Region.get -> Fluent.Pdf.PdfRect?
Fluent.Pdf.PdfRendering
Fluent.Pdf.PdfRepair
Fluent.Pdf.PdfRepairOptions
Fluent.Pdf.PdfRepairOptions.ActiveObjects.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfObjectId>?
Fluent.Pdf.PdfRepairOptions.AllowSignatureInvalidation.get -> bool
Fluent.Pdf.PdfRepairOptions.Orphans.get -> Fluent.Pdf.PdfRepairOrphanPolicy
Fluent.Pdf.PdfRepairOptions.PdfRepairOptions(Fluent.Pdf.PdfObjectId? root = null, Fluent.Pdf.PdfRepairOrphanPolicy orphans = Fluent.Pdf.PdfRepairOrphanPolicy.Preserve, Fluent.Pdf.PdfRepairStreamLength![]? streamLengths = null, Fluent.Pdf.PdfObjectId[]? activeObjects = null, bool rejectDanglingReferences = true, bool allowSignatureInvalidation = false) -> void
Fluent.Pdf.PdfRepairOptions.RejectDanglingReferences.get -> bool
Fluent.Pdf.PdfRepairOptions.Root.get -> Fluent.Pdf.PdfObjectId?
Fluent.Pdf.PdfRepairOptions.StreamLengths.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfRepairStreamLength!>!
Fluent.Pdf.PdfRepairOrphanPolicy
Fluent.Pdf.PdfRepairOrphanPolicy.Discard = 1 -> Fluent.Pdf.PdfRepairOrphanPolicy
Fluent.Pdf.PdfRepairOrphanPolicy.Preserve = 0 -> Fluent.Pdf.PdfRepairOrphanPolicy
Fluent.Pdf.PdfRepairRecord
Fluent.Pdf.PdfRepairRecord.Code.get -> string!
Fluent.Pdf.PdfRepairRecord.Message.get -> string!
Fluent.Pdf.PdfRepairRecord.ObjectId.get -> Fluent.Pdf.PdfObjectId?
Fluent.Pdf.PdfRepairRecord.SourceOffset.get -> long?
Fluent.Pdf.PdfRepairResult
Fluent.Pdf.PdfRepairResult.Ledger.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfRepairRecord!>!
Fluent.Pdf.PdfRepairResult.OpenOriginalStream() -> System.IO.Stream!
Fluent.Pdf.PdfRepairResult.OpenRepairedStream() -> System.IO.Stream!
Fluent.Pdf.PdfRepairResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfRepairResult.OriginalSha256.get -> string!
Fluent.Pdf.PdfRepairResult.RepairedSha256.get -> string!
Fluent.Pdf.PdfRepairResult.WriteLedger(System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfRepairStreamLength
Fluent.Pdf.PdfRepairStreamLength.Length.get -> long
Fluent.Pdf.PdfRepairStreamLength.ObjectId.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfRepairStreamLength.PdfRepairStreamLength(Fluent.Pdf.PdfObjectId objectId, long length) -> void
Fluent.Pdf.PdfResourceBinding
Fluent.Pdf.PdfResourceBinding.Name.get -> string!
Fluent.Pdf.PdfResourceBinding.PdfResourceBinding(string! name, Fluent.Pdf.PdfGraphicsResource! resource) -> void
Fluent.Pdf.PdfResourceBinding.Resource.get -> Fluent.Pdf.PdfGraphicsResource!
Fluent.Pdf.PdfResourceContent
Fluent.Pdf.PdfResourceContent.ContainerObjectId.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfResourceContent.Program.get -> Fluent.Pdf.PdfContentProgram!
Fluent.Pdf.PdfResourceContent.Resources.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfResourceDeclaration
Fluent.Pdf.PdfResourceDeclaration.Category.get -> string!
Fluent.Pdf.PdfResourceDeclaration.ContainerObjectId.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfResourceDeclaration.Name.get -> string!
Fluent.Pdf.PdfResourceDeclaration.ResourceIndex.get -> int
Fluent.Pdf.PdfResourceExtraction
Fluent.Pdf.PdfResourceKind
Fluent.Pdf.PdfResourceKind.Depth = 5 -> Fluent.Pdf.PdfResourceKind
Fluent.Pdf.PdfResourceKind.ExpandedBytes = 1 -> Fluent.Pdf.PdfResourceKind
Fluent.Pdf.PdfResourceKind.InputBytes = 0 -> Fluent.Pdf.PdfResourceKind
Fluent.Pdf.PdfResourceKind.Objects = 3 -> Fluent.Pdf.PdfResourceKind
Fluent.Pdf.PdfResourceKind.OutputBytes = 2 -> Fluent.Pdf.PdfResourceKind
Fluent.Pdf.PdfResourceKind.Pixels = 4 -> Fluent.Pdf.PdfResourceKind
Fluent.Pdf.PdfResourceKind.Workers = 6 -> Fluent.Pdf.PdfResourceKind
Fluent.Pdf.PdfResourceLimits
Fluent.Pdf.PdfResourceLimits.MaximumDepth.get -> int
Fluent.Pdf.PdfResourceLimits.MaximumDiagnostics.get -> int
Fluent.Pdf.PdfResourceLimits.MaximumElapsedTime.get -> System.TimeSpan
Fluent.Pdf.PdfResourceLimits.MaximumExpandedBytes.get -> long
Fluent.Pdf.PdfResourceLimits.MaximumInputBytes.get -> long
Fluent.Pdf.PdfResourceLimits.MaximumObjects.get -> long
Fluent.Pdf.PdfResourceLimits.MaximumOutputBytes.get -> long
Fluent.Pdf.PdfResourceLimits.MaximumPixels.get -> long
Fluent.Pdf.PdfResourceLimits.MaximumWorkers.get -> int
Fluent.Pdf.PdfResourceLimits.PdfResourceLimits(long maximumInputBytes = 67108864, long maximumExpandedBytes = 268435456, long maximumOutputBytes = 268435456, long maximumObjects = 1000000, long maximumPixels = 100000000, int maximumDepth = 128, int maximumWorkers = 8, int maximumDiagnostics = 1024, System.TimeSpan? maximumElapsedTime = null) -> void
Fluent.Pdf.PdfResourceOccurrence
Fluent.Pdf.PdfResourceOccurrence.IsArtifact.get -> bool
Fluent.Pdf.PdfResourceOccurrence.ResourceIndex.get -> int
Fluent.Pdf.PdfResourceOccurrence.Source.get -> Fluent.Pdf.PdfResourceSource!
Fluent.Pdf.PdfResourceOccurrence.Transform.get -> Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfResourceOccurrence.Usage.get -> string!
Fluent.Pdf.PdfResourceSet
Fluent.Pdf.PdfResourceSet.Bindings.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfResourceBinding!>!
Fluent.Pdf.PdfResourceSet.PdfResourceSet(params Fluent.Pdf.PdfResourceBinding![]! bindings) -> void
Fluent.Pdf.PdfResourceSource
Fluent.Pdf.PdfResourceSource.ContainerObjectId.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfResourceSource.FormInvocationIndices.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfResourceSource.FormPath.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfObjectId>!
Fluent.Pdf.PdfResourceSource.OperationIndex.get -> int
Fluent.Pdf.PdfResourceSource.PageIndex.get -> int
Fluent.Pdf.PdfResourceUsage
Fluent.Pdf.PdfResourceUsage.Consumed.get -> long
Fluent.Pdf.PdfResourceUsage.Peak.get -> long
Fluent.Pdf.PdfResourceUsage.Resource.get -> Fluent.Pdf.PdfResourceKind
Fluent.Pdf.PdfRevision
Fluent.Pdf.PdfRevision.Entries.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfCrossReference!>!
Fluent.Pdf.PdfRevision.IsHybrid.get -> bool
Fluent.Pdf.PdfRevision.StartXref.get -> long
Fluent.Pdf.PdfRevision.Trailer.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfRevisionAuditRecord
Fluent.Pdf.PdfRevisionAuditRecord.Changes.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfRevisionChange!>!
Fluent.Pdf.PdfRevisionAuditRecord.RevisionIndex.get -> int
Fluent.Pdf.PdfRevisionAuditRecord.Signatures.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfSignatureObservation!>!
Fluent.Pdf.PdfRevisionAuditRecord.StartXref.get -> long
Fluent.Pdf.PdfRevisionChange
Fluent.Pdf.PdfRevisionChange.Kind.get -> Fluent.Pdf.PdfCrossReferenceKind
Fluent.Pdf.PdfRevisionChange.ObjectId.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfRevisionChange.PreviouslyDefined.get -> bool
Fluent.Pdf.PdfRevocationStatus
Fluent.Pdf.PdfRevocationStatus.Good = 1 -> Fluent.Pdf.PdfRevocationStatus
Fluent.Pdf.PdfRevocationStatus.Revoked = 2 -> Fluent.Pdf.PdfRevocationStatus
Fluent.Pdf.PdfRevocationStatus.Unknown = 0 -> Fluent.Pdf.PdfRevocationStatus
Fluent.Pdf.PdfScanDewarpGrid
Fluent.Pdf.PdfScanDewarpGrid.Columns.get -> int
Fluent.Pdf.PdfScanDewarpGrid.MapPoint(Fluent.Pdf.PdfPoint point) -> Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfScanDewarpGrid.OutputHeight.get -> int
Fluent.Pdf.PdfScanDewarpGrid.OutputWidth.get -> int
Fluent.Pdf.PdfScanDewarpGrid.PdfScanDewarpGrid(int sourceWidth, int sourceHeight, int outputWidth, int outputHeight, int columns, int rows, Fluent.Pdf.PdfPoint[]! sourcePoints) -> void
Fluent.Pdf.PdfScanDewarpGrid.Rows.get -> int
Fluent.Pdf.PdfScanDewarpGrid.SourceHeight.get -> int
Fluent.Pdf.PdfScanDewarpGrid.SourcePoints.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfPoint>!
Fluent.Pdf.PdfScanDewarpGrid.SourceWidth.get -> int
Fluent.Pdf.PdfScanGeometry
Fluent.Pdf.PdfScanGeometry.Dewarp.get -> Fluent.Pdf.PdfScanDewarpGrid?
Fluent.Pdf.PdfScanGeometry.MapPoint(Fluent.Pdf.PdfPoint point) -> Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfScanGeometry.MapWord(Fluent.Pdf.PdfOcrWord! word, double maximumErrorPixels = 0.25, int maximumPoints = 4096, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfScanMappedBoundary!
Fluent.Pdf.PdfScanGeometry.OriginalHeight.get -> int
Fluent.Pdf.PdfScanGeometry.OriginalWidth.get -> int
Fluent.Pdf.PdfScanGeometry.ProcessedHeight.get -> int
Fluent.Pdf.PdfScanGeometry.ProcessedToDewarped.get -> Fluent.Pdf.PdfMatrix
Fluent.Pdf.PdfScanGeometry.ProcessedWidth.get -> int
Fluent.Pdf.PdfScanLayout
Fluent.Pdf.PdfScanLayoutOptions
Fluent.Pdf.PdfScanLayoutOptions.InferColumns.get -> bool
Fluent.Pdf.PdfScanLayoutOptions.LineTolerancePixels.get -> double
Fluent.Pdf.PdfScanLayoutOptions.MaximumAnalysisWork.get -> long
Fluent.Pdf.PdfScanLayoutOptions.MaximumColumns.get -> int
Fluent.Pdf.PdfScanLayoutOptions.MaximumWords.get -> int
Fluent.Pdf.PdfScanLayoutOptions.MinimumColumnGapPixels.get -> double
Fluent.Pdf.PdfScanLayoutOptions.MinimumRuleLengthPixels.get -> int
Fluent.Pdf.PdfScanLayoutOptions.MinimumWordsPerColumn.get -> int
Fluent.Pdf.PdfScanLayoutOptions.PdfScanLayoutOptions(bool inferColumns = true, bool rightToLeftColumns = false, bool rightToLeftWords = false, double minimumColumnGapPixels = 12, double lineTolerancePixels = 4, double spanningHeightFraction = 0.1, int minimumWordsPerColumn = 2, int maximumColumns = 16, int minimumRuleLengthPixels = 20, int maximumWords = 100000, long maximumAnalysisWork = 100000000, params Fluent.Pdf.PdfScanRegionHint![]! regions) -> void
Fluent.Pdf.PdfScanLayoutOptions.Regions.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfScanRegionHint!>!
Fluent.Pdf.PdfScanLayoutOptions.RightToLeftColumns.get -> bool
Fluent.Pdf.PdfScanLayoutOptions.RightToLeftWords.get -> bool
Fluent.Pdf.PdfScanLayoutOptions.SpanningHeightFraction.get -> double
Fluent.Pdf.PdfScanLayoutRegion
Fluent.Pdf.PdfScanLayoutRegion.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfScanLayoutRegion.ColumnIndex.get -> int?
Fluent.Pdf.PdfScanLayoutRegion.Confidence.get -> double
Fluent.Pdf.PdfScanLayoutRegion.Kind.get -> Fluent.Pdf.PdfScanRegionKind
Fluent.Pdf.PdfScanLayoutRegion.Provenance.get -> string!
Fluent.Pdf.PdfScanLayoutRegion.WordIndices.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfScanLayoutResult
Fluent.Pdf.PdfScanLayoutResult.AnalysisWork.get -> long
Fluent.Pdf.PdfScanLayoutResult.NonProseWordIndices.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfScanLayoutResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfScanLayoutResult.ProseReadingOrder.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfScanLayoutResult.Recognition.get -> Fluent.Pdf.PdfOcrResult!
Fluent.Pdf.PdfScanLayoutResult.Regions.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfScanLayoutRegion!>!
Fluent.Pdf.PdfScanMappedBoundary
Fluent.Pdf.PdfScanMappedBoundary.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfScanMappedBoundary.MaximumErrorPixels.get -> double
Fluent.Pdf.PdfScanMappedBoundary.OperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfScanMappedBoundary.Points.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfPoint>!
Fluent.Pdf.PdfScanOrientation
Fluent.Pdf.PdfScanOrientation.Automatic = 4 -> Fluent.Pdf.PdfScanOrientation
Fluent.Pdf.PdfScanOrientation.Clockwise180 = 2 -> Fluent.Pdf.PdfScanOrientation
Fluent.Pdf.PdfScanOrientation.Clockwise270 = 3 -> Fluent.Pdf.PdfScanOrientation
Fluent.Pdf.PdfScanOrientation.Clockwise90 = 1 -> Fluent.Pdf.PdfScanOrientation
Fluent.Pdf.PdfScanOrientation.None = 0 -> Fluent.Pdf.PdfScanOrientation
Fluent.Pdf.PdfScanOrientationScore
Fluent.Pdf.PdfScanOrientationScore.EngineConfidence.get -> double?
Fluent.Pdf.PdfScanOrientationScore.Orientation.get -> Fluent.Pdf.PdfScanOrientation
Fluent.Pdf.PdfScanPreprocessing
Fluent.Pdf.PdfScanPreprocessingOptions
Fluent.Pdf.PdfScanPreprocessingOptions.AnalysisStride.get -> int
Fluent.Pdf.PdfScanPreprocessingOptions.BinaryThreshold.get -> byte?
Fluent.Pdf.PdfScanPreprocessingOptions.ContrastHigh.get -> byte
Fluent.Pdf.PdfScanPreprocessingOptions.ContrastLow.get -> byte
Fluent.Pdf.PdfScanPreprocessingOptions.DetectSkew.get -> bool
Fluent.Pdf.PdfScanPreprocessingOptions.Dewarp.get -> Fluent.Pdf.PdfScanDewarpGrid?
Fluent.Pdf.PdfScanPreprocessingOptions.MaximumPixelVisits.get -> long
Fluent.Pdf.PdfScanPreprocessingOptions.MaximumSkewDegrees.get -> double
Fluent.Pdf.PdfScanPreprocessingOptions.MedianDenoise.get -> bool
Fluent.Pdf.PdfScanPreprocessingOptions.MinimumOrientationMargin.get -> double
Fluent.Pdf.PdfScanPreprocessingOptions.MinimumSkewMargin.get -> double
Fluent.Pdf.PdfScanPreprocessingOptions.Orientation.get -> Fluent.Pdf.PdfScanOrientation
Fluent.Pdf.PdfScanPreprocessingOptions.PdfScanPreprocessingOptions(Fluent.Pdf.PdfScanOrientation orientation = Fluent.Pdf.PdfScanOrientation.None, double skewDegrees = 0, bool detectSkew = false, double maximumSkewDegrees = 5, double skewStepDegrees = 0.5, int analysisStride = 2, bool medianDenoise = false, byte contrastLow = 0, byte contrastHigh = 255, byte? binaryThreshold = null, Fluent.Pdf.PdfScanDewarpGrid? dewarp = null, double minimumOrientationMargin = 0.05, double minimumSkewMargin = 0.01, long maximumPixelVisits = 100000000) -> void
Fluent.Pdf.PdfScanPreprocessingOptions.SkewDegrees.get -> double
Fluent.Pdf.PdfScanPreprocessingOptions.SkewStepDegrees.get -> double
Fluent.Pdf.PdfScanPreprocessingResult
Fluent.Pdf.PdfScanPreprocessingResult.AppliedOrientation.get -> Fluent.Pdf.PdfScanOrientation
Fluent.Pdf.PdfScanPreprocessingResult.Geometry.get -> Fluent.Pdf.PdfScanGeometry!
Fluent.Pdf.PdfScanPreprocessingResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfScanPreprocessingResult.Options.get -> Fluent.Pdf.PdfScanPreprocessingOptions!
Fluent.Pdf.PdfScanPreprocessingResult.OrientationMargin.get -> double?
Fluent.Pdf.PdfScanPreprocessingResult.OrientationScores.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfScanOrientationScore!>!
Fluent.Pdf.PdfScanPreprocessingResult.Original.get -> Fluent.Pdf.PdfOcrRaster!
Fluent.Pdf.PdfScanPreprocessingResult.PixelVisits.get -> long
Fluent.Pdf.PdfScanPreprocessingResult.Processed.get -> Fluent.Pdf.PdfOcrRaster!
Fluent.Pdf.PdfScanPreprocessingResult.SkewMargin.get -> double?
Fluent.Pdf.PdfScanPreprocessingResult.SourceSkewDegrees.get -> double
Fluent.Pdf.PdfScanRecognitionResult
Fluent.Pdf.PdfScanRecognitionResult.Layout.get -> Fluent.Pdf.PdfScanLayoutResult!
Fluent.Pdf.PdfScanRecognitionResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfScanRecognitionResult.Recognition.get -> Fluent.Pdf.PdfOcrResult!
Fluent.Pdf.PdfScanRecognitionResult.Scan.get -> Fluent.Pdf.PdfScanPreprocessingResult!
Fluent.Pdf.PdfScanRegionHint
Fluent.Pdf.PdfScanRegionHint.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfScanRegionHint.Confidence.get -> double
Fluent.Pdf.PdfScanRegionHint.Kind.get -> Fluent.Pdf.PdfScanRegionKind
Fluent.Pdf.PdfScanRegionHint.PdfScanRegionHint(Fluent.Pdf.PdfScanRegionKind kind, Fluent.Pdf.PdfRect bounds, double confidence = 1) -> void
Fluent.Pdf.PdfScanRegionKind
Fluent.Pdf.PdfScanRegionKind.Form = 2 -> Fluent.Pdf.PdfScanRegionKind
Fluent.Pdf.PdfScanRegionKind.Prose = 0 -> Fluent.Pdf.PdfScanRegionKind
Fluent.Pdf.PdfScanRegionKind.RuledTableOrForm = 3 -> Fluent.Pdf.PdfScanRegionKind
Fluent.Pdf.PdfScanRegionKind.Table = 1 -> Fluent.Pdf.PdfScanRegionKind
Fluent.Pdf.PdfScriptRecord
Fluent.Pdf.PdfScriptRecord.IsAttachment.get -> bool
Fluent.Pdf.PdfScriptRecord.Location.get -> string!
Fluent.Pdf.PdfScriptRecord.Source.get -> string!
Fluent.Pdf.PdfScripts
Fluent.Pdf.PdfSecurity
Fluent.Pdf.PdfSecurityResult
Fluent.Pdf.PdfSecurityResult.Cipher.get -> string!
Fluent.Pdf.PdfSecurityResult.EncryptMetadata.get -> bool
Fluent.Pdf.PdfSecurityResult.Permissions.get -> Fluent.Pdf.PdfDocumentPermissions
Fluent.Pdf.PdfSecurityResult.Report.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfSecurityResult.Revision.get -> int
Fluent.Pdf.PdfSecurityResult.Role.get -> Fluent.Pdf.PdfAuthenticationRole
Fluent.Pdf.PdfSepaCreditTransfer
Fluent.Pdf.PdfSepaCreditTransfer.Amount.get -> decimal
Fluent.Pdf.PdfSepaCreditTransfer.Beneficiary.get -> string!
Fluent.Pdf.PdfSepaCreditTransfer.Bic.get -> string!
Fluent.Pdf.PdfSepaCreditTransfer.CreditorReference.get -> string!
Fluent.Pdf.PdfSepaCreditTransfer.Iban.get -> string!
Fluent.Pdf.PdfSepaCreditTransfer.Information.get -> string!
Fluent.Pdf.PdfSepaCreditTransfer.Payload.get -> string!
Fluent.Pdf.PdfSepaCreditTransfer.PdfSepaCreditTransfer(string! beneficiary, string! iban, string! bic, decimal amount, string! creditorReference = "", string! remittance = "", string! purpose = "", string! information = "") -> void
Fluent.Pdf.PdfSepaCreditTransfer.Purpose.get -> string!
Fluent.Pdf.PdfSepaCreditTransfer.Remittance.get -> string!
Fluent.Pdf.PdfShading
Fluent.Pdf.PdfShading.ColorSpace.get -> Fluent.Pdf.PdfColorSpace!
Fluent.Pdf.PdfShading.Resource.get -> Fluent.Pdf.PdfGraphicsResource!
Fluent.Pdf.PdfShading.ShadingType.get -> int
Fluent.Pdf.PdfShapedGlyph
Fluent.Pdf.PdfShapedGlyph.AdvanceX.get -> double
Fluent.Pdf.PdfShapedGlyph.AdvanceY.get -> double
Fluent.Pdf.PdfShapedGlyph.Cluster.get -> int
Fluent.Pdf.PdfShapedGlyph.GlyphIndex.get -> int
Fluent.Pdf.PdfShapedGlyph.OffsetX.get -> double
Fluent.Pdf.PdfShapedGlyph.OffsetY.get -> double
Fluent.Pdf.PdfShapedGlyph.PdfShapedGlyph() -> void
Fluent.Pdf.PdfShapedGlyph.PdfShapedGlyph(int glyphIndex, int cluster, double advanceX, double advanceY, double offsetX = 0, double offsetY = 0) -> void
Fluent.Pdf.PdfShapedRun
Fluent.Pdf.PdfShapedRun.EmbeddingLevel.get -> int
Fluent.Pdf.PdfShapedRun.Glyphs.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfShapedGlyph>!
Fluent.Pdf.PdfShapedRun.ParagraphIndex.get -> int
Fluent.Pdf.PdfShapedRun.Request.get -> Fluent.Pdf.PdfShapingRequest!
Fluent.Pdf.PdfShapedText
Fluent.Pdf.PdfShapedText.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfShapedText.Options.get -> Fluent.Pdf.PdfTextOptions!
Fluent.Pdf.PdfShapedText.Runs.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfShapedRun!>!
Fluent.Pdf.PdfShapedText.Unicode.get -> Fluent.Pdf.PdfUnicodeAnalysis!
Fluent.Pdf.PdfShapingRequest
Fluent.Pdf.PdfShapingRequest.Direction.get -> Fluent.Pdf.PdfTextDirection
Fluent.Pdf.PdfShapingRequest.Features.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfTextFeature>!
Fluent.Pdf.PdfShapingRequest.Font.get -> Fluent.Pdf.PdfFont!
Fluent.Pdf.PdfShapingRequest.Language.get -> string!
Fluent.Pdf.PdfShapingRequest.Length.get -> int
Fluent.Pdf.PdfShapingRequest.ScriptTag.get -> uint
Fluent.Pdf.PdfShapingRequest.Start.get -> int
Fluent.Pdf.PdfShapingRequest.Text.get -> string!
Fluent.Pdf.PdfSignatureFieldDefinition
Fluent.Pdf.PdfSignatureFieldDefinition.Appearance.get -> Fluent.Pdf.PdfGraphicsResource?
Fluent.Pdf.PdfSignatureFieldDefinition.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfSignatureFieldDefinition.Name.get -> string!
Fluent.Pdf.PdfSignatureFieldDefinition.PageIndex.get -> int
Fluent.Pdf.PdfSignatureFieldDefinition.PdfSignatureFieldDefinition(string! name, int pageIndex = 0, Fluent.Pdf.PdfRect bounds = default(Fluent.Pdf.PdfRect), Fluent.Pdf.PdfGraphicsResource? appearance = null) -> void
Fluent.Pdf.PdfSignatureFieldLock
Fluent.Pdf.PdfSignatureFieldLock.Action.get -> Fluent.Pdf.PdfSignatureLockAction
Fluent.Pdf.PdfSignatureFieldLock.Fields.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfSignatureFieldLock.PdfSignatureFieldLock(Fluent.Pdf.PdfSignatureLockAction action, params string![]! fields) -> void
Fluent.Pdf.PdfSignatureLockAction
Fluent.Pdf.PdfSignatureLockAction.All = 0 -> Fluent.Pdf.PdfSignatureLockAction
Fluent.Pdf.PdfSignatureLockAction.Exclude = 2 -> Fluent.Pdf.PdfSignatureLockAction
Fluent.Pdf.PdfSignatureLockAction.Include = 1 -> Fluent.Pdf.PdfSignatureLockAction
Fluent.Pdf.PdfSignatureObservation
Fluent.Pdf.PdfSignatureObservation.ByteRange.get -> System.Collections.Generic.IReadOnlyList<long>!
Fluent.Pdf.PdfSignatureObservation.CryptographicallyValid.get -> bool?
Fluent.Pdf.PdfSignatureObservation.ObjectId.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfSignatureObservation.SignedRevisionLength.get -> long
Fluent.Pdf.PdfSignatureObservation.TrustEvaluated.get -> bool
Fluent.Pdf.PdfSignatureObservation.UnsupportedReason.get -> string?
Fluent.Pdf.PdfSignatureOptions
Fluent.Pdf.PdfSignatureOptions.Certification.get -> Fluent.Pdf.PdfCertificationPermission
Fluent.Pdf.PdfSignatureOptions.FieldLock.get -> Fluent.Pdf.PdfSignatureFieldLock?
Fluent.Pdf.PdfSignatureOptions.FieldName.get -> string!
Fluent.Pdf.PdfSignatureOptions.PdfSignatureOptions(string! fieldName, int reservedCmsBytes = 16384, Fluent.Pdf.PdfCertificationPermission certification = Fluent.Pdf.PdfCertificationPermission.None, Fluent.Pdf.PdfSignatureFieldLock? fieldLock = null, string? reason = null, System.DateTimeOffset? signingTime = null) -> void
Fluent.Pdf.PdfSignatureOptions.Reason.get -> string?
Fluent.Pdf.PdfSignatureOptions.ReservedCmsBytes.get -> int
Fluent.Pdf.PdfSignatureOptions.SigningTime.get -> System.DateTimeOffset?
Fluent.Pdf.PdfSignatureTrustDecision
Fluent.Pdf.PdfSignatureTrustDecision.Accepted.get -> bool
Fluent.Pdf.PdfSignatureTrustDecision.CertificateSha256.get -> string!
Fluent.Pdf.PdfSignatureTrustDecision.IdentityTrusted.get -> bool
Fluent.Pdf.PdfSignatureTrustDecision.PdfSignatureTrustDecision(bool identityTrusted, bool timeValid, Fluent.Pdf.PdfRevocationStatus revocation, string! certificateSha256) -> void
Fluent.Pdf.PdfSignatureTrustDecision.Revocation.get -> Fluent.Pdf.PdfRevocationStatus
Fluent.Pdf.PdfSignatureTrustDecision.TimeValid.get -> bool
Fluent.Pdf.PdfSignatureTrustOptions
Fluent.Pdf.PdfSignatureTrustOptions.EvaluationTime.get -> System.DateTimeOffset
Fluent.Pdf.PdfSignatureTrustOptions.PdfSignatureTrustOptions(System.DateTimeOffset evaluationTime) -> void
Fluent.Pdf.PdfSignatureVerification
Fluent.Pdf.PdfSignatureVerification.Accepted.get -> bool
Fluent.Pdf.PdfSignatureVerification.LaterByteCount.get -> long
Fluent.Pdf.PdfSignatureVerification.Signature.get -> Fluent.Pdf.PdfSignatureObservation!
Fluent.Pdf.PdfSignatureVerification.Trust.get -> Fluent.Pdf.PdfSignatureTrustDecision?
Fluent.Pdf.PdfSignatures
Fluent.Pdf.PdfSignedEditPlan
Fluent.Pdf.PdfSignedEditPlan.Allowed.get -> bool
Fluent.Pdf.PdfSignedEditPlan.PreservesOriginalPrefix.get -> bool
Fluent.Pdf.PdfSignedEditPlan.Reason.get -> string!
Fluent.Pdf.PdfSignedResult
Fluent.Pdf.PdfSignedResult.ByteLength.get -> long
Fluent.Pdf.PdfSignedResult.OpenPdfStream() -> System.IO.Stream!
Fluent.Pdf.PdfSignedResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfSignedResult.Save(System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfSignedResult.SaveFile(string! path, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfSimpleTextShaper
Fluent.Pdf.PdfSimpleTextShaper.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.PdfSimpleTextShaper.PdfSimpleTextShaper() -> void
Fluent.Pdf.PdfSimpleTextShaper.Shape(Fluent.Pdf.PdfShapingRequest! request, Fluent.Pdf.PdfOperationContext! context) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfShapedGlyph>!
Fluent.Pdf.PdfSoftMask
Fluent.Pdf.PdfSoftMask.Backdrop.get -> Fluent.Pdf.PdfColor?
Fluent.Pdf.PdfSoftMask.Group.get -> Fluent.Pdf.PdfTransparencyGroup!
Fluent.Pdf.PdfSoftMask.Kind.get -> Fluent.Pdf.PdfSoftMaskKind
Fluent.Pdf.PdfSoftMask.PdfSoftMask(Fluent.Pdf.PdfTransparencyGroup! group, Fluent.Pdf.PdfSoftMaskKind kind = Fluent.Pdf.PdfSoftMaskKind.Alpha, Fluent.Pdf.PdfColor? backdrop = null, Fluent.Pdf.PdfFunction? transfer = null) -> void
Fluent.Pdf.PdfSoftMask.Transfer.get -> Fluent.Pdf.PdfFunction?
Fluent.Pdf.PdfSoftMaskKind
Fluent.Pdf.PdfSoftMaskKind.Alpha = 0 -> Fluent.Pdf.PdfSoftMaskKind
Fluent.Pdf.PdfSoftMaskKind.Luminosity = 1 -> Fluent.Pdf.PdfSoftMaskKind
Fluent.Pdf.PdfSpotColorPolicy
Fluent.Pdf.PdfSpotColorPolicy.CompositeAlternate = 1 -> Fluent.Pdf.PdfSpotColorPolicy
Fluent.Pdf.PdfSpotColorPolicy.Reject = 0 -> Fluent.Pdf.PdfSpotColorPolicy
Fluent.Pdf.PdfStampContent
Fluent.Pdf.PdfStampContent.Kind.get -> Fluent.Pdf.PdfStampContentKind
Fluent.Pdf.PdfStampContent.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfStampContentKind
Fluent.Pdf.PdfStampContentKind.Drawing = 0 -> Fluent.Pdf.PdfStampContentKind
Fluent.Pdf.PdfStampContentKind.Html = 4 -> Fluent.Pdf.PdfStampContentKind
Fluent.Pdf.PdfStampContentKind.Image = 2 -> Fluent.Pdf.PdfStampContentKind
Fluent.Pdf.PdfStampContentKind.PdfPage = 3 -> Fluent.Pdf.PdfStampContentKind
Fluent.Pdf.PdfStampContentKind.Text = 1 -> Fluent.Pdf.PdfStampContentKind
Fluent.Pdf.PdfStampInfo
Fluent.Pdf.PdfStampInfo.ContentKind.get -> Fluent.Pdf.PdfStampContentKind
Fluent.Pdf.PdfStampInfo.Identifier.get -> string!
Fluent.Pdf.PdfStampInfo.Options.get -> Fluent.Pdf.PdfStampOptions!
Fluent.Pdf.PdfStampInfo.PageIndex.get -> int
Fluent.Pdf.PdfStampInspection
Fluent.Pdf.PdfStampInspection.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfStampInspection.Stamps.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfStampInfo!>!
Fluent.Pdf.PdfStampLayer
Fluent.Pdf.PdfStampLayer.Background = 0 -> Fluent.Pdf.PdfStampLayer
Fluent.Pdf.PdfStampLayer.Foreground = 1 -> Fluent.Pdf.PdfStampLayer
Fluent.Pdf.PdfStampOptions
Fluent.Pdf.PdfStampOptions.Artifact.get -> bool
Fluent.Pdf.PdfStampOptions.Columns.get -> int
Fluent.Pdf.PdfStampOptions.Destination.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfStampOptions.Fit.get -> Fluent.Pdf.PdfCanvasFit
Fluent.Pdf.PdfStampOptions.HorizontalGap.get -> double
Fluent.Pdf.PdfStampOptions.Layer.get -> Fluent.Pdf.PdfStampLayer
Fluent.Pdf.PdfStampOptions.Opacity.get -> double
Fluent.Pdf.PdfStampOptions.PdfStampOptions(Fluent.Pdf.PdfRect destination, Fluent.Pdf.PdfStampLayer layer = Fluent.Pdf.PdfStampLayer.Foreground, double opacity = 1, double rotation = 0, bool artifact = true, int columns = 1, int rows = 1, double horizontalGap = 0, double verticalGap = 0, Fluent.Pdf.PdfCanvasFit fit = Fluent.Pdf.PdfCanvasFit.Contain) -> void
Fluent.Pdf.PdfStampOptions.Rotation.get -> double
Fluent.Pdf.PdfStampOptions.Rows.get -> int
Fluent.Pdf.PdfStampOptions.VerticalGap.get -> double
Fluent.Pdf.PdfStamps
Fluent.Pdf.PdfStructuralInspection
Fluent.Pdf.PdfStructuralInspection.Findings.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfRepairRecord!>!
Fluent.Pdf.PdfStructuralInspection.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfStructuralInspection.Syntax.get -> Fluent.Pdf.PdfSyntaxDocument!
Fluent.Pdf.PdfStructuralValidation
Fluent.Pdf.PdfStructureContent
Fluent.Pdf.PdfStructureContent.AnnotationId.get -> Fluent.Pdf.PdfObjectId?
Fluent.Pdf.PdfStructureContent.Mcid.get -> int
Fluent.Pdf.PdfStructureContent.PageIndex.get -> int
Fluent.Pdf.PdfStructureContent.StreamId.get -> Fluent.Pdf.PdfObjectId?
Fluent.Pdf.PdfStructureElement
Fluent.Pdf.PdfStructureElement.ActualText.get -> string?
Fluent.Pdf.PdfStructureElement.AlternativeText.get -> string?
Fluent.Pdf.PdfStructureElement.Identifier.get -> string?
Fluent.Pdf.PdfStructureElement.Items.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfStructureItem!>!
Fluent.Pdf.PdfStructureElement.Language.get -> string?
Fluent.Pdf.PdfStructureElement.PdfStructureElement(string! role, Fluent.Pdf.PdfStructureItem![]! items, string? language = null, string? alternativeText = null, string? actualText = null, string? identifier = null, Fluent.Pdf.PdfStructureTableAttributes? table = null) -> void
Fluent.Pdf.PdfStructureElement.Role.get -> string!
Fluent.Pdf.PdfStructureElement.Table.get -> Fluent.Pdf.PdfStructureTableAttributes?
Fluent.Pdf.PdfStructureElementInfo
Fluent.Pdf.PdfStructureElementInfo.ActualText.get -> string?
Fluent.Pdf.PdfStructureElementInfo.AlternativeText.get -> string?
Fluent.Pdf.PdfStructureElementInfo.Children.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfValue!>!
Fluent.Pdf.PdfStructureElementInfo.Dictionary.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfStructureElementInfo.Language.get -> string?
Fluent.Pdf.PdfStructureElementInfo.ObjectId.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfStructureElementInfo.Role.get -> string!
Fluent.Pdf.PdfStructureItem
Fluent.Pdf.PdfStructurePlan
Fluent.Pdf.PdfStructurePlan.Language.get -> string!
Fluent.Pdf.PdfStructurePlan.PdfStructurePlan(string! language, Fluent.Pdf.PdfStructureElement! root, Fluent.Pdf.PdfAccessibilityProfile profile = Fluent.Pdf.PdfAccessibilityProfile.Ua1, bool replaceExisting = false) -> void
Fluent.Pdf.PdfStructurePlan.Profile.get -> Fluent.Pdf.PdfAccessibilityProfile
Fluent.Pdf.PdfStructurePlan.ReplaceExisting.get -> bool
Fluent.Pdf.PdfStructurePlan.Root.get -> Fluent.Pdf.PdfStructureElement!
Fluent.Pdf.PdfStructureTableAttributes
Fluent.Pdf.PdfStructureTableAttributes.ColumnSpan.get -> int
Fluent.Pdf.PdfStructureTableAttributes.HeaderScope.get -> string?
Fluent.Pdf.PdfStructureTableAttributes.Headers.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.PdfStructureTableAttributes.PdfStructureTableAttributes(int rowSpan = 1, int columnSpan = 1, string? headerScope = null, string![]? headers = null, string? summary = null) -> void
Fluent.Pdf.PdfStructureTableAttributes.RowSpan.get -> int
Fluent.Pdf.PdfStructureTableAttributes.Summary.get -> string?
Fluent.Pdf.PdfSyntax
Fluent.Pdf.PdfSyntaxDocument
Fluent.Pdf.PdfSyntaxDocument.DecodeStream(Fluent.Pdf.PdfObjectId id, int revisionIndex = 0, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), Fluent.Pdf.PdfProviderRegistry? providers = null) -> Fluent.Pdf.PdfDecodedData!
Fluent.Pdf.PdfSyntaxDocument.OpenOriginalStream() -> System.IO.Stream!
Fluent.Pdf.PdfSyntaxDocument.OperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfSyntaxDocument.Resolve(Fluent.Pdf.PdfObjectId id, int revisionIndex = 0) -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfSyntaxDocument.Revisions.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfRevision!>!
Fluent.Pdf.PdfSyntaxDocument.Version.get -> string!
Fluent.Pdf.PdfTable
Fluent.Pdf.PdfTable.AllowRowSplit.get -> bool
Fluent.Pdf.PdfTable.Columns.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfTableColumn!>!
Fluent.Pdf.PdfTable.HeaderRows.get -> int
Fluent.Pdf.PdfTable.Layout(Fluent.Pdf.PdfFlowOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFlowResult!
Fluent.Pdf.PdfTable.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfTable.Rows.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfTableRow!>!
Fluent.Pdf.PdfTableBorder
Fluent.Pdf.PdfTableBorder.Color.get -> Fluent.Pdf.PdfColor!
Fluent.Pdf.PdfTableBorder.PdfTableBorder(double width = 0.5, Fluent.Pdf.PdfColor? color = null) -> void
Fluent.Pdf.PdfTableBorder.Width.get -> double
Fluent.Pdf.PdfTableCell
Fluent.Pdf.PdfTableCell.ColumnSpan.get -> int
Fluent.Pdf.PdfTableCell.Content.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFlowBlock!>!
Fluent.Pdf.PdfTableCell.PdfTableCell(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowBlock!>! content, int columnSpan = 1, int rowSpan = 1, Fluent.Pdf.PdfTableCellStyle? style = null) -> void
Fluent.Pdf.PdfTableCell.RowSpan.get -> int
Fluent.Pdf.PdfTableCell.Style.get -> Fluent.Pdf.PdfTableCellStyle!
Fluent.Pdf.PdfTableCellFragment
Fluent.Pdf.PdfTableCellFragment.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfTableCellFragment.Cell.get -> Fluent.Pdf.PdfTableCell!
Fluent.Pdf.PdfTableCellFragment.Column.get -> int
Fluent.Pdf.PdfTableCellFragment.IsContinuation.get -> bool
Fluent.Pdf.PdfTableCellFragment.IsRepeatedHeader.get -> bool
Fluent.Pdf.PdfTableCellFragment.Row.get -> int
Fluent.Pdf.PdfTableCellStyle
Fluent.Pdf.PdfTableCellStyle.Background.get -> Fluent.Pdf.PdfColor?
Fluent.Pdf.PdfTableCellStyle.Bottom.get -> Fluent.Pdf.PdfTableBorder!
Fluent.Pdf.PdfTableCellStyle.Left.get -> Fluent.Pdf.PdfTableBorder!
Fluent.Pdf.PdfTableCellStyle.Padding.get -> Fluent.Pdf.PdfMargins
Fluent.Pdf.PdfTableCellStyle.PdfTableCellStyle(Fluent.Pdf.PdfMargins? padding = null, Fluent.Pdf.PdfColor? background = null, Fluent.Pdf.PdfTableBorder? left = null, Fluent.Pdf.PdfTableBorder? top = null, Fluent.Pdf.PdfTableBorder? right = null, Fluent.Pdf.PdfTableBorder? bottom = null, Fluent.Pdf.PdfTableVerticalAlignment verticalAlignment = Fluent.Pdf.PdfTableVerticalAlignment.Top) -> void
Fluent.Pdf.PdfTableCellStyle.Right.get -> Fluent.Pdf.PdfTableBorder!
Fluent.Pdf.PdfTableCellStyle.Top.get -> Fluent.Pdf.PdfTableBorder!
Fluent.Pdf.PdfTableCellStyle.VerticalAlignment.get -> Fluent.Pdf.PdfTableVerticalAlignment
Fluent.Pdf.PdfTableColumn
Fluent.Pdf.PdfTableColumn.IsFixed.get -> bool
Fluent.Pdf.PdfTableColumn.Maximum.get -> double
Fluent.Pdf.PdfTableColumn.Minimum.get -> double
Fluent.Pdf.PdfTableColumn.Weight.get -> double
Fluent.Pdf.PdfTableData
Fluent.Pdf.PdfTableDataColumn<T>
Fluent.Pdf.PdfTableDataColumn<T>.CellStyle.get -> Fluent.Pdf.PdfTableCellStyle!
Fluent.Pdf.PdfTableDataColumn<T>.Format.get -> string?
Fluent.Pdf.PdfTableDataColumn<T>.NullText.get -> string!
Fluent.Pdf.PdfTableDataColumn<T>.ParagraphStyle.get -> Fluent.Pdf.PdfFlowParagraphStyle!
Fluent.Pdf.PdfTableDataColumn<T>.PdfTableDataColumn(Fluent.Pdf.PdfTableColumn! width, System.Func<T, object?>! value, Fluent.Pdf.PdfFlowTextStyle! textStyle, string? format = null, string! nullText = "", Fluent.Pdf.PdfTableCellStyle? cellStyle = null, Fluent.Pdf.PdfFlowParagraphStyle? paragraphStyle = null) -> void
Fluent.Pdf.PdfTableDataColumn<T>.TextStyle.get -> Fluent.Pdf.PdfFlowTextStyle!
Fluent.Pdf.PdfTableDataColumn<T>.Value.get -> System.Func<T, object?>!
Fluent.Pdf.PdfTableDataColumn<T>.Width.get -> Fluent.Pdf.PdfTableColumn!
Fluent.Pdf.PdfTableDetectionMode
Fluent.Pdf.PdfTableDetectionMode.Borderless = 1 -> Fluent.Pdf.PdfTableDetectionMode
Fluent.Pdf.PdfTableDetectionMode.Ruled = 0 -> Fluent.Pdf.PdfTableDetectionMode
Fluent.Pdf.PdfTableExtraction
Fluent.Pdf.PdfTableExtractionOptions
Fluent.Pdf.PdfTableExtractionOptions.HeaderRows.get -> int
Fluent.Pdf.PdfTableExtractionOptions.JoinConsecutivePages.get -> bool
Fluent.Pdf.PdfTableExtractionOptions.MaximumCells.get -> int
Fluent.Pdf.PdfTableExtractionOptions.MinimumColumnGap.get -> double
Fluent.Pdf.PdfTableExtractionOptions.MinimumColumns.get -> int
Fluent.Pdf.PdfTableExtractionOptions.MinimumRows.get -> int
Fluent.Pdf.PdfTableExtractionOptions.Mode.get -> Fluent.Pdf.PdfTableDetectionMode
Fluent.Pdf.PdfTableExtractionOptions.NumericCulture.get -> string?
Fluent.Pdf.PdfTableExtractionOptions.PdfTableExtractionOptions(Fluent.Pdf.PdfTableDetectionMode mode, Fluent.Pdf.PdfRect? region = null, double tolerance = 1, double minimumColumnGap = 12, int minimumRows = 2, int minimumColumns = 2, int maximumCells = 100000, int headerRows = 0, bool joinConsecutivePages = false, string? numericCulture = null) -> void
Fluent.Pdf.PdfTableExtractionOptions.Region.get -> Fluent.Pdf.PdfRect?
Fluent.Pdf.PdfTableExtractionOptions.Tolerance.get -> double
Fluent.Pdf.PdfTableExtractionResult
Fluent.Pdf.PdfTableExtractionResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfTableExtractionResult.Sequences.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedTableSequence!>!
Fluent.Pdf.PdfTableExtractionResult.Source.get -> Fluent.Pdf.PdfDocumentSnapshot!
Fluent.Pdf.PdfTableExtractionResult.Tables.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedTable!>!
Fluent.Pdf.PdfTableExtractionResult.WriteCsv(System.IO.Stream! output, Fluent.Pdf.PdfMergedCellCsvPolicy mergedCells, int sequenceIndex = 0, bool omitRepeatedHeaders = false, bool escapeSpreadsheetFormulas = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfTableExtractionResult.WriteJson(System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfTableExtractionResult.WriteJsonFile(string! path, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfTableFragment
Fluent.Pdf.PdfTableFragment.BlockIndex.get -> int
Fluent.Pdf.PdfTableFragment.BodyEnd.get -> double
Fluent.Pdf.PdfTableFragment.BodyStart.get -> double
Fluent.Pdf.PdfTableFragment.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfTableFragment.Cells.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfTableCellFragment!>!
Fluent.Pdf.PdfTableFragment.IsRepeatedHeader.get -> bool
Fluent.Pdf.PdfTableFragment.NestedHeaderBandHeight.get -> double
Fluent.Pdf.PdfTableFragment.NestedHeaders.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfTableHeaderRepeat!>!
Fluent.Pdf.PdfTableFragment.Table.get -> Fluent.Pdf.PdfTable!
Fluent.Pdf.PdfTableHeaderRepeat
Fluent.Pdf.PdfTableHeaderRepeat.Bounds.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.PdfTableHeaderRepeat.Table.get -> Fluent.Pdf.PdfTable!
Fluent.Pdf.PdfTableRow
Fluent.Pdf.PdfTableRow.AllowSplit.get -> bool
Fluent.Pdf.PdfTableRow.Cells.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfTableCell!>!
Fluent.Pdf.PdfTableRow.MinimumHeight.get -> double
Fluent.Pdf.PdfTableRow.PdfTableRow(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfTableCell!>! cells, double minimumHeight = 0, bool allowSplit = true) -> void
Fluent.Pdf.PdfTableVerticalAlignment
Fluent.Pdf.PdfTableVerticalAlignment.Bottom = 2 -> Fluent.Pdf.PdfTableVerticalAlignment
Fluent.Pdf.PdfTableVerticalAlignment.Center = 1 -> Fluent.Pdf.PdfTableVerticalAlignment
Fluent.Pdf.PdfTableVerticalAlignment.Top = 0 -> Fluent.Pdf.PdfTableVerticalAlignment
Fluent.Pdf.PdfTemplateLayout
Fluent.Pdf.PdfText
Fluent.Pdf.PdfTextDirection
Fluent.Pdf.PdfTextDirection.LeftToRight = 0 -> Fluent.Pdf.PdfTextDirection
Fluent.Pdf.PdfTextDirection.RightToLeft = 1 -> Fluent.Pdf.PdfTextDirection
Fluent.Pdf.PdfTextDirection.TopToBottom = 2 -> Fluent.Pdf.PdfTextDirection
Fluent.Pdf.PdfTextDrawing
Fluent.Pdf.PdfTextDrawing.Content.get -> Fluent.Pdf.PdfContentProgram!
Fluent.Pdf.PdfTextDrawing.Fonts.get -> System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfFontResource!>!
Fluent.Pdf.PdfTextDrawing.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfTextExtraction
Fluent.Pdf.PdfTextExtractionOptions
Fluent.Pdf.PdfTextExtractionOptions.IncludeHiddenLayers.get -> bool
Fluent.Pdf.PdfTextExtractionOptions.IncludeInvisibleText.get -> bool
Fluent.Pdf.PdfTextExtractionOptions.LayerConfiguration.get -> Fluent.Pdf.PdfValue?
Fluent.Pdf.PdfTextExtractionOptions.LayerUsage.get -> Fluent.Pdf.PdfLayerUsage
Fluent.Pdf.PdfTextExtractionOptions.LineTolerance.get -> double
Fluent.Pdf.PdfTextExtractionOptions.PdfTextExtractionOptions(bool includeHiddenLayers = false, bool includeInvisibleText = true, double lineTolerance = 2, double wordGapRatio = 0.25, Fluent.Pdf.PdfLayerUsage layerUsage = Fluent.Pdf.PdfLayerUsage.View, Fluent.Pdf.PdfValue? layerConfiguration = null) -> void
Fluent.Pdf.PdfTextExtractionOptions.WordGapRatio.get -> double
Fluent.Pdf.PdfTextExtractionResult
Fluent.Pdf.PdfTextExtractionResult.ActualTextSpans.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedSpan!>!
Fluent.Pdf.PdfTextExtractionResult.Characters.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedCharacter!>!
Fluent.Pdf.PdfTextExtractionResult.Geometry.get -> Fluent.Pdf.PdfPageGeometry!
Fluent.Pdf.PdfTextExtractionResult.LayoutText.get -> string!
Fluent.Pdf.PdfTextExtractionResult.Lines.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedLine!>!
Fluent.Pdf.PdfTextExtractionResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfTextExtractionResult.PageIndex.get -> int
Fluent.Pdf.PdfTextExtractionResult.Paragraphs.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedParagraph!>!
Fluent.Pdf.PdfTextExtractionResult.RawText.get -> string!
Fluent.Pdf.PdfTextExtractionResult.Text.get -> string!
Fluent.Pdf.PdfTextExtractionResult.Words.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedWord!>!
Fluent.Pdf.PdfTextFeature
Fluent.Pdf.PdfTextFeature.End.get -> int
Fluent.Pdf.PdfTextFeature.PdfTextFeature() -> void
Fluent.Pdf.PdfTextFeature.PdfTextFeature(string! tag, uint value = 1, int start = 0, int end = 2147483647) -> void
Fluent.Pdf.PdfTextFeature.Start.get -> int
Fluent.Pdf.PdfTextFeature.Tag.get -> string!
Fluent.Pdf.PdfTextFeature.Value.get -> uint
Fluent.Pdf.PdfTextMappingConfidence
Fluent.Pdf.PdfTextMappingConfidence.ActualText = 1 -> Fluent.Pdf.PdfTextMappingConfidence
Fluent.Pdf.PdfTextMappingConfidence.Inferred = 2 -> Fluent.Pdf.PdfTextMappingConfidence
Fluent.Pdf.PdfTextMappingConfidence.ToUnicode = 0 -> Fluent.Pdf.PdfTextMappingConfidence
Fluent.Pdf.PdfTextMappingConfidence.Unmapped = 3 -> Fluent.Pdf.PdfTextMappingConfidence
Fluent.Pdf.PdfTextMatch
Fluent.Pdf.PdfTextMatch.Bounds.get -> Fluent.Pdf.PdfRect?
Fluent.Pdf.PdfTextMatch.Characters.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedCharacter!>!
Fluent.Pdf.PdfTextMatch.IsPartialCluster.get -> bool
Fluent.Pdf.PdfTextMatch.Start.get -> int
Fluent.Pdf.PdfTextMatch.Text.get -> string!
Fluent.Pdf.PdfTextMatch.UsesActualText.get -> bool
Fluent.Pdf.PdfTextOptions
Fluent.Pdf.PdfTextOptions.Features.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfTextFeature>!
Fluent.Pdf.PdfTextOptions.Language.get -> string!
Fluent.Pdf.PdfTextOptions.ParagraphDirection.get -> Fluent.Pdf.PdfParagraphDirection
Fluent.Pdf.PdfTextOptions.PdfTextOptions(Fluent.Pdf.PdfParagraphDirection paragraphDirection = Fluent.Pdf.PdfParagraphDirection.Auto, bool vertical = false, string! language = "und", params Fluent.Pdf.PdfTextFeature[]! features) -> void
Fluent.Pdf.PdfTextOptions.Vertical.get -> bool
Fluent.Pdf.PdfTextQuery
Fluent.Pdf.PdfTextQuery.FontName.get -> string?
Fluent.Pdf.PdfTextQuery.IgnoreCase.get -> bool
Fluent.Pdf.PdfTextQuery.IsRegex.get -> bool
Fluent.Pdf.PdfTextQuery.MaximumFontSize.get -> double?
Fluent.Pdf.PdfTextQuery.MaximumMatches.get -> int
Fluent.Pdf.PdfTextQuery.MinimumFontSize.get -> double?
Fluent.Pdf.PdfTextQuery.Pattern.get -> string!
Fluent.Pdf.PdfTextQuery.PdfTextQuery(string! pattern, bool regex = false, bool ignoreCase = false, int maximumMatches = 10000, int regexTimeoutMilliseconds = 100, Fluent.Pdf.PdfRect? region = null, string? fontName = null, double? minimumFontSize = null, double? maximumFontSize = null) -> void
Fluent.Pdf.PdfTextQuery.RegexTimeoutMilliseconds.get -> int
Fluent.Pdf.PdfTextQuery.Region.get -> Fluent.Pdf.PdfRect?
Fluent.Pdf.PdfTextReplacementOptions
Fluent.Pdf.PdfTextReplacementOptions.FontSize.get -> double
Fluent.Pdf.PdfTextReplacementOptions.Overflow.get -> Fluent.Pdf.PdfTextReplacementOverflow
Fluent.Pdf.PdfTextReplacementOptions.PdfTextReplacementOptions(double fontSize, Fluent.Pdf.PdfTextReplacementOverflow overflow = Fluent.Pdf.PdfTextReplacementOverflow.Reject) -> void
Fluent.Pdf.PdfTextReplacementOverflow
Fluent.Pdf.PdfTextReplacementOverflow.Reject = 0 -> Fluent.Pdf.PdfTextReplacementOverflow
Fluent.Pdf.PdfTextReplacementOverflow.ShrinkToFit = 1 -> Fluent.Pdf.PdfTextReplacementOverflow
Fluent.Pdf.PdfTextSearch
Fluent.Pdf.PdfTextSearchResult
Fluent.Pdf.PdfTextSearchResult.Matches.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfTextMatch!>!
Fluent.Pdf.PdfTextSearchResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfTextSearchResult.PageIndex.get -> int
Fluent.Pdf.PdfTextSearchResult.Text.get -> string!
Fluent.Pdf.PdfTextString
Fluent.Pdf.PdfTextString.Encoding.get -> Fluent.Pdf.PdfTextStringEncoding
Fluent.Pdf.PdfTextString.MinimumVersion.get -> string!
Fluent.Pdf.PdfTextString.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfTextString.Text.get -> string!
Fluent.Pdf.PdfTextString.Value.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfTextStringEncoding
Fluent.Pdf.PdfTextStringEncoding.PdfDocEncoding = 0 -> Fluent.Pdf.PdfTextStringEncoding
Fluent.Pdf.PdfTextStringEncoding.Utf16BigEndian = 1 -> Fluent.Pdf.PdfTextStringEncoding
Fluent.Pdf.PdfTextStringEncoding.Utf8 = 2 -> Fluent.Pdf.PdfTextStringEncoding
Fluent.Pdf.PdfTransaction
Fluent.Pdf.PdfTransaction.Add(Fluent.Pdf.PdfValue! value) -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfTransaction.Dispose() -> void
Fluent.Pdf.PdfTransaction.LastOperationReport.get -> Fluent.Pdf.PdfOperationReport?
Fluent.Pdf.PdfTransaction.PdfTransaction(Fluent.Pdf.PdfSyntaxDocument? source = null, Fluent.Pdf.PdfOperationPolicy? policy = null) -> void
Fluent.Pdf.PdfTransaction.Remove(Fluent.Pdf.PdfObjectId id) -> void
Fluent.Pdf.PdfTransaction.Resolve(Fluent.Pdf.PdfObjectId id) -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfTransaction.Save(System.IO.Stream! output, Fluent.Pdf.PdfWriteOptions? options = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfTransaction.SaveFile(string! path, Fluent.Pdf.PdfWriteOptions? options = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfTransaction.Set(Fluent.Pdf.PdfObjectId id, Fluent.Pdf.PdfValue! value) -> void
Fluent.Pdf.PdfTransaction.Source.get -> Fluent.Pdf.PdfSyntaxDocument?
Fluent.Pdf.PdfTransaction.Trailer.get -> Fluent.Pdf.PdfValue!
Fluent.Pdf.PdfTransaction.Trailer.set -> void
Fluent.Pdf.PdfTransitionDimension
Fluent.Pdf.PdfTransitionDimension.Horizontal = 0 -> Fluent.Pdf.PdfTransitionDimension
Fluent.Pdf.PdfTransitionDimension.Vertical = 1 -> Fluent.Pdf.PdfTransitionDimension
Fluent.Pdf.PdfTransitionMotion
Fluent.Pdf.PdfTransitionMotion.Inward = 0 -> Fluent.Pdf.PdfTransitionMotion
Fluent.Pdf.PdfTransitionMotion.Outward = 1 -> Fluent.Pdf.PdfTransitionMotion
Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransitionStyle.Blinds = 2 -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransitionStyle.Box = 3 -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransitionStyle.Cover = 9 -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransitionStyle.Dissolve = 5 -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransitionStyle.Fade = 11 -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransitionStyle.Fly = 7 -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransitionStyle.Glitter = 6 -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransitionStyle.Push = 8 -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransitionStyle.Replace = 0 -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransitionStyle.Split = 1 -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransitionStyle.Uncover = 10 -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransitionStyle.Wipe = 4 -> Fluent.Pdf.PdfTransitionStyle
Fluent.Pdf.PdfTransparencyGroup
Fluent.Pdf.PdfTransparencyGroup.ColorSpace.get -> Fluent.Pdf.PdfColorSpace!
Fluent.Pdf.PdfTransparencyGroup.Isolated.get -> bool
Fluent.Pdf.PdfTransparencyGroup.Knockout.get -> bool
Fluent.Pdf.PdfTransparencyGroup.Resource.get -> Fluent.Pdf.PdfGraphicsResource!
Fluent.Pdf.PdfType3Glyph
Fluent.Pdf.PdfType3Glyph.Name.get -> string!
Fluent.Pdf.PdfType3Glyph.PdfType3Glyph(string! name, string! unicodeText, Fluent.Pdf.PdfContentProgram! program) -> void
Fluent.Pdf.PdfType3Glyph.Program.get -> Fluent.Pdf.PdfContentProgram!
Fluent.Pdf.PdfType3Glyph.UnicodeText.get -> string!
Fluent.Pdf.PdfUnicode
Fluent.Pdf.PdfUnicodeAnalysis
Fluent.Pdf.PdfUnicodeAnalysis.Graphemes.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfGraphemeCluster>!
Fluent.Pdf.PdfUnicodeAnalysis.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfUnicodeAnalysis.Paragraphs.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfBidiParagraph>!
Fluent.Pdf.PdfUnicodeAnalysis.Scalars.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfUnicodeScalar>!
Fluent.Pdf.PdfUnicodeAnalysis.Text.get -> string!
Fluent.Pdf.PdfUnicodeAnalysis.VisualScalarOrder.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.PdfUnicodeMap
Fluent.Pdf.PdfUnicodeMap.Count.get -> int
Fluent.Pdf.PdfUnicodeMap.Decode(Fluent.Pdf.PdfValue! encodedString, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDecodedText!
Fluent.Pdf.PdfUnicodeMap.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfUnicodeScalar
Fluent.Pdf.PdfUnicodeScalar.BidiClass.get -> Fluent.Pdf.PdfBidiClass
Fluent.Pdf.PdfUnicodeScalar.DefaultIgnorable.get -> bool
Fluent.Pdf.PdfUnicodeScalar.EmbeddingLevel.get -> int
Fluent.Pdf.PdfUnicodeScalar.PdfUnicodeScalar() -> void
Fluent.Pdf.PdfUnicodeScalar.ScriptTag.get -> uint
Fluent.Pdf.PdfUnicodeScalar.Utf16Length.get -> int
Fluent.Pdf.PdfUnicodeScalar.Utf16Offset.get -> int
Fluent.Pdf.PdfUnicodeScalar.Value.get -> int
Fluent.Pdf.PdfUnits
Fluent.Pdf.PdfUnknownExtensionPolicy
Fluent.Pdf.PdfUnknownExtensionPolicy.Preserve = 0 -> Fluent.Pdf.PdfUnknownExtensionPolicy
Fluent.Pdf.PdfUnknownExtensionPolicy.Reject = 1 -> Fluent.Pdf.PdfUnknownExtensionPolicy
Fluent.Pdf.PdfUnknownOperatorPolicy
Fluent.Pdf.PdfUnknownOperatorPolicy.Preserve = 1 -> Fluent.Pdf.PdfUnknownOperatorPolicy
Fluent.Pdf.PdfUnknownOperatorPolicy.Reject = 0 -> Fluent.Pdf.PdfUnknownOperatorPolicy
Fluent.Pdf.PdfValue
Fluent.Pdf.PdfValue.BooleanValue.get -> bool
Fluent.Pdf.PdfValue.Entries.get -> System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfValue!>!
Fluent.Pdf.PdfValue.Items.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfValue!>!
Fluent.Pdf.PdfValue.Kind.get -> Fluent.Pdf.PdfValueKind
Fluent.Pdf.PdfValue.NameValue.get -> string!
Fluent.Pdf.PdfValue.NumberText.get -> string!
Fluent.Pdf.PdfValue.NumberValue.get -> double
Fluent.Pdf.PdfValue.OpenRawStream() -> System.IO.Stream!
Fluent.Pdf.PdfValue.RawStreamLength.get -> long
Fluent.Pdf.PdfValue.ReferenceId.get -> Fluent.Pdf.PdfObjectId
Fluent.Pdf.PdfValue.StringBytes.get -> System.Collections.Generic.IReadOnlyList<byte>!
Fluent.Pdf.PdfValueKind
Fluent.Pdf.PdfValueKind.Array = 5 -> Fluent.Pdf.PdfValueKind
Fluent.Pdf.PdfValueKind.Boolean = 1 -> Fluent.Pdf.PdfValueKind
Fluent.Pdf.PdfValueKind.Dictionary = 6 -> Fluent.Pdf.PdfValueKind
Fluent.Pdf.PdfValueKind.Name = 3 -> Fluent.Pdf.PdfValueKind
Fluent.Pdf.PdfValueKind.Null = 0 -> Fluent.Pdf.PdfValueKind
Fluent.Pdf.PdfValueKind.Number = 2 -> Fluent.Pdf.PdfValueKind
Fluent.Pdf.PdfValueKind.Reference = 7 -> Fluent.Pdf.PdfValueKind
Fluent.Pdf.PdfValueKind.Stream = 8 -> Fluent.Pdf.PdfValueKind
Fluent.Pdf.PdfValueKind.String = 4 -> Fluent.Pdf.PdfValueKind
Fluent.Pdf.PdfValues
Fluent.Pdf.PdfViewSession
Fluent.Pdf.PdfViewSession.CachedBytes.get -> long
Fluent.Pdf.PdfViewSession.CachedRasterCount.get -> int
Fluent.Pdf.PdfViewSession.CancelPending() -> void
Fluent.Pdf.PdfViewSession.ClearCache() -> void
Fluent.Pdf.PdfViewSession.DisplayToUser(int pageIndex, Fluent.Pdf.PdfPoint point) -> Fluent.Pdf.PdfPoint
Fluent.Pdf.PdfViewSession.Dispose() -> void
Fluent.Pdf.PdfViewSession.HitTest(int pageIndex, Fluent.Pdf.PdfPoint point, double radius = 2, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfExtractedCharacter?
Fluent.Pdf.PdfViewSession.PdfViewSession(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfProviderRegistry! providers, long maximumCachedBytes = 16777216, int maximumOutstandingRequests = 8, Fluent.Pdf.PdfOperationPolicy? policy = null) -> void
Fluent.Pdf.PdfViewSession.PlanTiles(int pageIndex, double dpi, int tilePixels = 256, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfRenderOptions!>!
Fluent.Pdf.PdfViewSession.RenderAsync(int pageIndex, Fluent.Pdf.PdfRenderOptions? options = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Threading.Tasks.Task<Fluent.Pdf.PdfRaster!>!
Fluent.Pdf.PdfViewSession.RenderThumbnailAsync(int pageIndex, int maximumWidth, int maximumHeight, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Threading.Tasks.Task<Fluent.Pdf.PdfRaster!>!
Fluent.Pdf.PdfViewSession.SelectText(int pageIndex, Fluent.Pdf.PdfRect region, Fluent.Pdf.PdfTextExtractionOptions? options = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedCharacter!>!
Fluent.Pdf.PdfWordConversion
Fluent.Pdf.PdfWordConversionOptions
Fluent.Pdf.PdfWordConversionOptions.MaximumArchiveEntries.get -> int
Fluent.Pdf.PdfWordConversionOptions.MaximumCharacters.get -> int
Fluent.Pdf.PdfWordConversionOptions.MaximumDepth.get -> int
Fluent.Pdf.PdfWordConversionOptions.MaximumInputBytes.get -> int
Fluent.Pdf.PdfWordConversionOptions.MaximumOutputBytes.get -> int
Fluent.Pdf.PdfWordConversionOptions.MaximumPages.get -> int
Fluent.Pdf.PdfWordConversionOptions.MaximumProviderExpandedBytes.get -> int
Fluent.Pdf.PdfWordConversionOptions.PdfWordConversionOptions(Fluent.Pdf.PdfWordReconstruction reconstruction, int maximumInputBytes = 4194304, int maximumOutputBytes = 16777216, int maximumProviderExpandedBytes = 33554432, int maximumCharacters = 131072, int maximumArchiveEntries = 1024, int maximumPages = 1000, int maximumDepth = 32, double rasterDpi = 144) -> void
Fluent.Pdf.PdfWordConversionOptions.RasterDpi.get -> double
Fluent.Pdf.PdfWordConversionOptions.Reconstruction.get -> Fluent.Pdf.PdfWordReconstruction
Fluent.Pdf.PdfWordFormat
Fluent.Pdf.PdfWordFormat.Doc = 0 -> Fluent.Pdf.PdfWordFormat
Fluent.Pdf.PdfWordFormat.Docx = 1 -> Fluent.Pdf.PdfWordFormat
Fluent.Pdf.PdfWordFormat.Rtf = 2 -> Fluent.Pdf.PdfWordFormat
Fluent.Pdf.PdfWordReconstruction
Fluent.Pdf.PdfWordReconstruction.Editable = 1 -> Fluent.Pdf.PdfWordReconstruction
Fluent.Pdf.PdfWordReconstruction.FixedLayout = 0 -> Fluent.Pdf.PdfWordReconstruction
Fluent.Pdf.PdfWriteMode
Fluent.Pdf.PdfWriteMode.Full = 0 -> Fluent.Pdf.PdfWriteMode
Fluent.Pdf.PdfWriteMode.Incremental = 1 -> Fluent.Pdf.PdfWriteMode
Fluent.Pdf.PdfWriteOptions
Fluent.Pdf.PdfWriteOptions.AllowSignatureInvalidation.get -> bool
Fluent.Pdf.PdfWriteOptions.CrossReferenceFormat.get -> Fluent.Pdf.PdfCrossReferenceFormat
Fluent.Pdf.PdfWriteOptions.Extensions.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtensionRequirement!>!
Fluent.Pdf.PdfWriteOptions.MinimumVersion.get -> string?
Fluent.Pdf.PdfWriteOptions.Mode.get -> Fluent.Pdf.PdfWriteMode
Fluent.Pdf.PdfWriteOptions.PdfWriteOptions(Fluent.Pdf.PdfWriteMode mode = Fluent.Pdf.PdfWriteMode.Full, Fluent.Pdf.PdfCrossReferenceFormat crossReferenceFormat = Fluent.Pdf.PdfCrossReferenceFormat.Auto, string? version = null, bool allowSignatureInvalidation = false, string? minimumVersion = null, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfExtensionRequirement!>? extensions = null, Fluent.Pdf.PdfUnknownExtensionPolicy unknownExtensions = Fluent.Pdf.PdfUnknownExtensionPolicy.Preserve) -> void
Fluent.Pdf.PdfWriteOptions.UnknownExtensions.get -> Fluent.Pdf.PdfUnknownExtensionPolicy
Fluent.Pdf.PdfWriteOptions.Version.get -> string?
Fluent.Pdf.PdfXfa
Fluent.Pdf.PdfXfaPacket
Fluent.Pdf.PdfXfaPacket.Name.get -> string!
Fluent.Pdf.PdfXfaPacket.ObjectId.get -> Fluent.Pdf.PdfObjectId?
Fluent.Pdf.PdfXfaPacket.Xml.get -> string!
Fluent.Pdf.PdfXfaSet
Fluent.Pdf.PdfXfaSet.Data.get -> System.Collections.Generic.IReadOnlyDictionary<string!, string!>!
Fluent.Pdf.PdfXfaSet.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.PdfXfaSet.Packets.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfXfaPacket!>!
const Fluent.Pdf.PdfOcrAccuracy.ComparisonProfile = "unicode-scalars+whitespace15.1+ordinal-v1" -> string!
override Fluent.Pdf.PdfObjectId.Equals(object? obj) -> bool
override Fluent.Pdf.PdfObjectId.GetHashCode() -> int
override Fluent.Pdf.PdfObjectId.ToString() -> string!
override Fluent.Pdf.PdfPageBoxes.Equals(object? obj) -> bool
override Fluent.Pdf.PdfPageBoxes.GetHashCode() -> int
override Fluent.Pdf.PdfPageGeometry.Equals(object? obj) -> bool
override Fluent.Pdf.PdfPageGeometry.GetHashCode() -> int
override Fluent.Pdf.PdfPageSize.Equals(object? obj) -> bool
override Fluent.Pdf.PdfPageSize.GetHashCode() -> int
static Fluent.Pdf.ManagedPdfProvider.Instance.get -> Fluent.Pdf.ManagedPdfProvider!
static Fluent.Pdf.Pdf.Create(Fluent.Pdf.PdfOptions? options = null, Fluent.Pdf.PdfProviderRegistry? providers = null) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Pdf.Open(System.IO.Stream! input, Fluent.Pdf.PdfOptions? options = null, Fluent.Pdf.PdfProviderRegistry? providers = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.Pdf.OpenFile(string! path, Fluent.Pdf.PdfOptions? options = null, Fluent.Pdf.PdfProviderRegistry? providers = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfAccessibility.Author(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfStructurePlan! plan, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfAccessibility.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfAccessibilityProfile profile = Fluent.Pdf.PdfAccessibilityProfile.Ua1, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfAccessibilityInspection!
static Fluent.Pdf.PdfAccessibility.Remediate(Fluent.Pdf.PdfAccessibilityInspection! source, Fluent.Pdf.PdfObjectId elementId, string? role = null, string? language = null, string? alternativeText = null, string? actualText = null, int[]? childOrder = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfAccessibility.Validate(System.IO.Stream! input, Fluent.Pdf.PdfAccessibilityProfile profile, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfAccessibilityHumanReview? humanReview = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfAccessibilityValidation!
static Fluent.Pdf.PdfAction.Advanced(Fluent.Pdf.PdfValue! definition) -> Fluent.Pdf.PdfAction!
static Fluent.Pdf.PdfAction.GoTo(Fluent.Pdf.PdfDestination! destination) -> Fluent.Pdf.PdfAction!
static Fluent.Pdf.PdfAction.GoToNamed(string! name) -> Fluent.Pdf.PdfAction!
static Fluent.Pdf.PdfAction.Launch(string! fileName, bool newWindow = false) -> Fluent.Pdf.PdfAction!
static Fluent.Pdf.PdfAction.Remote(string! fileName, string! destinationName, bool newWindow = false) -> Fluent.Pdf.PdfAction!
static Fluent.Pdf.PdfAction.Uri(string! uri) -> Fluent.Pdf.PdfAction!
static Fluent.Pdf.PdfAnnotation.Create(Fluent.Pdf.PdfAnnotationKind kind, string! identifier, Fluent.Pdf.PdfRect bounds, Fluent.Pdf.PdfGraphicsResource! normalAppearance, Fluent.Pdf.PdfValue? properties = null, Fluent.Pdf.PdfAnnotationMetadata? metadata = null, string! minimumVersion = "1.7", Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfAnnotation!
static Fluent.Pdf.PdfAnnotation.CreateFreeText(string! identifier, Fluent.Pdf.PdfRect bounds, Fluent.Pdf.PdfShapedText! text, double fontSize, Fluent.Pdf.PdfAnnotationMetadata? metadata = null, double padding = 2, bool allowClipping = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfAnnotation!
static Fluent.Pdf.PdfAnnotation.CreateMarkup(Fluent.Pdf.PdfAnnotationKind kind, string! identifier, Fluent.Pdf.PdfRect bounds, Fluent.Pdf.PdfColor! color, Fluent.Pdf.PdfValue? properties = null, Fluent.Pdf.PdfAnnotationMetadata? metadata = null, double strokeWidth = 1, double opacity = 1, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfAnnotation!
static Fluent.Pdf.PdfAnnotationSet.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfAnnotationSet!
static Fluent.Pdf.PdfAnnotationSet.ReadXfdf(System.IO.Stream! input, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfAnnotationSet!
static Fluent.Pdf.PdfAnnotations.Add(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfAnnotation!>! annotations, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfArchival.Convert(Fluent.Pdf.PdfDocumentSnapshot! source, System.IO.Stream! output, Fluent.Pdf.PdfArchivalOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfArchivalConversion!
static Fluent.Pdf.PdfArchival.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfArchivalProfile profile, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfArchivalInspection!
static Fluent.Pdf.PdfArchival.ProfileIdentifier(Fluent.Pdf.PdfArchivalProfile profile) -> string!
static Fluent.Pdf.PdfArchival.Validate(System.IO.Stream! input, Fluent.Pdf.PdfArchivalProfile profile, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfArchivalValidation!
static Fluent.Pdf.PdfAttachments.Add(Fluent.Pdf.PdfDocumentSnapshot! source, System.IO.Stream! input, Fluent.Pdf.PdfAttachmentOptions! attachment, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfAttachments.Extract(Fluent.Pdf.PdfAttachment! attachment, System.IO.Stream! output, string? variant = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfAttachments.ExtractToDirectory(Fluent.Pdf.PdfAttachment! attachment, string! directory, string? fileName = null, bool overwrite = false, string? variant = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfAttachments.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfAttachmentSet!
static Fluent.Pdf.PdfAttachments.RemoveAll(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfAttachments.SetCollection(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfValue? collection, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfAttachments.SetCollectionItem(Fluent.Pdf.PdfAttachment! attachment, Fluent.Pdf.PdfValue? item, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfBarcodes.Detect(Fluent.Pdf.PdfOcrRaster! raster, Fluent.Pdf.IPdfBarcodeDecoder? independentDecoder = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfBarcodeDetection!
static Fluent.Pdf.PdfBarcodes.DetectPage(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.IPdfBarcodeDecoder? independentDecoder = null, Fluent.Pdf.PdfRenderOptions? render = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfBarcodeDetection!
static Fluent.Pdf.PdfBarcodes.Generate(string! text, Fluent.Pdf.IPdfBarcodeDecoder? independentDecoder = null, Fluent.Pdf.PdfBarcodeOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfBarcodeSymbol!
static Fluent.Pdf.PdfBarcodes.GeneratePayment(Fluent.Pdf.PdfSepaCreditTransfer! payment, Fluent.Pdf.IPdfBarcodeDecoder? independentDecoder = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfBarcodeSymbol!
static Fluent.Pdf.PdfCanvas.Create(Fluent.Pdf.PdfPageGeometry! geometry, System.Action<Fluent.Pdf.PdfCanvas!>! draw, Fluent.Pdf.PdfCanvasOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfCanvasResult!
static Fluent.Pdf.PdfCanvasStyle.Default.get -> Fluent.Pdf.PdfCanvasStyle!
static Fluent.Pdf.PdfColorRendering.RenderPage(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfColorRenderOptions! colorManagement, Fluent.Pdf.PdfRenderOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfRaster!
static Fluent.Pdf.PdfColorSpace.CalGray(double whiteX, double whiteZ, double gamma = 1, double blackX = 0, double blackY = 0, double blackZ = 0) -> Fluent.Pdf.PdfColorSpace!
static Fluent.Pdf.PdfColorSpace.CalRgb(double whiteX, double whiteZ, double[]! gamma, double[]! matrix) -> Fluent.Pdf.PdfColorSpace!
static Fluent.Pdf.PdfColorSpace.Cmyk.get -> Fluent.Pdf.PdfColorSpace!
static Fluent.Pdf.PdfColorSpace.DeviceN(string![]! names, Fluent.Pdf.PdfColorSpace! alternate, Fluent.Pdf.PdfFunction! tint) -> Fluent.Pdf.PdfColorSpace!
static Fluent.Pdf.PdfColorSpace.Gray.get -> Fluent.Pdf.PdfColorSpace!
static Fluent.Pdf.PdfColorSpace.Icc(System.IO.Stream! input, Fluent.Pdf.PdfColorSpace? alternate = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfColorSpace!
static Fluent.Pdf.PdfColorSpace.Indexed(Fluent.Pdf.PdfColorSpace! baseSpace, int highValue, byte[]! palette, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfColorSpace!
static Fluent.Pdf.PdfColorSpace.Lab(double whiteX, double whiteZ, double aMinimum = -100, double aMaximum = 100, double bMinimum = -100, double bMaximum = 100, double blackX = 0, double blackY = 0, double blackZ = 0) -> Fluent.Pdf.PdfColorSpace!
static Fluent.Pdf.PdfColorSpace.Pattern(Fluent.Pdf.PdfColorSpace? baseSpace = null) -> Fluent.Pdf.PdfColorSpace!
static Fluent.Pdf.PdfColorSpace.Rgb.get -> Fluent.Pdf.PdfColorSpace!
static Fluent.Pdf.PdfColorSpace.Separation(string! name, Fluent.Pdf.PdfColorSpace! alternate, Fluent.Pdf.PdfFunction! tint) -> Fluent.Pdf.PdfColorSpace!
static Fluent.Pdf.PdfComparison.Compare(Fluent.Pdf.PdfDocumentSnapshot! before, Fluent.Pdf.PdfDocumentSnapshot! after, Fluent.Pdf.PdfComparisonOptions? options = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfComparisonResult!
static Fluent.Pdf.PdfCompatibility.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfCompatibilityInspection!
static Fluent.Pdf.PdfContentOperation.Extension(string! token, string! minimumVersion, params Fluent.Pdf.PdfValue![]! operands) -> Fluent.Pdf.PdfContentOperation!
static Fluent.Pdf.PdfContentOperation.InlineImage(Fluent.Pdf.PdfValue! dictionary, System.IO.Stream! encodedInput, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfContentOperation!
static Fluent.Pdf.PdfContentProgram.Create(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfContentOperation!>! operations, Fluent.Pdf.PdfContentKind kind = Fluent.Pdf.PdfContentKind.Page, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfContentProgram!
static Fluent.Pdf.PdfContentProgram.Parse(System.IO.Stream! decodedInput, Fluent.Pdf.PdfContentKind kind = Fluent.Pdf.PdfContentKind.Page, Fluent.Pdf.PdfUnknownOperatorPolicy unknownOperators = Fluent.Pdf.PdfUnknownOperatorPolicy.Reject, string? unknownOperatorVersion = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfContentProgram!
static Fluent.Pdf.PdfDataGeneration.Generate<T>(System.Collections.Generic.IEnumerable<T>! source, Fluent.Pdf.PdfGenerationTemplate<T>! template, Fluent.Pdf.PdfGenerationOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfDataGeneration.Write<T>(System.Collections.Generic.IEnumerable<T>! source, Fluent.Pdf.PdfGenerationTemplate<T>! template, Fluent.Pdf.PdfGenerationOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, System.IO.Stream! output, Fluent.Pdf.PdfWriteOptions? writeOptions = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfDataGeneration.WriteFile<T>(System.Collections.Generic.IEnumerable<T>! source, Fluent.Pdf.PdfGenerationTemplate<T>! template, Fluent.Pdf.PdfGenerationOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, string! path, Fluent.Pdf.PdfWriteOptions? writeOptions = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfDrawing.Create(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfContentOperation!>! operations, Fluent.Pdf.PdfResourceSet? resources = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDrawing!
static Fluent.Pdf.PdfEngineering.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfEngineeringInspection!
static Fluent.Pdf.PdfEngineering.Preserve(System.IO.Stream! input, System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfEngineeringPreservation!
static Fluent.Pdf.PdfFilters.Decode(Fluent.Pdf.PdfValue! encodedStream, Fluent.Pdf.PdfOperationContext! context, Fluent.Pdf.PdfProviderRegistry? providers = null) -> System.IO.Stream!
static Fluent.Pdf.PdfFilters.Encode(System.IO.Stream! decodedInput, System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFilter!>! decodingFilters, Fluent.Pdf.PdfOperationContext! context, Fluent.Pdf.PdfImageInfo? imageInfo = null, Fluent.Pdf.PdfProviderRegistry? providers = null) -> System.IO.Stream!
static Fluent.Pdf.PdfFilters.Inspect(Fluent.Pdf.PdfValue! dictionary) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfFilter!>!
static Fluent.Pdf.PdfFlow.Layout(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowBlock!>! blocks, Fluent.Pdf.PdfFlowOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFlowResult!
static Fluent.Pdf.PdfFlowList.Create(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowListItem!>! items, Fluent.Pdf.PdfFlowTextStyle! markerStyle, Fluent.Pdf.PdfFlowListKind kind = Fluent.Pdf.PdfFlowListKind.Decimal, int start = 1, string! prefix = "", string! suffix = ".", string! bullet = "•", double? markerWidth = null, double gap = 6, int maximumItems = 100000, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFlowList!
static Fluent.Pdf.PdfFont.Read(System.IO.Stream! input, Fluent.Pdf.PdfProviderRegistry! providers, int faceIndex = 0, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFont!
static Fluent.Pdf.PdfFont.Standard(string! postScriptName, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFont!
static Fluent.Pdf.PdfFont.Type3(string! postScriptName, Fluent.Pdf.PdfFontMetrics metrics, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfType3Glyph!>! glyphs, Fluent.Pdf.PdfValue? resources = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFont!
static Fluent.Pdf.PdfFontResource.Create(Fluent.Pdf.PdfFont! font, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfGlyphMapping!>! mappings, bool subset = true, bool vertical = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFontResource!
static Fluent.Pdf.PdfFontResource.CreateText(Fluent.Pdf.PdfFont! font, string! text, bool subset = true, bool vertical = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFontResource!
static Fluent.Pdf.PdfFormData.Create(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFormDataField!>! fields, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFormData!
static Fluent.Pdf.PdfFormData.Read(System.IO.Stream! input, Fluent.Pdf.PdfFormDataFormat format, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFormData!
static Fluent.Pdf.PdfForms.Add(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfFormFieldDefinition! definition, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfForms.AddWidget(Fluent.Pdf.PdfFormField! field, Fluent.Pdf.PdfFormWidgetDefinition! widget, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfForms.CopyField(Fluent.Pdf.PdfFormField! field, Fluent.Pdf.PdfDocumentSnapshot! target, string! partialName, System.Collections.Generic.IReadOnlyDictionary<int, int>! pageMap, bool retainOpaqueNameReferences = false, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfForms.CreateTextAppearance(Fluent.Pdf.PdfShapedText! text, Fluent.Pdf.PdfRect bounds, double fontSize, double padding = 2, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfGraphicsResource!
static Fluent.Pdf.PdfForms.DecorateWidget(Fluent.Pdf.PdfFormField! field, Fluent.Pdf.PdfObjectId widgetId, Fluent.Pdf.PdfGraphicsResource! decoration, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfForms.EditWidget(Fluent.Pdf.PdfFormField! field, Fluent.Pdf.PdfObjectId widgetId, Fluent.Pdf.PdfFormWidgetDefinition! definition, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfForms.Fill(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFormUpdate!>! updates, bool retainSourceRegenerationRequest = false, Fluent.Pdf.PdfWriteMode writeMode = Fluent.Pdf.PdfWriteMode.Full, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfForms.FillData(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfFormData! data, Fluent.Pdf.PdfFormTextStyle? textStyle = null, bool retainSourceRegenerationRequest = false, Fluent.Pdf.PdfWriteMode writeMode = Fluent.Pdf.PdfWriteMode.Full, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfForms.Flatten(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<string!>? fieldNames = null, Fluent.Pdf.PdfAnnotationView view = Fluent.Pdf.PdfAnnotationView.Screen, bool acceptRequestedRegeneration = false, bool retainOpaqueNameReferences = false, Fluent.Pdf.PdfWriteMode writeMode = Fluent.Pdf.PdfWriteMode.Full, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfForms.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFormSet!
static Fluent.Pdf.PdfForms.ReadValues(Fluent.Pdf.PdfDocumentSnapshot! source, bool includeNoExport = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFormData!
static Fluent.Pdf.PdfForms.Rename(Fluent.Pdf.PdfFormField! field, string! partialName, bool retainOpaqueNameReferences = false, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfForms.SetCalculationAction(Fluent.Pdf.PdfFormField! field, Fluent.Pdf.PdfAction? action, Fluent.Pdf.PdfActionPolicy? actionPolicy = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfForms.SetCalculationOrder(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfObjectId>! fields, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfForms.SetCommonFlags(Fluent.Pdf.PdfFormField! field, Fluent.Pdf.PdfFormFieldFlags flags, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfForms.SetTabOrder(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfFormTabOrder order, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfFunction.Exponential(double[]! start, double[]! end, double exponent = 1, double domainMinimum = 0, double domainMaximum = 1, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFunction!
static Fluent.Pdf.PdfFunction.Sampled(double[]! domain, double[]! range, int[]! size, int bitsPerSample, byte[]! samples, int order = 1, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFunction!
static Fluent.Pdf.PdfFunction.Stitch(Fluent.Pdf.PdfFunction![]! functions, double[]! bounds, double[]! encode, double domainMinimum = 0, double domainMaximum = 1, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfFunction!
static Fluent.Pdf.PdfGraphicsObjects.Form(Fluent.Pdf.PdfDrawing! drawing, Fluent.Pdf.PdfRect bounds, Fluent.Pdf.PdfMatrix? matrix = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfGraphicsResource!
static Fluent.Pdf.PdfGraphicsResource.Create(Fluent.Pdf.PdfGraphicsResourceCategory category, Fluent.Pdf.PdfValue! definition, string! minimumVersion, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfGraphicsResource!
static Fluent.Pdf.PdfGraphicsResource.Font(Fluent.Pdf.PdfFontResource! font) -> Fluent.Pdf.PdfGraphicsResource!
static Fluent.Pdf.PdfGraphicsState.Create(string! blendMode = "Normal", double fillAlpha = 1, double strokeAlpha = 1, Fluent.Pdf.PdfSoftMask? softMask = null, bool alphaIsShape = false, bool textKnockout = true, bool strokeOverprint = false, bool fillOverprint = false, int overprintMode = 0, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfGraphicsState!
static Fluent.Pdf.PdfGraphicsState.SupportedBlendModes.get -> System.Collections.Generic.IReadOnlyList<string!>!
static Fluent.Pdf.PdfHtml.Render(Fluent.Pdf.PdfHtmlSource! source, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfHtmlOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfHtmlPrintOptions.Default.get -> Fluent.Pdf.PdfHtmlPrintOptions!
static Fluent.Pdf.PdfHtmlReadinessOptions.Default.get -> Fluent.Pdf.PdfHtmlReadinessOptions!
static Fluent.Pdf.PdfHtmlResource.Read(System.Uri! address, string! mediaType, System.IO.Stream! input, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfHtmlResource!
static Fluent.Pdf.PdfHtmlSource.FromHtml(string! markup, System.Uri? address = null, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfHtmlResource!>? resources = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfHtmlSource!
static Fluent.Pdf.PdfHtmlSource.FromResource(System.Uri! address, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfHtmlResource!>! resources, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfHtmlSource!
static Fluent.Pdf.PdfHtmlSource.Read(System.IO.Stream! input, System.Uri? address = null, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfHtmlResource!>? resources = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfHtmlSource!
static Fluent.Pdf.PdfImages.Deduplicate(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfImageAsset!>! images, int maximumImages = 10000, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfImageDeduplicationResult!
static Fluent.Pdf.PdfImages.FromEncoded(System.IO.Stream! input, Fluent.Pdf.PdfImageInfo! info, Fluent.Pdf.PdfColorSpace! colorSpace, Fluent.Pdf.PdfFilter![]! filters, Fluent.Pdf.PdfImageAsset? softMask = null, double[]? decode = null, Fluent.Pdf.PdfImageMetadata? metadata = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfImageAsset!
static Fluent.Pdf.PdfImages.FromRgba(System.IO.Stream! input, int width, int height, int bitsPerComponent = 8, Fluent.Pdf.PdfColorSpace? colorSpace = null, Fluent.Pdf.PdfImageMetadata? metadata = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfImageAsset!
static Fluent.Pdf.PdfImages.FromSamples(System.IO.Stream! input, Fluent.Pdf.PdfImageInfo! info, Fluent.Pdf.PdfColorSpace! colorSpace, Fluent.Pdf.PdfImageAsset? softMask = null, double[]? decode = null, bool interpolate = false, Fluent.Pdf.PdfImageMetadata? metadata = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfImageAsset!
static Fluent.Pdf.PdfImages.FromStencil(System.IO.Stream! input, int width, int height, bool invert = false, Fluent.Pdf.PdfImageMetadata? metadata = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfImageAsset!
static Fluent.Pdf.PdfImages.Read(System.IO.Stream! input, Fluent.Pdf.PdfImageReadOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfImageAsset!
static Fluent.Pdf.PdfImages.WithColorKey(Fluent.Pdf.PdfImageAsset! image, int[]! componentRanges, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfImageAsset!
static Fluent.Pdf.PdfImages.WithHardMask(Fluent.Pdf.PdfImageAsset! image, Fluent.Pdf.PdfImageAsset! stencil, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfImageAsset!
static Fluent.Pdf.PdfInputConversion.Generate(System.IO.Stream! input, Fluent.Pdf.PdfInputFormat format, Fluent.Pdf.PdfInputConversionOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfInputConversion.Write(System.IO.Stream! input, Fluent.Pdf.PdfInputFormat format, Fluent.Pdf.PdfInputConversionOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, System.IO.Stream! output, Fluent.Pdf.PdfWriteOptions? writeOptions = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfInputConversion.WriteFile(System.IO.Stream! input, Fluent.Pdf.PdfInputFormat format, Fluent.Pdf.PdfInputConversionOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, string! path, Fluent.Pdf.PdfWriteOptions? writeOptions = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfInvoices.Write(Fluent.Pdf.PdfInvoiceData! invoice, System.IO.Stream! output, Fluent.Pdf.PdfFlowTextStyle! body, Fluent.Pdf.PdfFlowOptions! layout, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.IPdfInvoiceProducer? producer = null, Fluent.Pdf.IPdfArchivalValidator? archivalValidator = null, Fluent.Pdf.IPdfInvoiceValidator? invoiceValidator = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfInvoiceValidation!
static Fluent.Pdf.PdfLayers.AddGroup(Fluent.Pdf.PdfDocumentSnapshot! source, string! name, bool visible = true, Fluent.Pdf.PdfValue? usage = null, Fluent.Pdf.PdfValue? intents = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfLayers.BindXObject(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, string! resourceName, Fluent.Pdf.PdfValue! membership, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfLayers.EditGroup(Fluent.Pdf.PdfLayerGroup! group, string? name = null, Fluent.Pdf.PdfValue? usage = null, Fluent.Pdf.PdfValue? intents = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfLayers.Flatten(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfValue? configuration = null, Fluent.Pdf.PdfLayerUsage usage = Fluent.Pdf.PdfLayerUsage.View, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfLayers.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfLayerSet!
static Fluent.Pdf.PdfLayers.IsVisible(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfValue! membership, Fluent.Pdf.PdfValue? configuration = null, Fluent.Pdf.PdfLayerUsage usage = Fluent.Pdf.PdfLayerUsage.View, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfLayerVisibility!
static Fluent.Pdf.PdfLayers.SelectConfiguration(Fluent.Pdf.PdfDocumentSnapshot! source, int configurationIndex, Fluent.Pdf.PdfLayerUsage usage = Fluent.Pdf.PdfLayerUsage.View, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfLayers.SetConfiguration(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfValue! configuration, bool makeDefault = true, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfLinearization.Inspect(System.IO.Stream! input, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfLinearizationInfo?
static Fluent.Pdf.PdfLinearization.Validate(System.IO.Stream! input, Fluent.Pdf.PdfLinearizationOptions? options = null, Fluent.Pdf.IPdfLinearizationValidator? validator = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfLinearizationValidation!
static Fluent.Pdf.PdfLinearization.Write(System.IO.Stream! input, System.IO.Stream! output, Fluent.Pdf.PdfLinearizationOptions? options = null, Fluent.Pdf.IPdfLinearizationProducer? producer = null, Fluent.Pdf.IPdfLinearizationValidator? validator = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfLinearizationValidation!
static Fluent.Pdf.PdfLinearization.WriteFile(System.IO.Stream! input, string! path, Fluent.Pdf.PdfLinearizationOptions? options = null, Fluent.Pdf.IPdfLinearizationProducer? producer = null, Fluent.Pdf.IPdfLinearizationValidator? validator = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfLinearizationValidation!
static Fluent.Pdf.PdfMailMerge.Generate(Fluent.Pdf.PdfMergeTemplate! template, Fluent.Pdf.PdfMergeRecord! data, Fluent.Pdf.PdfMailMergeOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfMailMerge.Write(Fluent.Pdf.PdfMergeTemplate! template, Fluent.Pdf.PdfMergeRecord! data, Fluent.Pdf.PdfMailMergeOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, System.IO.Stream! output, Fluent.Pdf.PdfWriteOptions? writeOptions = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfMailMerge.WriteFile(Fluent.Pdf.PdfMergeTemplate! template, Fluent.Pdf.PdfMergeRecord! data, Fluent.Pdf.PdfMailMergeOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, string! path, Fluent.Pdf.PdfWriteOptions? writeOptions = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfMargins.Uniform(double value) -> Fluent.Pdf.PdfMargins
static Fluent.Pdf.PdfMatrix.Identity.get -> Fluent.Pdf.PdfMatrix
static Fluent.Pdf.PdfMatrixIccTransform.Transform(Fluent.Pdf.PdfColor! color, Fluent.Pdf.PdfColorSpace! outputProfile, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfColorTransformation!
static Fluent.Pdf.PdfMergeImage.ReadPng(System.IO.Stream! input, double width, double height, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfMergeImage!
static Fluent.Pdf.PdfMergeRun.Field(string! path, string! style = "body", string? format = null) -> Fluent.Pdf.PdfMergeRun!
static Fluent.Pdf.PdfMergeRun.Text(string! text, string! style = "body") -> Fluent.Pdf.PdfMergeRun!
static Fluent.Pdf.PdfMergeValue.Image(Fluent.Pdf.PdfMergeImage! value) -> Fluent.Pdf.PdfMergeValue!
static Fluent.Pdf.PdfMergeValue.Record(Fluent.Pdf.PdfMergeRecord! value) -> Fluent.Pdf.PdfMergeValue!
static Fluent.Pdf.PdfMergeValue.Records(params Fluent.Pdf.PdfMergeRecord![]! values) -> Fluent.Pdf.PdfMergeValue!
static Fluent.Pdf.PdfMergeValue.Scalar(object? value) -> Fluent.Pdf.PdfMergeValue!
static Fluent.Pdf.PdfMetadata.Edit(Fluent.Pdf.PdfDocumentSnapshot! source, System.Action<Fluent.Pdf.PdfMetadataEditor!>! edit, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfMetadata.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfMetadataSet!
static Fluent.Pdf.PdfMetadata.Remove(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfMetadataRemovalOptions? removal = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfNativeDeploymentInspector.CurrentRuntimeIdentifier.get -> string?
static Fluent.Pdf.PdfNativeDeploymentInspector.Inspect(Fluent.Pdf.PdfNativeDeployment! deployment, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfNativeDeploymentReport!
static Fluent.Pdf.PdfNativeDeploymentInspector.InspectWorker(Fluent.Pdf.PdfNativeDeployment! deployment, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfNativeDeploymentReport!
static Fluent.Pdf.PdfNativeDeploymentInspector.OperatingSystemRuntimeIdentifier.get -> string?
static Fluent.Pdf.PdfNativeDeploymentInspector.Verify(Fluent.Pdf.PdfNativeDeployment! deployment, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfNativeDeploymentReport!
static Fluent.Pdf.PdfNativeDeploymentInspector.VerifyWorker(Fluent.Pdf.PdfNativeDeployment! deployment, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfNativeDeploymentReport!
static Fluent.Pdf.PdfNavigation.AddLink(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfRect bounds, Fluent.Pdf.PdfAction! action, Fluent.Pdf.PdfActionPolicy? actionPolicy = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfNavigation.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfActionPolicy? actionPolicy = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfNavigationSet!
static Fluent.Pdf.PdfNavigation.Sanitize(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfActionPolicy? actionPolicy = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfNavigation.SetDocumentAction(Fluent.Pdf.PdfDocumentSnapshot! source, string! trigger, Fluent.Pdf.PdfAction? action, Fluent.Pdf.PdfActionPolicy? actionPolicy = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfNavigation.SetNamedDestination(Fluent.Pdf.PdfDocumentSnapshot! source, string! name, Fluent.Pdf.PdfDestination? destination, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfNavigation.SetPageAction(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, string! trigger, Fluent.Pdf.PdfAction? action, Fluent.Pdf.PdfActionPolicy? actionPolicy = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfObjectId.operator !=(Fluent.Pdf.PdfObjectId left, Fluent.Pdf.PdfObjectId right) -> bool
static Fluent.Pdf.PdfObjectId.operator ==(Fluent.Pdf.PdfObjectId left, Fluent.Pdf.PdfObjectId right) -> bool
static Fluent.Pdf.PdfOcr.Recognize(Fluent.Pdf.PdfOcrRaster! raster, Fluent.Pdf.PdfOcrOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOcrResult!
static Fluent.Pdf.PdfOcrAccuracy.Evaluate(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfOcrAccuracySample!>! samples, string! corpusIdentifier, string! corpusVersion, Fluent.Pdf.PdfOcrAccuracyOptions! options, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOcrAccuracyReport!
static Fluent.Pdf.PdfOcrOutput.Write(Fluent.Pdf.PdfOcrResult! result, int rasterWidth, int rasterHeight, Fluent.Pdf.PdfOcrOutputFormat format, System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfOcrOutput.WriteFile(Fluent.Pdf.PdfOcrResult! result, int rasterWidth, int rasterHeight, Fluent.Pdf.PdfOcrOutputFormat format, string! path, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfOcrRaster.Read(System.IO.Stream! input, int width, int height, int stride, Fluent.Pdf.PdfOcrPixelFormat pixelFormat, int dpi = 200, long maximumBytes = 67108864, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOcrRaster!
static Fluent.Pdf.PdfOcrResult.WithModels(string! text, Fluent.Pdf.PdfOcrWord![]! words, string! language, string! engineVersion, string! modelIdentity, double? meanConfidence, Fluent.Pdf.PdfOcrLanguageModel![]! models) -> Fluent.Pdf.PdfOcrResult!
static Fluent.Pdf.PdfOcrSearchable.Apply(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfOcrResult! recognition, Fluent.Pdf.PdfOcrPageFrame! frame, Fluent.Pdf.PdfFontFallback! fonts, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOcrOverlayOptions? options = null, Fluent.Pdf.PdfOrganizationOptions? organization = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfOcrSearchable.RecognizePage(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfOcrOptions! recognition, Fluent.Pdf.PdfFontFallback! fonts, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfRenderOptions? render = null, Fluent.Pdf.PdfOcrOverlayOptions? overlay = null, Fluent.Pdf.PdfOrganizationOptions? organization = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfOperationPolicy.Default.get -> Fluent.Pdf.PdfOperationPolicy!
static Fluent.Pdf.PdfOptimization.Optimize(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOptimizationOptions? options = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOptimizationResult!
static Fluent.Pdf.PdfOptimization.Write(Fluent.Pdf.PdfDocumentSnapshot! source, System.IO.Stream! output, Fluent.Pdf.PdfOptimizationOptions? options = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOptimizationResult!
static Fluent.Pdf.PdfOptimization.WriteFile(Fluent.Pdf.PdfDocumentSnapshot! source, string! path, Fluent.Pdf.PdfOptimizationOptions? options = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOptimizationResult!
static Fluent.Pdf.PdfOptions.Default.get -> Fluent.Pdf.PdfOptions!
static Fluent.Pdf.PdfPades.Upgrade(Fluent.Pdf.PdfSyntaxDocument! source, Fluent.Pdf.PdfPadesProfile profile, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfSignedResult!
static Fluent.Pdf.PdfPageDecoration.Flow(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowBlock!>! content) -> Fluent.Pdf.PdfPageDecoration!
static Fluent.Pdf.PdfPageDecoration.Html(string! html, Fluent.Pdf.IPdfTemplateHtmlProvider? provider = null) -> Fluent.Pdf.PdfPageDecoration!
static Fluent.Pdf.PdfPageDecoration.Vector(Fluent.Pdf.PdfDrawing! drawing, Fluent.Pdf.PdfRect sourceBounds) -> Fluent.Pdf.PdfPageDecoration!
static Fluent.Pdf.PdfPageGraphics.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPageGraphics!
static Fluent.Pdf.PdfPageImposition.Booklet(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfPageSize sheet, Fluent.Pdf.PdfMargins? margins = null, double gap = 0, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPageImposition.NUp(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfPageSize sheet, int columns = 2, int rows = 1, Fluent.Pdf.PdfMargins? margins = null, double gap = 0, Fluent.Pdf.PdfCanvasFit fit = Fluent.Pdf.PdfCanvasFit.Contain, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPageImposition.Tile(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfPageSize sheet, Fluent.Pdf.PdfMargins? margins = null, double overlap = 0, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPageOrganization.Delete(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfPageSelection! selection, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPageOrganization.Extract(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfPageSelection! selection, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPageOrganization.Insert(Fluent.Pdf.PdfDocumentSnapshot! host, int index, Fluent.Pdf.PdfPageInput! input, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPageOrganization.Merge(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfPageInput!>! inputs, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPageOrganization.Scale(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfPageSelection! selection, double factor, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPageOrganization.Split(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfPageSelection!>! selections, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfDocument!>!
static Fluent.Pdf.PdfPageOrganization.Transform(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfPageSelection! selection, System.Func<Fluent.Pdf.PdfPageGeometry!, Fluent.Pdf.PdfPageGeometry!>! transform, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPageSelection.Parse(string! expression, int pageCount, int maximumSelectedPages = 100000, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPageSelection!
static Fluent.Pdf.PdfPageSize.A4.get -> Fluent.Pdf.PdfPageSize
static Fluent.Pdf.PdfPageSize.Letter.get -> Fluent.Pdf.PdfPageSize
static Fluent.Pdf.PdfPageSize.operator !=(Fluent.Pdf.PdfPageSize left, Fluent.Pdf.PdfPageSize right) -> bool
static Fluent.Pdf.PdfPageSize.operator ==(Fluent.Pdf.PdfPageSize left, Fluent.Pdf.PdfPageSize right) -> bool
static Fluent.Pdf.PdfPaint.Black.get -> Fluent.Pdf.PdfPaint!
static Fluent.Pdf.PdfPaint.PatternFill(Fluent.Pdf.PdfPattern! pattern, Fluent.Pdf.PdfColor? baseColor = null) -> Fluent.Pdf.PdfPaint!
static Fluent.Pdf.PdfPaint.Solid(Fluent.Pdf.PdfColor! color) -> Fluent.Pdf.PdfPaint!
static Fluent.Pdf.PdfPath.Create(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfPathSegment!>! segments, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPath!
static Fluent.Pdf.PdfPathSegment.Close() -> Fluent.Pdf.PdfPathSegment!
static Fluent.Pdf.PdfPathSegment.Curve(double x1, double y1, double x2, double y2, double x3, double y3) -> Fluent.Pdf.PdfPathSegment!
static Fluent.Pdf.PdfPathSegment.Line(double x, double y) -> Fluent.Pdf.PdfPathSegment!
static Fluent.Pdf.PdfPathSegment.Move(double x, double y) -> Fluent.Pdf.PdfPathSegment!
static Fluent.Pdf.PdfPathSegment.Rectangle(Fluent.Pdf.PdfRect rectangle) -> Fluent.Pdf.PdfPathSegment!
static Fluent.Pdf.PdfPattern.Shading(Fluent.Pdf.PdfShading! shading, Fluent.Pdf.PdfMatrix? matrix = null, Fluent.Pdf.PdfGraphicsState? state = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPattern!
static Fluent.Pdf.PdfPattern.Tiling(Fluent.Pdf.PdfDrawing! drawing, Fluent.Pdf.PdfRect bounds, double xStep, double yStep, bool colored = true, int tilingType = 1, Fluent.Pdf.PdfMatrix? matrix = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPattern!
static Fluent.Pdf.PdfPreflight.Apply(System.IO.Stream! input, System.IO.Stream! output, Fluent.Pdf.PdfRemediationPlan! plan, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfRemediationAudit!
static Fluent.Pdf.PdfPreflight.ApplyFile(System.IO.Stream! input, string! path, Fluent.Pdf.PdfRemediationPlan! plan, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfRemediationAudit!
static Fluent.Pdf.PdfPreflight.Inspect(System.IO.Stream! input, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPreflightReport!
static Fluent.Pdf.PdfPreflight.WriteAudit(Fluent.Pdf.PdfRemediationAudit! audit, System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfPreflight.WriteReport(Fluent.Pdf.PdfPreflightReport! report, System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfPresentation.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPresentationSet!
static Fluent.Pdf.PdfPresentation.SetInitialView(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfDestination? destination, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPresentation.SetOutlines(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfBookmark!>! bookmarks, Fluent.Pdf.PdfActionPolicy? actionPolicy = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPresentation.SetPageLabels(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfPageNumbering? numbering, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPresentation.SetPageTransition(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfPageTransition? transition, double? displayDuration = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPresentation.SetView(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfPageLayout? layout = null, Fluent.Pdf.PdfPageMode? mode = null, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPresentation.SetViewerPreferences(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfValue! preferences, bool replace = false, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfPrinting.Cancel(string! printer, int jobId, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPrintJob!
static Fluent.Pdf.PdfPrinting.GetStatus(string! printer, int jobId, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPrintJob!
static Fluent.Pdf.PdfPrinting.Submit(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfPrintOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPrintJob!
static Fluent.Pdf.PdfProfileConversions.Convert(System.IO.Stream! input, System.IO.Stream! output, Fluent.Pdf.PdfProfileOptions! options, Fluent.Pdf.IPdfProfileProducer? producer = null, Fluent.Pdf.IPdfProfileValidator? validator = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfProfileValidation!
static Fluent.Pdf.PdfProfileConversions.Validate(System.IO.Stream! input, Fluent.Pdf.PdfProfileOptions! options, Fluent.Pdf.IPdfProfileValidator? validator = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfProfileValidation!
static Fluent.Pdf.PdfProviderRegistry.Default.get -> Fluent.Pdf.PdfProviderRegistry!
static Fluent.Pdf.PdfProviderRegistry.Empty.get -> Fluent.Pdf.PdfProviderRegistry!
static Fluent.Pdf.PdfRasterDocuments.Create(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfRasterInput!>! inputs, Fluent.Pdf.PdfRasterImportOptions? options = null, Fluent.Pdf.PdfProviderRegistry? codecs = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfRasterDocuments.Write(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfRasterInput!>! inputs, System.IO.Stream! output, Fluent.Pdf.PdfRasterImportOptions? options = null, Fluent.Pdf.PdfProviderRegistry? codecs = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfRasterDocuments.WriteFile(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfRasterInput!>! inputs, string! path, Fluent.Pdf.PdfRasterImportOptions? options = null, Fluent.Pdf.PdfProviderRegistry? codecs = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfRasterExport.Write(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, System.IO.Stream! output, Fluent.Pdf.PdfRasterExportOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfRasterExport.WriteFile(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, string! path, Fluent.Pdf.PdfRasterExportOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfRedaction.RasterRewrite(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfRedactionRegion!>! regions, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfRedactionRasterOptions! options, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfRedactionResult!
static Fluent.Pdf.PdfRedaction.SanitizeRasterRewrite(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfRedactionRasterOptions! options, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfRedactionResult!
static Fluent.Pdf.PdfRedaction.WriteRasterRewrite(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfRedactionRegion!>! regions, System.IO.Stream! output, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfRedactionRasterOptions! options, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfRedactionResult!
static Fluent.Pdf.PdfRedaction.WriteRasterRewriteFile(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfRedactionRegion!>! regions, string! path, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfRedactionRasterOptions! options, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfRedactionResult!
static Fluent.Pdf.PdfRendering.RenderPage(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfRenderOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfRaster!
static Fluent.Pdf.PdfRepair.Recover(System.IO.Stream! input, System.IO.Stream! output, Fluent.Pdf.PdfRepairOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfRepairResult!
static Fluent.Pdf.PdfResourceExtraction.ReadPage(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfUnknownOperatorPolicy unknownOperators = Fluent.Pdf.PdfUnknownOperatorPolicy.Reject, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPageResourceManifest!
static Fluent.Pdf.PdfResourceSet.Empty.get -> Fluent.Pdf.PdfResourceSet!
static Fluent.Pdf.PdfScanLayout.Analyze(Fluent.Pdf.PdfOcrResult! recognition, int rasterWidth, int rasterHeight, Fluent.Pdf.PdfScanLayoutOptions? options = null, Fluent.Pdf.PdfOcrRaster? raster = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfScanLayoutResult!
static Fluent.Pdf.PdfScanPreprocessing.Process(Fluent.Pdf.PdfOcrRaster! original, Fluent.Pdf.PdfScanPreprocessingOptions! options, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOcrOptions? orientationRecognition = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfScanPreprocessingResult!
static Fluent.Pdf.PdfScanPreprocessing.Recognize(Fluent.Pdf.PdfOcrRaster! original, Fluent.Pdf.PdfScanPreprocessingOptions! preprocessing, Fluent.Pdf.PdfOcrOptions! recognition, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfScanLayoutOptions? layout = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfScanRecognitionResult!
static Fluent.Pdf.PdfScripts.EvaluateCalculation(string! source, System.Collections.Generic.IReadOnlyDictionary<string!, decimal>! values, bool enableExecution = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfCalculationResult!
static Fluent.Pdf.PdfScripts.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfScriptRecord!>!
static Fluent.Pdf.PdfScripts.SetDocumentScript(Fluent.Pdf.PdfDocumentSnapshot! document, string! name, string? source, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfSecurity.DecryptWithCertificate(System.IO.Stream! input, System.IO.Stream! output, Fluent.Pdf.PdfCertificateCredential! credential, Fluent.Pdf.PdfProviderRegistry! providers, bool allowLegacyEncryption = false, bool allowSignatureInvalidation = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfSecurityResult!
static Fluent.Pdf.PdfSecurity.DecryptWithPassword(System.IO.Stream! input, System.IO.Stream! output, Fluent.Pdf.PdfPassword! password, bool allowLegacyEncryption = false, bool allowSignatureInvalidation = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfSecurityResult!
static Fluent.Pdf.PdfSecurity.EncryptWithCertificates(Fluent.Pdf.PdfDocumentSnapshot! source, System.IO.Stream! output, Fluent.Pdf.PdfCertificateProtection! protection, Fluent.Pdf.PdfProviderRegistry! providers, bool allowSignatureInvalidation = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfSecurityResult!
static Fluent.Pdf.PdfSecurity.EncryptWithPassword(Fluent.Pdf.PdfDocumentSnapshot! source, System.IO.Stream! output, Fluent.Pdf.PdfPasswordProtection! protection, bool allowSignatureInvalidation = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfSecurityResult!
static Fluent.Pdf.PdfSepaCreditTransfer.FromInvoice(Fluent.Pdf.PdfInvoiceData! invoice, string! iban, string! bic) -> Fluent.Pdf.PdfSepaCreditTransfer!
static Fluent.Pdf.PdfSepaCreditTransfer.Parse(string! payload) -> Fluent.Pdf.PdfSepaCreditTransfer!
static Fluent.Pdf.PdfShading.Axial(Fluent.Pdf.PdfColorSpace! colorSpace, Fluent.Pdf.PdfPoint start, Fluent.Pdf.PdfPoint end, Fluent.Pdf.PdfFunction! function, bool extendStart = false, bool extendEnd = false, Fluent.Pdf.PdfRect? bounds = null, bool antiAlias = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfShading!
static Fluent.Pdf.PdfShading.Functional(Fluent.Pdf.PdfColorSpace! colorSpace, Fluent.Pdf.PdfFunction! function, Fluent.Pdf.PdfRect domain, Fluent.Pdf.PdfMatrix? matrix = null, Fluent.Pdf.PdfRect? bounds = null, bool antiAlias = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfShading!
static Fluent.Pdf.PdfShading.Mesh(Fluent.Pdf.PdfColorSpace! colorSpace, int shadingType, System.IO.Stream! packedData, int bitsPerCoordinate, int bitsPerComponent, double[]! decode, int bitsPerFlag = 2, int verticesPerRow = 0, Fluent.Pdf.PdfFunction? function = null, Fluent.Pdf.PdfRect? bounds = null, bool antiAlias = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfShading!
static Fluent.Pdf.PdfShading.Radial(Fluent.Pdf.PdfColorSpace! colorSpace, Fluent.Pdf.PdfPoint start, double startRadius, Fluent.Pdf.PdfPoint end, double endRadius, Fluent.Pdf.PdfFunction! function, bool extendStart = false, bool extendEnd = false, Fluent.Pdf.PdfRect? bounds = null, bool antiAlias = false, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfShading!
static Fluent.Pdf.PdfSignatures.AuditRevisions(Fluent.Pdf.PdfSyntaxDocument! source, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfRevisionAuditRecord!>!
static Fluent.Pdf.PdfSignatures.Inspect(Fluent.Pdf.PdfSyntaxDocument! source, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfSignatureObservation!>!
static Fluent.Pdf.PdfSignatures.PreflightEdit(Fluent.Pdf.PdfSyntaxDocument! source, Fluent.Pdf.PdfWriteOptions! writeOptions, Fluent.Pdf.PdfSignatureOptions? signing = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfSignedEditPlan!
static Fluent.Pdf.PdfSignatures.PrepareFields(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfSignatureFieldDefinition!>! fields, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfSignatures.Sign(Fluent.Pdf.PdfSyntaxDocument! source, Fluent.Pdf.PdfSignatureOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfSignedResult!
static Fluent.Pdf.PdfSignatures.SignAsync(Fluent.Pdf.PdfSyntaxDocument! source, Fluent.Pdf.PdfSignatureOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Threading.Tasks.Task<Fluent.Pdf.PdfSignedResult!>!
static Fluent.Pdf.PdfSignatures.SignFile(Fluent.Pdf.PdfSyntaxDocument! source, Fluent.Pdf.PdfSignatureOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, string! path, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfSignedResult!
static Fluent.Pdf.PdfSignatures.SignFileAsync(Fluent.Pdf.PdfSyntaxDocument! source, Fluent.Pdf.PdfSignatureOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, string! path, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Threading.Tasks.Task<Fluent.Pdf.PdfSignedResult!>!
static Fluent.Pdf.PdfSignatures.Verify(Fluent.Pdf.PdfSyntaxDocument! source, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfSignatureTrustOptions! options, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfSignatureVerification!>!
static Fluent.Pdf.PdfStampContent.FromDrawing(Fluent.Pdf.PdfDrawing! drawing, Fluent.Pdf.PdfRect bounds, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfStampContent!
static Fluent.Pdf.PdfStampContent.FromHtml(Fluent.Pdf.PdfHtmlSource! source, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfHtmlOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfStampContent!
static Fluent.Pdf.PdfStampContent.FromImage(Fluent.Pdf.PdfImageAsset! image, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfStampContent!
static Fluent.Pdf.PdfStampContent.FromPage(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfStampContent!
static Fluent.Pdf.PdfStampContent.FromText(Fluent.Pdf.PdfShapedText! text, double fontSize, double lineHeight = 1.2, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfStampContent!
static Fluent.Pdf.PdfStamps.Apply(Fluent.Pdf.PdfDocumentSnapshot! source, string! identifier, Fluent.Pdf.PdfStampContent! content, System.Collections.Generic.IEnumerable<int>! pageIndices, Fluent.Pdf.PdfStampOptions! options, Fluent.Pdf.PdfOrganizationOptions? organization = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfStamps.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfStampInspection!
static Fluent.Pdf.PdfStamps.Remove(Fluent.Pdf.PdfDocumentSnapshot! source, string! identifier, Fluent.Pdf.PdfOrganizationOptions? organization = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfStamps.Update(Fluent.Pdf.PdfDocumentSnapshot! source, string! identifier, Fluent.Pdf.PdfStampContent! content, Fluent.Pdf.PdfStampOptions! options, Fluent.Pdf.PdfOrganizationOptions? organization = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfStructuralValidation.Inspect(System.IO.Stream! input, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfStructuralInspection!
static Fluent.Pdf.PdfStructureContent.Annotation(int pageIndex, Fluent.Pdf.PdfObjectId annotationId) -> Fluent.Pdf.PdfStructureContent!
static Fluent.Pdf.PdfStructureContent.MarkedContent(int pageIndex, int mcid, Fluent.Pdf.PdfObjectId? streamId = null) -> Fluent.Pdf.PdfStructureContent!
static Fluent.Pdf.PdfSyntax.Read(System.IO.Stream! input, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfSyntaxDocument!
static Fluent.Pdf.PdfTable.Create(Fluent.Pdf.PdfTableColumn![]! columns, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfTableRow!>! rows, int headerRows = 0, bool allowRowSplit = true, int maximumRows = 100000, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfTable!
static Fluent.Pdf.PdfTableBorder.None.get -> Fluent.Pdf.PdfTableBorder!
static Fluent.Pdf.PdfTableCell.Text(string! text, Fluent.Pdf.PdfFlowTextStyle! textStyle, int columnSpan = 1, int rowSpan = 1, Fluent.Pdf.PdfTableCellStyle? style = null) -> Fluent.Pdf.PdfTableCell!
static Fluent.Pdf.PdfTableColumn.Auto(double minimum = 0, double maximum = 14400, double weight = 1) -> Fluent.Pdf.PdfTableColumn!
static Fluent.Pdf.PdfTableColumn.Fixed(double width) -> Fluent.Pdf.PdfTableColumn!
static Fluent.Pdf.PdfTableData.Bind<T>(System.Collections.Generic.IEnumerable<T>! source, Fluent.Pdf.PdfTableDataColumn<T>![]! columns, System.Globalization.CultureInfo! culture, int maximumRows = 100000, Fluent.Pdf.PdfTableRow![]? headerRows = null, bool allowRowSplit = true, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfTable!
static Fluent.Pdf.PdfTableExtraction.Read(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IReadOnlyList<int>! pages, Fluent.Pdf.PdfTableExtractionOptions! options, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfTableExtractionResult!
static Fluent.Pdf.PdfTemplateLayout.Layout(System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFlowBlock!>! blocks, Fluent.Pdf.PdfFlowOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfPageTemplate! template, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfPageTemplateResult!
static Fluent.Pdf.PdfText.Shape(string! text, Fluent.Pdf.PdfFontFallback! fonts, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfTextOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfShapedText!
static Fluent.Pdf.PdfTextDrawing.Create(Fluent.Pdf.PdfShapedText! text, double x, double y, double fontSize, double lineHeight = 1.2, string! resourcePrefix = "FT", bool subset = true, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfTextDrawing!
static Fluent.Pdf.PdfTextExtraction.ReadPage(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfTextExtractionOptions? options = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfTextExtractionResult!
static Fluent.Pdf.PdfTextSearch.FindPage(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfTextQuery! query, Fluent.Pdf.PdfTextExtractionOptions? extraction = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfTextSearchResult!
static Fluent.Pdf.PdfTextSearch.ReplacePage(Fluent.Pdf.PdfDocumentSnapshot! source, int pageIndex, Fluent.Pdf.PdfTextQuery! query, string! replacement, Fluent.Pdf.PdfFontFallback! fonts, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfTextReplacementOptions! options, Fluent.Pdf.PdfOrganizationOptions? organization = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfTextString.Create(string! text, Fluent.Pdf.PdfTextStringEncoding encoding = Fluent.Pdf.PdfTextStringEncoding.Utf16BigEndian, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfTextString!
static Fluent.Pdf.PdfTextString.Read(Fluent.Pdf.PdfValue! value, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfTextString!
static Fluent.Pdf.PdfTransparencyGroup.Create(Fluent.Pdf.PdfDrawing! drawing, Fluent.Pdf.PdfRect bounds, Fluent.Pdf.PdfColorSpace! colorSpace, bool isolated = false, bool knockout = false, Fluent.Pdf.PdfMatrix? matrix = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfTransparencyGroup!
static Fluent.Pdf.PdfUnicode.Analyze(string! text, Fluent.Pdf.PdfParagraphDirection direction = Fluent.Pdf.PdfParagraphDirection.Auto, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfUnicodeAnalysis!
static Fluent.Pdf.PdfUnicode.Version.get -> string!
static Fluent.Pdf.PdfUnicodeMap.Parse(System.IO.Stream! decodedInput, System.Collections.Generic.IReadOnlyDictionary<string!, Fluent.Pdf.PdfUnicodeMap!>? inheritedMaps = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfUnicodeMap!
static Fluent.Pdf.PdfUnits.Convert(double value, Fluent.Pdf.PdfLengthUnit from, Fluent.Pdf.PdfLengthUnit to, double dpi = 96) -> double
static Fluent.Pdf.PdfUnits.PointsPerUnit(Fluent.Pdf.PdfLengthUnit unit, double dpi = 96) -> double
static Fluent.Pdf.PdfValue.Array(params Fluent.Pdf.PdfValue![]! items) -> Fluent.Pdf.PdfValue!
static Fluent.Pdf.PdfValue.Boolean(bool value) -> Fluent.Pdf.PdfValue!
static Fluent.Pdf.PdfValue.Dictionary(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string!, Fluent.Pdf.PdfValue!>>! entries) -> Fluent.Pdf.PdfValue!
static Fluent.Pdf.PdfValue.Integer(long value) -> Fluent.Pdf.PdfValue!
static Fluent.Pdf.PdfValue.Name(string! value) -> Fluent.Pdf.PdfValue!
static Fluent.Pdf.PdfValue.Null.get -> Fluent.Pdf.PdfValue!
static Fluent.Pdf.PdfValue.Number(string! text) -> Fluent.Pdf.PdfValue!
static Fluent.Pdf.PdfValue.Reference(Fluent.Pdf.PdfObjectId id) -> Fluent.Pdf.PdfValue!
static Fluent.Pdf.PdfValue.String(params byte[]! bytes) -> Fluent.Pdf.PdfValue!
static Fluent.Pdf.PdfValues.Real(double value) -> Fluent.Pdf.PdfValue!
static Fluent.Pdf.PdfValues.Stream(Fluent.Pdf.PdfValue! dictionary, System.IO.Stream! encodedInput, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfValue!
static Fluent.Pdf.PdfWordConversion.Import(System.IO.Stream! input, Fluent.Pdf.PdfWordFormat format, Fluent.Pdf.PdfWordConversionOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfWordConversion.Write(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfWordFormat format, Fluent.Pdf.PdfWordConversionOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, System.IO.Stream! output, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfWordConversion.WriteFile(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfWordFormat format, Fluent.Pdf.PdfWordConversionOptions! options, Fluent.Pdf.PdfProviderRegistry! providers, string! path, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.PdfWriteOptions.Default.get -> Fluent.Pdf.PdfWriteOptions!
static Fluent.Pdf.PdfXfa.ConvertToAcroForm(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<Fluent.Pdf.PdfFormFieldDefinition!>! fields, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfXfa.Fill(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IReadOnlyDictionary<string!, string!>! values, Fluent.Pdf.PdfOrganizationOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfDocument!
static Fluent.Pdf.PdfXfa.Inspect(Fluent.Pdf.PdfDocumentSnapshot! source, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfXfaSet!
```

## Member documentation

### <code>F:Fluent.Pdf.PdfAccessibilityProfile.Ua1</code>

PDF/UA-1.

### <code>F:Fluent.Pdf.PdfAccessibilityProfile.Ua2</code>

PDF/UA-2.

### <code>F:Fluent.Pdf.PdfAnnotationKind.Caret</code>

A caret.

### <code>F:Fluent.Pdf.PdfAnnotationKind.Circle</code>

An elliptical shape.

### <code>F:Fluent.Pdf.PdfAnnotationKind.FileAttachment</code>

An embedded file attachment.

### <code>F:Fluent.Pdf.PdfAnnotationKind.FreeText</code>

Explicitly shaped free text.

### <code>F:Fluent.Pdf.PdfAnnotationKind.Highlight</code>

A highlight.

### <code>F:Fluent.Pdf.PdfAnnotationKind.Ink</code>

Freehand ink.

### <code>F:Fluent.Pdf.PdfAnnotationKind.Line</code>

A line.

### <code>F:Fluent.Pdf.PdfAnnotationKind.PolyLine</code>

A polyline.

### <code>F:Fluent.Pdf.PdfAnnotationKind.Polygon</code>

A polygon.

### <code>F:Fluent.Pdf.PdfAnnotationKind.RichMedia</code>

Passive rich-media data with an explicit poster appearance; no media is executed.

### <code>F:Fluent.Pdf.PdfAnnotationKind.Square</code>

A rectangular shape.

### <code>F:Fluent.Pdf.PdfAnnotationKind.Squiggly</code>

A squiggly underline.

### <code>F:Fluent.Pdf.PdfAnnotationKind.Stamp</code>

A stamp.

### <code>F:Fluent.Pdf.PdfAnnotationKind.StrikeOut</code>

A strikeout.

### <code>F:Fluent.Pdf.PdfAnnotationKind.Text</code>

A text comment.

### <code>F:Fluent.Pdf.PdfAnnotationKind.Underline</code>

An underline.

### <code>F:Fluent.Pdf.PdfAnnotationView.Print</code>

Print visibility, requiring Print and excluding Hidden/Invisible.

### <code>F:Fluent.Pdf.PdfAnnotationView.Screen</code>

Screen visibility, including Hidden and NoView.

### <code>F:Fluent.Pdf.PdfArchivalProfile.A1A</code>

PDF/A-1a.

### <code>F:Fluent.Pdf.PdfArchivalProfile.A1B</code>

PDF/A-1b.

### <code>F:Fluent.Pdf.PdfArchivalProfile.A2A</code>

PDF/A-2a.

### <code>F:Fluent.Pdf.PdfArchivalProfile.A2B</code>

PDF/A-2b.

### <code>F:Fluent.Pdf.PdfArchivalProfile.A2U</code>

PDF/A-2u.

### <code>F:Fluent.Pdf.PdfArchivalProfile.A3A</code>

PDF/A-3a.

### <code>F:Fluent.Pdf.PdfArchivalProfile.A3B</code>

PDF/A-3b.

### <code>F:Fluent.Pdf.PdfArchivalProfile.A3U</code>

PDF/A-3u.

### <code>F:Fluent.Pdf.PdfArchivalProfile.A4</code>

PDF/A-4.

### <code>F:Fluent.Pdf.PdfArchivalProfile.A4E</code>

PDF/A-4e.

### <code>F:Fluent.Pdf.PdfArchivalProfile.A4F</code>

PDF/A-4f.

### <code>F:Fluent.Pdf.PdfAuthenticationRole.Certificate</code>

A recipient certificate matched with its explicit permissions.

### <code>F:Fluent.Pdf.PdfAuthenticationRole.CreatedProtection</code>

New protection was created; this does not authenticate possession of a recipient private key.

### <code>F:Fluent.Pdf.PdfAuthenticationRole.Owner</code>

The owner password or certificate owner authority matched.

### <code>F:Fluent.Pdf.PdfAuthenticationRole.User</code>

The user password matched.

### <code>F:Fluent.Pdf.PdfBarcodeErrorCorrection.High</code>

QR levelH.

### <code>F:Fluent.Pdf.PdfBarcodeErrorCorrection.Low</code>

QR levelL.

### <code>F:Fluent.Pdf.PdfBarcodeErrorCorrection.Medium</code>

QR levelM.

### <code>F:Fluent.Pdf.PdfBarcodeErrorCorrection.None</code>

No error correction. Code128 checksum still applies.

### <code>F:Fluent.Pdf.PdfBarcodeErrorCorrection.Quartile</code>

QR levelQ.

### <code>F:Fluent.Pdf.PdfBarcodeSymbology.Code128</code>

Code128 code setB, printable ASCII, with mandatory modulo103 checksum.

### <code>F:Fluent.Pdf.PdfBarcodeSymbology.QrCode</code>

QR Model2, versions1–40, UTF8 byte mode with ECI26.

### <code>F:Fluent.Pdf.PdfBidiClass.AL</code>

Arabic letter.

### <code>F:Fluent.Pdf.PdfBidiClass.AN</code>

Arabic number.

### <code>F:Fluent.Pdf.PdfBidiClass.B</code>

Paragraph separator.

### <code>F:Fluent.Pdf.PdfBidiClass.BN</code>

Boundary neutral.

### <code>F:Fluent.Pdf.PdfBidiClass.CS</code>

Common separator.

### <code>F:Fluent.Pdf.PdfBidiClass.EN</code>

European number.

### <code>F:Fluent.Pdf.PdfBidiClass.ES</code>

European separator.

### <code>F:Fluent.Pdf.PdfBidiClass.ET</code>

European terminator.

### <code>F:Fluent.Pdf.PdfBidiClass.FSI</code>

First-strong isolate.

### <code>F:Fluent.Pdf.PdfBidiClass.L</code>

Left-to-right.

### <code>F:Fluent.Pdf.PdfBidiClass.LRE</code>

Left-to-right embedding.

### <code>F:Fluent.Pdf.PdfBidiClass.LRI</code>

Left-to-right isolate.

### <code>F:Fluent.Pdf.PdfBidiClass.LRO</code>

Left-to-right override.

### <code>F:Fluent.Pdf.PdfBidiClass.NSM</code>

Nonspacing mark.

### <code>F:Fluent.Pdf.PdfBidiClass.ON</code>

Other neutral.

### <code>F:Fluent.Pdf.PdfBidiClass.PDF</code>

Pop directional format.

### <code>F:Fluent.Pdf.PdfBidiClass.PDI</code>

Pop directional isolate.

### <code>F:Fluent.Pdf.PdfBidiClass.R</code>

Right-to-left.

### <code>F:Fluent.Pdf.PdfBidiClass.RLE</code>

Right-to-left embedding.

### <code>F:Fluent.Pdf.PdfBidiClass.RLI</code>

Right-to-left isolate.

### <code>F:Fluent.Pdf.PdfBidiClass.RLO</code>

Right-to-left override.

### <code>F:Fluent.Pdf.PdfBidiClass.S</code>

Segment separator.

### <code>F:Fluent.Pdf.PdfBidiClass.WS</code>

Whitespace.

### <code>F:Fluent.Pdf.PdfCanvasAnchor.Bottom</code>

Bottom center.

### <code>F:Fluent.Pdf.PdfCanvasAnchor.BottomLeft</code>

Bottom left.

### <code>F:Fluent.Pdf.PdfCanvasAnchor.BottomRight</code>

Bottom right.

### <code>F:Fluent.Pdf.PdfCanvasAnchor.Center</code>

Center.

### <code>F:Fluent.Pdf.PdfCanvasAnchor.Left</code>

Middle left.

### <code>F:Fluent.Pdf.PdfCanvasAnchor.Right</code>

Middle right.

### <code>F:Fluent.Pdf.PdfCanvasAnchor.Top</code>

Top center.

### <code>F:Fluent.Pdf.PdfCanvasAnchor.TopLeft</code>

Top left.

### <code>F:Fluent.Pdf.PdfCanvasAnchor.TopRight</code>

Top right.

### <code>F:Fluent.Pdf.PdfCanvasApplyMode.Overlay</code>

Paint in front of existing content and earlier overlays.

### <code>F:Fluent.Pdf.PdfCanvasApplyMode.Replace</code>

Replace the visible content program and prior canvas layers.

### <code>F:Fluent.Pdf.PdfCanvasApplyMode.Underlay</code>

Paint behind existing content and earlier underlays.

### <code>F:Fluent.Pdf.PdfCanvasFit.Contain</code>

Preserve aspect and fit completely inside the destination.

### <code>F:Fluent.Pdf.PdfCanvasFit.Cover</code>

Preserve aspect and cover the destination, clipping overflow.

### <code>F:Fluent.Pdf.PdfCanvasFit.Stretch</code>

Use the full destination dimensions.

### <code>F:Fluent.Pdf.PdfCanvasOrigin.BottomLeft</code>

Origin at the bottom-left content corner, with x rightward and y upward.

### <code>F:Fluent.Pdf.PdfCanvasOrigin.TopLeft</code>

Origin at the top-left content corner, with x rightward and y downward.

### <code>F:Fluent.Pdf.PdfCapability.Barcodes</code>

Explicit independent barcode decoding and document symbol workflows.

### <code>F:Fluent.Pdf.PdfCapability.CmsEnvelopes</code>

Explicit offline CMS seed envelopes for certificate encryption.

### <code>F:Fluent.Pdf.PdfCapability.ConvertWord</code>

Explicit optional Word interchange with direction, reconstruction and fidelity diagnostics.

### <code>F:Fluent.Pdf.PdfCapability.DecodeRasterDocuments</code>

Explicit optional multi-frame raster-file decoding.

### <code>F:Fluent.Pdf.PdfCapability.Fonts</code>

Explicit offline font parsing, embedding and standard-font metrics.

### <code>F:Fluent.Pdf.PdfCapability.ImageCodecs</code>

Explicit optional image stream codecs; algorithm/direction support is reported by their provider.

### <code>F:Fluent.Pdf.PdfCapability.Pades</code>

Explicit incremental PAdES producer.

### <code>F:Fluent.Pdf.PdfCapability.PrintDocument</code>

Explicit printer selection, one spool submission and job observations.

### <code>F:Fluent.Pdf.PdfCapability.ReadDocument</code>

Read a PDF into the managed document model.

### <code>F:Fluent.Pdf.PdfCapability.RecognizeText</code>

Recognize text; the operation contract is defined by its owning requirement.

### <code>F:Fluent.Pdf.PdfCapability.RenderColorManaged</code>

Explicit ICC/overprint/spot-aware page rasterization.

### <code>F:Fluent.Pdf.PdfCapability.RenderHtml</code>

Render HTML; the operation contract is defined by its owning requirement.

### <code>F:Fluent.Pdf.PdfCapability.RenderPages</code>

Render PDF pages; the rendering operation contract is defined by its owning requirement.

### <code>F:Fluent.Pdf.PdfCapability.ShapeText</code>

Explicit Unicode-aware glyph shaping with logical cluster provenance.

### <code>F:Fluent.Pdf.PdfCapability.SignatureTrust</code>

Explicit signer identity, evaluation-time and revocation decisions.

### <code>F:Fluent.Pdf.PdfCapability.Signatures</code>

Explicit detached CMS signing and cryptographic integrity verification; trust is separate.

### <code>F:Fluent.Pdf.PdfCapability.ValidateAccessibility</code>

Explicit complete independent PDF/UA validation, separate from human semantic review.

### <code>F:Fluent.Pdf.PdfCapability.ValidateArchival</code>

Explicit complete independent PDF/A profile validation.

### <code>F:Fluent.Pdf.PdfCapability.ValidatePades</code>

Complete independent exact-byte PAdES validation.

### <code>F:Fluent.Pdf.PdfCapability.WriteDocument</code>

Write the managed document model as PDF.

### <code>F:Fluent.Pdf.PdfCertificationPermission.FormsAndSignatures</code>

Form filling and signing; only separately validated edit profiles may apply.

### <code>F:Fluent.Pdf.PdfCertificationPermission.FormsSignaturesAndAnnotations</code>

Form filling, signing and annotations; only separately validated edit profiles may apply.

### <code>F:Fluent.Pdf.PdfCertificationPermission.NoChanges</code>

No later changes.

### <code>F:Fluent.Pdf.PdfCertificationPermission.None</code>

An approval signature without document certification.

### <code>F:Fluent.Pdf.PdfCmsKeyTransport.LegacyPkcs1</code>

Explicit legacy RSA PKCS#1 v1.5 compatibility transport.

### <code>F:Fluent.Pdf.PdfCmsKeyTransport.OaepSha256</code>

RSA-OAEP with SHA-256 and an empty label.

### <code>F:Fluent.Pdf.PdfColorRenderingIntent.AbsoluteColorimetric</code>

ICC-absolute colorimetric mapping.

### <code>F:Fluent.Pdf.PdfColorRenderingIntent.Perceptual</code>

Perceptual gamut mapping.

### <code>F:Fluent.Pdf.PdfColorRenderingIntent.RelativeColorimetric</code>

Media-relative colorimetric mapping.

### <code>F:Fluent.Pdf.PdfColorRenderingIntent.Saturation</code>

Saturation mapping.

### <code>F:Fluent.Pdf.PdfConformanceProfile.E12008</code>

PDF/E-1:2008.

### <code>F:Fluent.Pdf.PdfConformanceProfile.X1A2001</code>

PDF/X-1a:2001.

### <code>F:Fluent.Pdf.PdfConformanceProfile.X1A2003</code>

PDF/X-1a:2003.

### <code>F:Fluent.Pdf.PdfConformanceProfile.X32002</code>

PDF/X-3:2002.

### <code>F:Fluent.Pdf.PdfConformanceProfile.X32003</code>

PDF/X-3:2003.

### <code>F:Fluent.Pdf.PdfConformanceProfile.X42010</code>

PDF/X-4:2010, not the external-output-intent variant.

### <code>F:Fluent.Pdf.PdfContentKind.Page</code>

Page or form content.

### <code>F:Fluent.Pdf.PdfContentKind.Type3Glyph</code>

A Type 3 font glyph procedure.

### <code>F:Fluent.Pdf.PdfCrossReferenceFormat.Auto</code>

Select a table within the configured offset/object ceilings, otherwise use a stream.

### <code>F:Fluent.Pdf.PdfCrossReferenceFormat.Stream</code>

Use a PDF 1.5-or-newer cross-reference stream with 64-bit offsets.

### <code>F:Fluent.Pdf.PdfCrossReferenceFormat.Table</code>

Use classic fixed-width table entries.

### <code>F:Fluent.Pdf.PdfCrossReferenceKind.Compressed</code>

An object inside an object stream.

### <code>F:Fluent.Pdf.PdfCrossReferenceKind.Free</code>

A free object, which shadows older generations.

### <code>F:Fluent.Pdf.PdfCrossReferenceKind.Offset</code>

An indirect object at a file offset.

### <code>F:Fluent.Pdf.PdfDestinationView.Fit</code>

Fit the complete page.

### <code>F:Fluent.Pdf.PdfDestinationView.FitB</code>

Fit the content bounding box.

### <code>F:Fluent.Pdf.PdfDestinationView.FitBH</code>

Fit content width at an optional top coordinate.

### <code>F:Fluent.Pdf.PdfDestinationView.FitBV</code>

Fit content height at an optional left coordinate.

### <code>F:Fluent.Pdf.PdfDestinationView.FitH</code>

Fit width at an optional top coordinate.

### <code>F:Fluent.Pdf.PdfDestinationView.FitR</code>

Fit left, bottom, right and top coordinates.

### <code>F:Fluent.Pdf.PdfDestinationView.FitV</code>

Fit height at an optional left coordinate.

### <code>F:Fluent.Pdf.PdfDestinationView.XYZ</code>

Left, top and zoom; null retains the viewer's current value.

### <code>F:Fluent.Pdf.PdfDiagnosticSeverity.Error</code>

Fatal operation failure.

### <code>F:Fluent.Pdf.PdfDiagnosticSeverity.Information</code>

Informational observation.

### <code>F:Fluent.Pdf.PdfDiagnosticSeverity.Warning</code>

Nonfatal observation or explicitly accepted loss.

### <code>F:Fluent.Pdf.PdfDifferenceKind.Geometry</code>

Page or advance-cell geometry changed.

### <code>F:Fluent.Pdf.PdfDifferenceKind.Metadata</code>

Info or exact XMP packet changed.

### <code>F:Fluent.Pdf.PdfDifferenceKind.Page</code>

Ordinal page presence changed.

### <code>F:Fluent.Pdf.PdfDifferenceKind.Text</code>

Logical extracted text changed.

### <code>F:Fluent.Pdf.PdfDifferenceKind.Visual</code>

Rendered samples changed outside the explicit channel tolerance.

### <code>F:Fluent.Pdf.PdfDocumentPermissions.All</code>

All defined user permission bits.

### <code>F:Fluent.Pdf.PdfDocumentPermissions.Annotate</code>

Permit annotation changes and applicable form operations.

### <code>F:Fluent.Pdf.PdfDocumentPermissions.Assemble</code>

Permit document assembly.

### <code>F:Fluent.Pdf.PdfDocumentPermissions.Extract</code>

Permit ordinary content extraction.

### <code>F:Fluent.Pdf.PdfDocumentPermissions.ExtractForAccessibility</code>

Legacy accessibility extraction bit; PDF 2.0 readers ignore this restriction.

### <code>F:Fluent.Pdf.PdfDocumentPermissions.FillForms</code>

Permit filling existing fields.

### <code>F:Fluent.Pdf.PdfDocumentPermissions.Modify</code>

Permit general content changes.

### <code>F:Fluent.Pdf.PdfDocumentPermissions.None</code>

No optional user permissions.

### <code>F:Fluent.Pdf.PdfDocumentPermissions.Print</code>

Permit printing at the quality allowed by the high-quality flag.

### <code>F:Fluent.Pdf.PdfDocumentPermissions.PrintHighQuality</code>

Permit high-quality printing when Print is also enabled.

### <code>F:Fluent.Pdf.PdfEngineeringContentKind.EmbeddedFile</code>

Encoded embedded-file payload; preserve-only.

### <code>F:Fluent.Pdf.PdfEngineeringContentKind.Geospatial</code>

Geospatial dictionary/metadata; preserve-only.

### <code>F:Fluent.Pdf.PdfEngineeringContentKind.Other3D</code>

Other declared3D stream; preserve-only.

### <code>F:Fluent.Pdf.PdfEngineeringContentKind.Prc</code>

Opaque PRC stream; preserve-only.

### <code>F:Fluent.Pdf.PdfEngineeringContentKind.RichMedia</code>

Rich-media dictionary; preserve-only.

### <code>F:Fluent.Pdf.PdfEngineeringContentKind.ThreeDAnnotation</code>

3D annotation dictionary; preserve-only.

### <code>F:Fluent.Pdf.PdfEngineeringContentKind.U3D</code>

Opaque Universal3D stream; preserve-only.

### <code>F:Fluent.Pdf.PdfExtractedResourceKind.ColorSpace</code>

A color space declaration.

### <code>F:Fluent.Pdf.PdfExtractedResourceKind.Font</code>

A font dictionary.

### <code>F:Fluent.Pdf.PdfExtractedResourceKind.FontProgram</code>

An embedded font file or explicitly identified Type3 glyph procedure.

### <code>F:Fluent.Pdf.PdfExtractedResourceKind.Form</code>

A Form XObject.

### <code>F:Fluent.Pdf.PdfExtractedResourceKind.GraphicsState</code>

An extended graphics-state dictionary.

### <code>F:Fluent.Pdf.PdfExtractedResourceKind.Image</code>

An image XObject or inline image.

### <code>F:Fluent.Pdf.PdfExtractedResourceKind.Pattern</code>

A pattern resource.

### <code>F:Fluent.Pdf.PdfExtractedResourceKind.Properties</code>

A marked-content property resource.

### <code>F:Fluent.Pdf.PdfExtractedResourceKind.Shading</code>

A shading resource.

### <code>F:Fluent.Pdf.PdfExtractedResourceKind.Unknown</code>

An unknown or unsupported resource role.

### <code>F:Fluent.Pdf.PdfFieldCollisionPolicy.Reject</code>

Reject ambiguous field identities.

### <code>F:Fluent.Pdf.PdfFieldCollisionPolicy.Rename</code>

Prefix the colliding root partial name with a deterministic source namespace.

### <code>F:Fluent.Pdf.PdfFlowAlignment.Center</code>

Center within the interval.

### <code>F:Fluent.Pdf.PdfFlowAlignment.End</code>

Align to the paragraph trailing edge.

### <code>F:Fluent.Pdf.PdfFlowAlignment.Justify</code>

Expand ordinary spaces on wrapped nonfinal lines.

### <code>F:Fluent.Pdf.PdfFlowAlignment.Start</code>

Align to the paragraph leading edge.

### <code>F:Fluent.Pdf.PdfFlowClear.Both</code>

Pass floats on both physical sides.

### <code>F:Fluent.Pdf.PdfFlowClear.Left</code>

Use the physical left side.

### <code>F:Fluent.Pdf.PdfFlowClear.None</code>

Pass no additional floats.

### <code>F:Fluent.Pdf.PdfFlowClear.Right</code>

Use the physical right side.

### <code>F:Fluent.Pdf.PdfFlowColumnOrder.LeftToRight</code>

Place successive logical columns from left to right.

### <code>F:Fluent.Pdf.PdfFlowColumnOrder.RightToLeft</code>

Place successive logical columns from right to left.

### <code>F:Fluent.Pdf.PdfFlowFloatSide.Left</code>

Use the physical left side.

### <code>F:Fluent.Pdf.PdfFlowFloatSide.Right</code>

Use the physical right side.

### <code>F:Fluent.Pdf.PdfFlowListKind.Bullet</code>

Use an explicitly supplied bullet.

### <code>F:Fluent.Pdf.PdfFlowListKind.Decimal</code>

Use invariant decimal counters.

### <code>F:Fluent.Pdf.PdfFlowListKind.LowerAlpha</code>

Use lowercase alphabetic counters.

### <code>F:Fluent.Pdf.PdfFlowListKind.LowerRoman</code>

Use lowercase Roman counters from 1 through 3999.

### <code>F:Fluent.Pdf.PdfFlowListKind.UpperAlpha</code>

Use uppercase alphabetic counters.

### <code>F:Fluent.Pdf.PdfFlowListKind.UpperRoman</code>

Use uppercase Roman counters from 1 through 3999.

### <code>F:Fluent.Pdf.PdfFlowOverflow.BreakGrapheme</code>

Reshape emergency breaks at extended grapheme boundaries.

### <code>F:Fluent.Pdf.PdfFlowOverflow.Clip</code>

Clip complete overlong words with an explicit loss diagnostic.

### <code>F:Fluent.Pdf.PdfFlowOverflow.Error</code>

Reject overlong words without producing a layout.

### <code>F:Fluent.Pdf.PdfFlowSectionStart.Continuous</code>

Continue on the current page.

### <code>F:Fluent.Pdf.PdfFlowSectionStart.NextEvenPage</code>

Begin on an even physical page, inserting a blank when needed.

### <code>F:Fluent.Pdf.PdfFlowSectionStart.NextOddPage</code>

Begin on an odd physical page, inserting a blank when needed.

### <code>F:Fluent.Pdf.PdfFlowSectionStart.NextPage</code>

Begin on the next available page.

### <code>F:Fluent.Pdf.PdfFontEmbeddingRights.BitmapOnly</code>

Only bitmap embedding is allowed; outline embedding must fail.

### <code>F:Fluent.Pdf.PdfFontEmbeddingRights.Editable</code>

Editable embedding.

### <code>F:Fluent.Pdf.PdfFontEmbeddingRights.Installable</code>

No program-declared embedding restriction.

### <code>F:Fluent.Pdf.PdfFontEmbeddingRights.NoSubsetting</code>

Subsetting is forbidden.

### <code>F:Fluent.Pdf.PdfFontEmbeddingRights.PreviewAndPrint</code>

Preview and print embedding only.

### <code>F:Fluent.Pdf.PdfFontEmbeddingRights.Restricted</code>

Restricted license embedding; embedding must fail.

### <code>F:Fluent.Pdf.PdfFontFormat.Cff</code>

Compact Font Format cubic outlines.

### <code>F:Fluent.Pdf.PdfFontFormat.Standard</code>

A standard PDF font without an embedded program.

### <code>F:Fluent.Pdf.PdfFontFormat.TrueType</code>

TrueType quadratic outlines in an SFNT program.

### <code>F:Fluent.Pdf.PdfFontFormat.Type1</code>

An encrypted Type 1 PostScript program.

### <code>F:Fluent.Pdf.PdfFontFormat.Type3</code>

Caller-authored PDF glyph procedures, represented by font resources rather than a binary font file.

### <code>F:Fluent.Pdf.PdfFormDataFormat.Fdf</code>

FDF field-value dictionaries; actions, appearances and other import instructions require a separate profile.

### <code>F:Fluent.Pdf.PdfFormDataFormat.Json</code>

Version-one typed JSON field values.

### <code>F:Fluent.Pdf.PdfFormDataFormat.Xfdf</code>

Standard XFDF field/value elements; field kind is supplied by the target form.

### <code>F:Fluent.Pdf.PdfFormDataFormat.Xml</code>

Version-one typed XML field values.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.Comb</code>

Distribute text into MaxLen character cells.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.CommitOnSelectionChange</code>

Commit a choice immediately when selection changes.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.DoNotScroll</code>

Disable viewer text scrolling.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.DoNotSpellCheck</code>

Disable viewer spell checking.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.Edit</code>

An editable combo choice.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.FileSelect</code>

Text represents a file selection.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.MultiSelect</code>

Multiple list selections.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.Multiline</code>

Multiline text.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.NoExport</code>

Exclude the value from form-data export.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.NoToggleToOff</code>

A selected radio option cannot be toggled off interactively.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.None</code>

No optional flags.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.Password</code>

Password presentation; this flag does not encrypt the stored value.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.RadiosInUnison</code>

Radio widgets with equal export names are selected together.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.ReadOnly</code>

Read-only interaction.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.Required</code>

A value is required for submission.

### <code>F:Fluent.Pdf.PdfFormFieldFlags.Sort</code>

The choice options are sorted for presentation.

### <code>F:Fluent.Pdf.PdfFormFieldKind.CheckBox</code>

A checkbox button.

### <code>F:Fluent.Pdf.PdfFormFieldKind.ComboBox</code>

A combo choice field.

### <code>F:Fluent.Pdf.PdfFormFieldKind.ListBox</code>

A list choice field.

### <code>F:Fluent.Pdf.PdfFormFieldKind.PushButton</code>

A push button.

### <code>F:Fluent.Pdf.PdfFormFieldKind.RadioButton</code>

A radio button group.

### <code>F:Fluent.Pdf.PdfFormFieldKind.Signature</code>

A signature field; creating an unsigned field does not sign a document.

### <code>F:Fluent.Pdf.PdfFormFieldKind.Text</code>

A text field.

### <code>F:Fluent.Pdf.PdfFormFieldKind.Unknown</code>

An inspectable unsupported source field type.

### <code>F:Fluent.Pdf.PdfFormTabOrder.Columns</code>

Order by columns.

### <code>F:Fluent.Pdf.PdfFormTabOrder.Rows</code>

Order by rows.

### <code>F:Fluent.Pdf.PdfFormTabOrder.Structure</code>

Use the page structure order; this does not create structure associations.

### <code>F:Fluent.Pdf.PdfGenerationEventKind.LayoutPrepared</code>

Layout/convergence finished; final page data is immutable.

### <code>F:Fluent.Pdf.PdfGenerationEventKind.LayoutStarting</code>

Binding finished and final layout is starting.

### <code>F:Fluent.Pdf.PdfGenerationEventKind.PagePrepared</code>

One final physical page is available for inspection.

### <code>F:Fluent.Pdf.PdfGenerationEventKind.Prepared</code>

All generation work is prepared; final resource checks and file publication still follow.

### <code>F:Fluent.Pdf.PdfGenerationEventKind.RecordPrepared</code>

The selected source record's blocks are captured.

### <code>F:Fluent.Pdf.PdfGenerationEventKind.RecordStarting</code>

A selected source record is about to render.

### <code>F:Fluent.Pdf.PdfGenerationEventKind.SavePrepared</code>

Serialization finished; an atomic destination has not yet been published.

### <code>F:Fluent.Pdf.PdfGenerationEventKind.SaveStarting</code>

Private model writing is about to start.

### <code>F:Fluent.Pdf.PdfGenerationEventKind.Starting</code>

The bounded generation operation is starting.

### <code>F:Fluent.Pdf.PdfGenerationEventKind.TemplatePrepared</code>

The invocation's blocks are captured; pagination has not necessarily occurred.

### <code>F:Fluent.Pdf.PdfGenerationEventKind.TemplateStarting</code>

A reusable template invocation is about to render.

### <code>F:Fluent.Pdf.PdfGraphicsOccurrenceKind.Form</code>

A reusable Form XObject.

### <code>F:Fluent.Pdf.PdfGraphicsOccurrenceKind.Image</code>

An image XObject.

### <code>F:Fluent.Pdf.PdfGraphicsOccurrenceKind.InlineImage</code>

An inline image.

### <code>F:Fluent.Pdf.PdfGraphicsResourceCategory.ColorSpace</code>

Color spaces.

### <code>F:Fluent.Pdf.PdfGraphicsResourceCategory.ExtGState</code>

Extended graphics states.

### <code>F:Fluent.Pdf.PdfGraphicsResourceCategory.Font</code>

Font resources.

### <code>F:Fluent.Pdf.PdfGraphicsResourceCategory.Pattern</code>

Tiling and shading patterns.

### <code>F:Fluent.Pdf.PdfGraphicsResourceCategory.Shading</code>

Shading dictionaries.

### <code>F:Fluent.Pdf.PdfGraphicsResourceCategory.XObject</code>

Form and image XObjects.

### <code>F:Fluent.Pdf.PdfHtmlMedia.Print</code>

Use print media.

### <code>F:Fluent.Pdf.PdfHtmlMedia.Screen</code>

Use screen media.

### <code>F:Fluent.Pdf.PdfImageCodec.Dct</code>

DCT JPEG image samples.

### <code>F:Fluent.Pdf.PdfImageCodec.Jbig2</code>

JBIG2 bitonal image samples.

### <code>F:Fluent.Pdf.PdfImageCodec.Jpx</code>

JPEG 2000 image samples.

### <code>F:Fluent.Pdf.PdfImageOrientation.MirrorHorizontal</code>

Reflect horizontally.

### <code>F:Fluent.Pdf.PdfImageOrientation.MirrorVertical</code>

Reflect vertically.

### <code>F:Fluent.Pdf.PdfImageOrientation.Normal</code>

Rows top to bottom, columns left to right.

### <code>F:Fluent.Pdf.PdfImageOrientation.Rotate180</code>

Rotate 180 degrees.

### <code>F:Fluent.Pdf.PdfImageOrientation.Rotate270</code>

Rotate 270 degrees clockwise.

### <code>F:Fluent.Pdf.PdfImageOrientation.Rotate90</code>

Rotate 90 degrees clockwise.

### <code>F:Fluent.Pdf.PdfImageOrientation.Transpose</code>

Transpose the row and column axes.

### <code>F:Fluent.Pdf.PdfImageOrientation.Transverse</code>

Transpose across the opposite diagonal.

### <code>F:Fluent.Pdf.PdfInputFormat.Json</code>

The version-one JSON document and scalar-binding schema.

### <code>F:Fluent.Pdf.PdfInputFormat.Markdown</code>

The documented bounded Markdown source profile.

### <code>F:Fluent.Pdf.PdfInputFormat.Text</code>

Strict Unicode text, divided into paragraphs at blank lines.

### <code>F:Fluent.Pdf.PdfInputFormat.Xml</code>

The version-one XML document schema, with DTDs and resolvers prohibited.

### <code>F:Fluent.Pdf.PdfInvoiceProfile.FacturX107Minimum</code>

Factur-X1.0.07 MINIMUM; complete XML/business/PDF validation is mandatory before publication.

### <code>F:Fluent.Pdf.PdfLayerUsage.Configured</code>

The configured states without usage applications, as used by a stand-alone printing/aggregation application.

### <code>F:Fluent.Pdf.PdfLayerUsage.Export</code>

A viewer export operation after applying initial viewing usage.

### <code>F:Fluent.Pdf.PdfLayerUsage.Print</code>

A viewer print operation after applying initial viewing usage.

### <code>F:Fluent.Pdf.PdfLayerUsage.View</code>

Screen viewing.

### <code>F:Fluent.Pdf.PdfLengthUnit.Centimeter</code>

One centimeter.

### <code>F:Fluent.Pdf.PdfLengthUnit.Inch</code>

One inch.

### <code>F:Fluent.Pdf.PdfLengthUnit.Millimeter</code>

One millimeter.

### <code>F:Fluent.Pdf.PdfLengthUnit.Pica</code>

Twelve points.

### <code>F:Fluent.Pdf.PdfLengthUnit.Pixel</code>

A pixel at the caller's explicit DPI setting.

### <code>F:Fluent.Pdf.PdfLengthUnit.Point</code>

One seventy-second of an inch.

### <code>F:Fluent.Pdf.PdfLineCap.Butt</code>

Butt cap.

### <code>F:Fluent.Pdf.PdfLineCap.Round</code>

Round cap.

### <code>F:Fluent.Pdf.PdfLineCap.Square</code>

Projecting square cap.

### <code>F:Fluent.Pdf.PdfLineJoin.Bevel</code>

Bevel join.

### <code>F:Fluent.Pdf.PdfLineJoin.Miter</code>

Miter join subject to its explicit limit.

### <code>F:Fluent.Pdf.PdfLineJoin.Round</code>

Round join.

### <code>F:Fluent.Pdf.PdfLossCategory.FidelityReduction</code>

Fidelity was reduced.

### <code>F:Fluent.Pdf.PdfLossCategory.None</code>

No content or fidelity loss.

### <code>F:Fluent.Pdf.PdfLossCategory.OmittedContent</code>

Content was omitted.

### <code>F:Fluent.Pdf.PdfLossCategory.SubstitutedContent</code>

Content was substituted.

### <code>F:Fluent.Pdf.PdfLossCategory.UnsupportedContent</code>

A requested content behavior cannot be supported.

### <code>F:Fluent.Pdf.PdfMergeMissingField.Empty</code>

Use empty text/collections and record an informational policy diagnostic.

### <code>F:Fluent.Pdf.PdfMergeMissingField.Error</code>

Fail with a field diagnostic before publication.

### <code>F:Fluent.Pdf.PdfMergeMissingField.PreserveScalar</code>

Retain scalar placeholders with an informational diagnostic; absent structural/image fields still reject.

### <code>F:Fluent.Pdf.PdfMergedCellCsvPolicy.AnchorOnly</code>

Write the text at its anchor and empty covered cells.

### <code>F:Fluent.Pdf.PdfMergedCellCsvPolicy.RepeatText</code>

Repeat the anchor text in every covered primitive cell.

### <code>F:Fluent.Pdf.PdfMetadataEmbeddedFilePolicy.Reject</code>

Reject removal when embedded files exist, requiring an explicit attachment decision.

### <code>F:Fluent.Pdf.PdfMetadataEmbeddedFilePolicy.Retain</code>

Preserve embedded file bytes and report that their internal metadata remains.

### <code>F:Fluent.Pdf.PdfMetadataSynchronization.InformationToXmp</code>

Copy representable Info fields into XMP, retaining unrelated properties.

### <code>F:Fluent.Pdf.PdfMetadataSynchronization.PreferNewest</code>

Use XMP when its MetadataDate is at least the Info ModDate; otherwise use Info.

### <code>F:Fluent.Pdf.PdfMetadataSynchronization.XmpToInformation</code>

Copy representable XMP fields into Info, retaining unrelated entries.

### <code>F:Fluent.Pdf.PdfMissingDestinationPolicy.OmitWithDiagnostic</code>

Omit the affected navigation entry and report the loss.

### <code>F:Fluent.Pdf.PdfMissingDestinationPolicy.Reject</code>

Reject before producing an incomplete destination.

### <code>F:Fluent.Pdf.PdfOcrAccuracy.ComparisonProfile</code>

Gets the fixed comparison definition: exact Unicode scalars, case-sensitive words split by Unicode15.1 White_Space, with no hidden normalization or trimming.

### <code>F:Fluent.Pdf.PdfOcrExistingTextPolicy.AllowDuplicates</code>

Explicitly permit duplicate text layers.

### <code>F:Fluent.Pdf.PdfOcrExistingTextPolicy.RejectOverlap</code>

Reject a sufficiently overlapping existing mapped word.

### <code>F:Fluent.Pdf.PdfOcrExistingTextPolicy.SkipOverlapping</code>

Skip OCR words covered by existing mapped digital words, retaining the original layer.

### <code>F:Fluent.Pdf.PdfOcrOutputFormat.AltoXml</code>

ALTO4 XML using pixel measurement units and engine word confidence converted to0–1.

### <code>F:Fluent.Pdf.PdfOcrOutputFormat.HOcr</code>

XHTML hOCR with top-left pixel boxes, line/word records and engine word confidence.

### <code>F:Fluent.Pdf.PdfOcrOutputFormat.Json</code>

Version-one JSON with raw Unicode, exact provenance, word geometry/confidence and engine IDs.

### <code>F:Fluent.Pdf.PdfOcrOutputFormat.PlainText</code>

Exact raw recognized Unicode, including engine whitespace.

### <code>F:Fluent.Pdf.PdfOcrPixelFormat.Bgra32</code>

Four blue, green, red, alpha bytes; only opaque samples are accepted by the local source profile.

### <code>F:Fluent.Pdf.PdfOcrPixelFormat.Gray8</code>

One unsigned gray byte per pixel.

### <code>F:Fluent.Pdf.PdfOcrPixelFormat.Rgb24</code>

Three unsigned red, green, blue bytes per pixel.

### <code>F:Fluent.Pdf.PdfOcrSegmentation.Automatic</code>

Automatic page segmentation without orientation detection.

### <code>F:Fluent.Pdf.PdfOcrSegmentation.SingleBlock</code>

A single uniform text block.

### <code>F:Fluent.Pdf.PdfOcrSegmentation.SingleLine</code>

A single text line.

### <code>F:Fluent.Pdf.PdfOcrSegmentation.SparseText</code>

Sparse text without an inferred document reading order.

### <code>F:Fluent.Pdf.PdfOperator.BeginCompatibility</code>

Begin a compatibility section: BX.

### <code>F:Fluent.Pdf.PdfOperator.BeginMarkedContent</code>

Begin marked content: BMC.

### <code>F:Fluent.Pdf.PdfOperator.BeginMarkedContentProperties</code>

Begin marked content with properties: BDC.

### <code>F:Fluent.Pdf.PdfOperator.BeginText</code>

Begin a text object: BT.

### <code>F:Fluent.Pdf.PdfOperator.CharacterSpacing</code>

Set character spacing: Tc.

### <code>F:Fluent.Pdf.PdfOperator.Clip</code>

Apply nonzero clipping at path end: W.

### <code>F:Fluent.Pdf.PdfOperator.ClipEvenOdd</code>

Apply even-odd clipping at path end: W*.

### <code>F:Fluent.Pdf.PdfOperator.CloseFillStroke</code>

Close, fill and stroke: b.

### <code>F:Fluent.Pdf.PdfOperator.CloseFillStrokeEvenOdd</code>

Close, even-odd fill and stroke: b*.

### <code>F:Fluent.Pdf.PdfOperator.ClosePath</code>

Close the current subpath: h.

### <code>F:Fluent.Pdf.PdfOperator.CloseStroke</code>

Close and stroke: s.

### <code>F:Fluent.Pdf.PdfOperator.CurveFromCurrent</code>

Add a curve with its first control point at the current point: v.

### <code>F:Fluent.Pdf.PdfOperator.CurveTo</code>

Add a cubic curve: c.

### <code>F:Fluent.Pdf.PdfOperator.CurveToEndpoint</code>

Add a curve with its final control point at its endpoint: y.

### <code>F:Fluent.Pdf.PdfOperator.Dash</code>

Set dash pattern: d.

### <code>F:Fluent.Pdf.PdfOperator.DrawObject</code>

Paint a named external object: Do.

### <code>F:Fluent.Pdf.PdfOperator.EndCompatibility</code>

End a compatibility section: EX.

### <code>F:Fluent.Pdf.PdfOperator.EndMarkedContent</code>

End marked content: EMC.

### <code>F:Fluent.Pdf.PdfOperator.EndPath</code>

End the path without painting: n.

### <code>F:Fluent.Pdf.PdfOperator.EndText</code>

End a text object: ET.

### <code>F:Fluent.Pdf.PdfOperator.ExtendedState</code>

Apply named extended graphics state: gs.

### <code>F:Fluent.Pdf.PdfOperator.Fill</code>

Fill with nonzero winding: f.

### <code>F:Fluent.Pdf.PdfOperator.FillCmyk</code>

Set fill CMYK: k.

### <code>F:Fluent.Pdf.PdfOperator.FillColor</code>

Set fill components: sc.

### <code>F:Fluent.Pdf.PdfOperator.FillColorExtended</code>

Set extended fill components/pattern: scn.

### <code>F:Fluent.Pdf.PdfOperator.FillColorSpace</code>

Set fill color space: cs.

### <code>F:Fluent.Pdf.PdfOperator.FillEvenOdd</code>

Fill with even-odd winding: f*.

### <code>F:Fluent.Pdf.PdfOperator.FillGray</code>

Set fill gray: g.

### <code>F:Fluent.Pdf.PdfOperator.FillRgb</code>

Set fill RGB: rg.

### <code>F:Fluent.Pdf.PdfOperator.FillStroke</code>

Fill and stroke: B.

### <code>F:Fluent.Pdf.PdfOperator.FillStrokeEvenOdd</code>

Even-odd fill and stroke: B*.

### <code>F:Fluent.Pdf.PdfOperator.Flatness</code>

Set flatness: i.

### <code>F:Fluent.Pdf.PdfOperator.Font</code>

Select a font and size: Tf.

### <code>F:Fluent.Pdf.PdfOperator.GlyphBounds</code>

Set Type 3 glyph width and bounds: d1.

### <code>F:Fluent.Pdf.PdfOperator.GlyphWidth</code>

Set Type 3 glyph width: d0.

### <code>F:Fluent.Pdf.PdfOperator.HorizontalScale</code>

Set horizontal text scale: Tz.

### <code>F:Fluent.Pdf.PdfOperator.InlineImage</code>

An opaque inline image with separately captured dictionary and data.

### <code>F:Fluent.Pdf.PdfOperator.Leading</code>

Set text leading: TL.

### <code>F:Fluent.Pdf.PdfOperator.LegacyFill</code>

Legacy nonzero fill spelling: F.

### <code>F:Fluent.Pdf.PdfOperator.LineCap</code>

Set line cap: J.

### <code>F:Fluent.Pdf.PdfOperator.LineJoin</code>

Set line join: j.

### <code>F:Fluent.Pdf.PdfOperator.LineTo</code>

Add a line: l.

### <code>F:Fluent.Pdf.PdfOperator.LineWidth</code>

Set line width: w.

### <code>F:Fluent.Pdf.PdfOperator.MarkPoint</code>

Mark a content point: MP.

### <code>F:Fluent.Pdf.PdfOperator.MarkPointProperties</code>

Mark a point with properties: DP.

### <code>F:Fluent.Pdf.PdfOperator.MiterLimit</code>

Set miter limit: M.

### <code>F:Fluent.Pdf.PdfOperator.MoveText</code>

Move the text line origin: Td.

### <code>F:Fluent.Pdf.PdfOperator.MoveTextLeading</code>

Move the text line origin and set leading: TD.

### <code>F:Fluent.Pdf.PdfOperator.MoveTo</code>

Move the current path point: m.

### <code>F:Fluent.Pdf.PdfOperator.NextLineShowText</code>

Move to the next line and show text: apostrophe.

### <code>F:Fluent.Pdf.PdfOperator.NextTextLine</code>

Move to the next text line: T*.

### <code>F:Fluent.Pdf.PdfOperator.Rectangle</code>

Add a rectangle: re.

### <code>F:Fluent.Pdf.PdfOperator.RenderingIntent</code>

Set rendering intent: ri.

### <code>F:Fluent.Pdf.PdfOperator.RestoreState</code>

Restore graphics state: Q.

### <code>F:Fluent.Pdf.PdfOperator.SaveState</code>

Save graphics state: q.

### <code>F:Fluent.Pdf.PdfOperator.Shading</code>

Paint a shading: sh.

### <code>F:Fluent.Pdf.PdfOperator.ShowPositionedText</code>

Show bytes and positioning adjustments: TJ.

### <code>F:Fluent.Pdf.PdfOperator.ShowText</code>

Show encoded glyph bytes: Tj.

### <code>F:Fluent.Pdf.PdfOperator.SpacedNextLineShowText</code>

Set spacing, move to the next line and show text: quotation mark.

### <code>F:Fluent.Pdf.PdfOperator.Stroke</code>

Stroke the path: S.

### <code>F:Fluent.Pdf.PdfOperator.StrokeCmyk</code>

Set stroke CMYK: K.

### <code>F:Fluent.Pdf.PdfOperator.StrokeColor</code>

Set stroke components: SC.

### <code>F:Fluent.Pdf.PdfOperator.StrokeColorExtended</code>

Set extended stroke components/pattern: SCN.

### <code>F:Fluent.Pdf.PdfOperator.StrokeColorSpace</code>

Set stroke color space: CS.

### <code>F:Fluent.Pdf.PdfOperator.StrokeGray</code>

Set stroke gray: G.

### <code>F:Fluent.Pdf.PdfOperator.StrokeRgb</code>

Set stroke RGB: RG.

### <code>F:Fluent.Pdf.PdfOperator.TextMatrix</code>

Set text and text-line matrices: Tm.

### <code>F:Fluent.Pdf.PdfOperator.TextRenderingMode</code>

Set text rendering mode: Tr.

### <code>F:Fluent.Pdf.PdfOperator.TextRise</code>

Set text rise: Ts.

### <code>F:Fluent.Pdf.PdfOperator.Transform</code>

Concatenate a transformation: cm.

### <code>F:Fluent.Pdf.PdfOperator.Unknown</code>

An explicitly preserved extension token.

### <code>F:Fluent.Pdf.PdfOperator.WordSpacing</code>

Set word spacing: Tw.

### <code>F:Fluent.Pdf.PdfOptimizationPreset.CompactImages</code>

Additionally resample supported eight-bit image planes; requires permissive loss policy.

### <code>F:Fluent.Pdf.PdfOptimizationPreset.Lossless</code>

Preserve decoded bytes and document semantics without resampling images.

### <code>F:Fluent.Pdf.PdfPadesProfile.B</code>

Baseline B.

### <code>F:Fluent.Pdf.PdfPadesProfile.LT</code>

Baseline LT with validation evidence.

### <code>F:Fluent.Pdf.PdfPadesProfile.LTA</code>

Baseline LTA with archival timestamp/renewal.

### <code>F:Fluent.Pdf.PdfPadesProfile.T</code>

Baseline T with trusted timestamp.

### <code>F:Fluent.Pdf.PdfPageBox.Art</code>

The meaningful artwork region.

### <code>F:Fluent.Pdf.PdfPageBox.Bleed</code>

The production bleed region.

### <code>F:Fluent.Pdf.PdfPageBox.Crop</code>

The default visible/printable region.

### <code>F:Fluent.Pdf.PdfPageBox.Media</code>

The physical medium.

### <code>F:Fluent.Pdf.PdfPageBox.Trim</code>

The intended finished-page region.

### <code>F:Fluent.Pdf.PdfPageLabelStyle.Decimal</code>

Invariant decimal numbers.

### <code>F:Fluent.Pdf.PdfPageLabelStyle.LowerLetters</code>

Lowercase repeated-letter PDF labels.

### <code>F:Fluent.Pdf.PdfPageLabelStyle.LowerRoman</code>

Lowercase Roman numbers from one through 3999.

### <code>F:Fluent.Pdf.PdfPageLabelStyle.None</code>

Prefix only.

### <code>F:Fluent.Pdf.PdfPageLabelStyle.UpperLetters</code>

Uppercase repeated-letter PDF labels.

### <code>F:Fluent.Pdf.PdfPageLabelStyle.UpperRoman</code>

Uppercase Roman numbers from one through 3999.

### <code>F:Fluent.Pdf.PdfPageLayout.OneColumn</code>

One continuous column.

### <code>F:Fluent.Pdf.PdfPageLayout.SinglePage</code>

Single page.

### <code>F:Fluent.Pdf.PdfPageLayout.TwoColumnLeft</code>

Two columns with odd pages left.

### <code>F:Fluent.Pdf.PdfPageLayout.TwoColumnRight</code>

Two columns with odd pages right.

### <code>F:Fluent.Pdf.PdfPageLayout.TwoPageLeft</code>

Two page spreads with odd pages left.

### <code>F:Fluent.Pdf.PdfPageLayout.TwoPageRight</code>

Two page spreads with odd pages right.

### <code>F:Fluent.Pdf.PdfPageMode.FullScreen</code>

Full screen.

### <code>F:Fluent.Pdf.PdfPageMode.UseAttachments</code>

Attachments panel.

### <code>F:Fluent.Pdf.PdfPageMode.UseNone</code>

No navigation panel.

### <code>F:Fluent.Pdf.PdfPageMode.UseOC</code>

Optional content panel.

### <code>F:Fluent.Pdf.PdfPageMode.UseOutlines</code>

Outline panel.

### <code>F:Fluent.Pdf.PdfPageMode.UseThumbs</code>

Thumbnail panel.

### <code>F:Fluent.Pdf.PdfPageRotation.Clockwise180</code>

Rotate clockwise by 180 degrees.

### <code>F:Fluent.Pdf.PdfPageRotation.Clockwise270</code>

Rotate clockwise by 270 degrees.

### <code>F:Fluent.Pdf.PdfPageRotation.Clockwise90</code>

Rotate clockwise by 90 degrees.

### <code>F:Fluent.Pdf.PdfPageRotation.None</code>

No rotation.

### <code>F:Fluent.Pdf.PdfParagraphDirection.Auto</code>

Resolve from the first strong character outside isolates, defaulting to left-to-right.

### <code>F:Fluent.Pdf.PdfParagraphDirection.LeftToRight</code>

Use an even left-to-right paragraph level.

### <code>F:Fluent.Pdf.PdfParagraphDirection.RightToLeft</code>

Use an odd right-to-left paragraph level.

### <code>F:Fluent.Pdf.PdfPasswordCipher.Aes128</code>

PDF 1.6 AES-128 with revision 4 authentication, for explicit compatibility workflows.

### <code>F:Fluent.Pdf.PdfPasswordCipher.Aes256</code>

PDF 2.0 AES-256 with revision 6 password authentication.

### <code>F:Fluent.Pdf.PdfPrintDuplex.LongEdge</code>

Bind along the long edge.

### <code>F:Fluent.Pdf.PdfPrintDuplex.ShortEdge</code>

Bind along the short edge.

### <code>F:Fluent.Pdf.PdfPrintDuplex.Simplex</code>

One side per sheet.

### <code>F:Fluent.Pdf.PdfPrintJobState.Aborted</code>

Aborted by the spooler.

### <code>F:Fluent.Pdf.PdfPrintJobState.Canceled</code>

Canceled by the spooler.

### <code>F:Fluent.Pdf.PdfPrintJobState.CancellationRequested</code>

Cancellation acknowledged; physical output may already exist.

### <code>F:Fluent.Pdf.PdfPrintJobState.Completed</code>

Completed according to the spooler.

### <code>F:Fluent.Pdf.PdfPrintJobState.Held</code>

Held by the spooler.

### <code>F:Fluent.Pdf.PdfPrintJobState.Pending</code>

Queued.

### <code>F:Fluent.Pdf.PdfPrintJobState.Processing</code>

Processing.

### <code>F:Fluent.Pdf.PdfPrintJobState.Stopped</code>

Stopped pending intervention.

### <code>F:Fluent.Pdf.PdfPrintJobState.Unknown</code>

The spooler supplied no recognized state.

### <code>F:Fluent.Pdf.PdfPrintScaling.Fit</code>

Fit the selected print area.

### <code>F:Fluent.Pdf.PdfPrintScaling.None</code>

Use physical PDF dimensions.

### <code>F:Fluent.Pdf.PdfProcessingMode.Permissive</code>

Allow reported losses and retain every record in the operation report.

### <code>F:Fluent.Pdf.PdfProcessingMode.Strict</code>

Reject every reported loss.

### <code>F:Fluent.Pdf.PdfPublicKeyCipher.Aes128</code>

Explicit PDF 1.6 AES-128 compatibility output.

### <code>F:Fluent.Pdf.PdfPublicKeyCipher.Aes256</code>

PDF 2.0 AES-256.

### <code>F:Fluent.Pdf.PdfRasterColor.Gray</code>

Device grayscale output in a BGRA envelope.

### <code>F:Fluent.Pdf.PdfRasterColor.Rgb</code>

Device RGB output.

### <code>F:Fluent.Pdf.PdfRasterFileFormat.Bmp</code>

Optional BMP file decoder.

### <code>F:Fluent.Pdf.PdfRasterFileFormat.Gif</code>

Optional GIF frame decoder.

### <code>F:Fluent.Pdf.PdfRasterFileFormat.Heif</code>

Optional HEIF/HEIC frame decoder.

### <code>F:Fluent.Pdf.PdfRasterFileFormat.Jpeg</code>

JPEG.

### <code>F:Fluent.Pdf.PdfRasterFileFormat.Png</code>

Static PNG; APNG requires another explicitly qualified profile.

### <code>F:Fluent.Pdf.PdfRasterFileFormat.Tiff</code>

Optional TIFF/multipage TIFF decoder.

### <code>F:Fluent.Pdf.PdfRasterFileFormat.WebP</code>

Optional WebP frame decoder.

### <code>F:Fluent.Pdf.PdfRasterFormat.Bmp</code>

Opaque24-bit bottom-up BMP with physical resolution.

### <code>F:Fluent.Pdf.PdfRasterFormat.Png</code>

Eight-bit RGBA PNG with physical resolution.

### <code>F:Fluent.Pdf.PdfRasterFormat.Tiff</code>

Multipage uncompressed eight-bit RGBA classic TIFF with resolution and optional ICC.

### <code>F:Fluent.Pdf.PdfRepairOrphanPolicy.Discard</code>

Discard and identify every unreachable indexed object; strict mode rejects this reported data loss.

### <code>F:Fluent.Pdf.PdfRepairOrphanPolicy.Preserve</code>

Retain and identify every unreachable indexed object.

### <code>F:Fluent.Pdf.PdfResourceKind.Depth</code>

Simultaneous recursion scopes.

### <code>F:Fluent.Pdf.PdfResourceKind.ExpandedBytes</code>

Expanded or decoded bytes.

### <code>F:Fluent.Pdf.PdfResourceKind.InputBytes</code>

Bytes delivered from input.

### <code>F:Fluent.Pdf.PdfResourceKind.Objects</code>

Parsed or generated objects.

### <code>F:Fluent.Pdf.PdfResourceKind.OutputBytes</code>

Written bytes and explicit output growth.

### <code>F:Fluent.Pdf.PdfResourceKind.Pixels</code>

Decoded or rendered pixels.

### <code>F:Fluent.Pdf.PdfResourceKind.Workers</code>

Simultaneous worker scopes.

### <code>F:Fluent.Pdf.PdfRevocationStatus.Good</code>

The explicitly configured authority reports a fresh good status.

### <code>F:Fluent.Pdf.PdfRevocationStatus.Revoked</code>

The explicitly configured authority reports revocation.

### <code>F:Fluent.Pdf.PdfRevocationStatus.Unknown</code>

No fresh authoritative status is available.

### <code>F:Fluent.Pdf.PdfScanOrientation.Automatic</code>

Compare four explicitly configured OCR observations and require a prior confidence margin.

### <code>F:Fluent.Pdf.PdfScanOrientation.Clockwise180</code>

Rotate180 degrees.

### <code>F:Fluent.Pdf.PdfScanOrientation.Clockwise270</code>

Rotate clockwise270 degrees.

### <code>F:Fluent.Pdf.PdfScanOrientation.Clockwise90</code>

Rotate clockwise90 degrees.

### <code>F:Fluent.Pdf.PdfScanOrientation.None</code>

Retain the original orientation.

### <code>F:Fluent.Pdf.PdfScanRegionKind.Form</code>

A caller-declared form whose raw engine order is retained.

### <code>F:Fluent.Pdf.PdfScanRegionKind.Prose</code>

Geometrically inferred prose; not an accessibility assertion.

### <code>F:Fluent.Pdf.PdfScanRegionKind.RuledTableOrForm</code>

Measured closed rulings; table-versus-form semantics remain unresolved and prose ordering is withheld.

### <code>F:Fluent.Pdf.PdfScanRegionKind.Table</code>

A caller-declared table whose raw engine order is retained.

### <code>F:Fluent.Pdf.PdfSignatureLockAction.All</code>

Lock every form field.

### <code>F:Fluent.Pdf.PdfSignatureLockAction.Exclude</code>

Lock all except listed fields and their descendants.

### <code>F:Fluent.Pdf.PdfSignatureLockAction.Include</code>

Lock listed fields and their descendants.

### <code>F:Fluent.Pdf.PdfSoftMaskKind.Alpha</code>

Use group alpha.

### <code>F:Fluent.Pdf.PdfSoftMaskKind.Luminosity</code>

Use group luminosity in its explicit blending space.

### <code>F:Fluent.Pdf.PdfSpotColorPolicy.CompositeAlternate</code>

Use declared alternates, reporting conversion loss.

### <code>F:Fluent.Pdf.PdfSpotColorPolicy.Reject</code>

Reject spot colors rather than substitute a process preview.

### <code>F:Fluent.Pdf.PdfStampContentKind.Drawing</code>

An original drawing.

### <code>F:Fluent.Pdf.PdfStampContentKind.Html</code>

An explicitly rendered HTML page.

### <code>F:Fluent.Pdf.PdfStampContentKind.Image</code>

An immutable image, retaining encoded samples and orientation.

### <code>F:Fluent.Pdf.PdfStampContentKind.PdfPage</code>

The passive visible content of a PDF page.

### <code>F:Fluent.Pdf.PdfStampContentKind.Text</code>

Explicitly shaped text and its embedded fonts.

### <code>F:Fluent.Pdf.PdfStampLayer.Background</code>

Paint behind existing page content.

### <code>F:Fluent.Pdf.PdfStampLayer.Foreground</code>

Paint in front of existing page content.

### <code>F:Fluent.Pdf.PdfTableDetectionMode.Borderless</code>

Infer left-aligned columns from geometric text lines with explicit minimum gutters.

### <code>F:Fluent.Pdf.PdfTableDetectionMode.Ruled</code>

Infer rectangular grids/merged cells from literal axis-aligned source stroke segments.

### <code>F:Fluent.Pdf.PdfTableVerticalAlignment.Bottom</code>

Place at the bottom.

### <code>F:Fluent.Pdf.PdfTableVerticalAlignment.Center</code>

Center vertically.

### <code>F:Fluent.Pdf.PdfTableVerticalAlignment.Top</code>

Place at the top.

### <code>F:Fluent.Pdf.PdfTextDirection.LeftToRight</code>

Left to right.

### <code>F:Fluent.Pdf.PdfTextDirection.RightToLeft</code>

Right to left.

### <code>F:Fluent.Pdf.PdfTextDirection.TopToBottom</code>

Top to bottom using vertical OpenType substitutions and positioning.

### <code>F:Fluent.Pdf.PdfTextMappingConfidence.ActualText</code>

Explicit source ActualText replacement.

### <code>F:Fluent.Pdf.PdfTextMappingConfidence.Inferred</code>

Inferred from a declared simple-font encoding through an explicit provider.

### <code>F:Fluent.Pdf.PdfTextMappingConfidence.ToUnicode</code>

Exact source ToUnicode mapping, without a claim about the source mapping's linguistic accuracy.

### <code>F:Fluent.Pdf.PdfTextMappingConfidence.Unmapped</code>

No mapping was available; a visible replacement is accompanied by diagnostics.

### <code>F:Fluent.Pdf.PdfTextReplacementOverflow.Reject</code>

Reject text that exceeds the captured frame.

### <code>F:Fluent.Pdf.PdfTextReplacementOverflow.ShrinkToFit</code>

Uniformly reduce the explicit font size to fit the captured frame.

### <code>F:Fluent.Pdf.PdfTextStringEncoding.PdfDocEncoding</code>

PDFDocEncoding with no marker.

### <code>F:Fluent.Pdf.PdfTextStringEncoding.Utf16BigEndian</code>

UTF-16 big endian with its byte-order marker.

### <code>F:Fluent.Pdf.PdfTextStringEncoding.Utf8</code>

PDF 2.0 UTF-8 with its required marker.

### <code>F:Fluent.Pdf.PdfTransitionDimension.Horizontal</code>

Horizontal.

### <code>F:Fluent.Pdf.PdfTransitionDimension.Vertical</code>

Vertical.

### <code>F:Fluent.Pdf.PdfTransitionMotion.Inward</code>

Inward.

### <code>F:Fluent.Pdf.PdfTransitionMotion.Outward</code>

Outward.

### <code>F:Fluent.Pdf.PdfTransitionStyle.Blinds</code>

Blinds.

### <code>F:Fluent.Pdf.PdfTransitionStyle.Box</code>

Box.

### <code>F:Fluent.Pdf.PdfTransitionStyle.Cover</code>

Cover.

### <code>F:Fluent.Pdf.PdfTransitionStyle.Dissolve</code>

Dissolve.

### <code>F:Fluent.Pdf.PdfTransitionStyle.Fade</code>

Fade.

### <code>F:Fluent.Pdf.PdfTransitionStyle.Fly</code>

Fly.

### <code>F:Fluent.Pdf.PdfTransitionStyle.Glitter</code>

Glitter.

### <code>F:Fluent.Pdf.PdfTransitionStyle.Push</code>

Push.

### <code>F:Fluent.Pdf.PdfTransitionStyle.Replace</code>

Immediate replacement.

### <code>F:Fluent.Pdf.PdfTransitionStyle.Split</code>

Split.

### <code>F:Fluent.Pdf.PdfTransitionStyle.Uncover</code>

Uncover.

### <code>F:Fluent.Pdf.PdfTransitionStyle.Wipe</code>

Wipe.

### <code>F:Fluent.Pdf.PdfUnknownExtensionPolicy.Preserve</code>

Preserve opaque extension dictionaries and require a supported declared base version.

### <code>F:Fluent.Pdf.PdfUnknownExtensionPolicy.Reject</code>

Reject developer extensions that the caller did not explicitly declare.

### <code>F:Fluent.Pdf.PdfUnknownOperatorPolicy.Preserve</code>

Preserve their exact token and operands without interpreting or executing them.

### <code>F:Fluent.Pdf.PdfUnknownOperatorPolicy.Reject</code>

Reject unknown tokens.

### <code>F:Fluent.Pdf.PdfValueKind.Array</code>

An ordered sequence.

### <code>F:Fluent.Pdf.PdfValueKind.Boolean</code>

A Boolean.

### <code>F:Fluent.Pdf.PdfValueKind.Dictionary</code>

A dictionary with ordinal byte-valued names.

### <code>F:Fluent.Pdf.PdfValueKind.Name</code>

A byte-valued name, represented as Latin-1 characters.

### <code>F:Fluent.Pdf.PdfValueKind.Null</code>

The null object.

### <code>F:Fluent.Pdf.PdfValueKind.Number</code>

A decimal PDF number, retaining its original spelling.

### <code>F:Fluent.Pdf.PdfValueKind.Reference</code>

An indirect reference.

### <code>F:Fluent.Pdf.PdfValueKind.Stream</code>

A dictionary and opaque encoded stream.

### <code>F:Fluent.Pdf.PdfValueKind.String</code>

An uninterpreted byte string.

### <code>F:Fluent.Pdf.PdfWordFormat.Doc</code>

Compound binary Word document.

### <code>F:Fluent.Pdf.PdfWordFormat.Docx</code>

Open XML WordprocessingML package.

### <code>F:Fluent.Pdf.PdfWordFormat.Rtf</code>

Rich Text Format.

### <code>F:Fluent.Pdf.PdfWordReconstruction.Editable</code>

Reconstruct editable content with explicit recovered-text versus inferred-structure diagnostics.

### <code>F:Fluent.Pdf.PdfWordReconstruction.FixedLayout</code>

Preserve page geometry using an explicitly supplied renderer; rasterization and resulting semantic losses must be reported.

### <code>F:Fluent.Pdf.PdfWriteMode.Full</code>

Write a new complete file, preserving active object identities.

### <code>F:Fluent.Pdf.PdfWriteMode.Incremental</code>

Copy the original bytes exactly and append changed objects and a cross-reference revision.

### <code>M:Fluent.Pdf.IPdfAccessibilityValidator.Validate(Fluent.Pdf.PdfAccessibilityProfile,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Validates the complete requested standard against borrowed exact input under the mandatory context. No engine/model/network discovery is implicit.

### <code>M:Fluent.Pdf.IPdfArchivalValidator.Validate(Fluent.Pdf.PdfArchivalProfile,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Validates the requested complete profile with the mandatory context and returns exact input/profile binding. No network, engine or font discovery is implicit.

### <code>M:Fluent.Pdf.IPdfAsyncSignatureProvider.SignAsync(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Signs once; all borrowed streams and sessions must finish before completion.

### <code>M:Fluent.Pdf.IPdfBarcodeDecoder.Decode(Fluent.Pdf.PdfOcrRaster,Fluent.Pdf.PdfOperationContext)</code>

Decodes an explicitly captured raster. This finite source profile returns at most one symbol per requested image/region; all landmarks use its top-left pixel frame. Pixel leases expire at return.

### <code>M:Fluent.Pdf.IPdfBarcodeDecoder.Supports(Fluent.Pdf.PdfBarcodeSymbology)</code>

Reports exact symbology availability before generation.

### <code>M:Fluent.Pdf.IPdfCertificateStatusProvider.Check(System.IO.Stream,System.DateTimeOffset,Fluent.Pdf.PdfOperationContext)</code>

Returns authoritative status for the exact borrowed DER certificate at the explicit instant. Implementations validate freshness and responses under the shared context.

### <code>M:Fluent.Pdf.IPdfCidFontProgram.MapCid(System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Maps a declared CID to a glyph identity, or zero when absent.

### <code>M:Fluent.Pdf.IPdfCmsEnvelopeProvider.Encrypt(System.Collections.Generic.IReadOnlyList{Fluent.Pdf.PdfCertificateRecipient},System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Encrypts one bounded PDF seed/permission payload for a nonempty, equally authorized recipient group. Algorithms and private staging must be explicit; no key or network discovery.

### <code>M:Fluent.Pdf.IPdfCmsEnvelopeProvider.TryDecrypt(Fluent.Pdf.PdfCertificateCredential,System.Boolean,System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Decrypts a matching recipient using only its explicit RSA handle, returning false without output when its certificate is absent. Legacy transport requires explicit permission. Caller streams and key remain borrowed.

### <code>M:Fluent.Pdf.IPdfColorManagedPageRenderer.Render(Fluent.Pdf.PdfRasterRequest,Fluent.Pdf.PdfColorRenderOptions,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Writes exact straight-alpha BGRA in the requested RGB output profile under shared render/pixel/worker limits.

### <code>M:Fluent.Pdf.IPdfDocumentReader.Read(System.IO.Stream,Fluent.Pdf.PdfDocument,Fluent.Pdf.PdfOperationContext)</code>

Reads from a borrowed read-only lease into the target; neither the lease nor target may be retained after return.

Remarks: The input position may advance. Charge resources before allocation, report losses and poll context checkpoints. No implicit rewind or ownership transfer occurs.

### <code>M:Fluent.Pdf.IPdfDocumentWriter.Write(Fluent.Pdf.PdfDocumentSnapshot,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Writes at the current position of a borrowed write-only lease and honors cancellation.

Remarks: Charge resources before allocation and report every loss through the context. The lease expires on return. Stream output may be partial on failure; file-path saves are committed atomically.

### <code>M:Fluent.Pdf.IPdfExternalSigningKey.OpenSessionAsync(Fluent.Pdf.PdfOperationContext)</code>

Opens one bounded authenticated session without exporting a private key.

### <code>M:Fluent.Pdf.IPdfExternalSigningSession.SignHashAsync(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Signs the exact32-byte SHA256 digest with RSA-PKCS1 and emits the raw RSA signature, never CMS. No secret logging is permitted.

### <code>M:Fluent.Pdf.IPdfFontEncodingMetricsProvider.GetSimpleAdvances(Fluent.Pdf.PdfValue,System.String,Fluent.Pdf.PdfOperationContext)</code>

Returns 256 advances in 1000-em units, indexed by encoded byte. Null denotes an unsupported glyph name; no machine fonts may be used.

### <code>M:Fluent.Pdf.IPdfFontEncodingProvider.DecodeSimple(Fluent.Pdf.PdfValue,Fluent.Pdf.PdfValue,System.String,Fluent.Pdf.PdfOperationContext)</code>

Decodes a direct simple-font encoding and immutable bytes under the mandatory context. Unknown mappings must be reported.

### <code>M:Fluent.Pdf.IPdfFontProgram.GetAdvance(System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Gets a glyph's horizontal advance in design units.

### <code>M:Fluent.Pdf.IPdfFontProgram.GetGlyphName(System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Gets an exact glyph name for simple Type 1/CFF encodings.

### <code>M:Fluent.Pdf.IPdfFontProgram.MapGlyph(System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Maps one Unicode scalar to a glyph identity; zero means unavailable.

### <code>M:Fluent.Pdf.IPdfFontProgram.Write(System.IO.Stream,System.Collections.Generic.IReadOnlyList{System.Int32},System.Boolean,Fluent.Pdf.PdfOperationContext)</code>

Writes a complete embeddable program, including composite dependencies for a requested subset. Retained glyph identities must remain stable.

### <code>M:Fluent.Pdf.IPdfFontProvider.Read(System.IO.Stream,System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Reads one explicitly selected collection face from a borrowed stream under the mandatory context.

### <code>M:Fluent.Pdf.IPdfFontProvider.Standard(System.String,Fluent.Pdf.PdfOperationContext)</code>

Returns one exact base-14 PostScript font name, or rejects unsupported names.

### <code>M:Fluent.Pdf.IPdfFontShapingSource.CopyShapingSource(System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Copies original bytes to the supplied bounded borrowed stream without modifying it or searching machine fonts.

### <code>M:Fluent.Pdf.IPdfHtmlRenderer.Render(Fluent.Pdf.PdfHtmlSource,Fluent.Pdf.PdfHtmlOptions,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Renders HTML/CSS and explicitly enabled JavaScript to a borrowed PDF-output lease. No network, local file, engine or font discovery is implicit.

### <code>M:Fluent.Pdf.IPdfImageCodecProvider.Decode(Fluent.Pdf.PdfImageCodec,Fluent.Pdf.PdfImageInfo,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfValue,System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Decodes into the SDK's bounded sample sink, polling the mandatory context and reporting every failure/loss.

### <code>M:Fluent.Pdf.IPdfImageCodecProvider.Encode(Fluent.Pdf.PdfImageCodec,Fluent.Pdf.PdfImageInfo,Fluent.Pdf.PdfValue,System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Encodes exactly the supplied sample geometry into the SDK's bounded sink; input remains caller-owned.

### <code>M:Fluent.Pdf.IPdfImageCodecProvider.Inspect(Fluent.Pdf.PdfImageCodec,System.IO.Stream,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfOperationContext)</code>

Inspects bounded encoded headers without allocating an image; the SDK charges returned pixels before decoding.

### <code>M:Fluent.Pdf.IPdfImageCodecProvider.Supports(Fluent.Pdf.PdfImageCodec,System.Boolean)</code>

Reports declared algorithm/direction support; this is not production qualification.

### <code>M:Fluent.Pdf.IPdfInvoiceProducer.Convert(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfInvoiceData,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Produces a candidate from the SDK-generated human-readable PDF and exact CII XML. Preserve literal human totals, metadata, associated XML and required PDF/A/invoice identification. Report all losses through context. Leases expire on return.

### <code>M:Fluent.Pdf.IPdfInvoiceProducer.Supports(Fluent.Pdf.PdfInvoiceProfile)</code>

Reports exact invoice edition/profile and local prerequisites.

### <code>M:Fluent.Pdf.IPdfInvoiceValidator.Supports(Fluent.Pdf.PdfInvoiceProfile)</code>

Reports exact version/profile validation availability.

### <code>M:Fluent.Pdf.IPdfInvoiceValidator.Validate(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfInvoiceData,System.String,Fluent.Pdf.PdfOperationContext)</code>

Validates the exact PDF/XML/model/request and complete edition-specific schema/business/metadata/reconciliation rules. A syntax parser or metadata check cannot return compliance. No ambient network/schema download is permitted.

### <code>M:Fluent.Pdf.IPdfLinearizationProducer.Linearize(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfLinearizationOptions,Fluent.Pdf.PdfOperationContext)</code>

Rebuilds all linearization offsets and hints for exact output bytes, retaining document semantics. Uses the mandatory shared context; borrowed leases expire on return.

### <code>M:Fluent.Pdf.IPdfLinearizationProducer.Supports(Fluent.Pdf.PdfLinearizationOptions)</code>

Checks exact settings and explicit local assets before input capture.

### <code>M:Fluent.Pdf.IPdfLinearizationValidator.Supports(Fluent.Pdf.PdfLinearizationOptions)</code>

Checks validation availability before source capture.

### <code>M:Fluent.Pdf.IPdfLinearizationValidator.Validate(System.IO.Stream,Fluent.Pdf.PdfLinearizationOptions,System.String,Fluent.Pdf.PdfOperationContext)</code>

Validates all linearization rules and binds its report to exact bytes and the supplied request digest, without downloads or implicit network access.

### <code>M:Fluent.Pdf.IPdfOcrProvider.Recognize(Fluent.Pdf.PdfOcrRaster,Fluent.Pdf.PdfOcrOptions,Fluent.Pdf.PdfOperationContext)</code>

Recognizes an immutable raster under the mandatory root context. Charge before native/raster/text allocation and preserve cancellation/latched failures.

### <code>M:Fluent.Pdf.IPdfPadesProvider.Upgrade(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfPadesProfile,Fluent.Pdf.PdfOperationContext)</code>

Appends the requested profile to exact source bytes. All previous bytes and validation evidence must survive. No implicit network access is authorized.

### <code>M:Fluent.Pdf.IPdfPadesValidator.Validate(System.IO.Stream,Fluent.Pdf.PdfPadesProfile,Fluent.Pdf.PdfOperationContext)</code>

Checks all requested profile, TSA, trust, revocation, freshness and renewal rules on exact staged bytes.

### <code>M:Fluent.Pdf.IPdfPageRenderer.Render(Fluent.Pdf.PdfRasterRequest,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Paints exactly request.ByteLength top-down, tightly packed, straight-alpha BGRA bytes to a borrowed output. Use the mandatory context for all nested work and isolated native execution.

### <code>M:Fluent.Pdf.IPdfPrintProvider.Cancel(System.String,System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Requests cancellation of the exact job; does not promise that physical output can be recalled.

### <code>M:Fluent.Pdf.IPdfPrintProvider.GetStatus(System.String,System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Gets one observation for the exact printer/job.

### <code>M:Fluent.Pdf.IPdfPrintProvider.Submit(System.IO.Stream,System.Int32,Fluent.Pdf.PdfPrintOptions,Fluent.Pdf.PdfOperationContext)</code>

Submits privately serialized PDF bytes exactly once, requiring all requested attributes. Borrowed streams expire on return.

### <code>M:Fluent.Pdf.IPdfProfileProducer.Convert(System.IO.Stream,Fluent.Pdf.PdfProfileOptions,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Creates a candidate from exact borrowed PDF bytes using the shared context. Strict unsupported features and all color/flattening losses must be reported. Any native engine requires separately qualified bounded isolation and explicit offline assets.

### <code>M:Fluent.Pdf.IPdfProfileProducer.Supports(Fluent.Pdf.PdfProfileOptions)</code>

Reports exact profile/print prerequisites before input capture.

### <code>M:Fluent.Pdf.IPdfProfileValidator.Supports(Fluent.Pdf.PdfConformanceProfile)</code>

Reports exact profile validation availability before input capture.

### <code>M:Fluent.Pdf.IPdfProfileValidator.Validate(System.IO.Stream,Fluent.Pdf.PdfProfileOptions,System.String,Fluent.Pdf.PdfOperationContext)</code>

Validates every applicable rule against exact borrowed bytes and requested output condition. Returns exact SHA256/profile/request binding; no implicit downloads/network. Leases expire at return.

### <code>M:Fluent.Pdf.IPdfRangeSource.GetIdentity(Fluent.Pdf.PdfOperationContext)</code>

Checks authorization and returns the current immutable revision under the supplied cancellation, deadline and budget context. Called even for cache hits.

### <code>M:Fluent.Pdf.IPdfRangeSource.OpenRange(System.Int64,System.Int32,Fluent.Pdf.PdfRangeIdentity,Fluent.Pdf.PdfOperationContext)</code>

Returns an exact range conditioned on the supplied revision, or fails. Honor cancellation/authentication before transport and while reading the body. The SDK checks identity, bounds, truncation and extra bytes.

### <code>M:Fluent.Pdf.IPdfRasterDocumentDecoder.DecodeFrame(System.IO.Stream,Fluent.Pdf.PdfRasterFileFormat,System.Int32,Fluent.Pdf.PdfRasterFrameInfo,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Decodes exactly one inspected complete RGBA8 frame, preserving declared color/alpha and polling the same context. The SDK precharges the final color/alpha pixel planes before this call; charge additional working allocations as expanded bytes. Leases expire on return.

### <code>M:Fluent.Pdf.IPdfRasterDocumentDecoder.Inspect(System.IO.Stream,Fluent.Pdf.PdfRasterFileFormat,Fluent.Pdf.PdfOperationContext)</code>

Checks content/format and finite frame metadata under the context before allocating decoded samples.

### <code>M:Fluent.Pdf.IPdfRasterDocumentDecoder.Supports(Fluent.Pdf.PdfRasterFileFormat)</code>

Reports exact installed format support before conversion.

### <code>M:Fluent.Pdf.IPdfRevisionWriter.Supports(Fluent.Pdf.PdfWriteOptions)</code>

Reports whether the specified choices are implemented; unsupported choices must fail before output changes.

### <code>M:Fluent.Pdf.IPdfSignatureProvider.Sign(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Writes one bounded DER detached CMS over the exact concatenated byte ranges. Key/certificate selection belongs to the explicitly configured provider.

### <code>M:Fluent.Pdf.IPdfSignatureProvider.Verify(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Checks detached CMS cryptographic integrity only, without claiming certificate trust, revocation or modification permissions. Unsupported verification profiles throw PdfException with code Signature.UnsupportedProfile.

### <code>M:Fluent.Pdf.IPdfSignatureTrustProvider.Evaluate(System.IO.Stream,Fluent.Pdf.PdfSignatureTrustOptions,Fluent.Pdf.PdfOperationContext)</code>

Evaluates the embedded signer under explicit trust/time/network authority and the shared budget.

### <code>M:Fluent.Pdf.IPdfTemplateHtmlProvider.Render(System.String,Fluent.Pdf.PdfPageSize,Fluent.Pdf.PdfOperationContext)</code>

Renders an explicit HTML fragment under the shared context; no implicit browser is supplied.

### <code>M:Fluent.Pdf.IPdfTextShaper.Shape(Fluent.Pdf.PdfShapingRequest,Fluent.Pdf.PdfOperationContext)</code>

Shapes the exact run; providers charge all allocations before native or managed processing and honor its shared context.

### <code>M:Fluent.Pdf.IPdfVerticalFontProgram.GetVerticalMetrics(System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Gets exact vertical advance and origin under the mandatory context.

### <code>M:Fluent.Pdf.IPdfWordConverter.Export(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfWordFormat,System.IO.Stream,Fluent.Pdf.PdfWordConversionOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationContext)</code>

Converts a PDF snapshot to Word with explicit reconstruction, renderer/font/provider choices and loss/provenance diagnostics.

### <code>M:Fluent.Pdf.IPdfWordConverter.Import(System.IO.Stream,Fluent.Pdf.PdfWordFormat,System.IO.Stream,Fluent.Pdf.PdfWordConversionOptions,Fluent.Pdf.PdfOperationContext)</code>

Converts borrowed Word source to PDF. Do not execute source actions or discover resources/fonts/models. Report all losses through the supplied context.

### <code>M:Fluent.Pdf.IPdfWordConverter.Supports(Fluent.Pdf.PdfWordFormat,System.Boolean)</code>

Reports exact direction/format availability without opening input or loading assets.

### <code>M:Fluent.Pdf.ManagedPdfProvider.#ctor</code>

Creates a stateless provider that can be shared by independent document sessions.

### <code>M:Fluent.Pdf.ManagedPdfProvider.Read(System.IO.Stream,Fluent.Pdf.PdfDocument,Fluent.Pdf.PdfOperationContext)</code>

Eagerly reads an immutable graph and a validated page tree under the supplied context.

### <code>M:Fluent.Pdf.ManagedPdfProvider.Supports(Fluent.Pdf.PdfWriteOptions)</code>

Reports support for the SDK's full/incremental and table/stream choices; source-specific restrictions are checked before output.

### <code>M:Fluent.Pdf.ManagedPdfProvider.Write(Fluent.Pdf.PdfDocumentSnapshot,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Preserves the original graph and page identities while applying page order/geometry changes and the selected revision strategy.

### <code>M:Fluent.Pdf.Pdf.Create(Fluent.Pdf.PdfOptions,Fluent.Pdf.PdfProviderRegistry)</code>

Creates an empty in-memory editing session with immutable defaults and explicit providers.

### <code>M:Fluent.Pdf.Pdf.Open(System.IO.Stream,Fluent.Pdf.PdfOptions,Fluent.Pdf.PdfProviderRegistry,System.Threading.CancellationToken)</code>

Reads eagerly from a borrowed readable stream, leaving it open at the position reached by the provider.

Remarks: On failure the partially populated document is disposed. The managed reader is the default; an explicitly empty registry disables it.

### <code>M:Fluent.Pdf.Pdf.OpenFile(System.String,Fluent.Pdf.PdfOptions,Fluent.Pdf.PdfProviderRegistry,System.Threading.CancellationToken)</code>

Opens a file with an explicit reader; the SDK-owned file handle is closed before return.

### <code>M:Fluent.Pdf.PdfAccessibility.Author(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfStructurePlan,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Authors a complete explicit structure tree over actual existing marked content/annotations; every MCID must have exactly one owner. No PDF/UA identification or conformance claim is fabricated.

### <code>M:Fluent.Pdf.PdfAccessibility.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfAccessibilityProfile,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inspects actual page/Form MCIDs, logical order, annotation/parent associations and incomplete automated blockers. Human review and complete independent validation remain separate requirements.

### <code>M:Fluent.Pdf.PdfAccessibility.Remediate(Fluent.Pdf.PdfAccessibilityInspection,Fluent.Pdf.PdfObjectId,System.String,System.String,System.String,System.String,System.Int32[],Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Changes author-supplied role/language/descriptions and optionally reorders existing children by an exact permutation, preserving identities and all content/parent associations. Null fields retain source values.

### <code>M:Fluent.Pdf.PdfAccessibility.Validate(System.IO.Stream,Fluent.Pdf.PdfAccessibilityProfile,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfAccessibilityHumanReview,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Runs complete independent exact-input validation, optionally attaching separately supplied matching human review. Caller input stays open; provider success never bypasses latched failures.

### <code>M:Fluent.Pdf.PdfAccessibilityFinding.#ctor(System.String,System.String,System.String)</code>

Captures a stable rule identifier, location and neutral message.

### <code>M:Fluent.Pdf.PdfAccessibilityHumanReview.#ctor(Fluent.Pdf.PdfAccessibilityProfile,System.String,System.String,System.String,System.Boolean,System.Boolean)</code>

Records explicit reviewer/report identity, profile/digest, reading-order review and semantic descriptions/table/form review. False values remain observations, never automatic approvals.

### <code>M:Fluent.Pdf.PdfAccessibilityValidation.#ctor(Fluent.Pdf.PdfAccessibilityProfile,System.String,System.Boolean,System.Boolean,Fluent.Pdf.PdfAccessibilityFinding[])</code>

Captures exact profile/digest and complete independent findings. Compliance requires completed validation and zero findings.

### <code>M:Fluent.Pdf.PdfAction.Advanced(Fluent.Pdf.PdfValue)</code>

Captures an advanced detached action dictionary. It remains subject to policy and target validation.

### <code>M:Fluent.Pdf.PdfAction.GoTo(Fluent.Pdf.PdfDestination)</code>

Creates a local page action.

### <code>M:Fluent.Pdf.PdfAction.GoToNamed(System.String)</code>

Creates a local named destination action.

### <code>M:Fluent.Pdf.PdfAction.Launch(System.String,System.Boolean)</code>

Creates a launch-file action, requiring explicit policy permission. No process is started.

### <code>M:Fluent.Pdf.PdfAction.Remote(System.String,System.String,System.Boolean)</code>

Creates a remote-file named destination action, requiring explicit policy permission.

### <code>M:Fluent.Pdf.PdfAction.Then(Fluent.Pdf.PdfAction[])</code>

Returns an action followed by a finite ordered batch. Instances are immutable.

### <code>M:Fluent.Pdf.PdfAction.Uri(System.String)</code>

Creates an absolute URI action; a separate policy controls allowed schemes.

### <code>M:Fluent.Pdf.PdfActionPolicy.#ctor(System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.String[])</code>

Creates a policy. Default permissions allow local navigation and absolute HTTP/HTTPS URI actions only.

### <code>M:Fluent.Pdf.PdfAnnotation.Create(Fluent.Pdf.PdfAnnotationKind,System.String,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfGraphicsResource,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfAnnotationMetadata,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates an annotation with caller-defined appearance and standard subtype properties. Geometry, attachment/media requirements and detached ownership are validated; nothing executes.

### <code>M:Fluent.Pdf.PdfAnnotation.CreateFreeText(System.String,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfShapedText,System.Double,Fluent.Pdf.PdfAnnotationMetadata,System.Double,System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a real embedded shaped-text appearance, using conservative font bounds to reject overflow. Geometry and font size use native page units; later arbitrary text regeneration is a separate editing profile.

### <code>M:Fluent.Pdf.PdfAnnotation.CreateMarkup(Fluent.Pdf.PdfAnnotationKind,System.String,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfColor,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfAnnotationMetadata,System.Double,System.Double,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates original vector appearances for text icons, shapes, ink, carets and text markup. QuadPoints use upper-left, upper-right, lower-left, lower-right order; complex text, stamps and media use explicit appearances.

### <code>M:Fluent.Pdf.PdfAnnotationMetadata.#ctor(System.String,System.String,System.String,System.Nullable{System.DateTimeOffset},System.Nullable{System.DateTimeOffset},System.String,System.String,System.String,System.Boolean)</code>

Captures bounded Unicode fields, dates and an optional reply target identifier in the destination document.

### <code>M:Fluent.Pdf.PdfAnnotationSet.AddPopup(System.Int32,Fluent.Pdf.PdfRect,System.Boolean,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Adds one reciprocal same-page popup for an existing annotation. Popup windows have no appearance stream.

### <code>M:Fluent.Pdf.PdfAnnotationSet.Delete(System.Collections.Generic.IEnumerable{System.Int32},System.Boolean,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Deletes the selected entries and, by default, their reply/popup descendants. Unreferenced source objects may remain; this is not secure erasure.

### <code>M:Fluent.Pdf.PdfAnnotationSet.Flatten(System.Collections.Generic.IEnumerable{System.Int32},Fluent.Pdf.PdfAnnotationView,System.Boolean,System.Boolean,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Paints selected normal appearances into native page content and removes their interactivity. Rich-media poster conversion requires explicit permissive loss policy.

### <code>M:Fluent.Pdf.PdfAnnotationSet.ImportTo(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,System.Collections.Generic.IEnumerable{System.Int32},System.Boolean,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Imports a captured annotation selection to one target page, preserving referenced resources, reply/popups and encoded payloads. Other source pages require explicit destination mapping.

### <code>M:Fluent.Pdf.PdfAnnotationSet.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inspects original annotation dictionaries, metadata and relationships without evaluating actions or media.

### <code>M:Fluent.Pdf.PdfAnnotationSet.ReadXfdf(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads the explicit owned-graph XFDF profile under a 16 MiB XML ceiling. DTDs, external entities, incomplete graphs and implicit appearance regeneration are rejected.

### <code>M:Fluent.Pdf.PdfAnnotationSet.SetReply(System.Int32,System.Nullable{System.Int32},System.Boolean,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets or removes one reply relationship by captured annotation identity, retaining state and popup metadata.

### <code>M:Fluent.Pdf.PdfAnnotationSet.Update(System.Int32,Fluent.Pdf.PdfValue,System.Nullable{Fluent.Pdf.PdfRect},Fluent.Pdf.PdfGraphicsResource,System.String[],Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Updates explicit properties/geometry/normal appearance, retaining all unspecified source keys, replies, popup ownership and indirect identity.

### <code>M:Fluent.Pdf.PdfAnnotationSet.WriteXfdf(System.IO.Stream,System.Collections.Generic.IEnumerable{System.Int32},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Exports common XFDF annotation metadata with a mandatory owned graph extension retaining exact appearances, references and payloads. This explicit profile is not a general XFDF 3.0 converter.

### <code>M:Fluent.Pdf.PdfAnnotations.Add(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfAnnotation},Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Adds an explicit finite batch to one page; identifiers and forward reply targets resolve atomically within the captured document.

### <code>M:Fluent.Pdf.PdfArchival.Convert(Fluent.Pdf.PdfDocumentSnapshot,System.IO.Stream,Fluent.Pdf.PdfArchivalOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes only an independently validated full rewrite. Unsupported repairs fail; metadata replacement is explicit and strict mode rejects its reported loss.

### <code>M:Fluent.Pdf.PdfArchival.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfArchivalProfile,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inspects structural/profile blockers. Absence of findings never establishes PDF/A conformance.

### <code>M:Fluent.Pdf.PdfArchival.ProfileIdentifier(Fluent.Pdf.PdfArchivalProfile)</code>

Returns the stable independent-validator profile token.

### <code>M:Fluent.Pdf.PdfArchival.Validate(System.IO.Stream,Fluent.Pdf.PdfArchivalProfile,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Validates exact source bytes through an explicit independent provider. A normal PDF lacking archival identification is not silently converted.

### <code>M:Fluent.Pdf.PdfArchivalFinding.#ctor(System.String,System.String,System.String)</code>

Captures bounded rule, location and description text.

### <code>M:Fluent.Pdf.PdfArchivalOptions.#ctor(Fluent.Pdf.PdfArchivalProfile,Fluent.Pdf.PdfColorSpace,System.String,System.Boolean,System.Boolean)</code>

Captures the requested profile and optional complete ICC RGB/CMYK output space. Metadata replacement is an explicitly reported loss; signatures require explicit invalidation permission.

### <code>M:Fluent.Pdf.PdfArchivalValidation.#ctor(Fluent.Pdf.PdfArchivalProfile,System.String,System.Boolean,System.Boolean,Fluent.Pdf.PdfArchivalFinding[])</code>

Captures a validator result. Compliance requires a complete result with zero violations; sourceSha256 is the lowercase exact-input digest.

### <code>M:Fluent.Pdf.PdfAttachmentOptions.#ctor(System.String,System.String,System.String,System.Nullable{System.DateTimeOffset},System.Nullable{System.DateTimeOffset},System.String)</code>

Captures a portable leaf filename, MIME type, explicit dates and optional standard/custom associated-file relationship.

### <code>M:Fluent.Pdf.PdfAttachments.Add(Fluent.Pdf.PdfDocumentSnapshot,System.IO.Stream,Fluent.Pdf.PdfAttachmentOptions,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Embeds a borrowed stream under a unique Unicode name, with size/MD5 checksum and explicit metadata. Associated files use PDF 2.0.

### <code>M:Fluent.Pdf.PdfAttachments.Extract(Fluent.Pdf.PdfAttachment,System.IO.Stream,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Decodes and validates one embedded variant before copying to a borrowed output stream. Existing output bytes are preserved on validation failure.

### <code>M:Fluent.Pdf.PdfAttachments.ExtractToDirectory(Fluent.Pdf.PdfAttachment,System.String,System.String,System.Boolean,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Extracts to the supplied existing directory using a portable leaf filename. Default no-overwrite commit is atomic and race-safe.

### <code>M:Fluent.Pdf.PdfAttachments.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Enumerates active file specifications, embedded streams and portable-collection metadata without decoding payloads.

### <code>M:Fluent.Pdf.PdfAttachments.RemoveAll(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Removes every embedded file, associated-file edge and collection entry with a full rewrite, dropping orphan payloads and historical revisions.

### <code>M:Fluent.Pdf.PdfAttachments.SetCollection(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets or removes an explicitly authored portable-collection dictionary. Validates standard schema/sort/item types; unknown source collections remain inspectable.

### <code>M:Fluent.Pdf.PdfAttachments.SetCollectionItem(Fluent.Pdf.PdfAttachment,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets or removes a collection item on an indirect file specification, validating it against the current collection schema.

### <code>M:Fluent.Pdf.PdfBarcodeObservation.#ctor(Fluent.Pdf.PdfBarcodeSymbology,System.String,Fluent.Pdf.PdfBarcodeErrorCorrection,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfPoint})</code>

Captures decoded text and detector landmarks. QR points are finder/alignment centers; Code128 points are scan-line endpoints. They are not symbol outline corners.

### <code>M:Fluent.Pdf.PdfBarcodeOptions.#ctor(Fluent.Pdf.PdfBarcodeSymbology,Fluent.Pdf.PdfBarcodeErrorCorrection,System.Int32,System.Int32,System.Int32)</code>

Selects QR/ECC or Code128B. Zero quiet zone selects4 QR or10 Code128 modules; explicit margins must meet those minima and be at most64. QR version zero chooses the smallest fitting version. Code128 supports1–256 printable ASCII characters.

### <code>M:Fluent.Pdf.PdfBarcodeSymbol.OpenVerificationPixels</code>

Opens an independent read-only detached view of the exact canonical Gray8 verification pixels, with stride equal to raster width.

### <code>M:Fluent.Pdf.PdfBarcodes.Detect(Fluent.Pdf.PdfOcrRaster,Fluent.Pdf.IPdfBarcodeDecoder,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Independently detects at most one supported symbol in an explicit captured raster. Points use its top-left pixel frame; decoded Unicode is never executed or treated as original byte encoding.

### <code>M:Fluent.Pdf.PdfBarcodes.DetectPage(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.IPdfBarcodeDecoder,Fluent.Pdf.PdfRenderOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Renders an explicitly selected page/region and detects one symbol under one shared context. Returned detector landmarks map to native PDF user coordinates, including crop, rotation and UserUnit. Use separate explicit regions for multiple symbols.

### <code>M:Fluent.Pdf.PdfBarcodes.Generate(System.String,Fluent.Pdf.IPdfBarcodeDecoder,Fluent.Pdf.PdfBarcodeOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Generates exact UTF8 QR or printable Code128B, independently decodes its canonical pixels, then creates a detached vector drawing. No symbol returns without exact payload/symbology/ECC agreement.

### <code>M:Fluent.Pdf.PdfBarcodes.GeneratePayment(Fluent.Pdf.PdfSepaCreditTransfer,Fluent.Pdf.IPdfBarcodeDecoder,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Generates independently decoded EPC002 UTF8 SCT at mandatory levelM, with at most version13. Payment data is validated before generation and never submitted or executed.

### <code>M:Fluent.Pdf.PdfBookmark.#ctor(System.String,Fluent.Pdf.PdfDestination,Fluent.Pdf.PdfAction,System.Boolean,System.Boolean,System.Boolean,Fluent.Pdf.PdfColor,Fluent.Pdf.PdfBookmark[])</code>

Creates a bounded bookmark. Destinations and actions are mutually exclusive; children retain their order.

### <code>M:Fluent.Pdf.PdfBookmark.WithChildren(Fluent.Pdf.PdfBookmark[])</code>

Returns a bookmark with replaced ordered children, retaining destination, action, expansion and styling.

### <code>M:Fluent.Pdf.PdfCanvas.Clip(Fluent.Pdf.PdfPath,System.Boolean)</code>

Intersects subsequent paint layers with an implicitly closed path captured at the current transform.

### <code>M:Fluent.Pdf.PdfCanvas.Create(Fluent.Pdf.PdfPageGeometry,System.Action{Fluent.Pdf.PdfCanvas},Fluent.Pdf.PdfCanvasOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a complete isolated canvas under one shared operation context; no page mutation occurs inside the callback.

### <code>M:Fluent.Pdf.PdfCanvas.DrawDrawing(Fluent.Pdf.PdfDrawing,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfCanvasFit,Fluent.Pdf.PdfCanvasAnchor,System.Int32)</code>

Fits a reusable vector drawing into an explicit canvas destination, retaining its upright appearance.

### <code>M:Fluent.Pdf.PdfCanvas.DrawForm(Fluent.Pdf.PdfGraphicsResource,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfCanvasFit,Fluent.Pdf.PdfCanvasAnchor,System.Int32)</code>

Fits an explicit Form XObject into a canvas destination, including its own form matrix.

### <code>M:Fluent.Pdf.PdfCanvas.DrawImage(Fluent.Pdf.PdfGraphicsResource,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfCanvasFit,Fluent.Pdf.PdfCanvasAnchor,System.Int32)</code>

Fits an explicit image XObject using its declared pixel aspect. Image decoding/encoding belongs to the image service.

### <code>M:Fluent.Pdf.PdfCanvas.DrawImageAsset(Fluent.Pdf.PdfImageAsset,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfCanvasFit,Fluent.Pdf.PdfCanvasAnchor,System.Int32)</code>

Fits an immutable image using its physical pixel aspect and Exif orientation without rewriting encoded samples.

### <code>M:Fluent.Pdf.PdfCanvas.DrawPath(Fluent.Pdf.PdfPath,Fluent.Pdf.PdfCanvasStyle,System.Int32)</code>

Paints an exact vector path using explicit fill/stroke data and records its conservative frame.

### <code>M:Fluent.Pdf.PdfCanvas.DrawText(Fluent.Pdf.PdfShapedText,Fluent.Pdf.PdfPoint,System.Double,System.Double,System.Boolean,System.Int32)</code>

Places immutable shaped text at a baseline in canvas units. Font size remains in typographic points.

### <code>M:Fluent.Pdf.PdfCanvas.Rotate(System.Double)</code>

Rotates local axes; positive angles turn toward positive local y from positive local x.

### <code>M:Fluent.Pdf.PdfCanvas.Save</code>

Captures transforms and clips until its scope closes in reverse order.

### <code>M:Fluent.Pdf.PdfCanvas.Scale(System.Double,System.Double)</code>

Scales current local axes; singular transforms remain representable and have no invertible hit frame.

### <code>M:Fluent.Pdf.PdfCanvas.Transform(Fluent.Pdf.PdfMatrix)</code>

Concatenates a local transform on the right of the current transform.

### <code>M:Fluent.Pdf.PdfCanvas.Translate(System.Double,System.Double)</code>

Translates in current canvas units.

### <code>M:Fluent.Pdf.PdfCanvasOptions.#ctor(Fluent.Pdf.PdfLengthUnit,Fluent.Pdf.PdfCanvasOrigin,Fluent.Pdf.PdfPageBox,Fluent.Pdf.PdfMargins,System.Boolean,System.Double)</code>

Captures units, viewing-box/origin selection, margins and the default content-frame clip.

### <code>M:Fluent.Pdf.PdfCanvasPlacement.HitTest(Fluent.Pdf.PdfPoint,System.Double,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Tests a point in base canvas coordinates against the actual convex frame and captured path clips, with absolute clip-local boundary tolerance.

### <code>M:Fluent.Pdf.PdfCanvasResult.ApplyTo(Fluent.Pdf.PdfPage,Fluent.Pdf.PdfCanvasApplyMode,System.Threading.CancellationToken)</code>

Atomically applies to a page with identical geometry. Original streams remain encoded and are isolated from new content.

### <code>M:Fluent.Pdf.PdfCanvasResult.HitTest(Fluent.Pdf.PdfPoint,System.Boolean,System.Double,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Returns hit frames from front to back under one bounded operation.

### <code>M:Fluent.Pdf.PdfCanvasStyle.#ctor(Fluent.Pdf.PdfPaint,Fluent.Pdf.PdfPaint,System.Double,Fluent.Pdf.PdfLineCap,Fluent.Pdf.PdfLineJoin,System.Double,System.Double[],System.Double,System.Boolean,Fluent.Pdf.PdfGraphicsState)</code>

Captures at least one paint, finite line geometry and an optional exact compositing state.

### <code>M:Fluent.Pdf.PdfCertificateCredential.#ctor(System.Security.Cryptography.X509Certificates.X509Certificate2,System.Security.Cryptography.RSA)</code>

Captures public bytes and borrows the explicitly supplied RSA handle without taking ownership.

### <code>M:Fluent.Pdf.PdfCertificateCredential.OpenCertificate</code>

Opens detached read-only public DER certificate bytes.

### <code>M:Fluent.Pdf.PdfCertificateProtection.#ctor(Fluent.Pdf.PdfCertificateRecipient[],Fluent.Pdf.PdfPublicKeyCipher,System.Boolean)</code>

Captures one through 64 public recipient snapshots. AES-256 is the default; repeated certificate identities are rejected during authoring.

### <code>M:Fluent.Pdf.PdfCertificateRecipient.#ctor(System.Security.Cryptography.X509Certificates.X509Certificate2,Fluent.Pdf.PdfDocumentPermissions,System.Boolean,Fluent.Pdf.PdfCmsKeyTransport)</code>

Captures only public certificate bytes; the caller retains certificate ownership. Certificate trust and validity policy remain explicit caller decisions.

### <code>M:Fluent.Pdf.PdfCertificateRecipient.OpenCertificate</code>

Opens detached read-only public DER certificate bytes.

### <code>M:Fluent.Pdf.PdfColor.#ctor(Fluent.Pdf.PdfColorSpace,System.Double[])</code>

Captures matching bounded channels. Pattern names are supplied separately when painting a pattern.

### <code>M:Fluent.Pdf.PdfColorRenderOptions.#ctor(Fluent.Pdf.PdfColorSpace,Fluent.Pdf.PdfColorRenderingIntent,System.Boolean,Fluent.Pdf.PdfSpotColorPolicy)</code>

Captures a complete RGB ICC output profile and explicit intent, overprint and spot policy. Embedded source profiles/output intents must be honored by the selected provider.

### <code>M:Fluent.Pdf.PdfColorRendering.RenderPage(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfColorRenderOptions,Fluent.Pdf.PdfRenderOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Renders through the explicitly registered color-managed service. RGB ICC output is required; process separations and CMYK envelopes require separate profiles.

### <code>M:Fluent.Pdf.PdfColorSpace.AsResource(Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures this exact color space as a named graphics resource with a bounded validation report.

### <code>M:Fluent.Pdf.PdfColorSpace.CalGray(System.Double,System.Double,System.Double,System.Double,System.Double,System.Double)</code>

Creates calibrated Gray with explicit white point, gamma and black point.

### <code>M:Fluent.Pdf.PdfColorSpace.CalRgb(System.Double,System.Double,System.Double[],System.Double[])</code>

Creates calibrated RGB with explicit white point, three positive gammas and a 3x3 XYZ matrix.

### <code>M:Fluent.Pdf.PdfColorSpace.DeviceN(System.String[],Fluent.Pdf.PdfColorSpace,Fluent.Pdf.PdfFunction)</code>

Creates DeviceN with exact distinct colorant names and an explicit multi-input tint transform.

### <code>M:Fluent.Pdf.PdfColorSpace.Icc(System.IO.Stream,Fluent.Pdf.PdfColorSpace,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads a bounded complete ICC v2/v4 Gray/RGB/CMYK profile and preserves it exactly; unsupported profiles fail without substitution.

### <code>M:Fluent.Pdf.PdfColorSpace.Indexed(Fluent.Pdf.PdfColorSpace,System.Int32,System.Byte[],Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates an exact eight-bit palette over an explicit non-indexed, non-pattern base space.

### <code>M:Fluent.Pdf.PdfColorSpace.Lab(System.Double,System.Double,System.Double,System.Double,System.Double,System.Double,System.Double,System.Double,System.Double)</code>

Creates CIE Lab with an explicit normalized white point, component ranges and optional black point.

### <code>M:Fluent.Pdf.PdfColorSpace.Pattern(Fluent.Pdf.PdfColorSpace)</code>

Creates colored Pattern, or uncolored Pattern with an explicit non-pattern base space.

### <code>M:Fluent.Pdf.PdfColorSpace.Separation(System.String,Fluent.Pdf.PdfColorSpace,Fluent.Pdf.PdfFunction)</code>

Creates a Separation color with an explicit one-input tint transform into the alternate space.

### <code>M:Fluent.Pdf.PdfComparison.Compare(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfComparisonOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Compares both inputs under one shared context. Optional providers are explicit; no renderer, font or action is discovered or executed implicitly.

### <code>M:Fluent.Pdf.PdfComparisonOptions.#ctor(System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Double,System.Byte,System.String[],Fluent.Pdf.PdfRenderOptions)</code>

Captures dimensions, finite displayed-point tolerance, pixel-channel tolerance and exact volatile Info keys. XMP is compared by exact decoded bytes.

### <code>M:Fluent.Pdf.PdfComparisonResult.CreateAnnotatedDocument(Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a separate annotated copy of the revised snapshot with explicit comment markers for each difference. Deleted-page/document differences attach to page zero; full details remain in the structured report. Tagged destinations require a separate association profile.

### <code>M:Fluent.Pdf.PdfComparisonResult.WriteReport(System.IO.Stream,System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes version-one JSON or escaped static HTML to a borrowed output under fresh export limits.

### <code>M:Fluent.Pdf.PdfCompatibility.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inspects declared versions and preserves raw extension/namespace values without following URLs, running actions or decrypting an encrypted input.

### <code>M:Fluent.Pdf.PdfContentOperation.#ctor(Fluent.Pdf.PdfOperator,Fluent.Pdf.PdfValue[])</code>

Creates a standard operator after validating its operand grammar.

### <code>M:Fluent.Pdf.PdfContentOperation.Extension(System.String,System.String,Fluent.Pdf.PdfValue[])</code>

Creates an explicitly preserved extension token with a caller-declared version floor.

### <code>M:Fluent.Pdf.PdfContentOperation.InlineImage(Fluent.Pdf.PdfValue,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Snapshots an inline image and validates its exact encoded boundary under a fresh operation budget.

### <code>M:Fluent.Pdf.PdfContentOperation.OpenInlineImageData</code>

Opens an independent read-only view of this inline image's encoded bytes.

### <code>M:Fluent.Pdf.PdfContentProgram.Create(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfContentOperation},Fluent.Pdf.PdfContentKind,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Copies and validates authored operators under a fresh operation budget.

### <code>M:Fluent.Pdf.PdfContentProgram.Edit(System.Int32,System.Int32,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfContentOperation},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Returns a separately validated program with an operator replaced, inserted or removed.

### <code>M:Fluent.Pdf.PdfContentProgram.Parse(System.IO.Stream,Fluent.Pdf.PdfContentKind,Fluent.Pdf.PdfUnknownOperatorPolicy,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Parses borrowed decoded bytes. Preserving unknown operators requires an explicit version floor.

### <code>M:Fluent.Pdf.PdfContentProgram.Write(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes canonical decoded content to a borrowed stream after validating the complete program.

### <code>M:Fluent.Pdf.PdfDataGeneration.Generate``1(System.Collections.Generic.IEnumerable{``0},Fluent.Pdf.PdfGenerationTemplate{``0},Fluent.Pdf.PdfGenerationOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Generates a caller-owned document. Hooks and immutable fragments complete before return; a later public Save is a separate operation.

### <code>M:Fluent.Pdf.PdfDataGeneration.WriteFile``1(System.Collections.Generic.IEnumerable{``0},Fluent.Pdf.PdfGenerationTemplate{``0},Fluent.Pdf.PdfGenerationOptions,Fluent.Pdf.PdfProviderRegistry,System.String,Fluent.Pdf.PdfWriteOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Generates, runs every hook and serializes privately under one context before atomic destination replacement. Hook/provider/cancellation failures preserve an existing destination.

### <code>M:Fluent.Pdf.PdfDataGeneration.Write``1(System.Collections.Generic.IEnumerable{``0},Fluent.Pdf.PdfGenerationTemplate{``0},Fluent.Pdf.PdfGenerationOptions,Fluent.Pdf.PdfProviderRegistry,System.IO.Stream,Fluent.Pdf.PdfWriteOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Generates and writes under one context at the borrowed output's current position. Hooks can fail before return; stream output can contain a prefix after failure.

### <code>M:Fluent.Pdf.PdfDecodedData.OpenRead</code>

Opens an independent read-only view of the decoded snapshot.

### <code>M:Fluent.Pdf.PdfDestination.#ctor(System.Int32,Fluent.Pdf.PdfDestinationView,System.Nullable{System.Double}[])</code>

Creates a standard destination. XYZ takes three values, FitR four nonnull values, H/V modes one, and Fit/FitB none.

### <code>M:Fluent.Pdf.PdfDiagnostic.#ctor(System.String,System.String,System.Nullable{Fluent.Pdf.PdfCapability},System.String,Fluent.Pdf.PdfDiagnosticSeverity,Fluent.Pdf.PdfLossCategory,System.String,System.Nullable{System.Int32},System.Nullable{System.Int64},System.String)</code>

Creates a diagnostic; code/message/provider identifiers are bounded configuration text.

### <code>M:Fluent.Pdf.PdfDocument.AddPage</code>

Adds a default-sized page and returns this document for fluent composition.

### <code>M:Fluent.Pdf.PdfDocument.AddPage(Fluent.Pdf.PdfPageGeometry)</code>

Adds a page with explicit boxes, UserUnit and viewing rotation.

### <code>M:Fluent.Pdf.PdfDocument.AddPage(Fluent.Pdf.PdfPageSize)</code>

Adds a page and returns this document for fluent composition.

### <code>M:Fluent.Pdf.PdfDocument.CreateSnapshot</code>

Captures immutable page geometry independent of subsequent edits and disposal.

### <code>M:Fluent.Pdf.PdfDocument.Dispose</code>

Disposes this session and invalidates live page/collection references. Repeated disposal is harmless.

Remarks: Disposal during a provider callback is rejected. Caller streams and provider instances are never disposed.

### <code>M:Fluent.Pdf.PdfDocument.Save(System.IO.Stream,System.Threading.CancellationToken,Fluent.Pdf.PdfWriteOptions)</code>

Writes through the registered writer at the current stream position, leaving the caller's stream open.

Remarks: Stream output can be partial on provider failure/cancellation. The SDK does not rewind or truncate the caller's stream.

### <code>M:Fluent.Pdf.PdfDocument.SaveFile(System.String,System.Threading.CancellationToken,Fluent.Pdf.PdfWriteOptions)</code>

Writes to a private file in the destination directory, then atomically moves/replaces the destination on success.

Remarks: The destination directory must exist. An unsupported atomic replace fails without a delete-and-copy fallback.

### <code>M:Fluent.Pdf.PdfDrawing.Create(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfContentOperation},Fluent.Pdf.PdfResourceSet,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Validates and captures a drawing, adding q/Q around the complete program and rejecting missing named resources.

### <code>M:Fluent.Pdf.PdfEngineering.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inspects every active physical object/direct value, including unattached payloads. Reads encoded bytes for hashes without decoding/executing them; snapshot and graph remain unchanged.

### <code>M:Fluent.Pdf.PdfEngineering.Preserve(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures and validates a nonempty PDF then copies the exact original bytes, retaining all payloads, associations/history and signature bytes. This does not assert signature validity or PDF/E conformance.

### <code>M:Fluent.Pdf.PdfException.#ctor(Fluent.Pdf.PdfDiagnostic,System.Exception)</code>

Creates a failure retaining its diagnostic and optional underlying exception.

### <code>M:Fluent.Pdf.PdfExtensionRequirement.#ctor(System.String,System.String,System.Int32)</code>

Creates a vendor prefix, base PDF version and positive extension level.

### <code>M:Fluent.Pdf.PdfExtensionRequirement.#ctor(System.String,System.String,System.Int32,System.String,System.String)</code>

Creates a PDF 2.0 developer declaration with an explicit passive documentation URL and optional revision text. No URL is fetched.

### <code>M:Fluent.Pdf.PdfExtractedResource.ReadFontProgram(Fluent.Pdf.PdfProviderRegistry,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inspects an explicitly selected embedded font file through an explicit provider, exposing actual format/embedding restrictions without host font discovery. Glyph procedures are not standalone font programs.

### <code>M:Fluent.Pdf.PdfExtractedResource.WriteFile(System.String,System.Boolean,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Atomically replaces a caller-selected file only after exact stream capture/decoding succeeds.

### <code>M:Fluent.Pdf.PdfExtractedResource.WriteStream(System.IO.Stream,System.Boolean,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Copies exact encoded PDF stream bytes, or explicitly decoded bytes. Decoding completes privately before caller output; the borrowed output remains open.

### <code>M:Fluent.Pdf.PdfFilter.#ctor(System.String,Fluent.Pdf.PdfValue)</code>

Creates a named filter; unknown names remain explicit and fail when requested without a provider.

### <code>M:Fluent.Pdf.PdfFilters.Decode(Fluent.Pdf.PdfValue,Fluent.Pdf.PdfOperationContext,Fluent.Pdf.PdfProviderRegistry)</code>

Eagerly decodes encoded stream bytes with the mandatory operation context, returning an owned read-only view.

### <code>M:Fluent.Pdf.PdfFilters.Encode(System.IO.Stream,System.Collections.Generic.IReadOnlyList{Fluent.Pdf.PdfFilter},Fluent.Pdf.PdfOperationContext,Fluent.Pdf.PdfImageInfo,Fluent.Pdf.PdfProviderRegistry)</code>

Encodes borrowed input by applying the declared decoding chain in reverse. The returned bytes require no further work.

### <code>M:Fluent.Pdf.PdfFilters.Inspect(Fluent.Pdf.PdfValue)</code>

Reads direct Filter/DecodeParms declarations in decoding order; no content is decoded.

### <code>M:Fluent.Pdf.PdfFlow.Layout(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowBlock},Fluent.Pdf.PdfFlowOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Lays out immutable blocks using explicitly supplied offline shaping, shared budgets and cooperative cancellation.

### <code>M:Fluent.Pdf.PdfFlowColumns.#ctor(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowBlock},System.Int32,System.Double,System.Nullable{System.Double},Fluent.Pdf.PdfFlowColumnOrder)</code>

Captures bounded logical content and a finite fixed-height column region.

### <code>M:Fluent.Pdf.PdfFlowComponent.#ctor(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowBlock},System.Boolean)</code>

Captures reusable immutable content and explicit keep behavior.

### <code>M:Fluent.Pdf.PdfFlowContents.#ctor(Fluent.Pdf.PdfFlowTextStyle,System.Int32,System.Int32,System.Double,System.Double)</code>

Creates a contents placeholder with an explicit heading range and point widths.

### <code>M:Fluent.Pdf.PdfFlowFloat.#ctor(Fluent.Pdf.PdfDrawing,Fluent.Pdf.PdfRect,System.Double,System.Double,Fluent.Pdf.PdfFlowFloatSide,System.Double,Fluent.Pdf.PdfFlowClear)</code>

Captures a finite vector float and physical exclusion rectangle.

### <code>M:Fluent.Pdf.PdfFlowHeading.#ctor(System.String,System.String,Fluent.Pdf.PdfFlowTextStyle,System.Int32,Fluent.Pdf.PdfFlowParagraphStyle)</code>

Creates an explicit heading with a stable unique destination ID.

### <code>M:Fluent.Pdf.PdfFlowIndex.#ctor(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowIndexTerm},Fluent.Pdf.PdfFlowTextStyle,System.Globalization.CultureInfo,System.Double)</code>

Creates an index using explicit culture-sensitive term sorting.

### <code>M:Fluent.Pdf.PdfFlowIndexTerm.#ctor(System.String,System.String[])</code>

Creates an indexed term with bounded explicit destination IDs.

### <code>M:Fluent.Pdf.PdfFlowList.Create(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowListItem},Fluent.Pdf.PdfFlowTextStyle,Fluent.Pdf.PdfFlowListKind,System.Int32,System.String,System.String,System.String,System.Nullable{System.Double},System.Double,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures a finite item source and fixes invariant markers/restarts under one operation budget.

### <code>M:Fluent.Pdf.PdfFlowListItem.#ctor(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowBlock},System.Nullable{System.Int32})</code>

Captures bounded item content and an optional counter restart.

### <code>M:Fluent.Pdf.PdfFlowNote.#ctor(System.String,System.String,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowBlock},Fluent.Pdf.PdfFlowTextStyle)</code>

Captures a bounded note ID/label, immutable body and explicit marker style.

### <code>M:Fluent.Pdf.PdfFlowNoteReference.#ctor(Fluent.Pdf.PdfFlowParagraph,Fluent.Pdf.PdfFlowNote,System.Boolean)</code>

Captures a paragraph reference and explicit footnote/endnote placement.

### <code>M:Fluent.Pdf.PdfFlowOptions.#ctor(Fluent.Pdf.PdfPageGeometry,System.Nullable{Fluent.Pdf.PdfMargins},Fluent.Pdf.PdfFlowOverflow,System.Int32,System.Double,System.Double[])</code>

Captures a positive displayed page frame, overflow policy and explicit tab geometry.

### <code>M:Fluent.Pdf.PdfFlowPageBreak.#ctor</code>

Creates an intentional page boundary.

### <code>M:Fluent.Pdf.PdfFlowParagraph.#ctor(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowRun})</code>

Captures a nonempty style-bearing run collection or one styled text paragraph.

### <code>M:Fluent.Pdf.PdfFlowParagraph.#ctor(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowRun},Fluent.Pdf.PdfFlowParagraphStyle)</code>

Captures a nonempty style-bearing run collection or one styled text paragraph.

### <code>M:Fluent.Pdf.PdfFlowParagraph.#ctor(System.String,Fluent.Pdf.PdfFlowTextStyle,Fluent.Pdf.PdfFlowParagraphStyle)</code>

Captures a nonempty style-bearing run collection or one styled text paragraph.

### <code>M:Fluent.Pdf.PdfFlowParagraphStyle.#ctor(Fluent.Pdf.PdfFlowAlignment,System.Double,System.Double,System.Double,System.Double,System.Double,System.Double,System.Int32,System.Int32,System.Boolean,System.Boolean,System.Boolean,Fluent.Pdf.PdfFlowClear)</code>

Captures finite paragraph geometry and explicit pagination constraints.

### <code>M:Fluent.Pdf.PdfFlowResult.CreateDocument(Fluent.Pdf.PdfOptions,System.Threading.CancellationToken)</code>

Creates a caller-owned document from already laid-out pages. Save is a separate bounded operation.

### <code>M:Fluent.Pdf.PdfFlowRun.#ctor(System.String,Fluent.Pdf.PdfFlowTextStyle)</code>

Captures logical UTF-16 text and its immutable style.

### <code>M:Fluent.Pdf.PdfFlowSectionBreak.#ctor(Fluent.Pdf.PdfFlowSectionStart)</code>

Captures an explicit continuous/new/odd/even section boundary.

### <code>M:Fluent.Pdf.PdfFlowTextStyle.#ctor(Fluent.Pdf.PdfFontFallback,System.Double,Fluent.Pdf.PdfTextOptions,Fluent.Pdf.PdfColor,System.Double)</code>

Captures explicit fonts, physical size, horizontal shaping options and color.

### <code>M:Fluent.Pdf.PdfFont.CopyShapingSource(System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Copies original shaping bytes under the caller's shared operation context and returns the collection face index.

### <code>M:Fluent.Pdf.PdfFont.GetGlyph(System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Queries one scalar. Glyph zero explicitly reports an unavailable mapping without substituting another font.

### <code>M:Fluent.Pdf.PdfFont.Read(System.IO.Stream,Fluent.Pdf.PdfProviderRegistry,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads an explicitly selected face from borrowed font bytes; no machine font discovery occurs.

### <code>M:Fluent.Pdf.PdfFont.Standard(System.String,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Selects an exact base-14 PostScript font name through an explicit provider.

### <code>M:Fluent.Pdf.PdfFont.Type3(System.String,Fluent.Pdf.PdfFontMetrics,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfType3Glyph},Fluent.Pdf.PdfValue,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a bounded immutable Type 3 face from caller-authored glyph procedures and explicit resources.

### <code>M:Fluent.Pdf.PdfFont.WriteProgram(System.IO.Stream,System.Collections.Generic.IEnumerable{System.Int32},System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes a full or dependency-complete subset font to borrowed output under the declared embedding restrictions.

### <code>M:Fluent.Pdf.PdfFontFallback.#ctor(Fluent.Pdf.PdfFont[])</code>

Captures up to 256 explicitly loaded fonts in deterministic preference order.

### <code>M:Fluent.Pdf.PdfFontFallback.Resolve(System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Resolves one scalar to the first actual glyph and fails explicitly when every font lacks it.

### <code>M:Fluent.Pdf.PdfFontInfo.#ctor(System.String,Fluent.Pdf.PdfFontFormat,System.Int32,Fluent.Pdf.PdfFontMetrics,System.String,Fluent.Pdf.PdfFontEmbeddingRights,System.Boolean)</code>

Captures the PostScript name, program identity, geometry and license flags.

### <code>M:Fluent.Pdf.PdfFontMetrics.#ctor(System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Boolean,System.Double)</code>

Creates finite design-space metrics with positive units per em and an ordered bounding box.

### <code>M:Fluent.Pdf.PdfFontProgramWriteResult.#ctor(System.String,System.String,System.Int64,System.Int64,System.Int64,System.Boolean)</code>

Captures the emitted PostScript name, PDF font-file subtype and Type 1 section extents.

### <code>M:Fluent.Pdf.PdfFontResource.Create(Fluent.Pdf.PdfFont,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfGlyphMapping},System.Boolean,System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Embeds exact glyph/Unicode mappings eagerly, retaining duplicate logical mappings as distinct encoded codes where needed.

### <code>M:Fluent.Pdf.PdfFontResource.CreateText(Fluent.Pdf.PdfFont,System.String,System.Boolean,System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Maps unshaped Unicode scalars through one exact font. Complex shaping belongs to the text-shaping service.

### <code>M:Fluent.Pdf.PdfFontVerticalMetrics.#ctor(System.Double,System.Double,System.Double)</code>

Captures a positive vertical advance and finite vertical-origin coordinates.

### <code>M:Fluent.Pdf.PdfFormChoice.#ctor(System.String,System.String)</code>

Captures bounded Unicode export and display text; an omitted display uses the export value.

### <code>M:Fluent.Pdf.PdfFormData.Create(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFormDataField},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures distinct, case-sensitive field names and validates supported value shapes.

### <code>M:Fluent.Pdf.PdfFormData.Read(System.IO.Stream,Fluent.Pdf.PdfFormDataFormat,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads the selected format from the current position without closing the input. All interchange profiles have a sixteen MiB extent ceiling.

### <code>M:Fluent.Pdf.PdfFormData.Write(System.IO.Stream,Fluent.Pdf.PdfFormDataFormat,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes field values only, leaving output open. FDF retains PDF value kinds; XFDF carries strings and target field types restore button names.

### <code>M:Fluent.Pdf.PdfFormDataField.#ctor(System.String,Fluent.Pdf.PdfValue)</code>

Captures a name and PDF null, name, text string or array of text strings. Complete Unicode/value validation occurs under the data operation budget.

### <code>M:Fluent.Pdf.PdfFormFieldDefinition.#ctor(System.String,Fluent.Pdf.PdfFormFieldKind,Fluent.Pdf.PdfFormWidgetDefinition[],Fluent.Pdf.PdfValue,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfFormFieldFlags,Fluent.Pdf.PdfFormChoice[],System.Nullable{System.Int32},System.String,System.String,Fluent.Pdf.PdfFontResource,System.Double)</code>

Captures explicit values, widget appearances and optional edit font. No field action or calculation is executed.

### <code>M:Fluent.Pdf.PdfFormTextStyle.#ctor(Fluent.Pdf.PdfFontFallback,Fluent.Pdf.PdfProviderRegistry,System.Double,System.Double,Fluent.Pdf.PdfTextOptions,System.String)</code>

Captures fonts/shaper, positive font size and padding. Password auto-appearance requires an explicit one-scalar mask.

### <code>M:Fluent.Pdf.PdfFormUpdate.#ctor(System.String,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfFormTextStyle,System.Collections.Generic.IReadOnlyDictionary{Fluent.Pdf.PdfObjectId,Fluent.Pdf.PdfGraphicsResource},Fluent.Pdf.PdfValue)</code>

Captures an exact qualified name/current value. Null defaultValue leaves DV unchanged; PDF null clears it. Variable text requires a style or a Form for every widget.

### <code>M:Fluent.Pdf.PdfFormWidgetDefinition.#ctor(System.Int32,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfGraphicsResource,System.String,Fluent.Pdf.PdfGraphicsResource,System.Int32)</code>

Captures native page bounds and a normal Form appearance. Button-state widgets also require an Off Form and non-Off export name.

### <code>M:Fluent.Pdf.PdfForms.Add(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfFormFieldDefinition,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Adds a terminal field and owned widgets, creating missing hierarchical containers. Duplicate logical names reject; use AddWidget to share a value.

### <code>M:Fluent.Pdf.PdfForms.AddWidget(Fluent.Pdf.PdfFormField,Fluent.Pdf.PdfFormWidgetDefinition,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Adds another widget representation of an existing logical field without creating a duplicate name.

### <code>M:Fluent.Pdf.PdfForms.CopyField(Fluent.Pdf.PdfFormField,Fluent.Pdf.PdfDocumentSnapshot,System.String,System.Collections.Generic.IReadOnlyDictionary{System.Int32,System.Int32},System.Boolean,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Copies one standard separate field/widget graph to an explicit new root partial name. Every widget/navigation page needs an explicit source-to-target page mapping; scripts remain passive.

### <code>M:Fluent.Pdf.PdfForms.CreateTextAppearance(Fluent.Pdf.PdfShapedText,Fluent.Pdf.PdfRect,System.Double,System.Double,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates an embedded shaped Unicode Form appearance with a conservative overflow check. Bounds/font size use native appearance units; no field value or viewer editing font is inferred.

### <code>M:Fluent.Pdf.PdfForms.DecorateWidget(Fluent.Pdf.PdfFormField,Fluent.Pdf.PdfObjectId,Fluent.Pdf.PdfGraphicsResource,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Overlays a private Form on every normal/rollover/down appearance state, preserving values, state names, widget identity, actions and structure associations.

### <code>M:Fluent.Pdf.PdfForms.EditWidget(Fluent.Pdf.PdfFormField,Fluent.Pdf.PdfObjectId,Fluent.Pdf.PdfFormWidgetDefinition,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Replaces one widget's appearance, same-page bounds and annotation flags while preserving field identity, actions, tab position and structure links. Cross-page movement requires a structure relocation profile.

### <code>M:Fluent.Pdf.PdfForms.Fill(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFormUpdate},System.Boolean,Fluent.Pdf.PdfWriteMode,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages distinct field value/reset updates and synchronizes all owned widgets under one context. Calculations/actions never run; explicit private Forms cover richer appearance profiles.

### <code>M:Fluent.Pdf.PdfForms.FillData(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfFormData,Fluent.Pdf.PdfFormTextStyle,System.Boolean,Fluent.Pdf.PdfWriteMode,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Applies data using target field types and optional explicit text generation settings. Unknown names and unavailable appearances reject atomically.

### <code>M:Fluent.Pdf.PdfForms.Flatten(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.String},Fluent.Pdf.PdfAnnotationView,System.Boolean,System.Boolean,Fluent.Pdf.PdfWriteMode,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Paints selected/all authored widget appearances and removes owned field/widget nodes. Incremental mode preserves original signed bytes; signature verification/transform permission is a separate requirement.

### <code>M:Fluent.Pdf.PdfForms.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads field inheritance, consistent duplicate representations and widget ownership without evaluating scripts/XFA.

### <code>M:Fluent.Pdf.PdfForms.ReadValues(Fluent.Pdf.PdfDocumentSnapshot,System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Extracts current data-bearing standard fields. Push/signature fields are excluded; NoExport is honored unless explicitly included.

### <code>M:Fluent.Pdf.PdfForms.Rename(Fluent.Pdf.PdfFormField,System.String,System.Boolean,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Renames the nearest named field ancestor, preserving identities, widgets, tags and calculation references. Descendant qualified names follow that ancestor.

### <code>M:Fluent.Pdf.PdfForms.SetCalculationAction(Fluent.Pdf.PdfFormField,Fluent.Pdf.PdfAction,Fluent.Pdf.PdfActionPolicy,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets/removes a passive calculation action and synchronizes CO for every terminal representation. Action authoring uses the explicit navigation policy; nothing executes.

### <code>M:Fluent.Pdf.PdfForms.SetCalculationOrder(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IReadOnlyList{Fluent.Pdf.PdfObjectId},Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets the passive calculation order using exact terminal identities. No field action executes.

### <code>M:Fluent.Pdf.PdfForms.SetCommonFlags(Fluent.Pdf.PdfFormField,Fluent.Pdf.PdfFormFieldFlags,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Changes only ReadOnly, Required and NoExport, preserving every kind-specific source flag, widget identity and structure association.

### <code>M:Fluent.Pdf.PdfForms.SetTabOrder(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfFormTabOrder,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets the page's standard Tabs key without reordering annotations or rewriting existing structure relationships.

### <code>M:Fluent.Pdf.PdfFunction.Exponential(System.Double[],System.Double[],System.Double,System.Double,System.Double,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a type-2 exponential interpolation function with exact endpoint vectors.

### <code>M:Fluent.Pdf.PdfFunction.Sampled(System.Double[],System.Double[],System.Int32[],System.Int32,System.Byte[],System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a type-0 sampled function with an exact packed sample plane. Samples are copied only after size and budget preflight.

### <code>M:Fluent.Pdf.PdfFunction.Stitch(Fluent.Pdf.PdfFunction[],System.Double[],System.Double[],System.Double,System.Double,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a type-3 piecewise function with explicit ordered bounds and per-piece input encoding.

### <code>M:Fluent.Pdf.PdfGenerationContext.Add(Fluent.Pdf.PdfFlowBlock)</code>

Captures one immutable block during template rendering. Layout remains a later shared-budget phase.

### <code>M:Fluent.Pdf.PdfGenerationContext.Add(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowBlock})</code>

Captures a finite block source with cooperative checkpoints and the same block/object ceilings.

### <code>M:Fluent.Pdf.PdfGenerationContext.AddTable``1(System.Collections.Generic.IEnumerable{``0},Fluent.Pdf.PdfTableDataColumn{``0}[],System.Int32,Fluent.Pdf.PdfTableRow[],System.Boolean)</code>

Binds and captures a typed finite table under this operation and culture without exposing unfinished model/report objects.

### <code>M:Fluent.Pdf.PdfGenerationContext.Apply``1(Fluent.Pdf.PdfGenerationTemplate{``0},``0)</code>

Invokes a reusable typed template within the same operation. Reference-record invocation cycles reject; every invocation uses bounded depth, including value records.

### <code>M:Fluent.Pdf.PdfGenerationContext.Format(System.Object,System.String,System.String)</code>

Formats only supported scalar types with the captured culture. Arbitrary object ToString and ambient culture are never used.

### <code>M:Fluent.Pdf.PdfGenerationContext.Paragraph(System.String,Fluent.Pdf.PdfFlowTextStyle,Fluent.Pdf.PdfFlowParagraphStyle)</code>

Captures a paragraph using an explicit immutable text style; no font selection occurs implicitly.

### <code>M:Fluent.Pdf.PdfGenerationOptions.#ctor(Fluent.Pdf.PdfFlowOptions,System.String,Fluent.Pdf.PdfPageTemplate,Fluent.Pdf.PdfOptions,System.Action{Fluent.Pdf.PdfGenerationEvent},System.Int32,System.Int32,System.Int32)</code>

Captures an explicit standard culture name (empty for invariant), layout, optional page decorations and a synchronous cooperative observer. No external resources are resolved implicitly.

### <code>M:Fluent.Pdf.PdfGenerationTemplate`1.#ctor(System.String,System.Action{Fluent.Pdf.PdfGenerationContext,`0})</code>

Captures a bounded ASCII identifier and synchronous cooperative renderer; records are never reflected over or implicitly converted.

### <code>M:Fluent.Pdf.PdfGlyphMapping.#ctor(System.Int32,System.String)</code>

Captures a glyph identity and a well-formed Unicode sequence; an empty sequence explicitly carries no logical text.

### <code>M:Fluent.Pdf.PdfGraphicsObjects.Form(Fluent.Pdf.PdfDrawing,Fluent.Pdf.PdfRect,System.Nullable{Fluent.Pdf.PdfMatrix},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates an immutable reusable Form XObject with explicit bounds and transform.

### <code>M:Fluent.Pdf.PdfGraphicsResource.Create(Fluent.Pdf.PdfGraphicsResourceCategory,Fluent.Pdf.PdfValue,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures an advanced direct resource definition with an explicit version floor. Foreign references are rejected; immutable nested streams are supported.

### <code>M:Fluent.Pdf.PdfGraphicsResource.Font(Fluent.Pdf.PdfFontResource)</code>

Wraps an eagerly embedded exact font for a reusable graphics resource scope.

### <code>M:Fluent.Pdf.PdfGraphicsState.Create(System.String,System.Double,System.Double,Fluent.Pdf.PdfSoftMask,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates explicit PDF 1.4 compositing state. Unknown blend modes fail strictly or report replacement by Normal under an explicit permissive policy.

### <code>M:Fluent.Pdf.PdfHtml.Render(Fluent.Pdf.PdfHtmlSource,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfHtmlOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Renders privately, validates/imports the generated PDF under the same context and returns an independent editable document. Save/SaveFile retain their ordinary ownership/atomicity rules.

### <code>M:Fluent.Pdf.PdfHtmlOptions.#ctor(System.Nullable{Fluent.Pdf.PdfPageSize},System.Int32,System.Int32,System.Boolean,System.Boolean,System.Int32)</code>

Captures page size, bounded viewport, script execution, background painting and imported page ceiling.

### <code>M:Fluent.Pdf.PdfHtmlOptions.#ctor(System.Nullable{Fluent.Pdf.PdfPageSize},System.Int32,System.Int32,System.Boolean,System.Boolean,System.Int32,Fluent.Pdf.PdfHtmlReadinessOptions,System.String,System.String,Fluent.Pdf.PdfHtmlMedia)</code>

Captures explicit finite readiness, browser locale/timezone/media and the ordinary bounded layout/print options. Unknown engine locale/timezone values reject.

### <code>M:Fluent.Pdf.PdfHtmlOptions.WithPrint(Fluent.Pdf.PdfHtmlPrintOptions)</code>

Returns an independent immutable options snapshot with the supplied print policy. Existing source/binary constructor signatures remain available.

### <code>M:Fluent.Pdf.PdfHtmlPrintOptions.#ctor(System.Boolean,System.Boolean,System.Double,System.Double,System.Double,System.Double,System.Double,System.String,System.String,System.Boolean)</code>

Captures paper/CSS sizing, orientation, explicit scale, point margins, bounded resource-free header/footer fragments and optional structural tagging.

### <code>M:Fluent.Pdf.PdfHtmlReadinessOptions.#ctor(System.Nullable{System.TimeSpan},System.Collections.Generic.IEnumerable{System.String},System.String,System.Boolean,System.Boolean,System.Boolean,System.Nullable{System.TimeSpan},System.Int32,System.Nullable{System.TimeSpan})</code>

Captures finite timeout, explicit CSS selectors/promise, native asset checks and observed network quietness. Operation limits can reject earlier.

### <code>M:Fluent.Pdf.PdfHtmlResource.OpenContent</code>

Opens an independent read-only content view; no caller input stream is retained.

### <code>M:Fluent.Pdf.PdfHtmlResource.Read(System.Uri,System.String,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures up to 16 MiB from a borrowed resource stream with explicit media type and virtual address.

### <code>M:Fluent.Pdf.PdfHtmlSource.FromHtml(System.String,System.Uri,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfHtmlResource},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures well-formed Unicode HTML and explicit local resources. The default base is a reserved virtual HTTPS origin.

### <code>M:Fluent.Pdf.PdfHtmlSource.FromResource(System.Uri,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfHtmlResource},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Uses an exact HTTP(S) document address. Bytes require supplied Resources or an explicit renderer acquisition policy; this method performs no network request.

### <code>M:Fluent.Pdf.PdfHtmlSource.Read(System.IO.Stream,System.Uri,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfHtmlResource},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures UTF-8 HTML from a borrowed file/document stream. A UTF-8 BOM is allowed; malformed Unicode rejects without guessing another encoding.

### <code>M:Fluent.Pdf.PdfImageAsset.OpenSamples(Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Decodes filter samples before Decode/color/alpha interpretation into an owned stream under one bounded operation. Optional image codecs must be explicit.

### <code>M:Fluent.Pdf.PdfImageAsset.WriteEncoded(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Exports exact PDF filter bytes without decoding, color conversion or recompression, retaining caller ownership.

### <code>M:Fluent.Pdf.PdfImageAsset.WriteOriginalFile(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Exports the exact retained image file; rejects assets without an original container.

### <code>M:Fluent.Pdf.PdfImageInfo.#ctor(System.Int32,System.Int32,System.Int32,System.Int32)</code>

Creates positive geometry with 1–32 components and 1–16 bits per component; arithmetic overflow is rejected.

### <code>M:Fluent.Pdf.PdfImageMetadata.#ctor(Fluent.Pdf.PdfImageOrientation,System.Double,System.Double,System.Double)</code>

Captures display metadata. Both DPI values must be positive or both zero; density also determines pixel aspect.

### <code>M:Fluent.Pdf.PdfImageReadOptions.#ctor(System.Boolean,System.Boolean,Fluent.Pdf.PdfColorSpace)</code>

Captures explicit permission for lossless layout/channel conversion and selection of Exif display orientation.

### <code>M:Fluent.Pdf.PdfImages.Deduplicate(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfImageAsset},System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Interns exact safe image graphs within one bounded batch, comparing bytes after hashing and retaining original-file/metadata identity.

### <code>M:Fluent.Pdf.PdfImages.FromEncoded(System.IO.Stream,Fluent.Pdf.PdfImageInfo,Fluent.Pdf.PdfColorSpace,Fluent.Pdf.PdfFilter[],Fluent.Pdf.PdfImageAsset,System.Double[],Fluent.Pdf.PdfImageMetadata,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Preserves exact encoded PDF filter bytes with explicit inspected geometry and a bounded standard filter chain.

### <code>M:Fluent.Pdf.PdfImages.FromRgba(System.IO.Stream,System.Int32,System.Int32,System.Int32,Fluent.Pdf.PdfColorSpace,Fluent.Pdf.PdfImageMetadata,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Separates exact unassociated RGBA samples into RGB and grayscale alpha planes without changing component precision.

### <code>M:Fluent.Pdf.PdfImages.FromSamples(System.IO.Stream,Fluent.Pdf.PdfImageInfo,Fluent.Pdf.PdfColorSpace,Fluent.Pdf.PdfImageAsset,System.Double[],System.Boolean,Fluent.Pdf.PdfImageMetadata,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures exact packed samples with explicit color, alpha, Decode and display metadata. No lossy compression is selected.

### <code>M:Fluent.Pdf.PdfImages.FromStencil(System.IO.Stream,System.Int32,System.Int32,System.Boolean,Fluent.Pdf.PdfImageMetadata,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a one-bit stencil; zero samples paint by default and inversion reverses that interpretation.

### <code>M:Fluent.Pdf.PdfImages.Read(System.IO.Stream,Fluent.Pdf.PdfImageReadOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads an original PNG or JPEG file offline with explicit orientation and lossless-transcode policy, retaining caller ownership and exact original bytes.

### <code>M:Fluent.Pdf.PdfImages.WithColorKey(Fluent.Pdf.PdfImageAsset,System.Int32[],Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Attaches a color key expressed as inclusive integer ranges in the encoded component domain, before Decode/color conversion.

### <code>M:Fluent.Pdf.PdfImages.WithHardMask(Fluent.Pdf.PdfImageAsset,Fluent.Pdf.PdfImageAsset,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Attaches an exact matching one-bit explicit mask without changing either image stream.

### <code>M:Fluent.Pdf.PdfInputConversion.Generate(System.IO.Stream,Fluent.Pdf.PdfInputFormat,Fluent.Pdf.PdfInputConversionOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Converts a borrowed source at its current position into a caller-owned document.

### <code>M:Fluent.Pdf.PdfInputConversion.Write(System.IO.Stream,Fluent.Pdf.PdfInputFormat,Fluent.Pdf.PdfInputConversionOptions,Fluent.Pdf.PdfProviderRegistry,System.IO.Stream,Fluent.Pdf.PdfWriteOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Parses, lays out and serializes under one context, leaving both caller streams open. Stream output may contain a prefix on failure.

### <code>M:Fluent.Pdf.PdfInputConversion.WriteFile(System.IO.Stream,Fluent.Pdf.PdfInputFormat,Fluent.Pdf.PdfInputConversionOptions,Fluent.Pdf.PdfProviderRegistry,System.String,Fluent.Pdf.PdfWriteOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Converts and serializes privately before atomic file replacement; failure preserves an existing destination.

### <code>M:Fluent.Pdf.PdfInputConversionOptions.#ctor(Fluent.Pdf.PdfGenerationOptions,System.Collections.Generic.IReadOnlyDictionary{System.String,Fluent.Pdf.PdfFlowTextStyle},System.Collections.Generic.IReadOnlyDictionary{System.String,Fluent.Pdf.PdfInputDrawing},System.Int32,System.Int32)</code>

Captures at most 4096 styles/resources. The body style is mandatory; other semantic styles are required when used. No default font or resource loader is installed.

### <code>M:Fluent.Pdf.PdfInputDrawing.#ctor(Fluent.Pdf.PdfDrawing,Fluent.Pdf.PdfRect,System.Double,System.Double)</code>

Captures drawing geometry without opening any resource or selecting a provider.

### <code>M:Fluent.Pdf.PdfInvoiceData.#ctor(Fluent.Pdf.PdfInvoiceProfile,System.String,System.String,System.String,System.DateTimeOffset,System.String,System.MidpointRounding,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfInvoiceLine})</code>

Captures1–1000 lines and computes per-line net and per-rate-category tax using the selected midpoint rounding and explicit currency minor units. Supported currency codes are EUR/USD/GBP(two), JPY(zero), BHD(three).

### <code>M:Fluent.Pdf.PdfInvoiceLine.#ctor(System.String,System.Decimal,System.Decimal,System.Decimal)</code>

Captures a literal description, positive quantity, nonnegative unit price and percentage tax rate. Each numeric input has at most4 fractional digits and finite magnitude.

### <code>M:Fluent.Pdf.PdfInvoiceValidation.#ctor(Fluent.Pdf.PdfInvoiceProfile,System.String,System.String,System.String,System.Boolean,System.Boolean,System.Collections.Generic.IEnumerable{System.String})</code>

Captures exact profile/PDF/XML/request binding and bounded rule findings. Compliance requires a complete result with zero findings.

### <code>M:Fluent.Pdf.PdfInvoices.Write(Fluent.Pdf.PdfInvoiceData,System.IO.Stream,Fluent.Pdf.PdfFlowTextStyle,Fluent.Pdf.PdfFlowOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.IPdfInvoiceProducer,Fluent.Pdf.IPdfArchivalValidator,Fluent.Pdf.IPdfInvoiceValidator,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages all layout/XML/attachment/producer/validation phases under one context. No PDF is exposed until exact associated XML, complete PDF/A-3b and invoice-schema/business/readable-total validations all pass. No complete producer/validator is bundled by this API.

### <code>M:Fluent.Pdf.PdfLayers.AddGroup(Fluent.Pdf.PdfDocumentSnapshot,System.String,System.Boolean,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Adds one Unicode OCG with explicit initial state and optional View/Print/Export usage and View/Design intents.

### <code>M:Fluent.Pdf.PdfLayers.BindXObject(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,System.String,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Binds a selected page XObject to an OCG or OCMD, cloning the stream and page resource scope.

### <code>M:Fluent.Pdf.PdfLayers.EditGroup(Fluent.Pdf.PdfLayerGroup,System.String,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Changes a captured group; null preserves a field and PDF null removes usage/intents. The edit stages a new document.

### <code>M:Fluent.Pdf.PdfLayers.Flatten(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfLayerUsage,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Flattens the selected configuration, removing hidden painting while preserving supported graphics-state effects. Unsupported state-dependent cases reject.

### <code>M:Fluent.Pdf.PdfLayers.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inspects registered groups and raw configurations without applying usage or evaluating document actions.

### <code>M:Fluent.Pdf.PdfLayers.IsVisible(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfLayerUsage,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Evaluates an OCG reference or OCMD, including nested VE expressions, with explicit configuration and usage.

### <code>M:Fluent.Pdf.PdfLayers.SelectConfiguration(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfLayerUsage,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Applies an alternate configuration to the initial view, resolving Unchanged and usage into explicit states.

### <code>M:Fluent.Pdf.PdfLayers.SetConfiguration(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfValue,System.Boolean,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets the default configuration or appends an alternate configuration, validating group references and standard supported keys.

### <code>M:Fluent.Pdf.PdfLinearization.Inspect(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads a bounded immutable PDF and reports structural dictionary bounds, or null for an ordinary PDF. This is not complete independent validation.

### <code>M:Fluent.Pdf.PdfLinearization.Validate(System.IO.Stream,Fluent.Pdf.PdfLinearizationOptions,Fluent.Pdf.IPdfLinearizationValidator,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Requires explicit complete independent validation of exact captured bytes and requested settings. Structural markers alone cannot return a valid result.

### <code>M:Fluent.Pdf.PdfLinearization.Write(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfLinearizationOptions,Fluent.Pdf.IPdfLinearizationProducer,Fluent.Pdf.IPdfLinearizationValidator,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages a complete rewrite, verifies page geometry and dictionary binding, then requires independent complete validation before copying to borrowed output.

### <code>M:Fluent.Pdf.PdfLinearization.WriteFile(System.IO.Stream,System.String,Fluent.Pdf.PdfLinearizationOptions,Fluent.Pdf.IPdfLinearizationProducer,Fluent.Pdf.IPdfLinearizationValidator,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages and independently validates before atomically replacing a file.

### <code>M:Fluent.Pdf.PdfLinearizationOptions.#ctor(System.Int32,System.Boolean)</code>

Selects the first displayed page and explicit authority for signature invalidation.

### <code>M:Fluent.Pdf.PdfLinearizationValidation.#ctor(System.String,System.String,System.Boolean,System.Boolean,System.Collections.Generic.IEnumerable{System.String})</code>

Captures bounded findings. A valid observation requires completed checking and zero findings; this does not qualify a production engine.

### <code>M:Fluent.Pdf.PdfMailMerge.Generate(Fluent.Pdf.PdfMergeTemplate,Fluent.Pdf.PdfMergeRecord,Fluent.Pdf.PdfMailMergeOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Evaluates into a caller-owned editable document; no arbitrary template code is executed.

### <code>M:Fluent.Pdf.PdfMailMerge.Write(Fluent.Pdf.PdfMergeTemplate,Fluent.Pdf.PdfMergeRecord,Fluent.Pdf.PdfMailMergeOptions,Fluent.Pdf.PdfProviderRegistry,System.IO.Stream,Fluent.Pdf.PdfWriteOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Evaluates and writes at a borrowed current output position. The stream remains open; failure may leave a prefix.

### <code>M:Fluent.Pdf.PdfMailMerge.WriteFile(Fluent.Pdf.PdfMergeTemplate,Fluent.Pdf.PdfMergeRecord,Fluent.Pdf.PdfMailMergeOptions,Fluent.Pdf.PdfProviderRegistry,System.String,Fluent.Pdf.PdfWriteOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Evaluates and serializes privately before atomic destination replacement.

### <code>M:Fluent.Pdf.PdfMailMergeOptions.#ctor(Fluent.Pdf.PdfGenerationOptions,System.Collections.Generic.IReadOnlyDictionary{System.String,Fluent.Pdf.PdfFlowTextStyle},Fluent.Pdf.PdfMergeMissingField,System.Int32,System.Int32)</code>

Captures bounded named styles and generation options. Missing fields and expanded visits/records remain explicit.

### <code>M:Fluent.Pdf.PdfMargins.#ctor(System.Double,System.Double,System.Double,System.Double)</code>

Captures left/top/right/bottom margins.

### <code>M:Fluent.Pdf.PdfMargins.Uniform(System.Double)</code>

Creates equal margins.

### <code>M:Fluent.Pdf.PdfMatrix.#ctor(System.Double,System.Double,System.Double,System.Double,System.Double,System.Double)</code>

Captures six finite affine coefficients. Singular matrices are representable but cannot be inverted.

### <code>M:Fluent.Pdf.PdfMatrix.Invert</code>

Returns the exact algebraic inverse, rejecting singular/nonfinite arithmetic.

### <code>M:Fluent.Pdf.PdfMatrix.Multiply(Fluent.Pdf.PdfMatrix)</code>

Returns this matrix times right; right is applied to points first.

### <code>M:Fluent.Pdf.PdfMatrix.Transform(Fluent.Pdf.PdfPoint)</code>

Transforms a point and rejects nonfinite results.

### <code>M:Fluent.Pdf.PdfMatrixIccTransform.Transform(Fluent.Pdf.PdfColor,Fluent.Pdf.PdfColorSpace,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Transforms one normalized RGB color through explicit matrix/TRC profiles. Out-of-gamut results reject instead of silently clipping. No profile or engine is discovered.

### <code>M:Fluent.Pdf.PdfMergeCondition.#ctor(System.String,Fluent.Pdf.PdfMergeBlock[],Fluent.Pdf.PdfMergeBlock[])</code>

Captures the boolean path and explicit true/false instruction lists.

### <code>M:Fluent.Pdf.PdfMergeEvaluationOptions.#ctor(System.String,Fluent.Pdf.PdfMergeMissingField,System.Int32,System.Int32)</code>

Captures a standard culture by name; empty selects invariant. No ambient culture, reflected property or expression evaluation is used.

### <code>M:Fluent.Pdf.PdfMergeImage.ReadPng(System.IO.Stream,System.Double,System.Double,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures and validates at most16MiB PNG bytes at the borrowed current position. No file, resource or codec is discovered; the input remains open.

### <code>M:Fluent.Pdf.PdfMergeImageBlock.#ctor(System.String)</code>

Captures one closed image field path.

### <code>M:Fluent.Pdf.PdfMergeParagraph.#ctor(Fluent.Pdf.PdfMergeRun[],Fluent.Pdf.PdfFlowParagraphStyle)</code>

Captures at most4096nonempty runs and immutable paragraph options.

### <code>M:Fluent.Pdf.PdfMergeRecord.#ctor(System.Collections.Generic.IReadOnlyDictionary{System.String,Fluent.Pdf.PdfMergeValue})</code>

Captures at most4096nonnull named fields. Names are closed identifiers; no object reflection or dictionary mutation occurs during evaluation.

### <code>M:Fluent.Pdf.PdfMergeRepeat.#ctor(System.String,Fluent.Pdf.PdfMergeBlock[])</code>

Captures a closed collection path and finite children.

### <code>M:Fluent.Pdf.PdfMergeRun.Field(System.String,System.String,System.String)</code>

Creates one closed scalar-field lookup with an optional explicit standard/custom scalar format.

### <code>M:Fluent.Pdf.PdfMergeRun.Text(System.String,System.String)</code>

Creates literal text. Placeholder-looking data remains literal.

### <code>M:Fluent.Pdf.PdfMergeTable.#ctor(System.String,Fluent.Pdf.PdfMergeTableColumn[])</code>

Captures a collection path and one to256nonnull columns. Header rows repeat through the ordinary table engine.

### <code>M:Fluent.Pdf.PdfMergeTableColumn.#ctor(Fluent.Pdf.PdfTableColumn,Fluent.Pdf.PdfMergeRun[],System.String)</code>

Captures column geometry, at most4096nonnull runs and an optional literal header.

### <code>M:Fluent.Pdf.PdfMergeTemplate.#ctor(System.String,Fluent.Pdf.PdfMergeBlock[])</code>

Captures an ASCII identifier and at most4096nonnull instructions.

### <code>M:Fluent.Pdf.PdfMergeValue.Image(Fluent.Pdf.PdfMergeImage)</code>

Captures an explicitly loaded immutable image and physical geometry.

### <code>M:Fluent.Pdf.PdfMergeValue.Record(Fluent.Pdf.PdfMergeRecord)</code>

Captures an immutable nested record.

### <code>M:Fluent.Pdf.PdfMergeValue.Records(Fluent.Pdf.PdfMergeRecord[])</code>

Captures at most100000immutable records; no enumeration or implicit conversion is performed.

### <code>M:Fluent.Pdf.PdfMergeValue.Scalar(System.Object)</code>

Captures a supported immutable scalar. Arbitrary objects, floating nonfinite numbers and custom formatting implementations are rejected.

### <code>M:Fluent.Pdf.PdfMetadata.Edit(Fluent.Pdf.PdfDocumentSnapshot,System.Action{Fluent.Pdf.PdfMetadataEditor},Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Applies one bounded synchronous metadata edit and returns a new document. No time or identity is inferred.

### <code>M:Fluent.Pdf.PdfMetadata.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inspects Info, XMP and trailer identity without fetching resources or executing document actions.

### <code>M:Fluent.Pdf.PdfMetadata.Remove(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfMetadataRemovalOptions,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Removes Info, PDF metadata streams and piece information in a full rewrite; embedded file retention requires an explicit decision.

### <code>M:Fluent.Pdf.PdfMetadataEditor.GetInformation(System.String)</code>

Gets a resolved Info text field, or null when absent. Unsupported field types reject this typed access.

### <code>M:Fluent.Pdf.PdfMetadataEditor.RemoveIdentity</code>

Removes trailer ID bytes in this full-rewrite edit.

### <code>M:Fluent.Pdf.PdfMetadataEditor.SetCreationTime(System.Nullable{System.DateTimeOffset})</code>

Sets or removes an explicit whole-second creation timestamp, retaining its supplied offset.

### <code>M:Fluent.Pdf.PdfMetadataEditor.SetIdentity(System.Byte[],System.Byte[])</code>

Sets caller-chosen permanent/revision ID bytes. Omitting revision uses the permanent ID for both entries.

### <code>M:Fluent.Pdf.PdfMetadataEditor.SetInformation(System.String,System.String)</code>

Sets or removes a standard/custom Info text field. Trapped uses its standard name representation.

### <code>M:Fluent.Pdf.PdfMetadataEditor.SetInformationText(System.String,Fluent.Pdf.PdfTextString)</code>

Writes exact explicitly encoded Info text and raises the catalog version floor for UTF-8. Date/Trapped semantic validation remains explicit.

### <code>M:Fluent.Pdf.PdfMetadataEditor.SetModificationTime(System.Nullable{System.DateTimeOffset})</code>

Sets or removes an explicit whole-second modification timestamp, retaining its supplied offset.

### <code>M:Fluent.Pdf.PdfMetadataEditor.SetXmp(System.String)</code>

Replaces or removes the XMP packet. XML remains offline and prohibits DTDs and external entities.

### <code>M:Fluent.Pdf.PdfMetadataEditor.SetXmpProperty(System.String,System.String,System.String)</code>

Sets or removes one scalar property by namespace URI/local name, preserving unrelated namespaces and structured properties.

### <code>M:Fluent.Pdf.PdfMetadataEditor.Synchronize(Fluent.Pdf.PdfMetadataSynchronization)</code>

Synchronizes representable fields using explicit authority. Structured values that cannot fit Info reject rather than flatten silently.

### <code>M:Fluent.Pdf.PdfMetadataRemovalOptions.#ctor(System.Boolean,Fluent.Pdf.PdfMetadataEmbeddedFilePolicy)</code>

Captures whether identifiers are removed and whether embedded files may retain internal metadata.

### <code>M:Fluent.Pdf.PdfNativeAsset.#ctor(System.String,System.Int64,System.String,System.Boolean)</code>

Captures a portable single file name, exact positive length, SHA-256 and binary/data role.

### <code>M:Fluent.Pdf.PdfNativeDeployment.#ctor(System.String,System.String,System.String,System.String,System.String,System.Int64,Fluent.Pdf.PdfNativeAsset[])</code>

Captures exact engine/ABI labels and 1–64 uniquely named assets under an explicit local absolute directory.

Remarks: The caller must provision a trusted directory and pin the complete dependency set. ABI is a declaration, not a verified compatibility claim.

### <code>M:Fluent.Pdf.PdfNativeDeploymentInspector.Inspect(Fluent.Pdf.PdfNativeDeployment,System.Threading.CancellationToken)</code>

Performs a read-only preflight with bounded buffers and cooperative cancellation between file reads.

Remarks: Root/file reparse points are rejected. Caller-controlled immutable directory ancestry is required; this is not a race-free secure loader or a native worker sandbox.

### <code>M:Fluent.Pdf.PdfNativeDeploymentInspector.InspectWorker(Fluent.Pdf.PdfNativeDeployment,System.Threading.CancellationToken)</code>

Inspects a worker deployment targeting either the current process RID or the native OS RID; no code is executed.

### <code>M:Fluent.Pdf.PdfNativeDeploymentInspector.Verify(Fluent.Pdf.PdfNativeDeployment,Fluent.Pdf.PdfOperationContext)</code>

Preflights within an SDK operation; any integrity failure is reported as a latched fatal diagnostic.

Remarks: The deployment byte ceiling bounds this configuration I/O; operation checkpoints enforce cancellation/deadline. No native execution occurs.

### <code>M:Fluent.Pdf.PdfNativeDeploymentInspector.VerifyWorker(Fluent.Pdf.PdfNativeDeployment,Fluent.Pdf.PdfOperationContext)</code>

Preflights an explicit out-of-process worker, allowing native OS architecture independently of caller bitness.

### <code>M:Fluent.Pdf.PdfNavigation.AddLink(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfAction,Fluent.Pdf.PdfActionPolicy,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Adds a Link annotation in native unrotated page coordinates. No URI is fetched or action executed.

### <code>M:Fluent.Pdf.PdfNavigation.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfActionPolicy,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inspects all active objects and physical action slots. Historical revisions and embedded foreign document formats are not interpreted.

### <code>M:Fluent.Pdf.PdfNavigation.Sanitize(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfActionPolicy,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Explicitly removes policy-blocked actions and opaque active content from all active objects. This full rewrite is not secure erasure.

### <code>M:Fluent.Pdf.PdfNavigation.SetDocumentAction(Fluent.Pdf.PdfDocumentSnapshot,System.String,Fluent.Pdf.PdfAction,Fluent.Pdf.PdfActionPolicy,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets or removes a catalog OpenAction or additional action (WC, WS, DS, WP, DP).

### <code>M:Fluent.Pdf.PdfNavigation.SetNamedDestination(Fluent.Pdf.PdfDocumentSnapshot,System.String,Fluent.Pdf.PdfDestination,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates or replaces one Unicode named destination. Existing ambiguous legacy names are rejected.

### <code>M:Fluent.Pdf.PdfNavigation.SetPageAction(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,System.String,Fluent.Pdf.PdfAction,Fluent.Pdf.PdfActionPolicy,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets or removes a page O (open) or C (close) additional action.

### <code>M:Fluent.Pdf.PdfObjectId.#ctor(System.Int32,System.Int32)</code>

Creates a nonnegative object number with a generation in 0–65535.

### <code>M:Fluent.Pdf.PdfObjectId.Equals(Fluent.Pdf.PdfObjectId)</code>

Compares the number and generation.

### <code>M:Fluent.Pdf.PdfObjectId.Equals(System.Object)</code>

### <code>M:Fluent.Pdf.PdfObjectId.GetHashCode</code>

### <code>M:Fluent.Pdf.PdfObjectId.ToString</code>

Formats an indirect reference using invariant decimal notation.

### <code>M:Fluent.Pdf.PdfObjectId.op_Equality(Fluent.Pdf.PdfObjectId,Fluent.Pdf.PdfObjectId)</code>

Compares the number and generation.

### <code>M:Fluent.Pdf.PdfObjectId.op_Inequality(Fluent.Pdf.PdfObjectId,Fluent.Pdf.PdfObjectId)</code>

Compares the number and generation.

### <code>M:Fluent.Pdf.PdfOcr.Recognize(Fluent.Pdf.PdfOcrRaster,Fluent.Pdf.PdfOcrOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Recognizes an explicitly captured raster. Results retain engine-order geometry/confidence; accuracy and semantic order require separate qualification.

### <code>M:Fluent.Pdf.PdfOcrAccuracy.Evaluate(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfOcrAccuracySample},System.String,System.String,Fluent.Pdf.PdfOcrAccuracyOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Measures a finite labeled corpus against explicit prior thresholds. It never downloads models or declares production readiness.

### <code>M:Fluent.Pdf.PdfOcrAccuracyOptions.#ctor(System.Collections.Generic.IReadOnlyDictionary{System.String,Fluent.Pdf.PdfOcrAccuracyThresholds},System.Int32,System.Int64,System.Int32)</code>

Captures1–256 exact language threshold keys, finite case/DP-work limits and per-case Unicode limits. No threshold is inferred from confidence or measured output.

### <code>M:Fluent.Pdf.PdfOcrAccuracySample.#ctor(System.String,System.String,System.String,System.String,System.String)</code>

Captures case/language/model identity and exact valid Unicode reference/recognized strings, each bounded to one million UTF16 units.

### <code>M:Fluent.Pdf.PdfOcrAccuracyThresholds.#ctor(System.Double,System.Double)</code>

Captures maximum character/word error ratios0–1. Measured ratios may exceed1 when insertions dominate.

### <code>M:Fluent.Pdf.PdfOcrLanguageModel.#ctor(System.String,System.String,System.String,System.Int64,System.String,System.String,System.String[],System.String[])</code>

Captures a portable lowercase native alias, exact version/content digest/length, license/source IDs and declared scripts/languages. A script model may use an explicit flat alias rather than a directory path.

### <code>M:Fluent.Pdf.PdfOcrModelCatalog.#ctor(Fluent.Pdf.PdfOcrLanguageModel[])</code>

Captures unique immutable model declarations. Every selected model must later match the native deployment's exact data asset.

### <code>M:Fluent.Pdf.PdfOcrModelCatalog.Select(System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Selects exact plus-separated aliases in caller order and returns a content/version-bound selection. Missing/duplicate aliases reject rather than falling back.

### <code>M:Fluent.Pdf.PdfOcrOptions.#ctor(System.String,Fluent.Pdf.PdfOcrSegmentation,System.Int32,System.Int32)</code>

Captures a bounded explicit language token, segmentation and character/word ceilings. No native/model download is implicit.

### <code>M:Fluent.Pdf.PdfOcrOutput.Write(Fluent.Pdf.PdfOcrResult,System.Int32,System.Int32,Fluent.Pdf.PdfOcrOutputFormat,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes an explicit finite interchange profile at the borrowed current position; the caller stream stays open. Streaming failure may leave a prefix.

### <code>M:Fluent.Pdf.PdfOcrOutput.WriteFile(Fluent.Pdf.PdfOcrResult,System.Int32,System.Int32,Fluent.Pdf.PdfOcrOutputFormat,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Atomically replaces one explicit file only after complete serialization and cancellation checks. No partial interchange file is committed.

### <code>M:Fluent.Pdf.PdfOcrOverlayOptions.#ctor(Fluent.Pdf.PdfOcrExistingTextPolicy,System.Double,System.Int32,System.Int64)</code>

Captures overlap coverage required on both word rectangles, finite overlay count and total overlap-comparison work.

### <code>M:Fluent.Pdf.PdfOcrPageFrame.#ctor(Fluent.Pdf.PdfPageGeometry,System.Int32,System.Int32,System.Nullable{Fluent.Pdf.PdfRect})</code>

Maps top-left raster pixels onto an explicit displayed CropBox region, in physical points. Omitted region selects the entire rotated crop.

### <code>M:Fluent.Pdf.PdfOcrRaster.OpenPixels(Fluent.Pdf.PdfOperationContext)</code>

Opens a read-only borrowed pixel lease under the mandatory provider context. The lease expires with that operation and must be disposed before the provider returns.

### <code>M:Fluent.Pdf.PdfOcrRaster.Read(System.IO.Stream,System.Int32,System.Int32,System.Int32,Fluent.Pdf.PdfOcrPixelFormat,System.Int32,System.Int64,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures exactly height*stride bytes and one EOF probe at the borrowed current position; the caller stream stays open. No image decoder, native library or model is discovered.

### <code>M:Fluent.Pdf.PdfOcrResult.#ctor(System.String,Fluent.Pdf.PdfOcrWord[],System.String,System.String,System.String,System.Nullable{System.Double})</code>

Captures finite recognized text/words and exact provider/model provenance. Arrays are cloned and no caller raster is retained.

### <code>M:Fluent.Pdf.PdfOcrResult.WithModels(System.String,Fluent.Pdf.PdfOcrWord[],System.String,System.String,System.String,System.Nullable{System.Double},Fluent.Pdf.PdfOcrLanguageModel[])</code>

Captures recognized data with explicit selected model declarations, preserving the legacy constructor signature.

### <code>M:Fluent.Pdf.PdfOcrSearchable.Apply(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfOcrResult,Fluent.Pdf.PdfOcrPageFrame,Fluent.Pdf.PdfFontFallback,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOcrOverlayOptions,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Applies explicit recognition data/frame/fonts to one page under one parse/shape/embed/write context. Tagged sources require a separate explicit structure-association profile and reject here.

### <code>M:Fluent.Pdf.PdfOcrSearchable.RecognizePage(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfOcrOptions,Fluent.Pdf.PdfFontFallback,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfRenderOptions,Fluent.Pdf.PdfOcrOverlayOptions,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Renders, captures opaque pixels, recognizes, shapes and overlays one page under the same root context. Providers are explicit; requested integer isotropic DPI must be72–600. Original page appearance is retained.

### <code>M:Fluent.Pdf.PdfOcrWord.#ctor(System.String,Fluent.Pdf.PdfRect,System.Nullable{System.Double},System.Int32,System.Int32,System.Int32,System.Int32)</code>

Captures text, top-left raster-pixel bounds, confidence0–100 or null, and engine block/paragraph/line/word identities. No PDF-space transform or reading order is inferred.

### <code>M:Fluent.Pdf.PdfOperationContext.BorrowExpandedInput(System.IO.Stream)</code>

Wraps a readable decoder output in a borrowed lease charging the shared expanded-byte budget.

Remarks: The caller owns the decoder. The lease never closes it and expires with this operation. No codec is selected implicitly.

### <code>M:Fluent.Pdf.PdfOperationContext.Checkpoint</code>

Checks cancellation, deadline and previously latched failures. A provider cannot suppress a fatal policy failure.

### <code>M:Fluent.Pdf.PdfOperationContext.Consume(Fluent.Pdf.PdfResourceKind,System.Int64)</code>

Charges a nonnegative cumulative resource amount before allocation; Depth/Workers require scoped acquisition.

### <code>M:Fluent.Pdf.PdfOperationContext.ConsumePixels(System.Int64,System.Int64)</code>

Charges positive pixel dimensions before allocation, without allowing multiplication or cumulative overflow.

### <code>M:Fluent.Pdf.PdfOperationContext.EnterScope(Fluent.Pdf.PdfResourceKind)</code>

Acquires one Depth or Workers unit until the returned idempotent lease is disposed.

### <code>M:Fluent.Pdf.PdfOperationContext.EnterTimeLimit(System.TimeSpan)</code>

Temporarily tightens the shared cancellation/deadline to a bounded phase. Disposal restores only the remaining original/outer time; an expired phase latches failure permanently.

Remarks: Durations are 1 ms through one day; at most 128 simultaneous scopes are supported. Every scope must be disposed before the provider returns. All providers retain the same context and cancellation token.

### <code>M:Fluent.Pdf.PdfOperationContext.Report(Fluent.Pdf.PdfDiagnostic)</code>

Records a diagnostic with SDK-bound provenance. Strict losses and error severity latch a fatal failure.

### <code>M:Fluent.Pdf.PdfOperationPolicy.#ctor(Fluent.Pdf.PdfProcessingMode,Fluent.Pdf.PdfResourceLimits)</code>

Creates a policy; strict processing is the default.

### <code>M:Fluent.Pdf.PdfOptimization.Optimize(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOptimizationOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages, reparses and verifies the complete optimized PDF before returning owned bytes.

### <code>M:Fluent.Pdf.PdfOptimization.Write(Fluent.Pdf.PdfDocumentSnapshot,System.IO.Stream,Fluent.Pdf.PdfOptimizationOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages all transformations and validation before copying to a borrowed output.

### <code>M:Fluent.Pdf.PdfOptimization.WriteFile(Fluent.Pdf.PdfDocumentSnapshot,System.String,Fluent.Pdf.PdfOptimizationOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages all transformations and validation before atomically replacing a file.

### <code>M:Fluent.Pdf.PdfOptimizationOptions.#ctor(Fluent.Pdf.PdfOptimizationPreset,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Boolean,System.Int32)</code>

Captures bounded settings. CompactImages defaults to a maximum dimension of1600 samples; an explicit override is1–32768. Lossless requires dimension zero.

### <code>M:Fluent.Pdf.PdfOptimizationResult.OpenPdfStream</code>

Opens an independent read-only detached view of completed output.

### <code>M:Fluent.Pdf.PdfOptions.#ctor(System.Nullable{Fluent.Pdf.PdfPageSize},System.Int32,Fluent.Pdf.PdfOperationPolicy)</code>

Creates options with a valid default size and a positive page limit.

### <code>M:Fluent.Pdf.PdfOrganizationOptions.#ctor(System.Boolean,Fluent.Pdf.PdfMissingDestinationPolicy,Fluent.Pdf.PdfFieldCollisionPolicy,System.Int32)</code>

Captures finite page limits and explicit signature, navigation and form-field collision policies.

### <code>M:Fluent.Pdf.PdfPades.Upgrade(Fluent.Pdf.PdfSyntaxDocument,Fluent.Pdf.PdfPadesProfile,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages an explicit producer's output, checks exact original-prefix and DSS evidence retention, then requires a complete exact-profile validator report. Returns no unvalidated bytes.

### <code>M:Fluent.Pdf.PdfPadesValidation.#ctor(System.String,Fluent.Pdf.PdfPadesProfile,System.Boolean,System.Boolean)</code>

Captures a lowercase SHA256, exact profile and complete validation outcome.

### <code>M:Fluent.Pdf.PdfPage.ReadContent(Fluent.Pdf.PdfUnknownOperatorPolicy,System.String,System.Threading.CancellationToken)</code>

Reads original or replacement operators under the document's operation policy without executing document actions.

### <code>M:Fluent.Pdf.PdfPage.ReadFonts(System.Threading.CancellationToken)</code>

Inspects detached original/replacement font bindings under the document policy without executing document actions.

### <code>M:Fluent.Pdf.PdfPage.RemoveFont(System.String)</code>

Removes a replacement binding, retaining the original source binding if one exists.

### <code>M:Fluent.Pdf.PdfPage.RemoveResource(Fluent.Pdf.PdfGraphicsResourceCategory,System.String)</code>

Removes a replacement binding, retaining an original source binding when present.

### <code>M:Fluent.Pdf.PdfPage.SetFont(System.String,Fluent.Pdf.PdfFontResource)</code>

Registers or replaces an exact named font resource for subsequent saves.

### <code>M:Fluent.Pdf.PdfPage.SetResource(System.String,Fluent.Pdf.PdfGraphicsResource)</code>

Registers or replaces a named graphics resource in its standard category.

### <code>M:Fluent.Pdf.PdfPageBoxes.#ctor(Fluent.Pdf.PdfRect,System.Nullable{Fluent.Pdf.PdfRect},System.Nullable{Fluent.Pdf.PdfRect},System.Nullable{Fluent.Pdf.PdfRect},System.Nullable{Fluent.Pdf.PdfRect})</code>

Captures positive media and optional boxes within it. Each omitted secondary box defaults to Crop, as specified by PDF.

### <code>M:Fluent.Pdf.PdfPageBoxes.Equals(Fluent.Pdf.PdfPageBoxes)</code>

### <code>M:Fluent.Pdf.PdfPageBoxes.Equals(System.Object)</code>

### <code>M:Fluent.Pdf.PdfPageBoxes.GetHashCode</code>

### <code>M:Fluent.Pdf.PdfPageCollection.Add</code>

Adds a page using the document's default size.

### <code>M:Fluent.Pdf.PdfPageCollection.Add(Fluent.Pdf.PdfPageGeometry)</code>

Adds a page with explicit immutable boxes, UserUnit and rotation.

### <code>M:Fluent.Pdf.PdfPageCollection.Add(Fluent.Pdf.PdfPageSize)</code>

Adds a page with a valid size, within the document's maximum page count.

### <code>M:Fluent.Pdf.PdfPageCollection.Clear</code>

Removes and invalidates all pages.

### <code>M:Fluent.Pdf.PdfPageCollection.GetEnumerator</code>

Enumerates attached pages; edits invalidate enumeration and disposal invalidates further moves.

### <code>M:Fluent.Pdf.PdfPageCollection.Move(System.Int32,System.Int32)</code>

Moves a page to its final zero-based index while retaining its identity.

### <code>M:Fluent.Pdf.PdfPageCollection.RemoveAt(System.Int32)</code>

Removes a page and invalidates previously retained references to that page.

### <code>M:Fluent.Pdf.PdfPageDecoration.Flow(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowBlock})</code>

Creates a decoration from bounded immutable flow blocks.

### <code>M:Fluent.Pdf.PdfPageDecoration.Html(System.String,Fluent.Pdf.IPdfTemplateHtmlProvider)</code>

Creates an HTML decoration requiring an explicit optional HTML provider.

### <code>M:Fluent.Pdf.PdfPageDecoration.Vector(Fluent.Pdf.PdfDrawing,Fluent.Pdf.PdfRect)</code>

Creates a fitted vector or image drawing decoration with explicit source bounds.

### <code>M:Fluent.Pdf.PdfPageFont.DecodeText(Fluent.Pdf.PdfValue,System.Collections.Generic.IReadOnlyDictionary{System.String,Fluent.Pdf.PdfUnicodeMap},Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Decodes encoded glyph bytes using explicit ToUnicode, otherwise an explicitly configured simple-font encoding provider.

### <code>M:Fluent.Pdf.PdfPageFont.ExtractProgram(Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Extracts exact decoded binary font bytes under a fresh budget. Standard and Type 3 fonts have no external program.

### <code>M:Fluent.Pdf.PdfPageFont.Load(Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Loads the exact embedded font through an explicit font provider, or returns the already loaded authored face.

### <code>M:Fluent.Pdf.PdfPageFont.ReadToUnicode(System.Collections.Generic.IReadOnlyDictionary{System.String,Fluent.Pdf.PdfUnicodeMap},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads the explicit ToUnicode stream. Named inherited CMaps must be supplied by the caller.

### <code>M:Fluent.Pdf.PdfPageGeometry.#ctor(Fluent.Pdf.PdfPageBoxes,Fluent.Pdf.PdfPageRotation,System.Double)</code>

Captures page geometry. UserUnit is positive and at most 75000 physical points per native PDF unit.

### <code>M:Fluent.Pdf.PdfPageGeometry.DisplayBounds(Fluent.Pdf.PdfPageBox)</code>

Gets rotated displayed extents in physical points. An empty crop can have zero extent.

### <code>M:Fluent.Pdf.PdfPageGeometry.DisplayToUser(Fluent.Pdf.PdfPageBox)</code>

Returns displayed top-left point coordinates to native PDF user coordinates.

### <code>M:Fluent.Pdf.PdfPageGeometry.Equals(Fluent.Pdf.PdfPageGeometry)</code>

### <code>M:Fluent.Pdf.PdfPageGeometry.Equals(System.Object)</code>

### <code>M:Fluent.Pdf.PdfPageGeometry.GetHashCode</code>

### <code>M:Fluent.Pdf.PdfPageGeometry.Resize(Fluent.Pdf.PdfPageSize)</code>

Resizes MediaBox and proportionally maps every secondary box, retaining origin, rotation and UserUnit.

### <code>M:Fluent.Pdf.PdfPageGeometry.UserToDisplay(Fluent.Pdf.PdfPageBox)</code>

Returns raw PDF user coordinates to displayed top-left coordinates in physical points for the selected box.

### <code>M:Fluent.Pdf.PdfPageGeometry.WithRotation(Fluent.Pdf.PdfPageRotation)</code>

Returns the same boxes and scale with another explicit rotation.

### <code>M:Fluent.Pdf.PdfPageGraphics.ExtractImage(System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Extracts typed image samples, masks and owned display metadata without decoding or recompressing their bytes. Images whose geometry is only inside a codec require an explicit adapter.

### <code>M:Fluent.Pdf.PdfPageGraphics.ExtractResource(System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Extracts an invocation as a detached reusable resource, preserving encoded streams and effective resource scopes. Owner-dependent structure/optional-content graphs are rejected.

### <code>M:Fluent.Pdf.PdfPageGraphics.Inspect(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Walks actually invoked content and scoped Form resources under one recursion and work budget.

### <code>M:Fluent.Pdf.PdfPageGraphics.Remove(System.Int32,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Removes exactly one captured image or form invocation, cloning the selected enclosing form path. This is content editing, not secure redaction.

### <code>M:Fluent.Pdf.PdfPageGraphics.ReplaceImage(System.Int32,Fluent.Pdf.PdfImageAsset,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Replaces one image invocation within its original unit-square frame, applying the replacement's orientation explicitly without recompressing samples.

### <code>M:Fluent.Pdf.PdfPageGraphics.ReplaceResource(System.Int32,Fluent.Pdf.PdfGraphicsResource,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Replaces exactly one captured invocation with a detached XObject, retaining its surrounding transform and clip. Form matrices/bounds remain explicit.

### <code>M:Fluent.Pdf.PdfPageImposition.Booklet(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfPageSize,System.Nullable{Fluent.Pdf.PdfMargins},System.Double,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Pads to a multiple of four and places outer/inner front and back pairs for a left-bound booklet.

### <code>M:Fluent.Pdf.PdfPageImposition.NUp(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfPageSize,System.Int32,System.Int32,System.Nullable{Fluent.Pdf.PdfMargins},System.Double,Fluent.Pdf.PdfCanvasFit,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Fits source pages into physical row-major sheet cells. Logical structure remains in source reading order.

### <code>M:Fluent.Pdf.PdfPageImposition.Tile(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfPageSize,System.Nullable{Fluent.Pdf.PdfMargins},System.Double,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Tiles displayed source pages at physical size with explicit overlap. Interactive or structured duplicates require an additional copy profile.

### <code>M:Fluent.Pdf.PdfPageInput.#ctor(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfPageSelection)</code>

Captures a snapshot and optional selection; null selects all pages in source order.

### <code>M:Fluent.Pdf.PdfPageNumberRange.#ctor(System.Int32,Fluent.Pdf.PdfPageLabelStyle,System.Int32,System.String)</code>

Creates a page-number restart at a zero-based physical page.

### <code>M:Fluent.Pdf.PdfPageNumbering.#ctor(Fluent.Pdf.PdfPageNumberRange[])</code>

Creates ordered numbering ranges; an empty array selects decimal numbering.

### <code>M:Fluent.Pdf.PdfPageNumbering.GetLabel(System.Int32)</code>

Formats the label for a zero-based physical page using invariant numbering.

### <code>M:Fluent.Pdf.PdfPageOrganization.Delete(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfPageSelection,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Deletes the selected physical pages, rejecting an empty result and applying the explicit omitted-destination policy.

### <code>M:Fluent.Pdf.PdfPageOrganization.Extract(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfPageSelection,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Extracts or reorders selected pages as an independent document.

### <code>M:Fluent.Pdf.PdfPageOrganization.Insert(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfPageInput,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inserts a detached source selection before a zero-based host index, preserving navigation through shared-context import and reordering.

### <code>M:Fluent.Pdf.PdfPageOrganization.Merge(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfPageInput},Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Imports ordered selections into one independent document with remapped object identities.

### <code>M:Fluent.Pdf.PdfPageOrganization.Scale(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfPageSelection,System.Double,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Scales all physical page coordinates and annotations uniformly by changing UserUnit while retaining native boxes, content, structure and rotation.

### <code>M:Fluent.Pdf.PdfPageOrganization.Split(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfPageSelection},Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Splits detached selections under one cumulative operation. The caller owns every returned document.

### <code>M:Fluent.Pdf.PdfPageOrganization.Transform(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfPageSelection,System.Func{Fluent.Pdf.PdfPageGeometry,Fluent.Pdf.PdfPageGeometry},Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Changes selected immutable page geometry without changing native content coordinates. Rotate, crop and resize are explicit geometry operations.

### <code>M:Fluent.Pdf.PdfPageSelection.Parse(System.String,System.Int32,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Parses one-based numbers, inclusive ascending/descending ranges, last, all, odd and even. Commas preserve order and duplicates.

### <code>M:Fluent.Pdf.PdfPageSize.#ctor(System.Double,System.Double)</code>

Creates a size with finite positive dimensions in points.

### <code>M:Fluent.Pdf.PdfPageSize.Equals(Fluent.Pdf.PdfPageSize)</code>

### <code>M:Fluent.Pdf.PdfPageSize.Equals(System.Object)</code>

### <code>M:Fluent.Pdf.PdfPageSize.GetHashCode</code>

### <code>M:Fluent.Pdf.PdfPageSize.op_Equality(Fluent.Pdf.PdfPageSize,Fluent.Pdf.PdfPageSize)</code>

Compares dimensions for exact equality.

### <code>M:Fluent.Pdf.PdfPageSize.op_Inequality(Fluent.Pdf.PdfPageSize,Fluent.Pdf.PdfPageSize)</code>

Compares dimensions for inequality.

### <code>M:Fluent.Pdf.PdfPageTemplate.#ctor(System.Double,System.Double,System.Double,System.Func{Fluent.Pdf.PdfPageTemplateContext,Fluent.Pdf.PdfPageDecoration},System.Func{Fluent.Pdf.PdfPageTemplateContext,Fluent.Pdf.PdfPageDecoration},Fluent.Pdf.PdfPageNumbering,System.Int32)</code>

Creates fixed header/footer reservations and synchronous final-page callbacks.

### <code>M:Fluent.Pdf.PdfPageTemplateResult.CreateDocument(Fluent.Pdf.PdfOptions,System.Threading.CancellationToken)</code>

Creates a document with final decorations, page labels and internal locator links under a new bounded operation.

### <code>M:Fluent.Pdf.PdfPageTransition.#ctor(Fluent.Pdf.PdfTransitionStyle,System.Double,System.Nullable{Fluent.Pdf.PdfTransitionDimension},System.Nullable{Fluent.Pdf.PdfTransitionMotion},System.Nullable{System.Int32},System.Nullable{System.Double},System.Nullable{System.Boolean})</code>

Creates a finite standard transition. Direction -1 denotes nondirectional Fly. Optional parameters must apply to the chosen style.

### <code>M:Fluent.Pdf.PdfPaint.PatternFill(Fluent.Pdf.PdfPattern,Fluent.Pdf.PdfColor)</code>

Captures a pattern and its required base color for an uncolored cell.

### <code>M:Fluent.Pdf.PdfPaint.Solid(Fluent.Pdf.PdfColor)</code>

Captures a non-pattern solid color.

### <code>M:Fluent.Pdf.PdfPassword.#ctor(System.Byte[])</code>

Captures at most 4096 prepared bytes. The caller retains ownership of the input array.

### <code>M:Fluent.Pdf.PdfPassword.Dispose</code>

Clears the owned bytes. Already acquired per-operation copies are independently cleared by their operations.

### <code>M:Fluent.Pdf.PdfPasswordProtection.#ctor(Fluent.Pdf.PdfPassword,Fluent.Pdf.PdfPassword,Fluent.Pdf.PdfDocumentPermissions,Fluent.Pdf.PdfPasswordCipher,System.Boolean)</code>

Captures explicit user/owner credentials and permissions. AES-256 revision 6 is the default; an empty user password intentionally allows opening without a secret.

### <code>M:Fluent.Pdf.PdfPath.Contains(Fluent.Pdf.PdfPoint,System.Boolean,System.Double,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Tests the implicitly closed fill region using nonzero/even-odd winding and explicit absolute boundary tolerance.

### <code>M:Fluent.Pdf.PdfPath.Create(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfPathSegment},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a validated vector path under a fresh operation budget.

### <code>M:Fluent.Pdf.PdfPath.Edit(System.Int32,System.Int32,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfPathSegment},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Returns a separately validated path with a logical segment range replaced.

### <code>M:Fluent.Pdf.PdfPath.GetBounds(Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Computes curve extrema and closed-path bounds under a fresh bounded geometry operation.

### <code>M:Fluent.Pdf.PdfPathSegment.Close</code>

Closes the current subpath.

### <code>M:Fluent.Pdf.PdfPathSegment.Curve(System.Double,System.Double,System.Double,System.Double,System.Double,System.Double)</code>

Adds a cubic Bézier segment.

### <code>M:Fluent.Pdf.PdfPathSegment.Line(System.Double,System.Double)</code>

Adds a straight segment.

### <code>M:Fluent.Pdf.PdfPathSegment.Move(System.Double,System.Double)</code>

Starts a subpath.

### <code>M:Fluent.Pdf.PdfPathSegment.Rectangle(Fluent.Pdf.PdfRect)</code>

Adds a closed rectangle.

### <code>M:Fluent.Pdf.PdfPattern.Shading(Fluent.Pdf.PdfShading,System.Nullable{Fluent.Pdf.PdfMatrix},Fluent.Pdf.PdfGraphicsState,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a type-2 shading pattern with an optional exact compositing state and transform.

### <code>M:Fluent.Pdf.PdfPattern.Tiling(Fluent.Pdf.PdfDrawing,Fluent.Pdf.PdfRect,System.Double,System.Double,System.Boolean,System.Int32,System.Nullable{Fluent.Pdf.PdfMatrix},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a type-1 tiling pattern with explicit cell bounds, steps, tiling policy and pattern-space transform.

### <code>M:Fluent.Pdf.PdfPoint.#ctor(System.Double,System.Double)</code>

Creates finite coordinates.

### <code>M:Fluent.Pdf.PdfPreflight.Apply(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfRemediationPlan,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Applies an explicitly selected plan only to matching source bytes. All fixes require permissive declared-loss authority; stages and verifies output before caller writes. Original bytes/history are not mutated.

### <code>M:Fluent.Pdf.PdfPreflight.ApplyFile(System.IO.Stream,System.String,Fluent.Pdf.PdfRemediationPlan,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages full rewrite and post-fix verification before atomic destination replacement.

### <code>M:Fluent.Pdf.PdfPreflight.Inspect(System.IO.Stream,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures at most64 MiB and inspects actions, font embedding/mapping, image geometry, signatures and explicit capability declarations without mutation or payload execution. Complete standard conformance remains unvalidated.

### <code>M:Fluent.Pdf.PdfPreflight.WriteAudit(Fluent.Pdf.PdfRemediationAudit,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes stable schema1 audit JSON with before/after hashes, findings, selected fixes, affected object locations and declared loss descriptions.

### <code>M:Fluent.Pdf.PdfPreflight.WriteReport(Fluent.Pdf.PdfPreflightReport,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes stable schema1 JSON with exact source binding and all findings; reports never claim complete standard compliance.

### <code>M:Fluent.Pdf.PdfPresentation.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inspects source outlines, labels, catalog settings and page transitions; unsupported settings are retained with warnings.

### <code>M:Fluent.Pdf.PdfPresentation.SetInitialView(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfDestination,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets or clears a standard catalog OpenAction destination array. No action runs.

### <code>M:Fluent.Pdf.PdfPresentation.SetOutlines(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfBookmark},Fluent.Pdf.PdfActionPolicy,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Replaces the outline tree with a finite ordered batch. An empty batch removes outlines.

### <code>M:Fluent.Pdf.PdfPresentation.SetPageLabels(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfPageNumbering,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets or clears standard physical-page label ranges. Every supplied range must start within this document.

### <code>M:Fluent.Pdf.PdfPresentation.SetPageTransition(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfPageTransition,System.Nullable{System.Double},Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets or clears a page transition and Dur value. Null settings remove the respective source keys; durations are finite seconds in 0..86400.

### <code>M:Fluent.Pdf.PdfPresentation.SetView(Fluent.Pdf.PdfDocumentSnapshot,System.Nullable{Fluent.Pdf.PdfPageLayout},System.Nullable{Fluent.Pdf.PdfPageMode},Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sets catalog layout and mode. Null values retain the corresponding source setting.

### <code>M:Fluent.Pdf.PdfPresentation.SetViewerPreferences(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfValue,System.Boolean,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Patches standard ViewerPreferences, or replaces the dictionary explicitly. Unknown supplied keys/values fail; preserved source keys remain inspectable.

### <code>M:Fluent.Pdf.PdfPrintJob.#ctor(System.String,System.Int32,Fluent.Pdf.PdfPrintJobState)</code>

Captures exact printer/job identity and an observed state.

### <code>M:Fluent.Pdf.PdfPrintOptions.#ctor(System.String,System.Int32,Fluent.Pdf.PdfPrintDuplex,Fluent.Pdf.PdfPrintScaling,System.Int32,System.Int32,System.Int32)</code>

Selects a printer explicitly. Last page zero means the captured document's final page. N-up supports 1, 2, 4, 6, 9 or 16.

### <code>M:Fluent.Pdf.PdfPrinting.Cancel(System.String,System.Int32,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Cancels only an exact caller-selected printer/job.

### <code>M:Fluent.Pdf.PdfPrinting.GetStatus(System.String,System.Int32,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Queries only an exact caller-selected printer/job.

### <code>M:Fluent.Pdf.PdfPrinting.Submit(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfPrintOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Serializes completely before the one explicit spool submission. A cancellation/transport failure after submission may leave an accepted job; use its spool identity where available and never blindly retry.

### <code>M:Fluent.Pdf.PdfProcessingJob.ReadDocument</code>

Reads the input once into the empty private document through its explicit registered reader.

### <code>M:Fluent.Pdf.PdfProcessingJob.Save(System.IO.Stream,Fluent.Pdf.PdfWriteOptions)</code>

Writes using the shared budget, preserving caller stream ownership. Stream output can be partial on failure.

### <code>M:Fluent.Pdf.PdfProcessingJob.SaveFile(System.String,Fluent.Pdf.PdfWriteOptions)</code>

Atomically writes a file using the shared budget. Failed/canceled writes preserve any prior destination.

### <code>M:Fluent.Pdf.PdfProcessingOptions.#ctor(System.Int32,System.Int32,System.Int64)</code>

Creates bounded admission limits. Per-job operation budgets remain independently mandatory.

### <code>M:Fluent.Pdf.PdfProcessingPool.#ctor(Fluent.Pdf.PdfProcessingOptions,Fluent.Pdf.PdfOptions,Fluent.Pdf.PdfProviderRegistry)</code>

Starts a fixed number of background workers. Registry providers must support independent concurrent operations or be separately instantiated per pool.

### <code>M:Fluent.Pdf.PdfProcessingPool.CompleteAsync</code>

Stops admission and drains existing work without canceling it.

### <code>M:Fluent.Pdf.PdfProcessingPool.Dispose</code>

Requests cancellation without blocking the calling thread. Await Completion to observe finished cleanup.

### <code>M:Fluent.Pdf.PdfProcessingPool.ShutdownAsync</code>

Stops admission, cancels waiting and running jobs, and returns completion of all callback/provider cleanup.

### <code>M:Fluent.Pdf.PdfProcessingPool.TrySubmit(System.Byte[],System.Action{Fluent.Pdf.PdfProcessingJob},System.Threading.Tasks.Task{Fluent.Pdf.PdfOperationReport}@,System.Threading.CancellationToken)</code>

Copies input and admits work only when both queue and resident-byte capacity are available; otherwise returns false without copying or creating a waiter.

Remarks: The caller must not mutate input during this call. On false, completion is null; retry after an admitted task completes. A stopped pool rejects admission. Exceptions from the callback are retained by its task.

### <code>M:Fluent.Pdf.PdfProfileConversions.Convert(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfProfileOptions,Fluent.Pdf.IPdfProfileProducer,Fluent.Pdf.IPdfProfileValidator,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages producer output, checks page geometry/ICC/condition and requires complete independent exact-byte validation before caller writes. Missing, stale, incomplete or failed validation cannot expose a candidate.

### <code>M:Fluent.Pdf.PdfProfileConversions.Validate(System.IO.Stream,Fluent.Pdf.PdfProfileOptions,Fluent.Pdf.IPdfProfileValidator,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Validates exact nonempty source PDF with explicit ICC/condition settings. Profile availability is checked before source capture.

### <code>M:Fluent.Pdf.PdfProfileOptions.#ctor(Fluent.Pdf.PdfConformanceProfile,Fluent.Pdf.PdfColorSpace,System.String,System.Boolean,System.Boolean)</code>

Requires a complete caller-supplied ICC output space and literal print condition for PDF/X. PDF/X-1a requires a CMYK output intent. No profile/font/engine discovery occurs.

### <code>M:Fluent.Pdf.PdfProfileValidation.#ctor(Fluent.Pdf.PdfConformanceProfile,System.String,System.String,System.Boolean,System.Boolean,System.Collections.Generic.IEnumerable{System.String})</code>

Captures bounded stable rule findings. A compliant observation must be complete with zero findings. Fixtures/providers alone do not qualify production support.

### <code>M:Fluent.Pdf.PdfProgressiveDocument.#ctor(Fluent.Pdf.IPdfRangeSource,Fluent.Pdf.PdfProgressiveOptions,Fluent.Pdf.PdfOperationPolicy)</code>

Creates a session without reading or taking ownership of the source. Every operation rechecks source authorization and revision, including warm cache operations.

### <code>M:Fluent.Pdf.PdfProgressiveDocument.DecodeStream(Fluent.Pdf.PdfObjectId,Fluent.Pdf.PdfProviderRegistry,System.Threading.CancellationToken)</code>

Decodes one stream and its declared filter dependencies under the same range/read/decode budget, returning detached bytes.

### <code>M:Fluent.Pdf.PdfProgressiveDocument.Dispose</code>

Clears cached blocks without disposing the caller's source. Concurrent operations/disposal are rejected.

### <code>M:Fluent.Pdf.PdfProgressiveDocument.InspectLinearization(System.Threading.CancellationToken)</code>

Inspects the first linearization dictionary through ranges. Its bounds are observations, not complete hint-table validation.

### <code>M:Fluent.Pdf.PdfProgressiveDocument.ReadObject(Fluent.Pdf.PdfObjectId,System.Threading.CancellationToken)</code>

Reads one indirect object lazily, retaining references to dependencies. Raw stream bytes are captured into a detached immutable store before returning.

### <code>M:Fluent.Pdf.PdfProgressiveDocument.ReadPage(System.Int32,System.Threading.CancellationToken)</code>

Reads only the indexes, catalog and page-tree paths needed for this page. Resources and content payloads are fetched only when separately requested. Declared subtree counts guide traversal; this is not full document validation.

### <code>M:Fluent.Pdf.PdfProgressiveOptions.#ctor(System.Int32,System.Int32)</code>

Captures block sizes of1024–1048576 bytes and a cache of at least one block and at most64MiB.

### <code>M:Fluent.Pdf.PdfProviderDescriptor.#ctor(System.String,System.String,Fluent.Pdf.PdfCapability[])</code>

Creates a descriptor with identifiers of at most 128 characters and a unique, nonempty capability list.

### <code>M:Fluent.Pdf.PdfProviderRegistry.#ctor(Fluent.Pdf.IPdfProvider[])</code>

Captures provider identities/capabilities; the supplied array is not retained.

### <code>M:Fluent.Pdf.PdfProviderRegistry.GetRequired``1(Fluent.Pdf.PdfCapability)</code>

Resolves a declared capability and required interface, or throws a structured failure.

The capability has no registered provider.

### <code>M:Fluent.Pdf.PdfProviderRegistry.Supports(Fluent.Pdf.PdfCapability)</code>

Reports whether a capability has an explicitly registered provider.

### <code>M:Fluent.Pdf.PdfProviderUnavailableException.#ctor(Fluent.Pdf.PdfCapability)</code>

Creates the stable Provider.Unavailable failure for a missing capability.

### <code>M:Fluent.Pdf.PdfQuad.#ctor(Fluent.Pdf.PdfPoint,Fluent.Pdf.PdfPoint,Fluent.Pdf.PdfPoint,Fluent.Pdf.PdfPoint)</code>

Captures finite corners in perimeter order and rejects a nonconvex ordering.

### <code>M:Fluent.Pdf.PdfQuad.Contains(Fluent.Pdf.PdfPoint)</code>

Tests the actual convex frame including its boundary; a degenerate frame has no area.

### <code>M:Fluent.Pdf.PdfRangeIdentity.#ctor(System.Int64,System.String)</code>

Captures a positive byte length and a bounded opaque version. Providers must change the version whenever any byte changes.

### <code>M:Fluent.Pdf.PdfRangeResponse.#ctor(System.Int64,System.Int64,Fluent.Pdf.PdfRangeIdentity,System.IO.Stream)</code>

Captures exact response bounds, source identity and a readable body. The body must contain precisely length bytes.

### <code>M:Fluent.Pdf.PdfRangeResponse.Dispose</code>

Closes the owned response body.

### <code>M:Fluent.Pdf.PdfRaster.OpenSamples</code>

Opens an independent read-only sample view without copying the raster.

### <code>M:Fluent.Pdf.PdfRasterDocuments.Create(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfRasterInput},Fluent.Pdf.PdfRasterImportOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates an owned editable document after every selected frame succeeds. Codec availability is checked before reading any input.

### <code>M:Fluent.Pdf.PdfRasterDocuments.Write(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfRasterInput},System.IO.Stream,Fluent.Pdf.PdfRasterImportOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages every selected frame and complete PDF before copying to a borrowed output.

### <code>M:Fluent.Pdf.PdfRasterDocuments.WriteFile(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfRasterInput},System.String,Fluent.Pdf.PdfRasterImportOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages conversion before atomic destination replacement.

### <code>M:Fluent.Pdf.PdfRasterExport.Write(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},System.IO.Stream,Fluent.Pdf.PdfRasterExportOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages all selected pages in exact caller order and writes only after every frame succeeds. Caller streams stay open. PNG/BMP require exactly one page; TIFF supports up to65535 pages, including repeated pages.

### <code>M:Fluent.Pdf.PdfRasterExport.WriteFile(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},System.String,Fluent.Pdf.PdfRasterExportOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Stages complete raster output before atomic file replacement.

### <code>M:Fluent.Pdf.PdfRasterExportOptions.#ctor(Fluent.Pdf.PdfRasterFormat,Fluent.Pdf.PdfRenderOptions,Fluent.Pdf.PdfColorRenderOptions)</code>

Captures an exact supported container and rendering policy. Optional ICC output requires a color-managed renderer and is embedded in PNG/TIFF; BMP ICC output rejects.

### <code>M:Fluent.Pdf.PdfRasterFileInfo.#ctor(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfRasterFrameInfo},System.Boolean)</code>

Captures one to4096 complete frame descriptors and explicit animation classification.

### <code>M:Fluent.Pdf.PdfRasterFrameInfo.#ctor(System.Int32,System.Int32,Fluent.Pdf.PdfImageMetadata,Fluent.Pdf.PdfColorSpace)</code>

Captures positive geometry and RGB/CalRGB/three-channel ICC color; samples must be complete decoded canvas frames, not animation deltas.

### <code>M:Fluent.Pdf.PdfRasterImportOptions.#ctor(System.Nullable{Fluent.Pdf.PdfPageSize},System.Double,System.Double,System.Int32,Fluent.Pdf.PdfImageReadOptions)</code>

Captures natural-density pages or an exact fixed page with contain fitting. Unknown density uses the explicit fallback DPI. Alpha is preserved.

### <code>M:Fluent.Pdf.PdfRasterInput.#ctor(System.IO.Stream,Fluent.Pdf.PdfRasterFileFormat,System.Collections.Generic.IEnumerable{System.Int32},System.Boolean)</code>

Captures one readable file, one to4096 frame indices and explicit animation-frame authority. No file is read here.

### <code>M:Fluent.Pdf.PdfRect.#ctor(System.Double,System.Double,System.Double,System.Double)</code>

Creates a rectangle from lower-left origin and nonnegative extent.

### <code>M:Fluent.Pdf.PdfRedaction.RasterRewrite(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfRedactionRegion},Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfRedactionRasterOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a fresh serialized PDF using only explicitly rendered/masked RGB samples. All original object programs, hidden data, metadata, attachments, actions and revisions are discarded. Every page is rasterized; original editable text/vector/structure are not retained.

### <code>M:Fluent.Pdf.PdfRedaction.SanitizeRasterRewrite(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfRedactionRasterOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Sanitizes all original object/history data using a fresh raster-only graph with no removal rectangles. Only configured visible crop pixels are adopted; this is an explicit flattening profile.

### <code>M:Fluent.Pdf.PdfRedaction.WriteRasterRewrite(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfRedactionRegion},System.IO.Stream,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfRedactionRasterOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Builds, validates and writes a fresh complete PDF under one context to an empty seekable borrowed destination. Renderer failures write no output; copy failures can leave only a sanitized prefix.

### <code>M:Fluent.Pdf.PdfRedaction.WriteRasterRewriteFile(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfRedactionRegion},System.String,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfRedactionRasterOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Builds a full sanitized PDF privately and atomically replaces the explicit destination only after complete rendering, masking and validation.

### <code>M:Fluent.Pdf.PdfRedactionRasterOptions.#ctor(System.Int32,System.UInt32,System.UInt32,System.Int32,System.Boolean,System.Boolean,System.Int32,System.Int64,System.Int64)</code>

Captures explicit DPI, mask/matte colors, outward pixel padding, annotation appearance policy, signature invalidation authority and finite work/extent limits. Output normalizes each visible crop to rotation0/UserUnit1.

### <code>M:Fluent.Pdf.PdfRedactionRegion.#ctor(System.Int32,Fluent.Pdf.PdfRect)</code>

Captures a zero-based page and positive displayed rectangle. No document text or identifier is retained in the removal audit.

### <code>M:Fluent.Pdf.PdfRedactionResult.OpenPdfStream</code>

Opens a detached read-only view of the exact fresh PDF, containing no original graph/history.

### <code>M:Fluent.Pdf.PdfRedactionResult.Save(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Copies exact sanitized bytes to an empty seekable borrowed destination. Nonempty/prefixed sinks reject so old tails or revisions cannot survive this API.

### <code>M:Fluent.Pdf.PdfRedactionResult.SaveFile(System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Atomically replaces a file with the exact fresh sanitized PDF. Filesystem storage/backup erasure is outside PDF full-rewrite semantics.

### <code>M:Fluent.Pdf.PdfRemediationPlan.#ctor(Fluent.Pdf.PdfPreflightReport,System.Collections.Generic.IEnumerable{System.String},System.Boolean)</code>

Selects the implemented RemoveBlockedActions fix only when present in the source report. It removes all actions/content blocked by the documented default action-retention policy; it is not secure erasure. Signature invalidation needs separate explicit authority and permissive loss policy.

### <code>M:Fluent.Pdf.PdfRenderOptions.#ctor(System.Double,System.Double,System.Nullable{Fluent.Pdf.PdfRect},System.Nullable{System.UInt32},Fluent.Pdf.PdfRasterColor,System.Boolean,System.Boolean)</code>

Captures requested DPI and an optional top-left physical-point region. Null background requests straight alpha; an RGB background is opaque.

### <code>M:Fluent.Pdf.PdfRendering.RenderPage(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfRenderOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Renders an immutable snapshot using an explicitly registered provider; no desktop or runtime download is implied.

### <code>M:Fluent.Pdf.PdfRepair.Recover(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfRepairOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Rebuilds one classic-index revision using recovered active/free identities or a complete explicit caller inventory. Exact stream overrides are explicit; duplicate histories, compressed-index and encrypted profiles fail closed.

### <code>M:Fluent.Pdf.PdfRepairOptions.#ctor(System.Nullable{Fluent.Pdf.PdfObjectId},Fluent.Pdf.PdfRepairOrphanPolicy,Fluent.Pdf.PdfRepairStreamLength[],Fluent.Pdf.PdfObjectId[],System.Boolean,System.Boolean)</code>

Captures root/orphan/length/signature choices. ActiveObjects, when supplied, is a complete caller-authorized identity inventory used when xref status rows cannot establish active objects; source bodies are never automatically resurrected.

### <code>M:Fluent.Pdf.PdfRepairResult.OpenOriginalStream</code>

Opens the exact original bytes, independently of caller stream lifetime.

### <code>M:Fluent.Pdf.PdfRepairResult.OpenRepairedStream</code>

Opens the exact reconstructed bytes, independently of caller output lifetime.

### <code>M:Fluent.Pdf.PdfRepairResult.WriteLedger(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes a versioned UTF-8 XML ledger to borrowed output, including exact source/output digests.

### <code>M:Fluent.Pdf.PdfRepairStreamLength.#ctor(Fluent.Pdf.PdfObjectId,System.Int64)</code>

Captures an exact object identity and nonnegative payload byte count.

### <code>M:Fluent.Pdf.PdfResourceBinding.#ctor(System.String,Fluent.Pdf.PdfGraphicsResource)</code>

Captures a PDF name and immutable graphics resource.

### <code>M:Fluent.Pdf.PdfResourceExtraction.ReadPage(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfUnknownOperatorPolicy,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures declared resources separately from actual uses, literal paths and explicit artifacts under one shared context. Unknown resource roles remain raw; unknown operators require explicit preservation.

### <code>M:Fluent.Pdf.PdfResourceLimits.#ctor(System.Int64,System.Int64,System.Int64,System.Int64,System.Int64,System.Int32,System.Int32,System.Int32,System.Nullable{System.TimeSpan})</code>

Creates finite limits. Elapsed time must be between one millisecond and one day.

### <code>M:Fluent.Pdf.PdfResourceSet.#ctor(Fluent.Pdf.PdfResourceBinding[])</code>

Captures at most 4096 explicit bindings; no global resource lookup occurs.

### <code>M:Fluent.Pdf.PdfScanDewarpGrid.#ctor(System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,Fluent.Pdf.PdfPoint[])</code>

Captures2–64 control columns/rows over a regular output grid. Source points are row-major edge coordinates within the exact original raster. No curve/model is inferred.

### <code>M:Fluent.Pdf.PdfScanDewarpGrid.MapPoint(Fluent.Pdf.PdfPoint)</code>

Maps a point in the explicit output extent to original source coordinates by bilinear interpolation.

### <code>M:Fluent.Pdf.PdfScanGeometry.MapPoint(Fluent.Pdf.PdfPoint)</code>

Maps one pixel-edge point to the original raster. White deskew padding can map outside the original; nonlinear maps reject points outside their explicit domain.

### <code>M:Fluent.Pdf.PdfScanGeometry.MapWord(Fluent.Pdf.PdfOcrWord,System.Double,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Maps a recognized processed word to original geometry. Nonlinear boundaries split at grid crossings and bound quadratic chord error. Words outside original coverage reject rather than clamp silently.

### <code>M:Fluent.Pdf.PdfScanLayout.Analyze(Fluent.Pdf.PdfOcrResult,System.Int32,System.Int32,Fluent.Pdf.PdfScanLayoutOptions,Fluent.Pdf.PdfOcrRaster,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Analyzes immutable words, explicit table/form hints and optional exact Gray8 pixels. Raw order is preserved separately; confidence is declared/heuristic rather than correctness.

### <code>M:Fluent.Pdf.PdfScanLayoutOptions.#ctor(System.Boolean,System.Boolean,System.Boolean,System.Double,System.Double,System.Double,System.Int32,System.Int32,System.Int32,System.Int32,System.Int64,Fluent.Pdf.PdfScanRegionHint[])</code>

Captures explicit column/gap/line/work limits. Optional Gray8 raster analysis identifies closed grids without inventing table/form semantics.

### <code>M:Fluent.Pdf.PdfScanPreprocessing.Process(Fluent.Pdf.PdfOcrRaster,Fluent.Pdf.PdfScanPreprocessingOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOcrOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Processes explicit raster samples. Gray conversion uses integer77R+150G+29B luma; resampling is deterministic nearest neighbor with white padding. Automatic orientation requires explicit OCR configuration, without implicit models.

### <code>M:Fluent.Pdf.PdfScanPreprocessing.Recognize(Fluent.Pdf.PdfOcrRaster,Fluent.Pdf.PdfScanPreprocessingOptions,Fluent.Pdf.PdfOcrOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfScanLayoutOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Preprocesses, recognizes and analyzes layout under one root context. Explicit OCR configuration also supplies any requested orientation observations.

### <code>M:Fluent.Pdf.PdfScanPreprocessingOptions.#ctor(Fluent.Pdf.PdfScanOrientation,System.Double,System.Boolean,System.Double,System.Double,System.Int32,System.Boolean,System.Byte,System.Byte,System.Nullable{System.Byte},Fluent.Pdf.PdfScanDewarpGrid,System.Double,System.Double,System.Int64)</code>

Captures orientation, explicit or measured skew,3x3 median denoise, linear gray contrast, optional threshold/dewarp and finite pixel visits. Automatic orientation requires explicit OCR providers/options.

### <code>M:Fluent.Pdf.PdfScanRegionHint.#ctor(Fluent.Pdf.PdfScanRegionKind,Fluent.Pdf.PdfRect,System.Double)</code>

Captures explicit table/form bounds and caller-declared confidence; this is not an SDK correctness estimate.

### <code>M:Fluent.Pdf.PdfScripts.EvaluateCalculation(System.String,System.Collections.Generic.IReadOnlyDictionary{System.String,System.Decimal},System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Evaluates only event.value = decimal arithmetic with parentheses and this.getField("name").value reads. Execution requires explicit opt-in. There are no loops, dynamic calls, objects, reflection, I/O or host APIs.

### <code>M:Fluent.Pdf.PdfScripts.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inspects all active JavaScript definitions and known trigger attachments without evaluating code.

### <code>M:Fluent.Pdf.PdfScripts.SetDocumentScript(Fluent.Pdf.PdfDocumentSnapshot,System.String,System.String,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Adds/replaces/removes one named document script without executing it. Mutation is explicit active-content authority; source null removes the named attachment. Removal is not secure sanitization.

### <code>M:Fluent.Pdf.PdfSecurity.DecryptWithCertificate(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfCertificateCredential,Fluent.Pdf.PdfProviderRegistry,System.Boolean,System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes a plaintext full rewrite after matching an explicit certificate/RSA credential through the chosen offline CMS provider.

### <code>M:Fluent.Pdf.PdfSecurity.DecryptWithPassword(System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfPassword,System.Boolean,System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Authenticates and writes a plaintext full rewrite. Legacy RC4 and revision 5 require explicit permission. Failed authentication writes no output; signed rewrites require explicit authorization.

### <code>M:Fluent.Pdf.PdfSecurity.EncryptWithCertificates(Fluent.Pdf.PdfDocumentSnapshot,System.IO.Stream,Fluent.Pdf.PdfCertificateProtection,Fluent.Pdf.PdfProviderRegistry,System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes a certificate-encrypted full rewrite using an explicit offline CMS provider and equally authorized recipient groups.

### <code>M:Fluent.Pdf.PdfSecurity.EncryptWithPassword(Fluent.Pdf.PdfDocumentSnapshot,System.IO.Stream,Fluent.Pdf.PdfPasswordProtection,System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes password-encrypted output from a detached snapshot. AES-256 revision 6 is the default. Existing signature rewrites require explicit authorization.

### <code>M:Fluent.Pdf.PdfSepaCreditTransfer.#ctor(System.String,System.String,System.String,System.Decimal,System.String,System.String,System.String,System.String)</code>

Captures exact literal fields without trimming/transliteration. Beneficiary is1–70 UTF16 units; references are mutually exclusive. Payload is at most330 UTF8 bytes so explicit ECI26 fits QR version13 at levelM.

### <code>M:Fluent.Pdf.PdfSepaCreditTransfer.FromInvoice(Fluent.Pdf.PdfInvoiceData,System.String,System.String)</code>

Uses the immutable invoice's literal seller, identifier and reconciled due amount. Non-EUR, zero/out-of-range amounts and overlong fields reject without conversion or truncation.

### <code>M:Fluent.Pdf.PdfSepaCreditTransfer.Parse(System.String)</code>

Validates exact canonical twelve-line EPC002/UTF8 input under the same checks. Unsupported editions, encodings, line layouts and noncanonical amounts reject.

### <code>M:Fluent.Pdf.PdfShading.Axial(Fluent.Pdf.PdfColorSpace,Fluent.Pdf.PdfPoint,Fluent.Pdf.PdfPoint,Fluent.Pdf.PdfFunction,System.Boolean,System.Boolean,System.Nullable{Fluent.Pdf.PdfRect},System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a type-2 axial gradient with an exact one-input color function.

### <code>M:Fluent.Pdf.PdfShading.Functional(Fluent.Pdf.PdfColorSpace,Fluent.Pdf.PdfFunction,Fluent.Pdf.PdfRect,System.Nullable{Fluent.Pdf.PdfMatrix},System.Nullable{Fluent.Pdf.PdfRect},System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a type-1 two-input function shading with an explicit domain and transform.

### <code>M:Fluent.Pdf.PdfShading.Mesh(Fluent.Pdf.PdfColorSpace,System.Int32,System.IO.Stream,System.Int32,System.Int32,System.Double[],System.Int32,System.Int32,Fluent.Pdf.PdfFunction,System.Nullable{Fluent.Pdf.PdfRect},System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures and validates decoded type-4/5/6/7 mesh packets without quantizing coordinates or colors.

### <code>M:Fluent.Pdf.PdfShading.Radial(Fluent.Pdf.PdfColorSpace,Fluent.Pdf.PdfPoint,System.Double,Fluent.Pdf.PdfPoint,System.Double,Fluent.Pdf.PdfFunction,System.Boolean,System.Boolean,System.Nullable{Fluent.Pdf.PdfRect},System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a type-3 radial gradient with explicit nonnegative circle radii.

### <code>M:Fluent.Pdf.PdfShapedGlyph.#ctor(System.Int32,System.Int32,System.Double,System.Double,System.Double,System.Double)</code>

Creates finite glyph geometry. The core validates glyph/cluster ranges against the request.

### <code>M:Fluent.Pdf.PdfSignatureFieldDefinition.#ctor(System.String,System.Int32,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfGraphicsResource)</code>

Captures an undotted field name, page and native unrotated PDF rectangle. A null appearance requires an empty rectangle; visible appearances must be detached Form XObjects.

### <code>M:Fluent.Pdf.PdfSignatureFieldLock.#ctor(Fluent.Pdf.PdfSignatureLockAction,System.String[])</code>

Captures at most1024 ordinal field names. All requires an empty list; Include/Exclude require a nonempty list.

### <code>M:Fluent.Pdf.PdfSignatureOptions.#ctor(System.String,System.Int32,Fluent.Pdf.PdfCertificationPermission,Fluent.Pdf.PdfSignatureFieldLock,System.String,System.Nullable{System.DateTimeOffset})</code>

Captures a pre-created field, optional certification/locks/reason/time, and CMS reservation. A too-small reservation fails without retrying the signer.

### <code>M:Fluent.Pdf.PdfSignatureTrustDecision.#ctor(System.Boolean,System.Boolean,Fluent.Pdf.PdfRevocationStatus,System.String)</code>

Captures explicit identity pin/chain trust, validity interval and revocation status.

### <code>M:Fluent.Pdf.PdfSignatureTrustOptions.#ctor(System.DateTimeOffset)</code>

Captures an exact evaluation instant.

### <code>M:Fluent.Pdf.PdfSignatures.AuditRevisions(Fluent.Pdf.PdfSyntaxDocument,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Audits every linked revision, including signatures removed from the active graph. Object changes are structural declarations; semantic modification validation remains separate.

### <code>M:Fluent.Pdf.PdfSignatures.Inspect(Fluent.Pdf.PdfSyntaxDocument,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Checks active indirect detached signatures, exact excluded Contents tokens and covered revision boundaries. This does not evaluate trust/revocation or authorize later document modifications.

### <code>M:Fluent.Pdf.PdfSignatures.PreflightEdit(Fluent.Pdf.PdfSyntaxDocument,Fluent.Pdf.PdfWriteOptions,Fluent.Pdf.PdfSignatureOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Preflights an ordinary full/incremental write or an exact pre-created-field signing request. It does not replace final self-verification or validate existing trust.

### <code>M:Fluent.Pdf.PdfSignatures.PrepareFields(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfSignatureFieldDefinition},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates at most128 flat signature fields on an unsigned source, including explicitly supplied Form appearances. Pre-create all future signing fields before certification.

### <code>M:Fluent.Pdf.PdfSignatures.Sign(Fluent.Pdf.PdfSyntaxDocument,Fluent.Pdf.PdfSignatureOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Signs one pre-created blank field by appending a revision to the exact immutable source. Existing bytes are never rewritten; cryptographic self-verification is mandatory before returning.

### <code>M:Fluent.Pdf.PdfSignatures.SignAsync(Fluent.Pdf.PdfSyntaxDocument,Fluent.Pdf.PdfSignatureOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Asynchronously signs privately, verifies exact CMS integrity, and returns only completed immutable output.

### <code>M:Fluent.Pdf.PdfSignatures.SignFile(Fluent.Pdf.PdfSyntaxDocument,Fluent.Pdf.PdfSignatureOptions,Fluent.Pdf.PdfProviderRegistry,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Signs privately and atomically replaces a file only after complete exact-byte self-verification.

### <code>M:Fluent.Pdf.PdfSignatures.SignFileAsync(Fluent.Pdf.PdfSyntaxDocument,Fluent.Pdf.PdfSignatureOptions,Fluent.Pdf.PdfProviderRegistry,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Asynchronously signs and self-verifies before atomic file replacement.

### <code>M:Fluent.Pdf.PdfSignatures.Verify(Fluent.Pdf.PdfSyntaxDocument,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfSignatureTrustOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Verifies active detached signatures with an explicitly registered trust service under one context. No revocation, chain, timestamp or later-edit success is invented.

### <code>M:Fluent.Pdf.PdfSignedResult.OpenPdfStream</code>

Opens a detached exact signed-file view.

### <code>M:Fluent.Pdf.PdfSignedResult.Save(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Copies exact bytes to an empty seekable borrowed sink. A copy failure may leave a partial signed file; file saves are atomic.

### <code>M:Fluent.Pdf.PdfSignedResult.SaveFile(System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Atomically replaces a destination only after exact signed-byte copying succeeds.

### <code>M:Fluent.Pdf.PdfSimpleTextShaper.#ctor</code>

Public parameterless construction. Explicit scalar-only Latin/Greek/Cyrillic shaping. Complex scripts, marks, feature overrides and vertical writing fail.

### <code>M:Fluent.Pdf.PdfSimpleTextShaper.Shape(Fluent.Pdf.PdfShapingRequest,Fluent.Pdf.PdfOperationContext)</code>

Maps simple scalars to exact glyphs and advances without claiming OpenType shaping.

### <code>M:Fluent.Pdf.PdfSoftMask.#ctor(Fluent.Pdf.PdfTransparencyGroup,Fluent.Pdf.PdfSoftMaskKind,Fluent.Pdf.PdfColor,Fluent.Pdf.PdfFunction)</code>

Captures a transparency group and optional luminosity backdrop/unary transfer function.

### <code>M:Fluent.Pdf.PdfStampContent.FromDrawing(Fluent.Pdf.PdfDrawing,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures an original private drawing with positive source bounds.

### <code>M:Fluent.Pdf.PdfStampContent.FromHtml(Fluent.Pdf.PdfHtmlSource,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfHtmlOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Renders an explicit HTML source and captures exactly one generated page under one shared context. Multi-page output requires an explicit caller selection through FromPage.

### <code>M:Fluent.Pdf.PdfStampContent.FromImage(Fluent.Pdf.PdfImageAsset,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures immutable image samples, explicit physical aspect and Exif orientation without recompression.

### <code>M:Fluent.Pdf.PdfStampContent.FromPage(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures one passive PDF page's visible content, CropBox, rotation and UserUnit. Annotations, tags and optional-content owners require an explicit flatten/import profile.

### <code>M:Fluent.Pdf.PdfStampContent.FromText(Fluent.Pdf.PdfShapedText,System.Double,System.Double,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Embeds explicitly shaped text without selecting fonts, shaping providers or host resources.

### <code>M:Fluent.Pdf.PdfStampOptions.#ctor(Fluent.Pdf.PdfRect,Fluent.Pdf.PdfStampLayer,System.Double,System.Double,System.Boolean,System.Int32,System.Int32,System.Double,System.Double,Fluent.Pdf.PdfCanvasFit)</code>

Captures a positive destination, opacity0–1, clockwise rotation, bounded repetition and explicit decoration classification. Repetition advances by destination size plus the supplied gap.

### <code>M:Fluent.Pdf.PdfStamps.Apply(Fluent.Pdf.PdfDocumentSnapshot,System.String,Fluent.Pdf.PdfStampContent,System.Collections.Generic.IEnumerable{System.Int32},Fluent.Pdf.PdfStampOptions,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Applies one uniquely identified stamp group to exact zero-based pages, once per selected page. Duplicate/empty page selections reject.

### <code>M:Fluent.Pdf.PdfStamps.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Passively validates and inventories owned streams without executing actions or decoding stamp images.

### <code>M:Fluent.Pdf.PdfStamps.Remove(Fluent.Pdf.PdfDocumentSnapshot,System.String,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Removes only canonical owned invocations of the exact identifier. Other content/resources and signed-edit safeguards remain intact; historical/unreachable bytes are not erased.

### <code>M:Fluent.Pdf.PdfStamps.Update(Fluent.Pdf.PdfDocumentSnapshot,System.String,Fluent.Pdf.PdfStampContent,Fluent.Pdf.PdfStampOptions,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Replaces every recognized invocation of the exact identifier, retaining its selected pages and applying an explicit new placement snapshot.

### <code>M:Fluent.Pdf.PdfStructuralValidation.Inspect(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Parses normally and reports active graph observations. Syntax failures stay fatal in both modes; no repair scanner is invoked.

### <code>M:Fluent.Pdf.PdfStructureContent.Annotation(System.Int32,Fluent.Pdf.PdfObjectId)</code>

Identifies an existing annotation on the selected page, including a Link or Widget.

### <code>M:Fluent.Pdf.PdfStructureContent.MarkedContent(System.Int32,System.Int32,System.Nullable{Fluent.Pdf.PdfObjectId})</code>

Identifies an existing marked-content sequence in a page or explicitly selected Form stream.

### <code>M:Fluent.Pdf.PdfStructureElement.#ctor(System.String,Fluent.Pdf.PdfStructureItem[],System.String,System.String,System.String,System.String,Fluent.Pdf.PdfStructureTableAttributes)</code>

Captures an explicit standard role, ordered items and meaningful optional semantic text. No semantic text is guessed.

### <code>M:Fluent.Pdf.PdfStructurePlan.#ctor(System.String,Fluent.Pdf.PdfStructureElement,Fluent.Pdf.PdfAccessibilityProfile,System.Boolean)</code>

Captures language/root/profile. Replacing an existing tree requires an explicit reported-loss choice.

### <code>M:Fluent.Pdf.PdfStructureTableAttributes.#ctor(System.Int32,System.Int32,System.String,System.String[],System.String)</code>

Captures cell spans, optional Row/Column/Both header scope, header IDs and optional table summary.

### <code>M:Fluent.Pdf.PdfSyntax.Read(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Reads from the current position of a borrowed stream into a bounded immutable snapshot.

### <code>M:Fluent.Pdf.PdfSyntaxDocument.DecodeStream(Fluent.Pdf.PdfObjectId,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken,Fluent.Pdf.PdfProviderRegistry)</code>

Decodes an indirect stream eagerly under a fresh policy, resolving filter parameter references at the selected revision.

### <code>M:Fluent.Pdf.PdfSyntaxDocument.OpenOriginalStream</code>

Opens a detached read-only view of the exact original file, including historical revisions.

### <code>M:Fluent.Pdf.PdfSyntaxDocument.Resolve(Fluent.Pdf.PdfObjectId,System.Int32)</code>

Resolves the exact generation at a revision. A free, missing, zero or mismatched-generation reference resolves to null.

### <code>M:Fluent.Pdf.PdfTable.Create(Fluent.Pdf.PdfTableColumn[],System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfTableRow},System.Int32,System.Boolean,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Snapshots a bounded row source, validates merged slots and rejects header/body span crossings.

### <code>M:Fluent.Pdf.PdfTable.Layout(Fluent.Pdf.PdfFlowOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Lays out this table through the same bounded flow pipeline as paragraphs and nested content.

### <code>M:Fluent.Pdf.PdfTableBorder.#ctor(System.Double,Fluent.Pdf.PdfColor)</code>

Captures a nonnegative width and explicit color. Zero disables painting the border.

### <code>M:Fluent.Pdf.PdfTableCell.#ctor(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowBlock},System.Int32,System.Int32,Fluent.Pdf.PdfTableCellStyle)</code>

Captures up to 4096 nonnull flow blocks, finite style and positive bounded spans.

### <code>M:Fluent.Pdf.PdfTableCell.Text(System.String,Fluent.Pdf.PdfFlowTextStyle,System.Int32,System.Int32,Fluent.Pdf.PdfTableCellStyle)</code>

Creates a one-cell styled text paragraph with optional spans.

### <code>M:Fluent.Pdf.PdfTableCellStyle.#ctor(System.Nullable{Fluent.Pdf.PdfMargins},Fluent.Pdf.PdfColor,Fluent.Pdf.PdfTableBorder,Fluent.Pdf.PdfTableBorder,Fluent.Pdf.PdfTableBorder,Fluent.Pdf.PdfTableBorder,Fluent.Pdf.PdfTableVerticalAlignment)</code>

Captures physical padding and edge paint. An omitted edge uses the default half-point border.

### <code>M:Fluent.Pdf.PdfTableColumn.Auto(System.Double,System.Double,System.Double)</code>

Creates an automatic column with finite physical limits and a positive growth weight.

### <code>M:Fluent.Pdf.PdfTableColumn.Fixed(System.Double)</code>

Creates a positive finite fixed column width.

### <code>M:Fluent.Pdf.PdfTableData.Bind``1(System.Collections.Generic.IEnumerable{``0},Fluent.Pdf.PdfTableDataColumn{``0}[],System.Globalization.CultureInfo,System.Int32,Fluent.Pdf.PdfTableRow[],System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Enumerates at most maximumRows plus one lookahead, disposes the source, and snapshots rows under one operation budget.

### <code>M:Fluent.Pdf.PdfTableDataColumn`1.#ctor(Fluent.Pdf.PdfTableColumn,System.Func{`0,System.Object},Fluent.Pdf.PdfFlowTextStyle,System.String,System.String,Fluent.Pdf.PdfTableCellStyle,Fluent.Pdf.PdfFlowParagraphStyle)</code>

Captures a caller-owned selector. Supported scalar values use the explicit binding culture; arbitrary object ToString is never invoked.

### <code>M:Fluent.Pdf.PdfTableExtraction.Read(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IReadOnlyList{System.Int32},Fluent.Pdf.PdfTableExtractionOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Infers one candidate per selected page/region using an explicit ruled/borderless profile. Page indices must be unique and ascending; each original page/table/header remains observable.

### <code>M:Fluent.Pdf.PdfTableExtractionOptions.#ctor(Fluent.Pdf.PdfTableDetectionMode,System.Nullable{Fluent.Pdf.PdfRect},System.Double,System.Double,System.Int32,System.Int32,System.Int32,System.Int32,System.Boolean,System.String)</code>

Captures one explicit inference profile per selected page/region. Numeric typing occurs only when a culture is explicitly supplied.

### <code>M:Fluent.Pdf.PdfTableExtractionResult.WriteCsv(System.IO.Stream,Fluent.Pdf.PdfMergedCellCsvPolicy,System.Int32,System.Boolean,System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes UTF-8 CSV with an explicit merged-cell policy. Header suppression and spreadsheet formula escaping require explicit opt-in; borrowed output stays open.

### <code>M:Fluent.Pdf.PdfTableExtractionResult.WriteJson(System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Writes a versioned UTF-8 JSON schema retaining cell spans, page coordinates, source indices and typed values. Borrowed output stays open.

### <code>M:Fluent.Pdf.PdfTableExtractionResult.WriteJsonFile(System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Atomically replaces a caller-selected UTF-8 JSON file after successful bounded serialization.

### <code>M:Fluent.Pdf.PdfTableRow.#ctor(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfTableCell},System.Double,System.Boolean)</code>

Captures up to 1024 nonnull cells, a finite minimum height and explicit split permission.

### <code>M:Fluent.Pdf.PdfTemplateLayout.Layout(System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFlowBlock},Fluent.Pdf.PdfFlowOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfPageTemplate,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Lays out body blocks with bounded contents/index convergence and final header/footer callbacks.

### <code>M:Fluent.Pdf.PdfText.Shape(System.String,Fluent.Pdf.PdfFontFallback,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfTextOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Shapes using the first supplied face covering each whole grapheme. Missing coverage and unsupported shaping fail explicitly.

### <code>M:Fluent.Pdf.PdfTextDrawing.Create(Fluent.Pdf.PdfShapedText,System.Double,System.Double,System.Double,System.Double,System.String,System.Boolean,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates explicit glyph matrices in PDF user coordinates. Paragraphs advance by the supplied positive line height.

### <code>M:Fluent.Pdf.PdfTextExtraction.ReadPage(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfTextExtractionOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Extracts one page under a shared parse/font/content budget. Returned quads are advance cells rather than glyph ink.

### <code>M:Fluent.Pdf.PdfTextExtractionOptions.#ctor(System.Boolean,System.Boolean,System.Double,System.Double,Fluent.Pdf.PdfLayerUsage,Fluent.Pdf.PdfValue)</code>

Captures selection and grouping. Tolerances use displayed physical points; no OCR or action execution occurs.

### <code>M:Fluent.Pdf.PdfTextFeature.#ctor(System.String,System.UInt32,System.Int32,System.Int32)</code>

Creates a four-byte ASCII feature tag and a half-open logical UTF-16 range.

### <code>M:Fluent.Pdf.PdfTextOptions.#ctor(Fluent.Pdf.PdfParagraphDirection,System.Boolean,System.String,Fluent.Pdf.PdfTextFeature[])</code>

Captures explicit shaping settings. No language, font or feature discovery occurs.

### <code>M:Fluent.Pdf.PdfTextQuery.#ctor(System.String,System.Boolean,System.Boolean,System.Int32,System.Int32,System.Nullable{Fluent.Pdf.PdfRect},System.String,System.Nullable{System.Double},System.Nullable{System.Double})</code>

Captures explicit matching, displayed-region and declared-font filters. Regex calls have a finite at-most-250ms timeout and shared operation checkpoints.

### <code>M:Fluent.Pdf.PdfTextReplacementOptions.#ctor(System.Double,Fluent.Pdf.PdfTextReplacementOverflow)</code>

Captures maximum font size and explicit overflow behavior. Replacement uses explicit fonts/shaper and black fill; paragraph reflow is a separate required profile.

### <code>M:Fluent.Pdf.PdfTextSearch.FindPage(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfTextQuery,Fluent.Pdf.PdfTextExtractionOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Searches literals/regex across runs/streams/Forms, retaining Unicode-cluster, region, style and ActualText provenance.

### <code>M:Fluent.Pdf.PdfTextSearch.ReplacePage(Fluent.Pdf.PdfDocumentSnapshot,System.Int32,Fluent.Pdf.PdfTextQuery,System.String,Fluent.Pdf.PdfFontFallback,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfTextReplacementOptions,Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Replaces every selected complete source cluster with explicitly shaped text, preserving unrelated source advances. This profile requires untagged, single-line native-horizontal page text without invoked Forms or target ActualText; unsupported edits reject transactionally.

### <code>M:Fluent.Pdf.PdfTextString.Create(System.String,Fluent.Pdf.PdfTextStringEncoding,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a bounded text string. UTF-16BE is the default; selecting UTF-8 explicitly requires PDF 2.0 when used in output.

### <code>M:Fluent.Pdf.PdfTextString.Read(Fluent.Pdf.PdfValue,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Decodes a declared human-readable PDF string and preserves its exact source bytes. Binary/font-code strings must not use this API.

### <code>M:Fluent.Pdf.PdfTransaction.#ctor(Fluent.Pdf.PdfSyntaxDocument,Fluent.Pdf.PdfOperationPolicy)</code>

Creates a new graph or an editing overlay. The source remains immutable and owns no caller stream.

### <code>M:Fluent.Pdf.PdfTransaction.Add(Fluent.Pdf.PdfValue)</code>

Adds an immutable value with a fresh generation-zero identity, without reusing old identities.

### <code>M:Fluent.Pdf.PdfTransaction.Dispose</code>

Discards this overlay; source graphs, streams and detached values remain valid.

### <code>M:Fluent.Pdf.PdfTransaction.Remove(Fluent.Pdf.PdfObjectId)</code>

Frees an active identity. References to that generation resolve to null.

### <code>M:Fluent.Pdf.PdfTransaction.Resolve(Fluent.Pdf.PdfObjectId)</code>

Resolves the exact active generation through this overlay. Missing/free identities resolve to PDF null.

### <code>M:Fluent.Pdf.PdfTransaction.Save(System.IO.Stream,Fluent.Pdf.PdfWriteOptions,System.Threading.CancellationToken)</code>

Saves at the current position of a borrowed stream. Stream output may be partial on failure.

### <code>M:Fluent.Pdf.PdfTransaction.SaveFile(System.String,Fluent.Pdf.PdfWriteOptions,System.Threading.CancellationToken)</code>

Stages a complete file in the destination directory and commits it atomically after every check succeeds.

### <code>M:Fluent.Pdf.PdfTransaction.Set(Fluent.Pdf.PdfObjectId,Fluent.Pdf.PdfValue)</code>

Replaces an existing active identity, preserving its number and generation.

### <code>M:Fluent.Pdf.PdfTransparencyGroup.Create(Fluent.Pdf.PdfDrawing,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfColorSpace,System.Boolean,System.Boolean,System.Nullable{Fluent.Pdf.PdfMatrix},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Creates a PDF 1.4 transparency group with an explicit nonspecial blending space.

### <code>M:Fluent.Pdf.PdfType3Glyph.#ctor(System.String,System.String,Fluent.Pdf.PdfContentProgram)</code>

Captures a unique glyph name, Unicode text and a program whose first operator declares its width.

### <code>M:Fluent.Pdf.PdfUnicode.Analyze(System.String,Fluent.Pdf.PdfParagraphDirection,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Analyzes well-formed logical UTF-16 without normalization or text substitution.

### <code>M:Fluent.Pdf.PdfUnicodeMap.Decode(Fluent.Pdf.PdfValue,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Decodes exact variable-length codes, rejecting missing/ambiguous codes under strict processing.

### <code>M:Fluent.Pdf.PdfUnicodeMap.Parse(System.IO.Stream,System.Collections.Generic.IReadOnlyDictionary{System.String,Fluent.Pdf.PdfUnicodeMap},Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Parses borrowed decoded CMap bytes. Named inherited maps must be supplied explicitly; no external resource is loaded.

### <code>M:Fluent.Pdf.PdfUnits.Convert(System.Double,Fluent.Pdf.PdfLengthUnit,Fluent.Pdf.PdfLengthUnit,System.Double)</code>

Converts a finite length and rejects nonfinite arithmetic.

### <code>M:Fluent.Pdf.PdfUnits.PointsPerUnit(Fluent.Pdf.PdfLengthUnit,System.Double)</code>

Gets points per selected unit. Pixel DPI defaults explicitly to 96 and must lie between 1 and 1000000.

### <code>M:Fluent.Pdf.PdfValue.Array(Fluent.Pdf.PdfValue[])</code>

Copies an ordered array of immutable values.

### <code>M:Fluent.Pdf.PdfValue.Boolean(System.Boolean)</code>

Creates a Boolean.

### <code>M:Fluent.Pdf.PdfValue.Dictionary(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{System.String,Fluent.Pdf.PdfValue}})</code>

Copies entries with unique ordinal byte-valued keys.

### <code>M:Fluent.Pdf.PdfValue.Integer(System.Int64)</code>

Creates an exact integer.

### <code>M:Fluent.Pdf.PdfValue.Name(System.String)</code>

Creates a byte-valued name. Characters above U+00FF are rejected.

### <code>M:Fluent.Pdf.PdfValue.Number(System.String)</code>

Creates an exact PDF decimal token. Exponents and a bare sign/dot are rejected.

### <code>M:Fluent.Pdf.PdfValue.OpenRawStream</code>

Opens an independent read-only view of opaque encoded bytes, without decoding or executing content.

### <code>M:Fluent.Pdf.PdfValue.Reference(Fluent.Pdf.PdfObjectId)</code>

Creates an indirect reference.

### <code>M:Fluent.Pdf.PdfValue.String(System.Byte[])</code>

Copies uninterpreted string bytes.

### <code>M:Fluent.Pdf.PdfValues.Real(System.Double)</code>

Converts a finite Double to a round-trippable PDF decimal token without exponent notation.

### <code>M:Fluent.Pdf.PdfValues.Stream(Fluent.Pdf.PdfValue,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Snapshots borrowed encoded bytes under a fresh operation budget. The caller retains the input stream.

### <code>M:Fluent.Pdf.PdfViewSession.#ctor(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfProviderRegistry,System.Int64,System.Int32,Fluent.Pdf.PdfOperationPolicy)</code>

Captures immutable content, explicit renderer, per-request policy and positive cache/request bounds. The session never owns caller providers.

### <code>M:Fluent.Pdf.PdfViewSession.CancelPending</code>

Cancels all requests currently in flight. Later requests receive fresh tokens; cancellation callbacks run outside the cache lock.

### <code>M:Fluent.Pdf.PdfViewSession.ClearCache</code>

Clears only the session's retained rasters. Caller-owned results stay readable.

### <code>M:Fluent.Pdf.PdfViewSession.DisplayToUser(System.Int32,Fluent.Pdf.PdfPoint)</code>

Maps top-left displayed physical points to native PDF user coordinates for selection/navigation overlays.

### <code>M:Fluent.Pdf.PdfViewSession.Dispose</code>

Cancels current requests and releases only session cache references. It does not dispose caller providers or block their cancellation callbacks.

### <code>M:Fluent.Pdf.PdfViewSession.HitTest(System.Int32,Fluent.Pdf.PdfPoint,System.Double,System.Threading.CancellationToken)</code>

Finds the nearest extracted advance cell within an explicit displayed-point radius, retaining source character/operation provenance.

### <code>M:Fluent.Pdf.PdfViewSession.PlanTiles(System.Int32,System.Double,System.Int32,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Builds exact full-page pixel-grid tiles with integer boundaries. Regions map to the same displayed CropBox grid used by the renderer, including rotation and UserUnit.

### <code>M:Fluent.Pdf.PdfViewSession.RenderAsync(System.Int32,Fluent.Pdf.PdfRenderOptions,System.Threading.CancellationToken)</code>

Renders asynchronously on a worker with independent request cancellation. Cached hits retain immutable samples; each caller still owns its returned reference.

### <code>M:Fluent.Pdf.PdfViewSession.RenderThumbnailAsync(System.Int32,System.Int32,System.Int32,System.Threading.CancellationToken)</code>

Renders a page thumbnail that fits explicit positive pixel bounds on the same full-page grid.

### <code>M:Fluent.Pdf.PdfViewSession.SelectText(System.Int32,Fluent.Pdf.PdfRect,Fluent.Pdf.PdfTextExtractionOptions,System.Threading.CancellationToken)</code>

Returns source character advance cells intersecting a displayed physical-point region. Extraction is explicit; no OCR or script execution occurs.

### <code>M:Fluent.Pdf.PdfWordConversion.Import(System.IO.Stream,Fluent.Pdf.PdfWordFormat,Fluent.Pdf.PdfWordConversionOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Converts Word input at its borrowed current position into a caller-owned editable PDF model. Input remains open.

### <code>M:Fluent.Pdf.PdfWordConversion.Write(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfWordFormat,Fluent.Pdf.PdfWordConversionOptions,Fluent.Pdf.PdfProviderRegistry,System.IO.Stream,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Exports at a borrowed output's current position; source is unchanged and the stream stays open. Failure may leave a prefix.

### <code>M:Fluent.Pdf.PdfWordConversion.WriteFile(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfWordFormat,Fluent.Pdf.PdfWordConversionOptions,Fluent.Pdf.PdfProviderRegistry,System.String,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Exports to a private destination-directory file before atomic replacement. Failures preserve an existing destination.

### <code>M:Fluent.Pdf.PdfWordConversionOptions.#ctor(Fluent.Pdf.PdfWordReconstruction,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Double)</code>

Captures bounded source/output/expansion/character/archive/page/depth limits in addition to the root policy. No resource, font or model is discovered.

### <code>M:Fluent.Pdf.PdfWriteOptions.#ctor(Fluent.Pdf.PdfWriteMode,Fluent.Pdf.PdfCrossReferenceFormat,System.String,System.Boolean,System.String,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfExtensionRequirement},Fluent.Pdf.PdfUnknownExtensionPolicy)</code>

Captures revision/xref choices, an optional exact target version and explicit permission to invalidate signatures during a full rewrite.

### <code>M:Fluent.Pdf.PdfXfa.ConvertToAcroForm(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{Fluent.Pdf.PdfFormFieldDefinition},Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Converts exactly covered static text fields using caller-supplied page/widget appearances and unchanged qualified identities. Unknown template constructs/scripts/dynamic bindings reject. Source packet capture remains separately available through Inspect.

### <code>M:Fluent.Pdf.PdfXfa.Fill(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IReadOnlyDictionary{System.String,System.String},Fluent.Pdf.PdfOrganizationOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Atomically replaces existing unambiguous scalar datasets values only, preserving template/other packets, namespaces and stream identities. Scripts/layout are not evaluated.

### <code>M:Fluent.Pdf.PdfXfa.Inspect(Fluent.Pdf.PdfDocumentSnapshot,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Inspects packet-array XFA. Single XDP streams and dynamic layout require a separate profile; no XFA instruction ever runs.

### <code>P:Fluent.Pdf.IPdfDocumentReader.RequiresSeekableStream</code>

Gets whether a seekable input stream is required.

### <code>P:Fluent.Pdf.IPdfDocumentWriter.RequiresSeekableStream</code>

Gets whether a seekable output stream is required.

### <code>P:Fluent.Pdf.IPdfFontProgram.Info</code>

Gets immutable identity, rights and metrics.

### <code>P:Fluent.Pdf.IPdfFontShapingSource.ShapingFaceIndex</code>

Gets the original collection face index.

### <code>P:Fluent.Pdf.IPdfInvoiceProducer.EngineId</code>

Gets a nonsecret pinned ASCII engine identity.

### <code>P:Fluent.Pdf.IPdfInvoiceValidator.EngineId</code>

Gets a nonsecret pinned ASCII identity distinct from the producer.

### <code>P:Fluent.Pdf.IPdfLinearizationProducer.EngineId</code>

Gets the nonsecret pinned ASCII engine/configuration identity.

### <code>P:Fluent.Pdf.IPdfLinearizationValidator.EngineId</code>

Gets a pinned nonsecret identity distinct from the producer.

### <code>P:Fluent.Pdf.IPdfProfileProducer.EngineId</code>

Gets a nonsecret pinned ASCII engine/configuration identity.

### <code>P:Fluent.Pdf.IPdfProfileValidator.EngineId</code>

Gets a nonsecret pinned ASCII validator/configuration identity, distinct from the producer.

### <code>P:Fluent.Pdf.IPdfProvider.Descriptor</code>

Gets immutable registration metadata; the registry captures this value once.

### <code>P:Fluent.Pdf.IPdfVerticalFontProgram.SupportsVerticalMetrics</code>

Gets whether the selected face supplies all required vertical metrics.

### <code>P:Fluent.Pdf.ManagedPdfProvider.Descriptor</code>

Gets the core read/write capability declaration.

### <code>P:Fluent.Pdf.ManagedPdfProvider.Instance</code>

Gets the shared stateless provider.

### <code>P:Fluent.Pdf.ManagedPdfProvider.RequiresSeekableStream</code>

Gets false: the reader snapshots borrowed input and the writer emits sequential output.

### <code>P:Fluent.Pdf.PdfAccessibilityFinding.Location</code>

Gets the source or validator location.

### <code>P:Fluent.Pdf.PdfAccessibilityFinding.Message</code>

Gets a neutral bounded description.

### <code>P:Fluent.Pdf.PdfAccessibilityFinding.Rule</code>

Gets the stable rule identifier.

### <code>P:Fluent.Pdf.PdfAccessibilityHumanReview.Profile</code>

Gets the explicitly reviewed profile.

### <code>P:Fluent.Pdf.PdfAccessibilityHumanReview.ReadingOrderReviewed</code>

Gets the author's explicit reading-order review observation.

### <code>P:Fluent.Pdf.PdfAccessibilityHumanReview.ReportId</code>

Gets retained human report identity.

### <code>P:Fluent.Pdf.PdfAccessibilityHumanReview.Reviewer</code>

Gets explicit reviewer identity.

### <code>P:Fluent.Pdf.PdfAccessibilityHumanReview.SemanticsReviewed</code>

Gets the author's explicit description/table/form semantic review observation.

### <code>P:Fluent.Pdf.PdfAccessibilityHumanReview.SourceSha256</code>

Gets the exact reviewed input digest.

### <code>P:Fluent.Pdf.PdfAccessibilityInspection.Content</code>

Gets actual marked-content and annotation associations in logical tree order.

### <code>P:Fluent.Pdf.PdfAccessibilityInspection.Elements</code>

Gets elements in source tree traversal order.

### <code>P:Fluent.Pdf.PdfAccessibilityInspection.Findings</code>

Gets automated source findings. Empty never establishes conformance.

### <code>P:Fluent.Pdf.PdfAccessibilityInspection.HumanReviewRequired</code>

Gets true: reading order, descriptions and semantic correctness always require human review.

### <code>P:Fluent.Pdf.PdfAccessibilityInspection.Language</code>

Gets declared document language.

### <code>P:Fluent.Pdf.PdfAccessibilityInspection.OperationReport</code>

Gets root operation diagnostics and budgets.

### <code>P:Fluent.Pdf.PdfAccessibilityInspection.Profile</code>

Gets requested profile, without conformance inference.

### <code>P:Fluent.Pdf.PdfAccessibilityInspection.Source</code>

Gets normalized immutable source identities for subsequent remediation.

### <code>P:Fluent.Pdf.PdfAccessibilityValidation.Completed</code>

Gets whether complete independent validation finished.

### <code>P:Fluent.Pdf.PdfAccessibilityValidation.Findings</code>

Gets the complete independent finding inventory.

### <code>P:Fluent.Pdf.PdfAccessibilityValidation.HumanReview</code>

Gets separately supplied exact-input human review, or null; automated success never invents review.

### <code>P:Fluent.Pdf.PdfAccessibilityValidation.IsCompliant</code>

Gets the independent automated conformance observation; human review remains separate.

### <code>P:Fluent.Pdf.PdfAccessibilityValidation.OperationReport</code>

Gets root operation report after public validation; null for a provider-only result.

### <code>P:Fluent.Pdf.PdfAccessibilityValidation.Profile</code>

Gets the exact validated profile.

### <code>P:Fluent.Pdf.PdfAccessibilityValidation.SourceSha256</code>

Gets the lowercase exact-input SHA256.

### <code>P:Fluent.Pdf.PdfActionPolicy.AllowJavaScript</code>

Gets passive script action retention permission.

### <code>P:Fluent.Pdf.PdfActionPolicy.AllowLaunch</code>

Gets file launch permission.

### <code>P:Fluent.Pdf.PdfActionPolicy.AllowLocalGoTo</code>

Gets local navigation permission.

### <code>P:Fluent.Pdf.PdfActionPolicy.AllowOtherActions</code>

Gets retention permission for other action types and opaque active content.

### <code>P:Fluent.Pdf.PdfActionPolicy.AllowRemote</code>

Gets remote-file navigation permission.

### <code>P:Fluent.Pdf.PdfActionPolicy.AllowUri</code>

Gets absolute URI action permission.

### <code>P:Fluent.Pdf.PdfActionPolicy.AllowedUriSchemes</code>

Gets the allowed absolute URI schemes. Relative URIs are rejected.

### <code>P:Fluent.Pdf.PdfActionRecord.Definition</code>

Gets the immutable source definition without executing or decoding it.

### <code>P:Fluent.Pdf.PdfActionRecord.IsAllowed</code>

Gets whether the inspection policy permits this definition.

### <code>P:Fluent.Pdf.PdfActionRecord.IsAttachment</code>

Gets whether this record is a known attachment slot rather than an object definition.

### <code>P:Fluent.Pdf.PdfActionRecord.Location</code>

Gets the object identity and direct key/index path. Shared references have separate attachment records.

### <code>P:Fluent.Pdf.PdfActionRecord.Subtype</code>

Gets the action S name, or an ActiveContent:key observation.

### <code>P:Fluent.Pdf.PdfAnnotation.Bounds</code>

Gets its rectangle in native page coordinates, independent of page viewing rotation.

### <code>P:Fluent.Pdf.PdfAnnotation.Identifier</code>

Gets its stable document-unique name.

### <code>P:Fluent.Pdf.PdfAnnotation.Kind</code>

Gets its standard subtype.

### <code>P:Fluent.Pdf.PdfAnnotation.Metadata</code>

Gets the immutable comment metadata.

### <code>P:Fluent.Pdf.PdfAnnotation.MinimumVersion</code>

Gets the aggregate feature version floor.

### <code>P:Fluent.Pdf.PdfAnnotation.NormalAppearance</code>

Gets the explicit normal Form appearance and all its detached resources.

### <code>P:Fluent.Pdf.PdfAnnotation.OperationReport</code>

Gets the completed bounded construction report.

### <code>P:Fluent.Pdf.PdfAnnotation.Properties</code>

Gets explicit additional detached standard properties, without page/relationship/appearance identities.

### <code>P:Fluent.Pdf.PdfAnnotationMetadata.Author</code>

Gets the author label.

### <code>P:Fluent.Pdf.PdfAnnotationMetadata.Contents</code>

Gets logical comment contents.

### <code>P:Fluent.Pdf.PdfAnnotationMetadata.Created</code>

Gets the creation instant and offset.

### <code>P:Fluent.Pdf.PdfAnnotationMetadata.GroupReply</code>

Gets whether the reply is grouped rather than an ordinary reply.

### <code>P:Fluent.Pdf.PdfAnnotationMetadata.Modified</code>

Gets the modification instant and offset.

### <code>P:Fluent.Pdf.PdfAnnotationMetadata.ReplyTo</code>

Gets an existing or same-batch target's stable annotation identifier.

### <code>P:Fluent.Pdf.PdfAnnotationMetadata.State</code>

Gets the optional state text.

### <code>P:Fluent.Pdf.PdfAnnotationMetadata.StateModel</code>

Gets the optional state-model text.

### <code>P:Fluent.Pdf.PdfAnnotationMetadata.Subject</code>

Gets the subject.

### <code>P:Fluent.Pdf.PdfAnnotationRecord.Bounds</code>

Gets native page-coordinate bounds.

### <code>P:Fluent.Pdf.PdfAnnotationRecord.Definition</code>

Gets the exact immutable dictionary, retaining references into the captured source graph.

### <code>P:Fluent.Pdf.PdfAnnotationRecord.Identifier</code>

Gets the optional stable annotation name.

### <code>P:Fluent.Pdf.PdfAnnotationRecord.Index</code>

Gets its stable index within the captured set.

### <code>P:Fluent.Pdf.PdfAnnotationRecord.ObjectId</code>

Gets the original indirect identity, or null for a direct annotation dictionary.

### <code>P:Fluent.Pdf.PdfAnnotationRecord.PageIndex</code>

Gets its zero-based source page.

### <code>P:Fluent.Pdf.PdfAnnotationRecord.PageOrdinal</code>

Gets its zero-based position in the original page annotation array.

### <code>P:Fluent.Pdf.PdfAnnotationRecord.Subtype</code>

Gets the original subtype name, including unsupported kinds.

### <code>P:Fluent.Pdf.PdfAnnotationRecord.TextProperties</code>

Gets decoded standard text properties by PDF key, including original date/state strings.

### <code>P:Fluent.Pdf.PdfAnnotationSet.Annotations</code>

Gets all page annotations, including popups, widgets, links and unknown subtypes.

### <code>P:Fluent.Pdf.PdfAnnotationSet.OperationReport</code>

Gets the completed bounded inspection report.

### <code>P:Fluent.Pdf.PdfAnnotationSet.Source</code>

Gets the immutable captured source graph and pages.

### <code>P:Fluent.Pdf.PdfArchivalConversion.OperationReport</code>

Gets full operation diagnostics including explicit losses.

### <code>P:Fluent.Pdf.PdfArchivalConversion.Validation</code>

Gets the completed independent observation on the bytes copied to caller output.

### <code>P:Fluent.Pdf.PdfArchivalFinding.Location</code>

Gets the graph/validator location.

### <code>P:Fluent.Pdf.PdfArchivalFinding.Message</code>

Gets the rule description.

### <code>P:Fluent.Pdf.PdfArchivalFinding.Rule</code>

Gets the stable rule identifier.

### <code>P:Fluent.Pdf.PdfArchivalInspection.Findings</code>

Gets structural/profile blockers discovered by this bounded preflight.

### <code>P:Fluent.Pdf.PdfArchivalInspection.OperationReport</code>

Gets the operation diagnostics and shared resource usage.

### <code>P:Fluent.Pdf.PdfArchivalOptions.AllowSignatureInvalidation</code>

Gets whether an invalidating signature rewrite is authorized and reported.

### <code>P:Fluent.Pdf.PdfArchivalOptions.OutputColorSpace</code>

Gets the caller-supplied complete ICC output space, or null to retain existing output intents.

### <code>P:Fluent.Pdf.PdfArchivalOptions.OutputCondition</code>

Gets the output-condition identifier.

### <code>P:Fluent.Pdf.PdfArchivalOptions.Profile</code>

Gets the requested profile.

### <code>P:Fluent.Pdf.PdfArchivalOptions.ReplaceMetadata</code>

Gets whether existing XMP may be replaced with synchronized standard metadata.

### <code>P:Fluent.Pdf.PdfArchivalValidation.Completed</code>

Gets whether full validation completed.

### <code>P:Fluent.Pdf.PdfArchivalValidation.Findings</code>

Gets all reported violations.

### <code>P:Fluent.Pdf.PdfArchivalValidation.IsCompliant</code>

Gets the independent validator's conformance observation, never an SDK production qualification.

### <code>P:Fluent.Pdf.PdfArchivalValidation.OperationReport</code>

Gets the complete SDK operation report after a public validation/conversion call; null on a provider-only observation or failed operation.

### <code>P:Fluent.Pdf.PdfArchivalValidation.Profile</code>

Gets the exact validated profile.

### <code>P:Fluent.Pdf.PdfArchivalValidation.SourceSha256</code>

Gets the exact validated input digest.

### <code>P:Fluent.Pdf.PdfAttachment.Description</code>

Gets the optional Unicode description.

### <code>P:Fluent.Pdf.PdfAttachment.FileName</code>

Gets UF, falling back to F; no path is accessed while inspecting this value.

### <code>P:Fluent.Pdf.PdfAttachment.Id</code>

Gets the indirect file specification identity, or null for a malformed direct source specification.

### <code>P:Fluent.Pdf.PdfAttachment.Name</code>

Gets its first EmbeddedFiles name-tree name, or null when it is not in that tree.

### <code>P:Fluent.Pdf.PdfAttachment.Relationship</code>

Gets the passive AFRelationship name, including custom values.

### <code>P:Fluent.Pdf.PdfAttachment.Source</code>

Gets the immutable captured source; later extraction does not need the original live document.

### <code>P:Fluent.Pdf.PdfAttachment.Specification</code>

Gets the raw resolved file specification.

### <code>P:Fluent.Pdf.PdfAttachment.Variants</code>

Gets resolved embedded streams by their EF keys; absent variants denote an external specification.

### <code>P:Fluent.Pdf.PdfAttachmentOptions.CreationTime</code>

Gets the explicit creation timestamp.

### <code>P:Fluent.Pdf.PdfAttachmentOptions.Description</code>

Gets the optional Unicode description.

### <code>P:Fluent.Pdf.PdfAttachmentOptions.FileName</code>

Gets the portable leaf filename written to UF and used as the name-tree key.

### <code>P:Fluent.Pdf.PdfAttachmentOptions.MimeType</code>

Gets the embedded stream MIME subtype.

### <code>P:Fluent.Pdf.PdfAttachmentOptions.ModificationTime</code>

Gets the explicit modification timestamp.

### <code>P:Fluent.Pdf.PdfAttachmentOptions.Relationship</code>

Gets the associated-file relationship, or null for an ordinary attachment.

### <code>P:Fluent.Pdf.PdfAttachmentSet.Collection</code>

Gets the raw resolved Collection dictionary, or null; inspection does not open its initial file.

### <code>P:Fluent.Pdf.PdfAttachmentSet.EmbeddedStreams</code>

Gets all active EmbeddedFile streams, including orphans without a file specification.

### <code>P:Fluent.Pdf.PdfAttachmentSet.Items</code>

Gets file specifications, including orphan and external definitions.

### <code>P:Fluent.Pdf.PdfAttachmentSet.OperationReport</code>

Gets operation diagnostics and resource usage.

### <code>P:Fluent.Pdf.PdfAttachmentSet.Source</code>

Gets the captured source.

### <code>P:Fluent.Pdf.PdfBarcodeDetection.Observation</code>

Gets the single detected observation, or null when no supported symbol decoded.

### <code>P:Fluent.Pdf.PdfBarcodeDetection.OperationReport</code>

Gets the complete operation report.

### <code>P:Fluent.Pdf.PdfBarcodeDetection.PageIndex</code>

Gets a zero-based PDF page index or null for a standalone raster.

### <code>P:Fluent.Pdf.PdfBarcodeObservation.ErrorCorrection</code>

Gets observed QR error correction, or None for Code128.

### <code>P:Fluent.Pdf.PdfBarcodeObservation.Points</code>

Gets detector landmarks in image pixels, or native PDF user coordinates for a page result.

### <code>P:Fluent.Pdf.PdfBarcodeObservation.Symbology</code>

Gets the decoded symbol profile.

### <code>P:Fluent.Pdf.PdfBarcodeObservation.Text</code>

Gets decoded Unicode, never automatically interpreted as a URI, command or payment instruction.

### <code>P:Fluent.Pdf.PdfBarcodeOptions.BarHeight</code>

Gets Code128 bar height in modules.

### <code>P:Fluent.Pdf.PdfBarcodeOptions.ErrorCorrection</code>

Gets the requested error correction.

### <code>P:Fluent.Pdf.PdfBarcodeOptions.QrVersion</code>

Gets an explicit QR version or zero for smallest fitting.

### <code>P:Fluent.Pdf.PdfBarcodeOptions.QuietZone</code>

Gets white quiet-zone modules on every side.

### <code>P:Fluent.Pdf.PdfBarcodeOptions.Symbology</code>

Gets the exact symbol profile.

### <code>P:Fluent.Pdf.PdfBarcodeSymbol.Drawing</code>

Gets a device-gray vector drawing with an opaque white quiet zone. Place with its module bounds and aspect-preserving canvas fit.

### <code>P:Fluent.Pdf.PdfBarcodeSymbol.Height</code>

Gets module height including the white quiet zone.

### <code>P:Fluent.Pdf.PdfBarcodeSymbol.Mask</code>

Gets actual QR mask0–7 or minus one for Code128.

### <code>P:Fluent.Pdf.PdfBarcodeSymbol.OperationReport</code>

Gets the completed generation/verification/drawing report.

### <code>P:Fluent.Pdf.PdfBarcodeSymbol.Options</code>

Gets immutable requested settings.

### <code>P:Fluent.Pdf.PdfBarcodeSymbol.QrVersion</code>

Gets actual QR version or zero for Code128.

### <code>P:Fluent.Pdf.PdfBarcodeSymbol.Text</code>

Gets exact independently decoded Unicode payload.

### <code>P:Fluent.Pdf.PdfBarcodeSymbol.VerificationRaster</code>

Gets the exact canonical opaque grayscale pixels checked by the independent decoder.

### <code>P:Fluent.Pdf.PdfBarcodeSymbol.VerifierIdentity</code>

Gets the explicit decoder identity/version, without a production qualification claim.

### <code>P:Fluent.Pdf.PdfBarcodeSymbol.Width</code>

Gets module width including the white quiet zone.

### <code>P:Fluent.Pdf.PdfBidiParagraph.EmbeddingLevel</code>

Gets its resolved base level.

### <code>P:Fluent.Pdf.PdfBidiParagraph.FirstScalar</code>

Gets its first scalar index.

### <code>P:Fluent.Pdf.PdfBidiParagraph.ScalarCount</code>

Gets its scalar count, including its terminating paragraph separator when present.

### <code>P:Fluent.Pdf.PdfBookmark.Action</code>

Gets the optional passive action.

### <code>P:Fluent.Pdf.PdfBookmark.Bold</code>

Gets bold title styling.

### <code>P:Fluent.Pdf.PdfBookmark.Children</code>

Gets the immutable ordered children.

### <code>P:Fluent.Pdf.PdfBookmark.Color</code>

Gets optional device RGB title color.

### <code>P:Fluent.Pdf.PdfBookmark.Destination</code>

Gets the optional local destination.

### <code>P:Fluent.Pdf.PdfBookmark.IsOpen</code>

Gets the initial expansion state.

### <code>P:Fluent.Pdf.PdfBookmark.Italic</code>

Gets italic title styling.

### <code>P:Fluent.Pdf.PdfBookmark.Title</code>

Gets the Unicode title.

### <code>P:Fluent.Pdf.PdfCalculationResult.OperationReport</code>

Gets bounded evaluation report.

### <code>P:Fluent.Pdf.PdfCalculationResult.Value</code>

Gets exact decimal result.

### <code>P:Fluent.Pdf.PdfCanvas.Bounds</code>

Gets the margin content frame in canvas units.

### <code>P:Fluent.Pdf.PdfCanvas.Geometry</code>

Gets the captured immutable page geometry.

### <code>P:Fluent.Pdf.PdfCanvas.Options</code>

Gets captured explicit coordinate settings.

### <code>P:Fluent.Pdf.PdfCanvas.ToUser</code>

Gets the base canvas-to-native-PDF transform, including viewing rotation, origin, margins and UserUnit.

### <code>P:Fluent.Pdf.PdfCanvasOptions.Box</code>

Gets the selected standard viewing box.

### <code>P:Fluent.Pdf.PdfCanvasOptions.ClipToContent</code>

Gets whether painting is clipped to the margin content frame.

### <code>P:Fluent.Pdf.PdfCanvasOptions.Dpi</code>

Gets the explicit pixel conversion DPI.

### <code>P:Fluent.Pdf.PdfCanvasOptions.Margins</code>

Gets margins in canvas units.

### <code>P:Fluent.Pdf.PdfCanvasOptions.Origin</code>

Gets the explicit origin/axis orientation.

### <code>P:Fluent.Pdf.PdfCanvasOptions.Unit</code>

Gets the physical coordinate unit.

### <code>P:Fluent.Pdf.PdfCanvasPageLayer.Content</code>

Gets its isolated Do program.

### <code>P:Fluent.Pdf.PdfCanvasPageLayer.Mode</code>

Gets its placement relative to original/replacement base content.

### <code>P:Fluent.Pdf.PdfCanvasPageLayer.ResourceName</code>

Gets its generated XObject resource name.

### <code>P:Fluent.Pdf.PdfCanvasPlacement.Bounds</code>

Gets the transformed axis-aligned frame in canvas coordinates.

### <code>P:Fluent.Pdf.PdfCanvasPlacement.Index</code>

Gets stable insertion order.

### <code>P:Fluent.Pdf.PdfCanvasPlacement.LocalBounds</code>

Gets the original source/metric frame.

### <code>P:Fluent.Pdf.PdfCanvasPlacement.Quad</code>

Gets transformed perimeter corners in canvas coordinates.

### <code>P:Fluent.Pdf.PdfCanvasPlacement.Resource</code>

Gets the exact immutable painted resource.

### <code>P:Fluent.Pdf.PdfCanvasPlacement.Transform</code>

Gets the source-frame-to-canvas transform, including a Form's own matrix.

### <code>P:Fluent.Pdf.PdfCanvasPlacement.UserBounds</code>

Gets the transformed frame in native PDF user coordinates.

### <code>P:Fluent.Pdf.PdfCanvasPlacement.VisibleBounds</code>

Gets the conservative intersection with captured clip bounds and effective CropBox.

### <code>P:Fluent.Pdf.PdfCanvasPlacement.ZIndex</code>

Gets explicit z-order; equal values retain insertion order.

### <code>P:Fluent.Pdf.PdfCanvasResult.Bounds</code>

Gets the margin content frame in canvas units.

### <code>P:Fluent.Pdf.PdfCanvasResult.Drawing</code>

Gets the completed vector drawing in native PDF user coordinates.

### <code>P:Fluent.Pdf.PdfCanvasResult.Geometry</code>

Gets captured immutable geometry.

### <code>P:Fluent.Pdf.PdfCanvasResult.OperationReport</code>

Gets the successful shared-context canvas report.

### <code>P:Fluent.Pdf.PdfCanvasResult.Options</code>

Gets explicit canvas settings.

### <code>P:Fluent.Pdf.PdfCanvasResult.Placements</code>

Gets paint frames in ascending z-order and stable insertion order.

### <code>P:Fluent.Pdf.PdfCanvasResult.ToUser</code>

Gets base canvas-to-native-user coordinate conversion.

### <code>P:Fluent.Pdf.PdfCanvasStyle.Dash</code>

Gets immutable dash lengths in canvas units.

### <code>P:Fluent.Pdf.PdfCanvasStyle.DashPhase</code>

Gets dash phase in canvas units.

### <code>P:Fluent.Pdf.PdfCanvasStyle.Default</code>

Gets the default black-fill style.

### <code>P:Fluent.Pdf.PdfCanvasStyle.EvenOdd</code>

Gets whether filling uses the even-odd rule.

### <code>P:Fluent.Pdf.PdfCanvasStyle.Fill</code>

Gets optional fill paint.

### <code>P:Fluent.Pdf.PdfCanvasStyle.LineCap</code>

Gets line cap behavior.

### <code>P:Fluent.Pdf.PdfCanvasStyle.LineJoin</code>

Gets line join behavior.

### <code>P:Fluent.Pdf.PdfCanvasStyle.LineWidth</code>

Gets line width in canvas units; zero requests a device hairline.

### <code>P:Fluent.Pdf.PdfCanvasStyle.MiterLimit</code>

Gets the explicit miter ratio limit.

### <code>P:Fluent.Pdf.PdfCanvasStyle.State</code>

Gets optional exact compositing state.

### <code>P:Fluent.Pdf.PdfCanvasStyle.Stroke</code>

Gets optional stroke paint.

### <code>P:Fluent.Pdf.PdfCertificateCredential.PrivateKey</code>

Gets the caller-owned RSA key, which is never disposed by the SDK.

### <code>P:Fluent.Pdf.PdfCertificateProtection.Cipher</code>

Gets the file cipher.

### <code>P:Fluent.Pdf.PdfCertificateProtection.EncryptMetadata</code>

Gets whether metadata stream bytes are encrypted.

### <code>P:Fluent.Pdf.PdfCertificateProtection.Recipients</code>

Gets immutable public recipient snapshots.

### <code>P:Fluent.Pdf.PdfCertificateRecipient.KeyTransport</code>

Gets the explicit key transport.

### <code>P:Fluent.Pdf.PdfCertificateRecipient.OwnerAuthority</code>

Gets whether this recipient may change protection and exercise owner authority.

### <code>P:Fluent.Pdf.PdfCertificateRecipient.Permissions</code>

Gets recipient permission flags.

### <code>P:Fluent.Pdf.PdfColor.ColorSpace</code>

Gets the exact color space.

### <code>P:Fluent.Pdf.PdfColor.Components</code>

Gets immutable validated component values.

### <code>P:Fluent.Pdf.PdfColorRenderOptions.Intent</code>

Gets explicit rendering intent.

### <code>P:Fluent.Pdf.PdfColorRenderOptions.OutputProfile</code>

Gets exact RGB output profile.

### <code>P:Fluent.Pdf.PdfColorRenderOptions.SimulateOverprint</code>

Gets whether overprint simulation is required.

### <code>P:Fluent.Pdf.PdfColorRenderOptions.SpotColors</code>

Gets explicit spot policy. Alternate conversion requires a diagnostic.

### <code>P:Fluent.Pdf.PdfColorSpace.Cmyk</code>

Gets explicit DeviceCMYK.

### <code>P:Fluent.Pdf.PdfColorSpace.Components</code>

Gets the number of numeric color channels.

### <code>P:Fluent.Pdf.PdfColorSpace.Definition</code>

Gets its direct immutable PDF definition, including any nested ICC stream.

### <code>P:Fluent.Pdf.PdfColorSpace.Gray</code>

Gets explicit DeviceGray.

### <code>P:Fluent.Pdf.PdfColorSpace.IsPattern</code>

Gets whether the space requires a pattern name.

### <code>P:Fluent.Pdf.PdfColorSpace.MinimumVersion</code>

Gets its required PDF version floor.

### <code>P:Fluent.Pdf.PdfColorSpace.OperationReport</code>

Gets a successful construction report, or null for a fixed device-space singleton.

### <code>P:Fluent.Pdf.PdfColorSpace.Ranges</code>

Gets inclusive paired component bounds.

### <code>P:Fluent.Pdf.PdfColorSpace.Rgb</code>

Gets explicit DeviceRGB.

### <code>P:Fluent.Pdf.PdfColorTransformation.Color</code>

Gets color components in the exact requested output profile.

### <code>P:Fluent.Pdf.PdfColorTransformation.OperationReport</code>

Gets operation diagnostics/accounting.

### <code>P:Fluent.Pdf.PdfComparisonOptions.Geometry</code>

Gets geometry comparison selection.

### <code>P:Fluent.Pdf.PdfComparisonOptions.GeometryTolerance</code>

Gets displayed-point tolerance.

### <code>P:Fluent.Pdf.PdfComparisonOptions.IgnoredInformationKeys</code>

Gets exact case-sensitive ignored Info keys.

### <code>P:Fluent.Pdf.PdfComparisonOptions.Metadata</code>

Gets Info/XMP comparison selection.

### <code>P:Fluent.Pdf.PdfComparisonOptions.PixelTolerance</code>

Gets maximum ignored absolute channel difference.

### <code>P:Fluent.Pdf.PdfComparisonOptions.Rendering</code>

Gets explicit render policy including annotations, layers/print intent and DPI.

### <code>P:Fluent.Pdf.PdfComparisonOptions.Text</code>

Gets text comparison selection.

### <code>P:Fluent.Pdf.PdfComparisonOptions.Visual</code>

Gets explicit renderer comparison selection.

### <code>P:Fluent.Pdf.PdfComparisonResult.Differences</code>

Gets stable dimension/page/key ordered differences.

### <code>P:Fluent.Pdf.PdfComparisonResult.OperationReport</code>

Gets shared operation accounting and diagnostics.

### <code>P:Fluent.Pdf.PdfComparisonResult.Options</code>

Gets exact comparison choices.

### <code>P:Fluent.Pdf.PdfCompatibilityInspection.CatalogAssociatedFiles</code>

Gets the resolved catalog AF value or null. Associated files on other objects are outside this catalog inventory.

### <code>P:Fluent.Pdf.PdfCompatibilityInspection.EffectiveVersion</code>

Gets the larger supported header/catalog version. It does not establish feature conformance.

### <code>P:Fluent.Pdf.PdfCompatibilityInspection.Extensions</code>

Gets the resolved outer Extensions dictionary or null. Unknown payload values remain exact immutable PDF values.

### <code>P:Fluent.Pdf.PdfCompatibilityInspection.HeaderVersion</code>

Gets the physical original header version.

### <code>P:Fluent.Pdf.PdfCompatibilityInspection.OperationReport</code>

Gets diagnostics and shared resource accounting.

### <code>P:Fluent.Pdf.PdfCompatibilityInspection.Source</code>

Gets the captured immutable source, retaining indirect extension/namespace relationships.

### <code>P:Fluent.Pdf.PdfCompatibilityInspection.StructureNamespaces</code>

Gets the resolved structure-root Namespaces value or null. Namespace semantics are not inferred or rewritten.

### <code>P:Fluent.Pdf.PdfContentOperation.InlineImageDictionary</code>

Gets the immutable inline-image dictionary, or null for a non-image operator.

### <code>P:Fluent.Pdf.PdfContentOperation.InlineImageLength</code>

Gets the encoded inline-image byte count, or zero for a non-image operator.

### <code>P:Fluent.Pdf.PdfContentOperation.MinimumVersion</code>

Gets the minimum declared version required by this operator.

### <code>P:Fluent.Pdf.PdfContentOperation.Operands</code>

Gets defensively copied direct operands.

### <code>P:Fluent.Pdf.PdfContentOperation.Operation</code>

Gets the standard operator kind, or Unknown for an explicit extension.

### <code>P:Fluent.Pdf.PdfContentOperation.Token</code>

Gets its exact operator spelling.

### <code>P:Fluent.Pdf.PdfContentProgram.Kind</code>

Gets the program's page/form or Type 3 glyph context.

### <code>P:Fluent.Pdf.PdfContentProgram.MinimumVersion</code>

Gets the aggregate standard and explicitly declared extension version floor.

### <code>P:Fluent.Pdf.PdfContentProgram.OperationReport</code>

Gets the successful construction or parsing report.

### <code>P:Fluent.Pdf.PdfContentProgram.Operations</code>

Gets the operators in execution order.

### <code>P:Fluent.Pdf.PdfContentsEntry.DestinationId</code>

Gets the stable body destination ID.

### <code>P:Fluent.Pdf.PdfContentsEntry.PageIndex</code>

Gets the zero-based physical page index.

### <code>P:Fluent.Pdf.PdfContentsEntry.Point</code>

Gets the destination in native PDF page coordinates.

### <code>P:Fluent.Pdf.PdfContentsEntry.Title</code>

Gets the visible heading title.

### <code>P:Fluent.Pdf.PdfCrossReference.ContainerObjectNumber</code>

Gets the object stream number for a compressed entry.

### <code>P:Fluent.Pdf.PdfCrossReference.Id</code>

Gets the object identity.

### <code>P:Fluent.Pdf.PdfCrossReference.Kind</code>

Gets the entry kind.

### <code>P:Fluent.Pdf.PdfCrossReference.MemberIndex</code>

Gets the zero-based object-stream member index.

### <code>P:Fluent.Pdf.PdfCrossReference.Offset</code>

Gets the file offset for an in-use entry, or the next-free object number for a free entry.

### <code>P:Fluent.Pdf.PdfDecodedData.Length</code>

Gets the decoded byte count.

### <code>P:Fluent.Pdf.PdfDecodedData.OperationReport</code>

Gets the complete successful decode report.

### <code>P:Fluent.Pdf.PdfDecodedText.OperationReport</code>

Gets the complete operation report.

### <code>P:Fluent.Pdf.PdfDecodedText.Text</code>

Gets decoded logical Unicode.

### <code>P:Fluent.Pdf.PdfDestination.Coordinates</code>

Gets the immutable native coordinates.

### <code>P:Fluent.Pdf.PdfDestination.PageIndex</code>

Gets the physical page index in the captured input.

### <code>P:Fluent.Pdf.PdfDestination.View</code>

Gets the standard destination view.

### <code>P:Fluent.Pdf.PdfDiagnostic.Capability</code>

Gets the related capability, when applicable.

### <code>P:Fluent.Pdf.PdfDiagnostic.Code</code>

Gets a stable machine-readable code.

### <code>P:Fluent.Pdf.PdfDiagnostic.LossCategory</code>

Gets the explicitly reported loss category.

### <code>P:Fluent.Pdf.PdfDiagnostic.Message</code>

Gets the supplied description. Callers/providers must exclude document payloads and credentials.

### <code>P:Fluent.Pdf.PdfDiagnostic.ObjectId</code>

Gets the affected nonnegative object identifier, when known.

### <code>P:Fluent.Pdf.PdfDiagnostic.Operation</code>

Gets operation provenance when available.

### <code>P:Fluent.Pdf.PdfDiagnostic.PageIndex</code>

Gets the zero-based affected page, when known.

### <code>P:Fluent.Pdf.PdfDiagnostic.ProviderId</code>

Gets the related registered provider identifier, when applicable.

### <code>P:Fluent.Pdf.PdfDiagnostic.Remediation</code>

Gets bounded remediation guidance, when supplied.

### <code>P:Fluent.Pdf.PdfDiagnostic.Severity</code>

Gets diagnostic severity; permissive loss reports normalize losses to Warning.

### <code>P:Fluent.Pdf.PdfDifference.After</code>

Gets revised observation.

### <code>P:Fluent.Pdf.PdfDifference.AfterObject</code>

Gets revised page/catalog identity when present.

### <code>P:Fluent.Pdf.PdfDifference.Before</code>

Gets original observation.

### <code>P:Fluent.Pdf.PdfDifference.BeforeObject</code>

Gets original page/catalog identity when present.

### <code>P:Fluent.Pdf.PdfDifference.Kind</code>

Gets independent difference dimension.

### <code>P:Fluent.Pdf.PdfDifference.PageIndex</code>

Gets ordinal page, or null for document metadata.

### <code>P:Fluent.Pdf.PdfDocument.IsDisposed</code>

Gets whether this session was disposed. This property remains usable after disposal.

### <code>P:Fluent.Pdf.PdfDocument.LastOperationReport</code>

Gets the last completed provider operation report, including failed operations. Remains readable after disposal.

### <code>P:Fluent.Pdf.PdfDocument.Options</code>

Gets the immutable options captured for this session.

### <code>P:Fluent.Pdf.PdfDocument.Pages</code>

Gets the zero-based editable page model.

### <code>P:Fluent.Pdf.PdfDocument.Providers</code>

Gets the explicitly configured immutable provider registry.

### <code>P:Fluent.Pdf.PdfDocument.SourceDocument</code>

Gets the immutable original object graph, or null for a newly authored session.

### <code>P:Fluent.Pdf.PdfDocumentSnapshot.Options</code>

Gets the immutable options captured from the source session.

### <code>P:Fluent.Pdf.PdfDocumentSnapshot.PageNumbering</code>

Gets explicit generated page-label ranges, or null to preserve source labels.

### <code>P:Fluent.Pdf.PdfDocumentSnapshot.Pages</code>

Gets immutable pages in document order.

### <code>P:Fluent.Pdf.PdfDocumentSnapshot.SourceDocument</code>

Gets the immutable original graph, or null for a newly authored document.

### <code>P:Fluent.Pdf.PdfDocumentSnapshot.WriteOptions</code>

Gets the immutable save choices captured for this writer invocation.

### <code>P:Fluent.Pdf.PdfDrawing.Content</code>

Gets the validated state-isolated program.

### <code>P:Fluent.Pdf.PdfDrawing.MinimumVersion</code>

Gets its aggregate version floor.

### <code>P:Fluent.Pdf.PdfDrawing.OperationReport</code>

Gets the successful bounded composition report.

### <code>P:Fluent.Pdf.PdfDrawing.Resources</code>

Gets its private named resources.

### <code>P:Fluent.Pdf.PdfEngineeringInspection.OperationReport</code>

Gets complete shared operation diagnostics.

### <code>P:Fluent.Pdf.PdfEngineeringInspection.Records</code>

Gets immutable content/provenance observations.

### <code>P:Fluent.Pdf.PdfEngineeringPreservation.Length</code>

Gets the exact retained file length.

### <code>P:Fluent.Pdf.PdfEngineeringPreservation.OperationReport</code>

Gets completed capture/parse/copy diagnostics.

### <code>P:Fluent.Pdf.PdfEngineeringPreservation.Sha256</code>

Gets the exact input/output SHA256.

### <code>P:Fluent.Pdf.PdfEngineeringRecord.Definition</code>

Gets the detached immutable source definition. Reading a stream is explicit; no payload executes.

### <code>P:Fluent.Pdf.PdfEngineeringRecord.EncodedSha256</code>

Gets SHA256 of exact encoded stream bytes, or null for a dictionary observation. This is not semantic/decoded equivalence.

### <code>P:Fluent.Pdf.PdfEngineeringRecord.Kind</code>

Gets passive content family.

### <code>P:Fluent.Pdf.PdfEngineeringRecord.Location</code>

Gets physical object identity and direct key/index provenance.

### <code>P:Fluent.Pdf.PdfException.Diagnostic</code>

Gets the structured diagnostic.

### <code>P:Fluent.Pdf.PdfExtensionRequirement.BaseVersion</code>

Gets the required base PDF version.

### <code>P:Fluent.Pdf.PdfExtensionRequirement.DocumentationUrl</code>

Gets the explicit passive documentation URL; null on a legacy declaration.

### <code>P:Fluent.Pdf.PdfExtensionRequirement.ExtensionLevel</code>

Gets the required developer extension level.

### <code>P:Fluent.Pdf.PdfExtensionRequirement.Prefix</code>

Gets the developer prefix.

### <code>P:Fluent.Pdf.PdfExtensionRequirement.Revision</code>

Gets explicit extension revision text.

### <code>P:Fluent.Pdf.PdfExtractedArtifact.EndOperationIndex</code>

Gets the matching EMC index in the same container.

### <code>P:Fluent.Pdf.PdfExtractedArtifact.OccurrenceIndices</code>

Gets all resource uses within the scope, including invoked Forms.

### <code>P:Fluent.Pdf.PdfExtractedArtifact.PathIndices</code>

Gets all literal paths within the scope, including invoked Forms.

### <code>P:Fluent.Pdf.PdfExtractedArtifact.Properties</code>

Gets resolved explicit properties, or null for generic BMC.

### <code>P:Fluent.Pdf.PdfExtractedArtifact.Source</code>

Gets its begin location.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.BaselineEnd</code>

Gets the displayed baseline end before character/word spacing.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.BaselineStart</code>

Gets the displayed baseline start.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.Code</code>

Gets the uninterpreted source character code.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.CodeLength</code>

Gets its encoded byte length.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.Confidence</code>

Gets the mapping provenance, without inferred certainty.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.FontName</code>

Gets the declared PDF font name.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.FontSize</code>

Gets the effective displayed em height in physical points.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.GeometryEstimated</code>

Gets whether missing source metrics required an explicitly diagnosed estimate.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.Index</code>

Gets raw execution order within the selected page.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.LayerVisible</code>

Gets optional-content visibility separately from rendering mode and clipping.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.OperationIndex</code>

Gets source operator execution order, including nested Forms.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.Quad</code>

Gets the advance-cell quad in displayed top-left physical points, not an ink outline.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.RenderingMode</code>

Gets the PDF text rendering mode.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.Text</code>

Gets the decoded Unicode cluster; it may contain multiple scalars.

### <code>P:Fluent.Pdf.PdfExtractedCharacter.UserQuad</code>

Gets the advance-cell quad in native unrotated PDF user coordinates.

### <code>P:Fluent.Pdf.PdfExtractedLine.Bounds</code>

Gets the displayed advance-cell union.

### <code>P:Fluent.Pdf.PdfExtractedLine.Words</code>

Gets words in an explicitly inferred geometric order.

### <code>P:Fluent.Pdf.PdfExtractedParagraph.Bounds</code>

Gets their displayed advance-cell union.

### <code>P:Fluent.Pdf.PdfExtractedParagraph.Lines</code>

Gets inferred lines.

### <code>P:Fluent.Pdf.PdfExtractedPath.Elements</code>

Gets ordered construction operators and their individual transforms.

### <code>P:Fluent.Pdf.PdfExtractedPath.EndOperation</code>

Gets the terminating paint or n operator, retaining implicit-close/fill rules.

### <code>P:Fluent.Pdf.PdfExtractedPath.EvenOddClip</code>

Gets whether the requested clip uses even-odd winding.

### <code>P:Fluent.Pdf.PdfExtractedPath.IsArtifact</code>

Gets explicit inherited Artifact classification.

### <code>P:Fluent.Pdf.PdfExtractedPath.SetsClip</code>

Gets whether W/W* requests a clip update at this path end.

### <code>P:Fluent.Pdf.PdfExtractedPath.Source</code>

Gets the terminating operator's source location.

### <code>P:Fluent.Pdf.PdfExtractedPathElement.Operation</code>

Gets the immutable original construction operator/operands.

### <code>P:Fluent.Pdf.PdfExtractedPathElement.Source</code>

Gets its original source location.

### <code>P:Fluent.Pdf.PdfExtractedPathElement.Transform</code>

Gets its construction-time transform into native page user coordinates.

### <code>P:Fluent.Pdf.PdfExtractedResource.DeclaredSubtype</code>

Gets the declared subtype or font-file key. This is a declaration, not program validation.

### <code>P:Fluent.Pdf.PdfExtractedResource.Definition</code>

Gets the immutable original definition; references belong to the manifest snapshot.

### <code>P:Fluent.Pdf.PdfExtractedResource.FontProgramIndices</code>

Gets associated embedded font-program resource indices. An empty list does not authorize font substitution.

### <code>P:Fluent.Pdf.PdfExtractedResource.HasStream</code>

Gets whether this resource has captured stream bytes.

### <code>P:Fluent.Pdf.PdfExtractedResource.Index</code>

Gets its stable index within this manifest.

### <code>P:Fluent.Pdf.PdfExtractedResource.Kind</code>

Gets the declared resource role.

### <code>P:Fluent.Pdf.PdfExtractedResource.ObjectId</code>

Gets the defining indirect identity, or null for direct/inline definitions.

### <code>P:Fluent.Pdf.PdfExtractedSpan.Bounds</code>

Gets the union of covered displayed advance cells, or null when the replacement has no glyph geometry.

### <code>P:Fluent.Pdf.PdfExtractedSpan.CharacterCount</code>

Gets the number of covered selected characters.

### <code>P:Fluent.Pdf.PdfExtractedSpan.FirstCharacter</code>

Gets the first covered selected character index.

### <code>P:Fluent.Pdf.PdfExtractedSpan.Text</code>

Gets the exact declared logical replacement.

### <code>P:Fluent.Pdf.PdfExtractedTable.Bounds</code>

Gets its inferred displayed top-left region.

### <code>P:Fluent.Pdf.PdfExtractedTable.Cells</code>

Gets anchor cells with explicit row/column spans.

### <code>P:Fluent.Pdf.PdfExtractedTable.ColumnBoundaries</code>

Gets inferred displayed X boundaries used for explicit continuation comparison.

### <code>P:Fluent.Pdf.PdfExtractedTable.ColumnCount</code>

Gets primitive grid columns.

### <code>P:Fluent.Pdf.PdfExtractedTable.Confidence</code>

Gets a heuristic score, not calibrated probability.

### <code>P:Fluent.Pdf.PdfExtractedTable.Mode</code>

Gets the explicit detection profile.

### <code>P:Fluent.Pdf.PdfExtractedTable.PageIndex</code>

Gets its source page index.

### <code>P:Fluent.Pdf.PdfExtractedTable.RowCount</code>

Gets primitive grid rows, including merged/header rows.

### <code>P:Fluent.Pdf.PdfExtractedTableCell.Bounds</code>

Gets displayed top-left advance/rule bounds; these are inferred cell edges, not ink.

### <code>P:Fluent.Pdf.PdfExtractedTableCell.Characters</code>

Gets exact covered source-code records with operator execution provenance.

### <code>P:Fluent.Pdf.PdfExtractedTableCell.Column</code>

Gets the zero-based column.

### <code>P:Fluent.Pdf.PdfExtractedTableCell.ColumnSpan</code>

Gets its inferred rectangular column span.

### <code>P:Fluent.Pdf.PdfExtractedTableCell.Confidence</code>

Gets a documented heuristic score, not a calibrated probability or qualification metric.

### <code>P:Fluent.Pdf.PdfExtractedTableCell.Number</code>

Gets an explicitly culture-parsed decimal, or null for text/empty/unparsed cells.

### <code>P:Fluent.Pdf.PdfExtractedTableCell.PageIndex</code>

Gets the zero-based source page.

### <code>P:Fluent.Pdf.PdfExtractedTableCell.Row</code>

Gets the zero-based row in its page table.

### <code>P:Fluent.Pdf.PdfExtractedTableCell.RowSpan</code>

Gets its inferred rectangular row span.

### <code>P:Fluent.Pdf.PdfExtractedTableCell.RuleSources</code>

Gets literal rule source locations contributing to this candidate; borderless cells have none.

### <code>P:Fluent.Pdf.PdfExtractedTableCell.Text</code>

Gets semantic cell text with explicit ActualText and inferred geometric separators.

### <code>P:Fluent.Pdf.PdfExtractedTableSequence.RepeatedHeaderRows</code>

Gets the explicitly compared repeated header-row count; original source rows remain retained.

### <code>P:Fluent.Pdf.PdfExtractedTableSequence.TableIndices</code>

Gets ordered table indices in the owning result.

### <code>P:Fluent.Pdf.PdfExtractedWord.Bounds</code>

Gets the union of displayed advance cells.

### <code>P:Fluent.Pdf.PdfExtractedWord.Characters</code>

Gets exact underlying records in source execution order.

### <code>P:Fluent.Pdf.PdfExtractedWord.Index</code>

Gets its stable raw word order.

### <code>P:Fluent.Pdf.PdfExtractedWord.Text</code>

Gets source-order Unicode; logical replacements are exposed separately.

### <code>P:Fluent.Pdf.PdfFilter.Name</code>

Gets the exact decoding filter name.

### <code>P:Fluent.Pdf.PdfFilter.Parameters</code>

Gets immutable direct parameters or PDF null.

### <code>P:Fluent.Pdf.PdfFlowColumnPlacement.BlockIndex</code>

Gets the expanded zero-based logical source block index.

### <code>P:Fluent.Pdf.PdfFlowColumnPlacement.Bounds</code>

Gets a content-relative rectangle in physical points.

### <code>P:Fluent.Pdf.PdfFlowColumnPlacement.LogicalColumnIndex</code>

Gets the sequential logical column index across physical pages.

### <code>P:Fluent.Pdf.PdfFlowColumnPlacement.PhysicalColumnIndex</code>

Gets the physical slot on its page.

### <code>P:Fluent.Pdf.PdfFlowColumnPlacement.Source</code>

Gets the immutable source column page, including its logical lines and local notes.

### <code>P:Fluent.Pdf.PdfFlowColumns.Content</code>

Gets immutable logical content blocks.

### <code>P:Fluent.Pdf.PdfFlowColumns.Count</code>

Gets the explicit physical column count.

### <code>P:Fluent.Pdf.PdfFlowColumns.Gap</code>

Gets the physical inter-column or marker gap in points.

### <code>P:Fluent.Pdf.PdfFlowColumns.Height</code>

Gets fixed physical region height, or null for a full page frame.

### <code>P:Fluent.Pdf.PdfFlowColumns.Order</code>

Gets physical column ordering for logical content.

### <code>P:Fluent.Pdf.PdfFlowComponent.Content</code>

Gets immutable logical content blocks.

### <code>P:Fluent.Pdf.PdfFlowComponent.KeepTogether</code>

Gets whether this component must fit an empty page as a whole.

### <code>P:Fluent.Pdf.PdfFlowContents.LevelIndent</code>

Gets the title indentation per heading level in points.

### <code>P:Fluent.Pdf.PdfFlowContents.MaximumLevel</code>

Gets the last included heading level.

### <code>P:Fluent.Pdf.PdfFlowContents.MinimumLevel</code>

Gets the first included heading level.

### <code>P:Fluent.Pdf.PdfFlowContents.PageColumnWidth</code>

Gets the locator column width in points.

### <code>P:Fluent.Pdf.PdfFlowContents.TextStyle</code>

Gets the explicit text style.

### <code>P:Fluent.Pdf.PdfFlowFloat.Clear</code>

Gets which preceding floats the block must pass.

### <code>P:Fluent.Pdf.PdfFlowFloat.Drawing</code>

Gets the immutable reusable vector drawing.

### <code>P:Fluent.Pdf.PdfFlowFloat.Gap</code>

Gets the text exclusion gap in points.

### <code>P:Fluent.Pdf.PdfFlowFloat.Height</code>

Gets the displayed height in physical points.

### <code>P:Fluent.Pdf.PdfFlowFloat.Side</code>

Gets its physical exclusion side.

### <code>P:Fluent.Pdf.PdfFlowFloat.SourceBounds</code>

Gets its source coordinate frame.

### <code>P:Fluent.Pdf.PdfFlowFloat.Width</code>

Gets the displayed width in physical points.

### <code>P:Fluent.Pdf.PdfFlowFloatPlacement.BlockIndex</code>

Gets the zero-based source block index.

### <code>P:Fluent.Pdf.PdfFlowFloatPlacement.Bounds</code>

Gets the available rectangle relative to the page content origin, in physical points.

### <code>P:Fluent.Pdf.PdfFlowFloatPlacement.Float</code>

Gets the original immutable float description.

### <code>P:Fluent.Pdf.PdfFlowHeading.Id</code>

Gets the stable unique body destination ID.

### <code>P:Fluent.Pdf.PdfFlowHeading.Level</code>

Gets the heading level from one through six.

### <code>P:Fluent.Pdf.PdfFlowHeading.Paragraph</code>

Gets the heading paragraph.

### <code>P:Fluent.Pdf.PdfFlowHeading.Title</code>

Gets the visible heading title.

### <code>P:Fluent.Pdf.PdfFlowHeadingPlacement.Bounds</code>

Gets bounds in the containing content coordinate system.

### <code>P:Fluent.Pdf.PdfFlowHeadingPlacement.Heading</code>

Gets the originating body heading.

### <code>P:Fluent.Pdf.PdfFlowHeadingPlacement.PageIndex</code>

Gets the zero-based physical page index.

### <code>P:Fluent.Pdf.PdfFlowIndex.PageColumnWidth</code>

Gets the locator column width in points.

### <code>P:Fluent.Pdf.PdfFlowIndex.Terms</code>

Gets the terms sorted with the supplied culture.

### <code>P:Fluent.Pdf.PdfFlowIndex.TextStyle</code>

Gets the explicit text style.

### <code>P:Fluent.Pdf.PdfFlowIndexTerm.DestinationIds</code>

Gets the immutable body destination IDs.

### <code>P:Fluent.Pdf.PdfFlowIndexTerm.Text</code>

Gets the indexed term.

### <code>P:Fluent.Pdf.PdfFlowLine.Advance</code>

Gets the natural visible text advance before alignment or justification.

### <code>P:Fluent.Pdf.PdfFlowLine.Baseline</code>

Gets the baseline below the page content top, in physical points.

### <code>P:Fluent.Pdf.PdfFlowLine.BlockIndex</code>

Gets the zero-based source block index.

### <code>P:Fluent.Pdf.PdfFlowLine.Bounds</code>

Gets the available rectangle relative to the page content origin, in physical points.

### <code>P:Fluent.Pdf.PdfFlowLine.EndsHardBreak</code>

Gets whether the line ends at an explicit logical line separator.

### <code>P:Fluent.Pdf.PdfFlowLine.IsClipped</code>

Gets whether explicit diagnosed overflow clipping applies.

### <code>P:Fluent.Pdf.PdfFlowLine.ShapedText</code>

Gets immutable runs shaped with retained paragraph levels and line-specific reordering.

### <code>P:Fluent.Pdf.PdfFlowLine.Text</code>

Gets unchanged logical Unicode.

### <code>P:Fluent.Pdf.PdfFlowLine.Utf16Length</code>

Gets the logical UTF-16 extent.

### <code>P:Fluent.Pdf.PdfFlowLine.Utf16Offset</code>

Gets the UTF-16 offset in the logical source paragraph.

### <code>P:Fluent.Pdf.PdfFlowList.Counters</code>

Gets stable item counter values.

### <code>P:Fluent.Pdf.PdfFlowList.Gap</code>

Gets the physical inter-column or marker gap in points.

### <code>P:Fluent.Pdf.PdfFlowList.Items</code>

Gets immutable source items.

### <code>P:Fluent.Pdf.PdfFlowList.MarkerStyle</code>

Gets the explicit font/style for markers.

### <code>P:Fluent.Pdf.PdfFlowList.MarkerWidth</code>

Gets fixed marker width, or null for measured width.

### <code>P:Fluent.Pdf.PdfFlowList.Markers</code>

Gets invariant markers fixed before pagination.

### <code>P:Fluent.Pdf.PdfFlowList.OperationReport</code>

Gets the successful bounded construction report.

### <code>P:Fluent.Pdf.PdfFlowListItem.Content</code>

Gets immutable logical content blocks.

### <code>P:Fluent.Pdf.PdfFlowListItem.CounterValue</code>

Gets an explicit item counter restart, or null to continue.

### <code>P:Fluent.Pdf.PdfFlowNote.Content</code>

Gets immutable logical content blocks.

### <code>P:Fluent.Pdf.PdfFlowNote.Id</code>

Gets the stable bounded note definition ID.

### <code>P:Fluent.Pdf.PdfFlowNote.Label</code>

Gets its explicit visible label.

### <code>P:Fluent.Pdf.PdfFlowNote.MarkerStyle</code>

Gets the explicit font/style for markers.

### <code>P:Fluent.Pdf.PdfFlowNotePlacement.Bounds</code>

Gets a content-relative rectangle in physical points.

### <code>P:Fluent.Pdf.PdfFlowNotePlacement.IsEndnote</code>

Gets whether the note is appended after body content.

### <code>P:Fluent.Pdf.PdfFlowNotePlacement.Note</code>

Gets its immutable note definition.

### <code>P:Fluent.Pdf.PdfFlowNotePlacement.ReferenceBlockIndices</code>

Gets logical source references associated with this note instance.

### <code>P:Fluent.Pdf.PdfFlowNoteReference.IsEndnote</code>

Gets whether the note is appended after body content.

### <code>P:Fluent.Pdf.PdfFlowNoteReference.Note</code>

Gets its immutable note definition.

### <code>P:Fluent.Pdf.PdfFlowNoteReference.Paragraph</code>

Gets the source reference paragraph.

### <code>P:Fluent.Pdf.PdfFlowOptions.Frame</code>

Gets the displayed page content frame in physical points.

### <code>P:Fluent.Pdf.PdfFlowOptions.Geometry</code>

Gets immutable page boxes, user scale and viewing rotation.

### <code>P:Fluent.Pdf.PdfFlowOptions.Margins</code>

Gets displayed margins in physical points.

### <code>P:Fluent.Pdf.PdfFlowOptions.MaximumPages</code>

Gets the layout page-count ceiling.

### <code>P:Fluent.Pdf.PdfFlowOptions.Overflow</code>

Gets the explicit overlong-word policy.

### <code>P:Fluent.Pdf.PdfFlowOptions.TabInterval</code>

Gets the repeating physical tab interval after the final explicit stop.

### <code>P:Fluent.Pdf.PdfFlowOptions.TabStops</code>

Gets ordered explicit left-to-right physical tab stops relative to the line start.

### <code>P:Fluent.Pdf.PdfFlowPage.Canvas</code>

Gets the completed page canvas with content-relative point coordinates.

### <code>P:Fluent.Pdf.PdfFlowPage.Columns</code>

Gets logical columns in content-stream and structure order.

### <code>P:Fluent.Pdf.PdfFlowPage.ContentHeight</code>

Gets the occupied vertical extent below the page content top, in physical points.

### <code>P:Fluent.Pdf.PdfFlowPage.Floats</code>

Gets immutable floating rectangles.

### <code>P:Fluent.Pdf.PdfFlowPage.Headings</code>

Gets resolved explicit body headings on this page.

### <code>P:Fluent.Pdf.PdfFlowPage.Index</code>

Gets the zero-based output page index.

### <code>P:Fluent.Pdf.PdfFlowPage.Lines</code>

Gets immutable lines in logical block order.

### <code>P:Fluent.Pdf.PdfFlowPage.Notes</code>

Gets footnotes/endnotes and their reference associations.

### <code>P:Fluent.Pdf.PdfFlowPage.Tables</code>

Gets immutable table header/body slices on this page.

### <code>P:Fluent.Pdf.PdfFlowParagraph.Runs</code>

Gets immutable logical runs in source order.

### <code>P:Fluent.Pdf.PdfFlowParagraph.Style</code>

Gets the immutable formatting policy.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.Alignment</code>

Gets horizontal alignment within the available interval.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.Clear</code>

Gets which preceding floats the block must pass.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.FirstLineIndent</code>

Gets additional physical first-line indentation in points.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.KeepTogether</code>

Gets whether the complete paragraph must fit one page.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.KeepWithNext</code>

Gets whether this paragraph must share a page with the next paragraph opening.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.LeftIndent</code>

Gets physical left indentation in points.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.LineHeight</code>

Gets the minimum line-height multiplier; font ink envelopes can require more space.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.OrphanLines</code>

Gets the minimum lines on the first page of a split paragraph.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.PageBreakBefore</code>

Gets whether a nonempty current page ends before this paragraph.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.RightIndent</code>

Gets physical right indentation in points.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.SpaceAfter</code>

Gets nonnegative spacing after the paragraph in points.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.SpaceBefore</code>

Gets nonnegative spacing before the paragraph in points.

### <code>P:Fluent.Pdf.PdfFlowParagraphStyle.WidowLines</code>

Gets the minimum lines on the last page of a split paragraph.

### <code>P:Fluent.Pdf.PdfFlowResult.OperationReport</code>

Gets the successful shared-context layout report.

### <code>P:Fluent.Pdf.PdfFlowResult.Options</code>

Gets captured immutable layout options.

### <code>P:Fluent.Pdf.PdfFlowResult.Pages</code>

Gets immutable output pages.

### <code>P:Fluent.Pdf.PdfFlowRun.Style</code>

Gets the immutable formatting policy.

### <code>P:Fluent.Pdf.PdfFlowRun.Text</code>

Gets unchanged logical Unicode.

### <code>P:Fluent.Pdf.PdfFlowSectionBreak.Start</code>

Gets the section boundary policy.

### <code>P:Fluent.Pdf.PdfFlowTextStyle.BaselineShift</code>

Gets a finite physical baseline raise, in points.

### <code>P:Fluent.Pdf.PdfFlowTextStyle.Color</code>

Gets the explicit text paint color.

### <code>P:Fluent.Pdf.PdfFlowTextStyle.FontSize</code>

Gets the typographic size in physical points.

### <code>P:Fluent.Pdf.PdfFlowTextStyle.Fonts</code>

Gets the explicit immutable font preference order.

### <code>P:Fluent.Pdf.PdfFlowTextStyle.TextOptions</code>

Gets immutable language, direction and shaping feature choices.

### <code>P:Fluent.Pdf.PdfFont.Info</code>

Gets immutable font identity, rights and metrics.

### <code>P:Fluent.Pdf.PdfFont.OperationReport</code>

Gets the successful loading report.

### <code>P:Fluent.Pdf.PdfFont.ProviderId</code>

Gets the selected font provider identity.

### <code>P:Fluent.Pdf.PdfFontFallback.Fonts</code>

Gets the captured preference order.

### <code>P:Fluent.Pdf.PdfFontGlyph.Advance</code>

Gets the unscaled horizontal advance.

### <code>P:Fluent.Pdf.PdfFontGlyph.Font</code>

Gets the explicitly selected font.

### <code>P:Fluent.Pdf.PdfFontGlyph.GlyphIndex</code>

Gets its glyph identity, or zero when unavailable.

### <code>P:Fluent.Pdf.PdfFontGlyph.GlyphName</code>

Gets the provider's glyph name.

### <code>P:Fluent.Pdf.PdfFontGlyph.OperationReport</code>

Gets the successful query or fallback-resolution report.

### <code>P:Fluent.Pdf.PdfFontGlyph.UnicodeScalar</code>

Gets the queried Unicode scalar.

### <code>P:Fluent.Pdf.PdfFontGlyph.VerticalAdvance</code>

Gets the unscaled vertical advance.

### <code>P:Fluent.Pdf.PdfFontInfo.EmbeddingRights</code>

Gets declared embedding restrictions.

### <code>P:Fluent.Pdf.PdfFontInfo.Format</code>

Gets its outline format.

### <code>P:Fluent.Pdf.PdfFontInfo.GlyphCount</code>

Gets the number of glyph identities, including the missing glyph at zero.

### <code>P:Fluent.Pdf.PdfFontInfo.Metrics</code>

Gets design-space geometry.

### <code>P:Fluent.Pdf.PdfFontInfo.PostScriptName</code>

Gets the program's PostScript name.

### <code>P:Fluent.Pdf.PdfFontInfo.Sha256</code>

Gets the source program/face identity hash.

### <code>P:Fluent.Pdf.PdfFontInfo.Symbolic</code>

Gets whether the font uses a symbolic encoding.

### <code>P:Fluent.Pdf.PdfFontMetrics.Ascender</code>

Gets the typographic ascender.

### <code>P:Fluent.Pdf.PdfFontMetrics.Bottom</code>

Gets the font bounding-box bottom edge.

### <code>P:Fluent.Pdf.PdfFontMetrics.CapHeight</code>

Gets the capital height.

### <code>P:Fluent.Pdf.PdfFontMetrics.Descender</code>

Gets the typographic descender.

### <code>P:Fluent.Pdf.PdfFontMetrics.FixedPitch</code>

Gets whether advances have fixed pitch.

### <code>P:Fluent.Pdf.PdfFontMetrics.ItalicAngle</code>

Gets the italic angle in counterclockwise degrees from vertical.

### <code>P:Fluent.Pdf.PdfFontMetrics.Left</code>

Gets the font bounding-box left edge.

### <code>P:Fluent.Pdf.PdfFontMetrics.Right</code>

Gets the font bounding-box right edge.

### <code>P:Fluent.Pdf.PdfFontMetrics.Top</code>

Gets the font bounding-box top edge.

### <code>P:Fluent.Pdf.PdfFontMetrics.UnitsPerEm</code>

Gets design units per em.

### <code>P:Fluent.Pdf.PdfFontProgramWriteResult.CidKeyed</code>

Gets whether emitted CFF charsets use CID identities.

### <code>P:Fluent.Pdf.PdfFontProgramWriteResult.FontFileSubtype</code>

Gets the FontFile3 subtype, or null for a FontFile/FontFile2 program.

### <code>P:Fluent.Pdf.PdfFontProgramWriteResult.Length1</code>

Gets the clear Type 1 prefix extent, or TrueType program length.

### <code>P:Fluent.Pdf.PdfFontProgramWriteResult.Length2</code>

Gets the encrypted Type 1 section extent.

### <code>P:Fluent.Pdf.PdfFontProgramWriteResult.Length3</code>

Gets the Type 1 trailer extent.

### <code>P:Fluent.Pdf.PdfFontProgramWriteResult.PostScriptName</code>

Gets the emitted program's font name.

### <code>P:Fluent.Pdf.PdfFontResource.EncodedText</code>

Gets a direct immutable byte string ready for Tj/TJ operands, in the supplied mapping order.

### <code>P:Fluent.Pdf.PdfFontResource.Font</code>

Gets the exact selected font.

### <code>P:Fluent.Pdf.PdfFontResource.GlyphMappings</code>

Gets unique mappings in first-use order; EncodedText carries the exact selected byte codes.

### <code>P:Fluent.Pdf.PdfFontResource.MinimumVersion</code>

Gets the minimum required PDF version.

### <code>P:Fluent.Pdf.PdfFontResource.OperationReport</code>

Gets the successful eager font-resource creation report.

### <code>P:Fluent.Pdf.PdfFontResource.Vertical</code>

Gets whether vertical writing was explicitly selected with exact vertical metrics.

### <code>P:Fluent.Pdf.PdfFontVerticalMetrics.Advance</code>

Gets the positive vertical advance.

### <code>P:Fluent.Pdf.PdfFontVerticalMetrics.OriginX</code>

Gets the vertical origin's horizontal coordinate.

### <code>P:Fluent.Pdf.PdfFontVerticalMetrics.OriginY</code>

Gets the vertical origin's vertical coordinate.

### <code>P:Fluent.Pdf.PdfFontWriteResult.OperationReport</code>

Gets the successful bounded-write report.

### <code>P:Fluent.Pdf.PdfFontWriteResult.Program</code>

Gets the emitted program format details.

### <code>P:Fluent.Pdf.PdfFormChoice.DisplayValue</code>

Gets the viewer display value.

### <code>P:Fluent.Pdf.PdfFormChoice.ExportValue</code>

Gets the exported value.

### <code>P:Fluent.Pdf.PdfFormData.Fields</code>

Gets fields in captured order.

### <code>P:Fluent.Pdf.PdfFormData.OperationReport</code>

Gets the successful capture/parse report.

### <code>P:Fluent.Pdf.PdfFormDataField.Name</code>

Gets the case-sensitive qualified name.

### <code>P:Fluent.Pdf.PdfFormDataField.Value</code>

Gets detached value data. Name values retain exact case-sensitive button export bytes.

### <code>P:Fluent.Pdf.PdfFormField.DefaultValue</code>

Gets the resolved inherited reset value.

### <code>P:Fluent.Pdf.PdfFormField.Flags</code>

Gets all raw flag bits, retaining unsupported source flags.

### <code>P:Fluent.Pdf.PdfFormField.Inherited</code>

Gets captured inherited field/default-appearance properties for inspection.

### <code>P:Fluent.Pdf.PdfFormField.Kind</code>

Gets the interpreted standard kind, or Unknown.

### <code>P:Fluent.Pdf.PdfFormField.Name</code>

Gets the fully qualified name.

### <code>P:Fluent.Pdf.PdfFormField.Representations</code>

Gets exact terminal dictionary identities sharing this logical field.

### <code>P:Fluent.Pdf.PdfFormField.Source</code>

Gets the captured immutable source.

### <code>P:Fluent.Pdf.PdfFormField.Value</code>

Gets the resolved current inherited value.

### <code>P:Fluent.Pdf.PdfFormField.Widgets</code>

Gets attached and unattached widgets in source tree order.

### <code>P:Fluent.Pdf.PdfFormFieldDefinition.AlternateName</code>

Gets the optional alternate/accessibility name.

### <code>P:Fluent.Pdf.PdfFormFieldDefinition.Choices</code>

Gets ordered choice export/display pairs.

### <code>P:Fluent.Pdf.PdfFormFieldDefinition.DefaultValue</code>

Gets explicit reset value data; PDF null means absent.

### <code>P:Fluent.Pdf.PdfFormFieldDefinition.EditingFont</code>

Gets an explicit default editing font resource, independently of private appearance fonts.

### <code>P:Fluent.Pdf.PdfFormFieldDefinition.Flags</code>

Gets requested behavior flags.

### <code>P:Fluent.Pdf.PdfFormFieldDefinition.FontSize</code>

Gets the default appearance font size in native appearance units.

### <code>P:Fluent.Pdf.PdfFormFieldDefinition.Kind</code>

Gets the terminal kind.

### <code>P:Fluent.Pdf.PdfFormFieldDefinition.MappingName</code>

Gets the optional export mapping name.

### <code>P:Fluent.Pdf.PdfFormFieldDefinition.MaximumLength</code>

Gets optional text MaxLen.

### <code>P:Fluent.Pdf.PdfFormFieldDefinition.Name</code>

Gets the fully qualified hierarchical field name.

### <code>P:Fluent.Pdf.PdfFormFieldDefinition.Value</code>

Gets explicit current value data; PDF null means absent.

### <code>P:Fluent.Pdf.PdfFormFieldDefinition.Widgets</code>

Gets owned widget definitions.

### <code>P:Fluent.Pdf.PdfFormSet.Dictionary</code>

Gets the resolved raw form dictionary, or null.

### <code>P:Fluent.Pdf.PdfFormSet.Fields</code>

Gets logical terminal fields, preserving consistent unnamed representations.

### <code>P:Fluent.Pdf.PdfFormSet.OperationReport</code>

Gets passive validation diagnostics and resource usage.

### <code>P:Fluent.Pdf.PdfFormSet.Source</code>

Gets the captured source.

### <code>P:Fluent.Pdf.PdfFormTextStyle.FontSize</code>

Gets font size in native appearance units.

### <code>P:Fluent.Pdf.PdfFormTextStyle.Fonts</code>

Gets immutable explicit font preference order.

### <code>P:Fluent.Pdf.PdfFormTextStyle.Padding</code>

Gets positive/zero padding in native appearance units.

### <code>P:Fluent.Pdf.PdfFormTextStyle.PasswordMask</code>

Gets the explicit password presentation scalar, or null; stored field values remain unencrypted.

### <code>P:Fluent.Pdf.PdfFormTextStyle.Providers</code>

Gets the explicitly provisioned shaping provider registry.

### <code>P:Fluent.Pdf.PdfFormTextStyle.TextOptions</code>

Gets explicit Unicode shaping choices.

### <code>P:Fluent.Pdf.PdfFormUpdate.Appearances</code>

Gets detached normal Forms keyed by exact source widget identity.

### <code>P:Fluent.Pdf.PdfFormUpdate.DefaultValue</code>

Gets an optional reset value update, or null to retain the source reset value.

### <code>P:Fluent.Pdf.PdfFormUpdate.Name</code>

Gets the exact field name.

### <code>P:Fluent.Pdf.PdfFormUpdate.TextStyle</code>

Gets optional shared-context text generation settings.

### <code>P:Fluent.Pdf.PdfFormUpdate.Value</code>

Gets requested current data; a standard interchange text value is converted to a button export name using the target field kind.

### <code>P:Fluent.Pdf.PdfFormWidget.Bounds</code>

Gets native unrotated bounds.

### <code>P:Fluent.Pdf.PdfFormWidget.Definition</code>

Gets raw appearance/state, tab/structure and action metadata without executing it.

### <code>P:Fluent.Pdf.PdfFormWidget.Id</code>

Gets its indirect identity.

### <code>P:Fluent.Pdf.PdfFormWidget.PageIndex</code>

Gets its attached page, or null for an unattached source widget.

### <code>P:Fluent.Pdf.PdfFormWidgetDefinition.AnnotationFlags</code>

Gets standard annotation flags; Print is the default.

### <code>P:Fluent.Pdf.PdfFormWidgetDefinition.Bounds</code>

Gets native unrotated widget bounds.

### <code>P:Fluent.Pdf.PdfFormWidgetDefinition.ExportValue</code>

Gets a button's on-state PDF name, or null for a non-state widget.

### <code>P:Fluent.Pdf.PdfFormWidgetDefinition.NormalAppearance</code>

Gets the normal/on-state private Form appearance.

### <code>P:Fluent.Pdf.PdfFormWidgetDefinition.OffAppearance</code>

Gets the required Off appearance for a state button.

### <code>P:Fluent.Pdf.PdfFormWidgetDefinition.PageIndex</code>

Gets the zero-based physical page index.

### <code>P:Fluent.Pdf.PdfFunction.Definition</code>

Gets immutable direct function data; nested sampled-function streams are hoisted by graphics resource embedding.

### <code>P:Fluent.Pdf.PdfFunction.Domain</code>

Gets paired input bounds.

### <code>P:Fluent.Pdf.PdfFunction.InputComponents</code>

Gets its input channel count.

### <code>P:Fluent.Pdf.PdfFunction.OperationReport</code>

Gets its successful bounded construction report.

### <code>P:Fluent.Pdf.PdfFunction.OutputComponents</code>

Gets its output channel count.

### <code>P:Fluent.Pdf.PdfFunction.Range</code>

Gets paired output bounds, or an empty array when omitted for an exponential/stitching function.

### <code>P:Fluent.Pdf.PdfGenerationContext.BlockCount</code>

Gets captured top-level blocks.

### <code>P:Fluent.Pdf.PdfGenerationContext.Culture</code>

Gets the captured immutable standard formatting culture.

### <code>P:Fluent.Pdf.PdfGenerationContext.OperationContext</code>

Gets the shared context for cooperative provider work.

### <code>P:Fluent.Pdf.PdfGenerationContext.Providers</code>

Gets the explicitly supplied immutable provider registry; no discovery or resource loading occurs.

### <code>P:Fluent.Pdf.PdfGenerationContext.RecordIndex</code>

Gets the current zero-based source record, or -1 before enumeration.

### <code>P:Fluent.Pdf.PdfGenerationContext.RecordsPrepared</code>

Gets fully rendered source records.

### <code>P:Fluent.Pdf.PdfGenerationEvent.BlockCount</code>

Gets captured top-level block count.

### <code>P:Fluent.Pdf.PdfGenerationEvent.Kind</code>

Gets the ordered phase.

### <code>P:Fluent.Pdf.PdfGenerationEvent.OperationContext</code>

Gets the shared operation context for cooperative callback/provider work; no worker/scope may outlive its operation.

### <code>P:Fluent.Pdf.PdfGenerationEvent.Ordinal</code>

Gets the zero-based dispatched hook ordinal.

### <code>P:Fluent.Pdf.PdfGenerationEvent.Page</code>

Gets the immutable final page for PagePrepared, or null otherwise.

### <code>P:Fluent.Pdf.PdfGenerationEvent.PageCount</code>

Gets final page count when layout exists.

### <code>P:Fluent.Pdf.PdfGenerationEvent.PageIndex</code>

Gets the selected final page, or -1 for operation-wide phases.

### <code>P:Fluent.Pdf.PdfGenerationEvent.Pages</code>

Gets immutable final page observations after LayoutPrepared; no unfinished layout/model objects are exposed.

### <code>P:Fluent.Pdf.PdfGenerationEvent.RecordIndex</code>

Gets the current zero-based source record, or -1 before enumeration.

### <code>P:Fluent.Pdf.PdfGenerationEvent.RecordsPrepared</code>

Gets fully rendered source records.

### <code>P:Fluent.Pdf.PdfGenerationEvent.TemplateDepth</code>

Gets the active invocation depth.

### <code>P:Fluent.Pdf.PdfGenerationEvent.TemplateIdentifier</code>

Gets the current explicit template identity, or null outside an invocation.

### <code>P:Fluent.Pdf.PdfGenerationOptions.Culture</code>

Gets a captured read-only standard culture without user overrides.

### <code>P:Fluent.Pdf.PdfGenerationOptions.DocumentOptions</code>

Gets the generated document's options and default operation policy.

### <code>P:Fluent.Pdf.PdfGenerationOptions.Layout</code>

Gets immutable explicit layout settings.

### <code>P:Fluent.Pdf.PdfGenerationOptions.MaximumBlocks</code>

Gets the finite captured top-level block ceiling.

### <code>P:Fluent.Pdf.PdfGenerationOptions.MaximumEvents</code>

Gets the finite dispatched observer-invocation ceiling.

### <code>P:Fluent.Pdf.PdfGenerationOptions.MaximumRecords</code>

Gets the finite source record ceiling, with at most one additional lookahead.

### <code>P:Fluent.Pdf.PdfGenerationOptions.Observer</code>

Gets the caller-owned synchronous cooperative observer; it receives immutable views.

### <code>P:Fluent.Pdf.PdfGenerationOptions.PageTemplate</code>

Gets optional final page decorations/numbering/contents convergence settings.

### <code>P:Fluent.Pdf.PdfGenerationPage.ColumnCount</code>

Gets final logical-column count.

### <code>P:Fluent.Pdf.PdfGenerationPage.ContentHeight</code>

Gets the occupied body extent in physical points.

### <code>P:Fluent.Pdf.PdfGenerationPage.Geometry</code>

Gets explicit immutable boxes, rotation and UserUnit.

### <code>P:Fluent.Pdf.PdfGenerationPage.Index</code>

Gets the zero-based physical page index.

### <code>P:Fluent.Pdf.PdfGenerationPage.LineCount</code>

Gets final body line count.

### <code>P:Fluent.Pdf.PdfGenerationPage.NoteCount</code>

Gets final note-placement count.

### <code>P:Fluent.Pdf.PdfGenerationPage.TableFragmentCount</code>

Gets final table-fragment count.

### <code>P:Fluent.Pdf.PdfGenerationTemplate`1.Identifier</code>

Gets the explicit trace identity. Cycle detection also retains actual template/record reference identity.

### <code>P:Fluent.Pdf.PdfGlyphMapping.GlyphIndex</code>

Gets the actual glyph identity.

### <code>P:Fluent.Pdf.PdfGlyphMapping.UnicodeText</code>

Gets exact logical Unicode text for ToUnicode mapping.

### <code>P:Fluent.Pdf.PdfGraphemeCluster.FirstScalar</code>

Gets the index of its first scalar.

### <code>P:Fluent.Pdf.PdfGraphemeCluster.ScalarCount</code>

Gets the scalar count.

### <code>P:Fluent.Pdf.PdfGraphemeCluster.Utf16Length</code>

Gets the original UTF-16 extent.

### <code>P:Fluent.Pdf.PdfGraphemeCluster.Utf16Offset</code>

Gets the original UTF-16 offset.

### <code>P:Fluent.Pdf.PdfGraphicsOccurrence.Definition</code>

Gets the immutable original definition, whose references belong to the manifest source graph.

### <code>P:Fluent.Pdf.PdfGraphicsOccurrence.Kind</code>

Gets whether this invocation is an image, inline image or form.

### <code>P:Fluent.Pdf.PdfGraphicsOccurrence.OperationIndex</code>

Gets the zero-based operator index within its immediate content stream program.

### <code>P:Fluent.Pdf.PdfGraphicsOccurrence.Transform</code>

Gets the accumulated matrix into native page user coordinates, including a form's own matrix.

### <code>P:Fluent.Pdf.PdfGraphicsResource.Category</code>

Gets the standard resource category.

### <code>P:Fluent.Pdf.PdfGraphicsResource.MinimumVersion</code>

Gets its aggregate explicitly validated PDF version floor.

### <code>P:Fluent.Pdf.PdfGraphicsResource.OperationReport</code>

Gets the bounded construction report.

### <code>P:Fluent.Pdf.PdfGraphicsState.BlendMode</code>

Gets the actual standard blend mode.

### <code>P:Fluent.Pdf.PdfGraphicsState.FillAlpha</code>

Gets the fill opacity.

### <code>P:Fluent.Pdf.PdfGraphicsState.Resource</code>

Gets its reusable ExtGState resource.

### <code>P:Fluent.Pdf.PdfGraphicsState.SoftMask</code>

Gets its optional exact soft mask.

### <code>P:Fluent.Pdf.PdfGraphicsState.StrokeAlpha</code>

Gets the stroke opacity.

### <code>P:Fluent.Pdf.PdfGraphicsState.SupportedBlendModes</code>

Gets all standard PDF separable and nonseparable blend mode names.

### <code>P:Fluent.Pdf.PdfHtmlOptions.EnableJavaScript</code>

Gets whether source document JavaScript execution is explicitly enabled.

### <code>P:Fluent.Pdf.PdfHtmlOptions.Locale</code>

Gets the explicit browser locale, independent of the host account.

### <code>P:Fluent.Pdf.PdfHtmlOptions.MaximumPages</code>

Gets the maximum accepted generated page count.

### <code>P:Fluent.Pdf.PdfHtmlOptions.Media</code>

Gets the explicit CSS media selected before navigation/readiness.

### <code>P:Fluent.Pdf.PdfHtmlOptions.PageSize</code>

Gets the requested physical print page size. The imported model retains the engine's actual page boxes and print precision.

### <code>P:Fluent.Pdf.PdfHtmlOptions.Print</code>

Gets the immutable explicit print policy.

### <code>P:Fluent.Pdf.PdfHtmlOptions.PrintBackground</code>

Gets whether CSS backgrounds are painted.

### <code>P:Fluent.Pdf.PdfHtmlOptions.Readiness</code>

Gets the immutable finite main-document readiness policy.

### <code>P:Fluent.Pdf.PdfHtmlOptions.TimeZone</code>

Gets the explicit browser timezone, independent of the host account.

### <code>P:Fluent.Pdf.PdfHtmlOptions.ViewportHeight</code>

Gets the CSS viewport height in device-independent pixels.

### <code>P:Fluent.Pdf.PdfHtmlOptions.ViewportWidth</code>

Gets the CSS viewport width in device-independent pixels.

### <code>P:Fluent.Pdf.PdfHtmlPrintOptions.Default</code>

Gets the shared immutable default: requested paper, zero margins, scale1, no templates or tagged-output claim.

### <code>P:Fluent.Pdf.PdfHtmlPrintOptions.FooterHtml</code>

Gets the corresponding footer fragment, or null. Native pageNumber/totalPages classes refer to final printed pages.

### <code>P:Fluent.Pdf.PdfHtmlPrintOptions.HeaderHtml</code>

Gets a bounded resource-free HTML/XHTML header fragment, or null. The renderer validates the explicit supported fragment profile under the operation context.

### <code>P:Fluent.Pdf.PdfHtmlPrintOptions.Landscape</code>

Gets whether the browser rotates the requested base paper to landscape.

### <code>P:Fluent.Pdf.PdfHtmlPrintOptions.MarginBottom</code>

Gets the bottom margin in PDF points.

### <code>P:Fluent.Pdf.PdfHtmlPrintOptions.MarginLeft</code>

Gets the left margin in PDF points.

### <code>P:Fluent.Pdf.PdfHtmlPrintOptions.MarginRight</code>

Gets the right margin in PDF points.

### <code>P:Fluent.Pdf.PdfHtmlPrintOptions.MarginTop</code>

Gets the top margin in PDF points.

### <code>P:Fluent.Pdf.PdfHtmlPrintOptions.PreferCssPageSize</code>

Gets whether CSS @page size takes precedence. False asks the engine to fit CSS layout to the requested paper.

### <code>P:Fluent.Pdf.PdfHtmlPrintOptions.Scale</code>

Gets the explicit browser content scale in0.1–2.

### <code>P:Fluent.Pdf.PdfHtmlPrintOptions.Tagged</code>

Gets whether to request and structurally validate tagged output. This does not request or establish PDF/UA conformance.

### <code>P:Fluent.Pdf.PdfHtmlReadinessOptions.Default</code>

Gets the shared immutable finite default.

### <code>P:Fluent.Pdf.PdfHtmlReadinessOptions.JavaScriptPromise</code>

Gets an explicitly trusted main-world JavaScript expression whose promise must resolve, or null. No document expression is discovered or executed implicitly.

### <code>P:Fluent.Pdf.PdfHtmlReadinessOptions.MaximumInFlightRequests</code>

Gets the permitted number of observed pending requests during the quiet interval.

### <code>P:Fluent.Pdf.PdfHtmlReadinessOptions.NetworkQuietPeriod</code>

Gets how long observed HTTP activity must remain at or below the configured in-flight threshold; zero disables this additional wait.

### <code>P:Fluent.Pdf.PdfHtmlReadinessOptions.PollingInterval</code>

Gets the cooperative DOM/asset inspection interval.

### <code>P:Fluent.Pdf.PdfHtmlReadinessOptions.Selectors</code>

Gets selectors which must each match a main-document element. Selector errors reject without echoing their source.

### <code>P:Fluent.Pdf.PdfHtmlReadinessOptions.Timeout</code>

Gets the phase timeout, further bounded by the operation/outer deadlines.

### <code>P:Fluent.Pdf.PdfHtmlReadinessOptions.WaitForDocumentComplete</code>

Gets whether the main document must reach complete readyState.

### <code>P:Fluent.Pdf.PdfHtmlReadinessOptions.WaitForFonts</code>

Gets whether declared fonts must stop loading. Known failures or explicitly skipped pending fonts remain reported losses.

### <code>P:Fluent.Pdf.PdfHtmlReadinessOptions.WaitForImages</code>

Gets whether main-document images must complete. Known failures or explicitly skipped pending images remain reported losses.

### <code>P:Fluent.Pdf.PdfHtmlResource.Address</code>

Gets the exact absolute HTTP(S) address used only for resource matching.

### <code>P:Fluent.Pdf.PdfHtmlResource.Length</code>

Gets the captured immutable byte length.

### <code>P:Fluent.Pdf.PdfHtmlResource.MediaType</code>

Gets the explicit response Content-Type.

### <code>P:Fluent.Pdf.PdfHtmlSource.Address</code>

Gets the absolute virtual main address and relative-resource base.

### <code>P:Fluent.Pdf.PdfHtmlSource.Markup</code>

Gets explicit logical HTML markup, or null when the main document requires a supplied resource or explicit renderer acquisition policy.

### <code>P:Fluent.Pdf.PdfHtmlSource.OperationReport</code>

Gets the successful source preparation report.

### <code>P:Fluent.Pdf.PdfHtmlSource.Resources</code>

Gets the explicit resource map; document creation never follows these URLs.

### <code>P:Fluent.Pdf.PdfImageAsset.Definition</code>

Gets the exact detached PDF image stream, including color, Decode, masks and metadata.

### <code>P:Fluent.Pdf.PdfImageAsset.DisplayAspect</code>

Gets physical display aspect after orientation and nonsquare-pixel correction.

### <code>P:Fluent.Pdf.PdfImageAsset.HasOriginalFile</code>

Gets whether the source file is retained exactly for explicit export.

### <code>P:Fluent.Pdf.PdfImageAsset.Info</code>

Gets encoded sample geometry before display orientation.

### <code>P:Fluent.Pdf.PdfImageAsset.IsStencil</code>

Gets whether the image is a one-bit paint stencil.

### <code>P:Fluent.Pdf.PdfImageAsset.MediaType</code>

Gets the original file media type, or null for authored/PDF-stream images.

### <code>P:Fluent.Pdf.PdfImageAsset.Metadata</code>

Gets immutable display and resolution metadata.

### <code>P:Fluent.Pdf.PdfImageAsset.NaturalSize</code>

Gets the natural oriented size in points when physical resolution is known and representable; otherwise null.

### <code>P:Fluent.Pdf.PdfImageAsset.OperationReport</code>

Gets the successful bounded construction or extraction report.

### <code>P:Fluent.Pdf.PdfImageAsset.Resource</code>

Gets the exact reusable PDF image XObject, including its color/mask dependencies.

### <code>P:Fluent.Pdf.PdfImageAsset.SoftMask</code>

Gets the optional separate unassociated alpha image.

### <code>P:Fluent.Pdf.PdfImageDeduplicationResult.Images</code>

Gets input-order images with only identical safe assets sharing an instance.

### <code>P:Fluent.Pdf.PdfImageDeduplicationResult.OperationReport</code>

Gets the completed shared-context comparison report.

### <code>P:Fluent.Pdf.PdfImageDeduplicationResult.UniqueCount</code>

Gets the number of distinct retained asset instances.

### <code>P:Fluent.Pdf.PdfImageInfo.BitsPerComponent</code>

Gets the precision of each unsigned component.

### <code>P:Fluent.Pdf.PdfImageInfo.ByteLength</code>

Gets the complete sample byte count, including row padding.

### <code>P:Fluent.Pdf.PdfImageInfo.Components</code>

Gets the interleaved component count.

### <code>P:Fluent.Pdf.PdfImageInfo.Height</code>

Gets the positive sample height.

### <code>P:Fluent.Pdf.PdfImageInfo.RowBytes</code>

Gets the byte-aligned row length.

### <code>P:Fluent.Pdf.PdfImageInfo.Width</code>

Gets the positive sample width.

### <code>P:Fluent.Pdf.PdfImageMetadata.HorizontalDpi</code>

Gets horizontal samples per inch, or zero when unknown.

### <code>P:Fluent.Pdf.PdfImageMetadata.Orientation</code>

Gets the explicit display orientation.

### <code>P:Fluent.Pdf.PdfImageMetadata.PixelAspectRatio</code>

Gets physical pixel width divided by physical pixel height.

### <code>P:Fluent.Pdf.PdfImageMetadata.VerticalDpi</code>

Gets vertical samples per inch, or zero when unknown.

### <code>P:Fluent.Pdf.PdfImageReadOptions.AllowLosslessTranscode</code>

Gets permission to separate alpha or deinterlace using an explicit lossless transcode when byte-preserving PDF packaging is impossible.

### <code>P:Fluent.Pdf.PdfImageReadOptions.ApplyExifOrientation</code>

Gets whether file-declared Exif orientation controls placement.

### <code>P:Fluent.Pdf.PdfImageReadOptions.ColorSpaceOverride</code>

Gets an explicit component interpretation for a file without an embedded ICC profile; sample values remain unchanged.

### <code>P:Fluent.Pdf.PdfInputConversionOptions.Generation</code>

Gets generation layout, culture and precommit hooks.

### <code>P:Fluent.Pdf.PdfInputConversionOptions.MaximumInputBytes</code>

Gets the source-byte ceiling, in addition to operation limits.

### <code>P:Fluent.Pdf.PdfInputConversionOptions.MaximumNodes</code>

Gets the total syntax/model node ceiling.

### <code>P:Fluent.Pdf.PdfInputConversionOptions.Resources</code>

Gets captured drawings by ordinal name; source paths and URLs are never resolved.

### <code>P:Fluent.Pdf.PdfInputConversionOptions.Styles</code>

Gets explicit styles by ordinal name.

### <code>P:Fluent.Pdf.PdfInputDrawing.Drawing</code>

Gets the supplied drawing.

### <code>P:Fluent.Pdf.PdfInputDrawing.Height</code>

Gets the requested physical height.

### <code>P:Fluent.Pdf.PdfInputDrawing.SourceBounds</code>

Gets the source coordinate rectangle.

### <code>P:Fluent.Pdf.PdfInputDrawing.Width</code>

Gets the requested physical width.

### <code>P:Fluent.Pdf.PdfInvoiceData.Buyer</code>

Gets literal buyer name.

### <code>P:Fluent.Pdf.PdfInvoiceData.Currency</code>

Gets explicit currency code.

### <code>P:Fluent.Pdf.PdfInvoiceData.GrandTotal</code>

Gets reconciled gross/due amount; no prepayment or discount is inferred.

### <code>P:Fluent.Pdf.PdfInvoiceData.Identifier</code>

Gets literal invoice identifier.

### <code>P:Fluent.Pdf.PdfInvoiceData.IssueTime</code>

Gets explicit whole-second UTC issue time.

### <code>P:Fluent.Pdf.PdfInvoiceData.LineNetAmounts</code>

Gets per-line rounded net amounts.

### <code>P:Fluent.Pdf.PdfInvoiceData.Lines</code>

Gets immutable literal lines.

### <code>P:Fluent.Pdf.PdfInvoiceData.MinorUnits</code>

Gets currency minor-unit exponent.

### <code>P:Fluent.Pdf.PdfInvoiceData.NetTotal</code>

Gets the exact sum of rounded line net amounts.

### <code>P:Fluent.Pdf.PdfInvoiceData.Profile</code>

Gets exact requested profile.

### <code>P:Fluent.Pdf.PdfInvoiceData.Rounding</code>

Gets explicit midpoint rounding policy.

### <code>P:Fluent.Pdf.PdfInvoiceData.Seller</code>

Gets literal seller name.

### <code>P:Fluent.Pdf.PdfInvoiceData.TaxCategories</code>

Gets the ordered per-rate tax breakdown.

### <code>P:Fluent.Pdf.PdfInvoiceData.TaxTotal</code>

Gets the exact sum of rounded category tax amounts.

### <code>P:Fluent.Pdf.PdfInvoiceLine.Description</code>

Gets literal description.

### <code>P:Fluent.Pdf.PdfInvoiceLine.Quantity</code>

Gets positive quantity.

### <code>P:Fluent.Pdf.PdfInvoiceLine.TaxRate</code>

Gets percentage tax rate.

### <code>P:Fluent.Pdf.PdfInvoiceLine.UnitPrice</code>

Gets nonnegative unit price.

### <code>P:Fluent.Pdf.PdfInvoiceTaxCategory.TaxAmount</code>

Gets tax rounded once on the category taxable amount.

### <code>P:Fluent.Pdf.PdfInvoiceTaxCategory.TaxRate</code>

Gets the percentage tax rate.

### <code>P:Fluent.Pdf.PdfInvoiceTaxCategory.TaxableAmount</code>

Gets the sum of rounded line net amounts in this category.

### <code>P:Fluent.Pdf.PdfInvoiceValidation.ArchivalValidation</code>

Gets separately bound complete PDF/A-3b validation after successful SDK publication.

### <code>P:Fluent.Pdf.PdfInvoiceValidation.Completed</code>

Gets complete-rule validation status.

### <code>P:Fluent.Pdf.PdfInvoiceValidation.Findings</code>

Gets immutable stable rule findings.

### <code>P:Fluent.Pdf.PdfInvoiceValidation.IsCompliant</code>

Gets independent compliance observation.

### <code>P:Fluent.Pdf.PdfInvoiceValidation.OperationReport</code>

Gets complete SDK operation diagnostics after successful publication.

### <code>P:Fluent.Pdf.PdfInvoiceValidation.PdfSha256</code>

Gets exact validated PDF digest.

### <code>P:Fluent.Pdf.PdfInvoiceValidation.Profile</code>

Gets exact invoice version/profile.

### <code>P:Fluent.Pdf.PdfInvoiceValidation.RequestSha256</code>

Gets the exact SDK model/settings request digest.

### <code>P:Fluent.Pdf.PdfInvoiceValidation.XmlSha256</code>

Gets exact associated XML digest.

### <code>P:Fluent.Pdf.PdfLayerGroup.Definition</code>

Gets raw intent, usage and custom values without evaluating external context.

### <code>P:Fluent.Pdf.PdfLayerGroup.Id</code>

Gets the exact indirect identity used in membership/configuration dictionaries.

### <code>P:Fluent.Pdf.PdfLayerGroup.Name</code>

Gets the Unicode layer name.

### <code>P:Fluent.Pdf.PdfLayerGroup.Source</code>

Gets the captured source.

### <code>P:Fluent.Pdf.PdfLayerSet.Configurations</code>

Gets resolved alternate configurations in source order.

### <code>P:Fluent.Pdf.PdfLayerSet.Groups</code>

Gets registered OCG identities, retaining duplicate display names.

### <code>P:Fluent.Pdf.PdfLayerSet.OperationReport</code>

Gets inventory diagnostics and resource usage.

### <code>P:Fluent.Pdf.PdfLayerSet.Properties</code>

Gets the raw resolved OCProperties dictionary, or null.

### <code>P:Fluent.Pdf.PdfLayerSet.Source</code>

Gets the captured source.

### <code>P:Fluent.Pdf.PdfLayerVisibility.OperationReport</code>

Gets diagnostics and resource usage.

### <code>P:Fluent.Pdf.PdfLayerVisibility.Visible</code>

Gets whether the membership is visible for the selected configuration, intents and usage event.

### <code>P:Fluent.Pdf.PdfLinearizationInfo.FileLength</code>

Gets the exact declared file length.

### <code>P:Fluent.Pdf.PdfLinearizationInfo.FirstPageEnd</code>

Gets the declared end of the first-page section.

### <code>P:Fluent.Pdf.PdfLinearizationInfo.FirstPageIndex</code>

Gets the declared zero-based first displayed page index.

### <code>P:Fluent.Pdf.PdfLinearizationInfo.FirstPageObjectNumber</code>

Gets the first displayed page object number.

### <code>P:Fluent.Pdf.PdfLinearizationInfo.HintLength</code>

Gets the primary hint stream object's declared byte extent.

### <code>P:Fluent.Pdf.PdfLinearizationInfo.HintOffset</code>

Gets the primary hint stream object's byte offset.

### <code>P:Fluent.Pdf.PdfLinearizationInfo.MainCrossReferenceOffset</code>

Gets the declared main cross-reference entry offset.

### <code>P:Fluent.Pdf.PdfLinearizationInfo.PageCount</code>

Gets the declared page count.

### <code>P:Fluent.Pdf.PdfLinearizationOptions.AllowSignatureInvalidation</code>

Gets explicit full-rewrite signature invalidation authority.

### <code>P:Fluent.Pdf.PdfLinearizationOptions.FirstPageIndex</code>

Gets the zero-based first displayed page.

### <code>P:Fluent.Pdf.PdfLinearizationValidation.Completed</code>

Gets whether every applicable offset and hint rule was checked.

### <code>P:Fluent.Pdf.PdfLinearizationValidation.Findings</code>

Gets the immutable rule findings.

### <code>P:Fluent.Pdf.PdfLinearizationValidation.IsValid</code>

Gets the independent validity observation.

### <code>P:Fluent.Pdf.PdfLinearizationValidation.OperationReport</code>

Gets the completed public operation report, or null for a provider-only result.

### <code>P:Fluent.Pdf.PdfLinearizationValidation.RequestSha256</code>

Gets the SDK-supplied request digest.

### <code>P:Fluent.Pdf.PdfLinearizationValidation.SourceSha256</code>

Gets the exact validated PDF digest.

### <code>P:Fluent.Pdf.PdfMailMergeOptions.Evaluation</code>

Gets closed finite evaluation policy with the same explicit culture.

### <code>P:Fluent.Pdf.PdfMailMergeOptions.Generation</code>

Gets generation layout, explicit culture and precommit hooks.

### <code>P:Fluent.Pdf.PdfMailMergeOptions.Styles</code>

Gets immutable explicitly supplied text styles.

### <code>P:Fluent.Pdf.PdfMargins.Bottom</code>

Gets the bottom margin.

### <code>P:Fluent.Pdf.PdfMargins.Left</code>

Gets the left margin.

### <code>P:Fluent.Pdf.PdfMargins.Right</code>

Gets the right margin.

### <code>P:Fluent.Pdf.PdfMargins.Top</code>

Gets the top margin.

### <code>P:Fluent.Pdf.PdfMatrix.A</code>

Gets x scale.

### <code>P:Fluent.Pdf.PdfMatrix.B</code>

Gets x-to-y skew.

### <code>P:Fluent.Pdf.PdfMatrix.C</code>

Gets y-to-x skew.

### <code>P:Fluent.Pdf.PdfMatrix.D</code>

Gets y scale.

### <code>P:Fluent.Pdf.PdfMatrix.E</code>

Gets horizontal translation.

### <code>P:Fluent.Pdf.PdfMatrix.F</code>

Gets vertical translation.

### <code>P:Fluent.Pdf.PdfMatrix.Identity</code>

Gets the identity transform.

### <code>P:Fluent.Pdf.PdfMergeCondition.Path</code>

Gets the boolean field path.

### <code>P:Fluent.Pdf.PdfMergeCondition.WhenFalse</code>

Gets the false instructions.

### <code>P:Fluent.Pdf.PdfMergeCondition.WhenTrue</code>

Gets the true instructions.

### <code>P:Fluent.Pdf.PdfMergeEvaluationOptions.Culture</code>

Gets the captured immutable standard culture.

### <code>P:Fluent.Pdf.PdfMergeEvaluationOptions.MaximumRecords</code>

Gets the total expanded record ceiling across all nested repetitions.

### <code>P:Fluent.Pdf.PdfMergeEvaluationOptions.MaximumVisits</code>

Gets the total instruction/field visit ceiling.

### <code>P:Fluent.Pdf.PdfMergeEvaluationOptions.MissingFields</code>

Gets the explicit absent-field policy.

### <code>P:Fluent.Pdf.PdfMergeImage.Height</code>

Gets physical height in points.

### <code>P:Fluent.Pdf.PdfMergeImage.Width</code>

Gets physical width in points.

### <code>P:Fluent.Pdf.PdfMergeImageBlock.Path</code>

Gets the image field path.

### <code>P:Fluent.Pdf.PdfMergeParagraph.Runs</code>

Gets immutable runs.

### <code>P:Fluent.Pdf.PdfMergeParagraph.Style</code>

Gets paragraph layout options.

### <code>P:Fluent.Pdf.PdfMergeRecord.Fields</code>

Gets immutable ordinal fields.

### <code>P:Fluent.Pdf.PdfMergeRepeat.Children</code>

Gets immutable child instructions.

### <code>P:Fluent.Pdf.PdfMergeRepeat.Path</code>

Gets the collection field path.

### <code>P:Fluent.Pdf.PdfMergeRun.Format</code>

Gets the optional scalar format.

### <code>P:Fluent.Pdf.PdfMergeRun.IsField</code>

Gets whether this is a field lookup.

### <code>P:Fluent.Pdf.PdfMergeRun.Style</code>

Gets the explicit style name.

### <code>P:Fluent.Pdf.PdfMergeRun.Value</code>

Gets literal text or a field path.

### <code>P:Fluent.Pdf.PdfMergeTable.Columns</code>

Gets immutable column definitions.

### <code>P:Fluent.Pdf.PdfMergeTable.Path</code>

Gets the collection path.

### <code>P:Fluent.Pdf.PdfMergeTableColumn.Column</code>

Gets immutable column width policy.

### <code>P:Fluent.Pdf.PdfMergeTableColumn.Header</code>

Gets optional literal header text; null means no header text for this column.

### <code>P:Fluent.Pdf.PdfMergeTableColumn.Runs</code>

Gets immutable row-relative cell runs.

### <code>P:Fluent.Pdf.PdfMergeTemplate.Blocks</code>

Gets immutable root instructions.

### <code>P:Fluent.Pdf.PdfMergeTemplate.Identifier</code>

Gets the stable template identifier.

### <code>P:Fluent.Pdf.PdfMetadataRemovalOptions.EmbeddedFiles</code>

Gets the explicit embedded-file decision.

### <code>P:Fluent.Pdf.PdfMetadataRemovalOptions.RemoveIdentifiers</code>

Gets whether trailer identifiers are removed.

### <code>P:Fluent.Pdf.PdfMetadataSet.Identity</code>

Gets the resolved trailer ID value, including unsupported source representations.

### <code>P:Fluent.Pdf.PdfMetadataSet.Information</code>

Gets the resolved original Info dictionary or null, retaining unsupported values.

### <code>P:Fluent.Pdf.PdfMetadataSet.OperationReport</code>

Gets the operation diagnostics and resource usage.

### <code>P:Fluent.Pdf.PdfMetadataSet.Source</code>

Gets the captured source used for subsequent metadata edits.

### <code>P:Fluent.Pdf.PdfMetadataSet.Text</code>

Gets decoded Info text strings. Date strings retain their original precision and offsets.

### <code>P:Fluent.Pdf.PdfMetadataSet.XmpXml</code>

Gets the validated XML packet, or null when no document XMP exists.

### <code>P:Fluent.Pdf.PdfNamedDestination.Definition</code>

Gets the preserved source value.

### <code>P:Fluent.Pdf.PdfNamedDestination.Name</code>

Gets the decoded destination name.

### <code>P:Fluent.Pdf.PdfNamedDestination.PageIndex</code>

Gets the target's physical index in the captured snapshot.

### <code>P:Fluent.Pdf.PdfNativeAsset.FileName</code>

Gets the single file name, relative to the explicitly configured directory.

### <code>P:Fluent.Pdf.PdfNativeAsset.IsNativeBinary</code>

Gets whether this asset requires a native binary header/architecture check.

### <code>P:Fluent.Pdf.PdfNativeAsset.Length</code>

Gets the expected positive byte length.

### <code>P:Fluent.Pdf.PdfNativeAsset.Sha256</code>

Gets the expected lowercase SHA-256 hex digest.

### <code>P:Fluent.Pdf.PdfNativeDeployment.Abi</code>

Gets the caller-declared ABI label; exports/libc/loader compatibility require a later isolated probe.

### <code>P:Fluent.Pdf.PdfNativeDeployment.Assets</code>

Gets the defensive read-only asset inventory, in diagnostic order.

### <code>P:Fluent.Pdf.PdfNativeDeployment.EngineId</code>

Gets the declared engine identity.

### <code>P:Fluent.Pdf.PdfNativeDeployment.EngineVersion</code>

Gets the exact caller-pinned engine version label.

### <code>P:Fluent.Pdf.PdfNativeDeployment.MaximumTotalBytes</code>

Gets the maximum cumulative declared asset bytes.

### <code>P:Fluent.Pdf.PdfNativeDeployment.RootDirectory</code>

Gets the normalized local directory; the inspector creates no files there.

### <code>P:Fluent.Pdf.PdfNativeDeployment.RuntimeIdentifier</code>

Gets the declared target RID.

### <code>P:Fluent.Pdf.PdfNativeDeploymentInspector.CurrentRuntimeIdentifier</code>

Gets a supported RID for the current OS and process architecture, or null.

### <code>P:Fluent.Pdf.PdfNativeDeploymentInspector.OperatingSystemRuntimeIdentifier</code>

Gets the supported native OS RID, which can differ from a 32-bit or emulated caller process.

### <code>P:Fluent.Pdf.PdfNativeDeploymentReport.AssetsVerified</code>

Gets whether every asset passed on the declared host; this never asserts dependency loading or execution safety.

### <code>P:Fluent.Pdf.PdfNativeDeploymentReport.CurrentRuntimeIdentifier</code>

Gets the actual supported OS/process-architecture RID, or null for an unsupported host.

### <code>P:Fluent.Pdf.PdfNativeDeploymentReport.Deployment</code>

Gets the immutable deployment description inspected.

### <code>P:Fluent.Pdf.PdfNativeDeploymentReport.Diagnostics</code>

Gets stable failures in asset inventory order, including portable asset names but excluding directory paths and file contents.

### <code>P:Fluent.Pdf.PdfNativeDeploymentReport.VerifiedAssetCount</code>

Gets how many pinned files passed size, hash and applicable header/architecture checks.

### <code>P:Fluent.Pdf.PdfNavigationSet.Actions</code>

Gets every action definition, known attachment slot and opaque active-content observation.

### <code>P:Fluent.Pdf.PdfNavigationSet.NamedDestinations</code>

Gets the named destinations, resolving page identities without executing actions.

### <code>P:Fluent.Pdf.PdfNavigationSet.OperationReport</code>

Gets the bounded inspection report.

### <code>P:Fluent.Pdf.PdfNavigationSet.Source</code>

Gets the detached normalized input snapshot.

### <code>P:Fluent.Pdf.PdfObjectId.Generation</code>

Gets the generation.

### <code>P:Fluent.Pdf.PdfObjectId.Number</code>

Gets the object number.

### <code>P:Fluent.Pdf.PdfOcrAccuracyGroup.CharacterErrorRate</code>

Gets CER, or null when the corpus group has no reference characters.

### <code>P:Fluent.Pdf.PdfOcrAccuracyGroup.CharacterErrors</code>

Gets character substitutions/insertions/deletions.

### <code>P:Fluent.Pdf.PdfOcrAccuracyGroup.Language</code>

Gets exact language/selection identity.

### <code>P:Fluent.Pdf.PdfOcrAccuracyGroup.ModelIdentity</code>

Gets exact model/version selection identity.

### <code>P:Fluent.Pdf.PdfOcrAccuracyGroup.ReferenceCharacters</code>

Gets corpus reference Unicode scalar count, including whitespace.

### <code>P:Fluent.Pdf.PdfOcrAccuracyGroup.ReferenceWords</code>

Gets corpus reference whitespace-delimited word count.

### <code>P:Fluent.Pdf.PdfOcrAccuracyGroup.Samples</code>

Gets the observed case count.

### <code>P:Fluent.Pdf.PdfOcrAccuracyGroup.Thresholds</code>

Gets the predeclared limits applied to this observation.

### <code>P:Fluent.Pdf.PdfOcrAccuracyGroup.ThresholdsMet</code>

Gets whether nonempty reference character/word observations meet both thresholds. All-blank groups cannot pass.

### <code>P:Fluent.Pdf.PdfOcrAccuracyGroup.WordErrorRate</code>

Gets WER, or null when the corpus group has no reference words.

### <code>P:Fluent.Pdf.PdfOcrAccuracyGroup.WordErrors</code>

Gets word substitutions/insertions/deletions.

### <code>P:Fluent.Pdf.PdfOcrAccuracyOptions.MaximumCharactersPerSample</code>

Gets each reference/recognized string's UTF16 ceiling.

### <code>P:Fluent.Pdf.PdfOcrAccuracyOptions.MaximumComparisonCells</code>

Gets the total bounded character-plus-word edit-distance matrix cell ceiling.

### <code>P:Fluent.Pdf.PdfOcrAccuracyOptions.MaximumSamples</code>

Gets the finite corpus case ceiling.

### <code>P:Fluent.Pdf.PdfOcrAccuracyOptions.Thresholds</code>

Gets explicit exact-language thresholds.

### <code>P:Fluent.Pdf.PdfOcrAccuracyReport.ComparisonCells</code>

Gets bounded comparison work charged against the explicit evaluation ceiling.

### <code>P:Fluent.Pdf.PdfOcrAccuracyReport.CorpusIdentifier</code>

Gets explicit retained corpus identity.

### <code>P:Fluent.Pdf.PdfOcrAccuracyReport.CorpusVersion</code>

Gets explicit corpus version.

### <code>P:Fluent.Pdf.PdfOcrAccuracyReport.Groups</code>

Gets separate observations for each exact language/model pair.

### <code>P:Fluent.Pdf.PdfOcrAccuracyReport.OperationReport</code>

Gets root diagnostics and budgets.

### <code>P:Fluent.Pdf.PdfOcrAccuracyReport.RecognitionSha256</code>

Gets exact ordered sample/identity/recognition digest.

### <code>P:Fluent.Pdf.PdfOcrAccuracyReport.ReferenceSha256</code>

Gets exact ordered sample/identity/reference digest.

### <code>P:Fluent.Pdf.PdfOcrAccuracySample.Identifier</code>

Gets the separately labeled corpus case identity.

### <code>P:Fluent.Pdf.PdfOcrAccuracySample.Language</code>

Gets the exact declared language or mixed-model alias selection.

### <code>P:Fluent.Pdf.PdfOcrAccuracySample.ModelIdentity</code>

Gets the exact observed model selection identity; versions are never pooled implicitly.

### <code>P:Fluent.Pdf.PdfOcrAccuracySample.RecognizedText</code>

Gets exact recognized Unicode, without confidence-based alteration.

### <code>P:Fluent.Pdf.PdfOcrAccuracySample.ReferenceText</code>

Gets exact corpus reference Unicode.

### <code>P:Fluent.Pdf.PdfOcrAccuracyThresholds.MaximumCharacterErrorRate</code>

Gets the predeclared maximum character error ratio.

### <code>P:Fluent.Pdf.PdfOcrAccuracyThresholds.MaximumWordErrorRate</code>

Gets the predeclared maximum word error ratio.

### <code>P:Fluent.Pdf.PdfOcrLanguageModel.ByteLength</code>

Gets the exact trained-data byte length.

### <code>P:Fluent.Pdf.PdfOcrLanguageModel.FileName</code>

Gets the exact flat staged model filename.

### <code>P:Fluent.Pdf.PdfOcrLanguageModel.Identifier</code>

Gets the exact native alias, without a path or inferred language selection.

### <code>P:Fluent.Pdf.PdfOcrLanguageModel.Languages</code>

Gets declared language tags; empty denotes an explicitly script-only declaration.

### <code>P:Fluent.Pdf.PdfOcrLanguageModel.License</code>

Gets the explicitly declared license identity; legal qualification remains separate.

### <code>P:Fluent.Pdf.PdfOcrLanguageModel.Scripts</code>

Gets declared supported scripts, without an accuracy guarantee.

### <code>P:Fluent.Pdf.PdfOcrLanguageModel.Sha256</code>

Gets the exact lowercase trained-data SHA256.

### <code>P:Fluent.Pdf.PdfOcrLanguageModel.SourceId</code>

Gets a retained provenance/source identity; no URI or resource is fetched.

### <code>P:Fluent.Pdf.PdfOcrLanguageModel.Version</code>

Gets the explicit model release/version.

### <code>P:Fluent.Pdf.PdfOcrModelCatalog.Models</code>

Gets immutable explicit model metadata.

### <code>P:Fluent.Pdf.PdfOcrModelSelection.Identity</code>

Gets the lowercase digest binding ordered aliases, versions, content hashes and byte lengths.

### <code>P:Fluent.Pdf.PdfOcrModelSelection.Language</code>

Gets exact ordered plus-separated native aliases.

### <code>P:Fluent.Pdf.PdfOcrModelSelection.Models</code>

Gets exact model metadata in requested order.

### <code>P:Fluent.Pdf.PdfOcrModelSelection.OperationReport</code>

Gets root operation diagnostics after public selection; null for internal same-context selection.

### <code>P:Fluent.Pdf.PdfOcrOptions.Language</code>

Gets the explicitly requested language/model selection token.

### <code>P:Fluent.Pdf.PdfOcrOptions.MaximumCharacters</code>

Gets the total returned UTF16 character ceiling across text and word records.

### <code>P:Fluent.Pdf.PdfOcrOptions.MaximumWords</code>

Gets the finite word record ceiling.

### <code>P:Fluent.Pdf.PdfOcrOptions.Segmentation</code>

Gets explicit segmentation.

### <code>P:Fluent.Pdf.PdfOcrOverlayOptions.ExistingTextPolicy</code>

Gets the explicitly selected duplicate-layer policy.

### <code>P:Fluent.Pdf.PdfOcrOverlayOptions.MaximumOverlapComparisons</code>

Gets total finite digital/OCR word comparison ceiling.

### <code>P:Fluent.Pdf.PdfOcrOverlayOptions.MaximumWords</code>

Gets maximum recognized words considered.

### <code>P:Fluent.Pdf.PdfOcrOverlayOptions.OverlapCoverage</code>

Gets minimum area coverage on both existing/OCR word rectangles.

### <code>P:Fluent.Pdf.PdfOcrPageFrame.DisplayedRegion</code>

Gets explicit top-left displayed physical-point region.

### <code>P:Fluent.Pdf.PdfOcrPageFrame.Geometry</code>

Gets exact source page geometry; applying to another geometry rejects.

### <code>P:Fluent.Pdf.PdfOcrPageFrame.PixelToUser</code>

Gets the exact pixel-to-native-PDF affine transform.

### <code>P:Fluent.Pdf.PdfOcrPageFrame.RasterHeight</code>

Gets exact raster height.

### <code>P:Fluent.Pdf.PdfOcrPageFrame.RasterWidth</code>

Gets exact raster width.

### <code>P:Fluent.Pdf.PdfOcrRaster.ByteLength</code>

Gets exact captured byte count.

### <code>P:Fluent.Pdf.PdfOcrRaster.Dpi</code>

Gets explicit source DPI.

### <code>P:Fluent.Pdf.PdfOcrRaster.Height</code>

Gets pixel height.

### <code>P:Fluent.Pdf.PdfOcrRaster.OperationReport</code>

Gets the capture report, or null for an internal same-context raster.

### <code>P:Fluent.Pdf.PdfOcrRaster.PixelFormat</code>

Gets explicit sample order.

### <code>P:Fluent.Pdf.PdfOcrRaster.Stride</code>

Gets declared bytes per scanline including explicit padding.

### <code>P:Fluent.Pdf.PdfOcrRaster.Width</code>

Gets pixel width.

### <code>P:Fluent.Pdf.PdfOcrResult.EngineVersion</code>

Gets the actually checked native engine version.

### <code>P:Fluent.Pdf.PdfOcrResult.Language</code>

Gets the exact recognized language selection, without silent fallback.

### <code>P:Fluent.Pdf.PdfOcrResult.MeanConfidence</code>

Gets engine mean confidence0–100, or null; it does not qualify accuracy.

### <code>P:Fluent.Pdf.PdfOcrResult.ModelIdentity</code>

Gets exact explicit model provenance, such as a pinned content digest.

### <code>P:Fluent.Pdf.PdfOcrResult.Models</code>

Gets exact selected model declarations; empty means the provider supplied only a legacy digest without complete model metadata.

### <code>P:Fluent.Pdf.PdfOcrResult.OperationReport</code>

Gets the root public operation report, or null for a provider-only result.

### <code>P:Fluent.Pdf.PdfOcrResult.Text</code>

Gets raw recognized Unicode in engine order.

### <code>P:Fluent.Pdf.PdfOcrResult.Words</code>

Gets words in engine-returned order, separately from later inferred reading order.

### <code>P:Fluent.Pdf.PdfOcrWord.Block</code>

Gets the engine block identity.

### <code>P:Fluent.Pdf.PdfOcrWord.Bounds</code>

Gets bounds in original raster pixels, with top-left origin.

### <code>P:Fluent.Pdf.PdfOcrWord.Confidence</code>

Gets engine confidence0–100, or null when absent. It is not an accuracy measurement.

### <code>P:Fluent.Pdf.PdfOcrWord.Line</code>

Gets the engine line identity within its paragraph.

### <code>P:Fluent.Pdf.PdfOcrWord.Paragraph</code>

Gets the engine paragraph identity within its block.

### <code>P:Fluent.Pdf.PdfOcrWord.Text</code>

Gets recognized Unicode without a correctness guarantee.

### <code>P:Fluent.Pdf.PdfOcrWord.Word</code>

Gets the engine word identity within its line.

### <code>P:Fluent.Pdf.PdfOperationContext.CancellationToken</code>

Gets cancellation combining the caller token and the operation deadline.

### <code>P:Fluent.Pdf.PdfOperationContext.Capability</code>

Gets the selected capability.

### <code>P:Fluent.Pdf.PdfOperationContext.Operation</code>

Gets the operation name assigned by the SDK.

### <code>P:Fluent.Pdf.PdfOperationContext.Policy</code>

Gets the immutable policy.

### <code>P:Fluent.Pdf.PdfOperationContext.ProviderId</code>

Gets the selected provider identity captured by the registry.

### <code>P:Fluent.Pdf.PdfOperationPolicy.Default</code>

Gets the immutable shared default policy.

### <code>P:Fluent.Pdf.PdfOperationPolicy.Limits</code>

Gets the immutable limits applied to each new operation.

### <code>P:Fluent.Pdf.PdfOperationPolicy.Mode</code>

Gets how declared fidelity losses are handled.

### <code>P:Fluent.Pdf.PdfOperationReport.Diagnostics</code>

Gets all accepted diagnostics, in reporting order.

### <code>P:Fluent.Pdf.PdfOperationReport.ElapsedTime</code>

Gets the measured monotonic duration.

### <code>P:Fluent.Pdf.PdfOperationReport.Failure</code>

Gets the latched policy failure, when one caused failure; original I/O/cancellation exceptions propagate separately.

### <code>P:Fluent.Pdf.PdfOperationReport.Operation</code>

Gets the SDK operation name.

### <code>P:Fluent.Pdf.PdfOperationReport.ProviderId</code>

Gets the captured registered provider ID.

### <code>P:Fluent.Pdf.PdfOperationReport.Resources</code>

Gets final cumulative/current counters and high-water marks for all resource kinds.

### <code>P:Fluent.Pdf.PdfOperationReport.Succeeded</code>

Gets whether the operation returned successfully, including file commit where applicable.

### <code>P:Fluent.Pdf.PdfOptimizationOptions.AllowSignatureInvalidation</code>

Gets explicit authority to rewrite signature/certification byte ranges without a validity claim.

### <code>P:Fluent.Pdf.PdfOptimizationOptions.CleanupObjects</code>

Gets whether unreachable active objects are removed.

### <code>P:Fluent.Pdf.PdfOptimizationOptions.CompressStreams</code>

Gets whether unfiltered internal streams are losslessly compressed when their encoded bytes shrink.

### <code>P:Fluent.Pdf.PdfOptimizationOptions.DeduplicateResources</code>

Gets whether exactly equal safe image/font-program streams are shared.

### <code>P:Fluent.Pdf.PdfOptimizationOptions.MaximumImageDimension</code>

Gets the image-plane dimension bound, or zero for lossless.

### <code>P:Fluent.Pdf.PdfOptimizationOptions.Preset</code>

Gets the selected explicit preset.

### <code>P:Fluent.Pdf.PdfOptimizationOptions.RemoveUnusedResources</code>

Gets whether conservative complete-content resource-name analysis removes unused bindings.

### <code>P:Fluent.Pdf.PdfOptimizationResult.CompressedStreams</code>

Gets the number of streams whose encoded payload shrank through lossless compression.

### <code>P:Fluent.Pdf.PdfOptimizationResult.DownsampledImages</code>

Gets the number of explicitly resampled image planes.

### <code>P:Fluent.Pdf.PdfOptimizationResult.OperationReport</code>

Gets the complete operation evidence and explicit losses.

### <code>P:Fluent.Pdf.PdfOptimizationResult.OutputBytes</code>

Gets completed optimized PDF byte length.

### <code>P:Fluent.Pdf.PdfOptimizationResult.OutputSha256</code>

Gets exact output SHA256.

### <code>P:Fluent.Pdf.PdfOptimizationResult.RemovedObjects</code>

Gets the number of removed unreachable objects.

### <code>P:Fluent.Pdf.PdfOptimizationResult.RemovedResourceBindings</code>

Gets the number of resource-name bindings removed by conservative usage analysis.

### <code>P:Fluent.Pdf.PdfOptimizationResult.SavedBytes</code>

Gets measured byte savings; negative values disclose growth.

### <code>P:Fluent.Pdf.PdfOptimizationResult.SharedStreams</code>

Gets the number of exact duplicate image/font-program stream identities shared.

### <code>P:Fluent.Pdf.PdfOptimizationResult.SourceBytes</code>

Gets the normalized pre-optimization PDF byte length.

### <code>P:Fluent.Pdf.PdfOptimizationResult.SourceSha256</code>

Gets exact normalized source SHA256.

### <code>P:Fluent.Pdf.PdfOptions.Default</code>

Gets the shared immutable default options.

### <code>P:Fluent.Pdf.PdfOptions.DefaultPageSize</code>

Gets the size used by parameterless page creation.

### <code>P:Fluent.Pdf.PdfOptions.MaximumPages</code>

Gets the maximum number of pages retained by a document.

### <code>P:Fluent.Pdf.PdfOptions.OperationPolicy</code>

Gets the immutable policy applied independently to each read/save operation.

### <code>P:Fluent.Pdf.PdfOrganizationOptions.AllowSignatureInvalidation</code>

Gets explicit authorization to invalidate source signatures while rewriting.

### <code>P:Fluent.Pdf.PdfOrganizationOptions.FieldCollisions</code>

Gets the root form-field collision policy.

### <code>P:Fluent.Pdf.PdfOrganizationOptions.MaximumPages</code>

Gets the finite output page ceiling.

### <code>P:Fluent.Pdf.PdfOrganizationOptions.MissingDestinations</code>

Gets the omitted-target navigation policy.

### <code>P:Fluent.Pdf.PdfOutlineRecord.Children</code>

Gets the ordered immutable children.

### <code>P:Fluent.Pdf.PdfOutlineRecord.Definition</code>

Gets the original dictionary, including unsupported keys and actions.

### <code>P:Fluent.Pdf.PdfOutlineRecord.IsOpen</code>

Gets the source expansion state.

### <code>P:Fluent.Pdf.PdfOutlineRecord.ObjectId</code>

Gets the native source identity.

### <code>P:Fluent.Pdf.PdfOutlineRecord.TargetPageIndex</code>

Gets a resolved local target, or null for a grouping/remote/other action.

### <code>P:Fluent.Pdf.PdfOutlineRecord.Title</code>

Gets the decoded Unicode title.

### <code>P:Fluent.Pdf.PdfPadesValidation.Completed</code>

Gets whether full trust/time/revocation/profile checks completed.

### <code>P:Fluent.Pdf.PdfPadesValidation.Compliant</code>

Gets the independent compliance observation.

### <code>P:Fluent.Pdf.PdfPadesValidation.Profile</code>

Gets exact validated profile.

### <code>P:Fluent.Pdf.PdfPadesValidation.SourceSha256</code>

Gets exact validated byte identity.

### <code>P:Fluent.Pdf.PdfPage.CanvasLayers</code>

Gets immutable generated content layers in application order.

### <code>P:Fluent.Pdf.PdfPage.Content</code>

Gets or sets replacement decoded content. Null retains original content; an empty program clears it.

### <code>P:Fluent.Pdf.PdfPage.Geometry</code>

Gets or atomically replaces explicit page boxes, UserUnit and rotation.

### <code>P:Fluent.Pdf.PdfPage.GraphicsResources</code>

Captures the explicit replacement graphics bindings; original inherited resources remain in Source.

### <code>P:Fluent.Pdf.PdfPage.Rotation</code>

Gets or changes the clockwise display rotation without changing the physical size.

### <code>P:Fluent.Pdf.PdfPage.Size</code>

Gets or changes the unrotated physical page dimensions.

### <code>P:Fluent.Pdf.PdfPage.Source</code>

Gets the immutable original binding, or null for a newly authored page.

### <code>P:Fluent.Pdf.PdfPageBoxes.Art</code>

Gets effective ArtBox.

### <code>P:Fluent.Pdf.PdfPageBoxes.Bleed</code>

Gets effective BleedBox.

### <code>P:Fluent.Pdf.PdfPageBoxes.Crop</code>

Gets effective CropBox.

### <code>P:Fluent.Pdf.PdfPageBoxes.Item(Fluent.Pdf.PdfPageBox)</code>

Gets an explicitly selected standard box.

### <code>P:Fluent.Pdf.PdfPageBoxes.Media</code>

Gets MediaBox.

### <code>P:Fluent.Pdf.PdfPageBoxes.Trim</code>

Gets effective TrimBox.

### <code>P:Fluent.Pdf.PdfPageCollection.Count</code>

Gets the number of attached pages.

### <code>P:Fluent.Pdf.PdfPageCollection.Item(System.Int32)</code>

Gets the page at a zero-based index.

### <code>P:Fluent.Pdf.PdfPageFont.BaseFont</code>

Gets the declared/generated BaseFont name, or an empty string when a Type 3 source omits it.

### <code>P:Fluent.Pdf.PdfPageFont.ResourceName</code>

Gets the page resource dictionary key.

### <code>P:Fluent.Pdf.PdfPageFont.SourceDictionary</code>

Gets the immutable source dictionary, or null for an authored replacement.

### <code>P:Fluent.Pdf.PdfPageFont.Subtype</code>

Gets the PDF font subtype.

### <code>P:Fluent.Pdf.PdfPageGeometry.Boxes</code>

Gets the exact effective standard boxes in native user coordinates.

### <code>P:Fluent.Pdf.PdfPageGeometry.Rotation</code>

Gets clockwise viewer rotation.

### <code>P:Fluent.Pdf.PdfPageGeometry.Size</code>

Gets unrotated physical MediaBox dimensions in points.

### <code>P:Fluent.Pdf.PdfPageGeometry.UserUnit</code>

Gets physical points per native user unit.

### <code>P:Fluent.Pdf.PdfPageGraphics.Occurrences</code>

Gets invoked forms and images in depth-first paint order, including repeated uses separately.

### <code>P:Fluent.Pdf.PdfPageGraphics.OperationReport</code>

Gets the completed shared-context inspection report.

### <code>P:Fluent.Pdf.PdfPageGraphics.PageIndex</code>

Gets the zero-based captured page index.

### <code>P:Fluent.Pdf.PdfPageGraphics.Source</code>

Gets the detached source snapshot to which invocation identities belong.

### <code>P:Fluent.Pdf.PdfPageInput.Document</code>

Gets the detached input snapshot.

### <code>P:Fluent.Pdf.PdfPageInput.Selection</code>

Gets the optional ordered physical selection.

### <code>P:Fluent.Pdf.PdfPageLinkSnapshot.Bounds</code>

Gets bounds in the containing content coordinate system.

### <code>P:Fluent.Pdf.PdfPageLinkSnapshot.TargetPageIndex</code>

Gets the zero-based physical destination page index, or -1 for a URI.

### <code>P:Fluent.Pdf.PdfPageLinkSnapshot.TargetPoint</code>

Gets the destination in native PDF page coordinates.

### <code>P:Fluent.Pdf.PdfPageLinkSnapshot.Uri</code>

Gets a passive external URI, or null for an internal destination. No request is made by the SDK.

### <code>P:Fluent.Pdf.PdfPageNumberRange.FirstNumber</code>

Gets the positive first counter value.

### <code>P:Fluent.Pdf.PdfPageNumberRange.Prefix</code>

Gets the literal label prefix.

### <code>P:Fluent.Pdf.PdfPageNumberRange.StartPageIndex</code>

Gets the zero-based physical page where this range starts.

### <code>P:Fluent.Pdf.PdfPageNumberRange.Style</code>

Gets the standard PDF label style.

### <code>P:Fluent.Pdf.PdfPageNumbering.Ranges</code>

Gets the immutable ordered numbering ranges.

### <code>P:Fluent.Pdf.PdfPagePresentationRecord.DisplayDuration</code>

Gets the preserved Dur value or null.

### <code>P:Fluent.Pdf.PdfPagePresentationRecord.PageIndex</code>

Gets the physical input page.

### <code>P:Fluent.Pdf.PdfPagePresentationRecord.Transition</code>

Gets the preserved transition dictionary or null.

### <code>P:Fluent.Pdf.PdfPageResourceManifest.Artifacts</code>

Gets explicit Artifact scopes and associated paths/resource uses.

### <code>P:Fluent.Pdf.PdfPageResourceManifest.Content</code>

Gets captured container programs and effective resource scopes.

### <code>P:Fluent.Pdf.PdfPageResourceManifest.Declarations</code>

Gets resource bindings, separate from identities and actual uses.

### <code>P:Fluent.Pdf.PdfPageResourceManifest.Occurrences</code>

Gets actual page/Form resource uses with accumulated transforms.

### <code>P:Fluent.Pdf.PdfPageResourceManifest.OperationReport</code>

Gets complete bounded inspection diagnostics.

### <code>P:Fluent.Pdf.PdfPageResourceManifest.PageIndex</code>

Gets the current page index.

### <code>P:Fluent.Pdf.PdfPageResourceManifest.Paths</code>

Gets literal path constructions and paint/clip endings.

### <code>P:Fluent.Pdf.PdfPageResourceManifest.Resources</code>

Gets unique resource definitions, including declared unused resources.

### <code>P:Fluent.Pdf.PdfPageResourceManifest.Source</code>

Gets the detached captured graph owning all exposed references.

### <code>P:Fluent.Pdf.PdfPageResourceManifest.Stamps</code>

Gets recognized owned stamps on this page, validated independently of visual heuristics.

### <code>P:Fluent.Pdf.PdfPageSelection.Indices</code>

Gets physical page indices in requested output order.

### <code>P:Fluent.Pdf.PdfPageSize.A4</code>

Gets ISO A4 (210 by 297 mm), expressed in points.

### <code>P:Fluent.Pdf.PdfPageSize.Height</code>

Gets the unrotated height in points.

### <code>P:Fluent.Pdf.PdfPageSize.IsValid</code>

Gets whether this value has usable dimensions; the default struct value is invalid.

### <code>P:Fluent.Pdf.PdfPageSize.Letter</code>

Gets US Letter (8.5 by 11 inches), expressed in points.

### <code>P:Fluent.Pdf.PdfPageSize.Width</code>

Gets the unrotated width in points.

### <code>P:Fluent.Pdf.PdfPageSnapshot.CanvasLayers</code>

Gets generated immutable page-content layers in application order.

### <code>P:Fluent.Pdf.PdfPageSnapshot.Content</code>

Gets immutable replacement content, or null to retain original bytes.

### <code>P:Fluent.Pdf.PdfPageSnapshot.FontResources</code>

Gets immutable named replacement font resources.

### <code>P:Fluent.Pdf.PdfPageSnapshot.Geometry</code>

Gets immutable page boxes, UserUnit and rotation.

### <code>P:Fluent.Pdf.PdfPageSnapshot.GraphicsResources</code>

Gets immutable named replacement graphics resources.

### <code>P:Fluent.Pdf.PdfPageSnapshot.Links</code>

Gets resolved immutable internal links.

### <code>P:Fluent.Pdf.PdfPageSnapshot.Rotation</code>

Gets the clockwise display rotation.

### <code>P:Fluent.Pdf.PdfPageSnapshot.Size</code>

Gets the unrotated physical size.

### <code>P:Fluent.Pdf.PdfPageSnapshot.Source</code>

Gets the immutable original page binding, or null for a newly authored page.

### <code>P:Fluent.Pdf.PdfPageSnapshot.StructureRoles</code>

Gets ordered basic structure roles matching generated page MCIDs; this does not declare PDF/UA conformance.

### <code>P:Fluent.Pdf.PdfPageSource.Dictionary</code>

Gets the original leaf dictionary with unknown entries preserved.

### <code>P:Fluent.Pdf.PdfPageSource.Document</code>

Gets the immutable source object graph.

### <code>P:Fluent.Pdf.PdfPageSource.EffectiveAttributes</code>

Gets effective MediaBox/CropBox/Resources/Rotate entries, retaining references into Document.

### <code>P:Fluent.Pdf.PdfPageSource.Geometry</code>

Gets original effective page boxes, UserUnit and viewing rotation.

### <code>P:Fluent.Pdf.PdfPageSource.ObjectId</code>

Gets the original page object identity.

### <code>P:Fluent.Pdf.PdfPageSource.Rotation</code>

Gets the normalized original display rotation.

### <code>P:Fluent.Pdf.PdfPageSource.Size</code>

Gets the original unrotated physical size in points.

### <code>P:Fluent.Pdf.PdfPageTemplate.Footer</code>

Gets the final-page footer callback.

### <code>P:Fluent.Pdf.PdfPageTemplate.FooterHeight</code>

Gets the reserved footer height in points.

### <code>P:Fluent.Pdf.PdfPageTemplate.Gap</code>

Gets the gap between decoration and body in points.

### <code>P:Fluent.Pdf.PdfPageTemplate.Header</code>

Gets the final-page header callback.

### <code>P:Fluent.Pdf.PdfPageTemplate.HeaderHeight</code>

Gets the reserved header height in points.

### <code>P:Fluent.Pdf.PdfPageTemplate.MaximumPasses</code>

Gets the finite convergence pass limit.

### <code>P:Fluent.Pdf.PdfPageTemplate.Numbering</code>

Gets the immutable page numbering configuration.

### <code>P:Fluent.Pdf.PdfPageTemplateContext.Body</code>

Gets the final body layout.

### <code>P:Fluent.Pdf.PdfPageTemplateContext.IsFirstPage</code>

Gets whether this is the first physical page.

### <code>P:Fluent.Pdf.PdfPageTemplateContext.IsOddPage</code>

Gets whether the one-based physical page number is odd.

### <code>P:Fluent.Pdf.PdfPageTemplateContext.OperationContext</code>

Gets the shared context for synchronous callback work.

### <code>P:Fluent.Pdf.PdfPageTemplateContext.PageIndex</code>

Gets the zero-based physical page index.

### <code>P:Fluent.Pdf.PdfPageTemplateContext.PageLabel</code>

Gets the final formatted page label.

### <code>P:Fluent.Pdf.PdfPageTemplateContext.PageNumber</code>

Gets the one-based physical page number.

### <code>P:Fluent.Pdf.PdfPageTemplateContext.TotalPages</code>

Gets the final converged physical page count.

### <code>P:Fluent.Pdf.PdfPageTemplateResult.Body</code>

Gets the final body layout.

### <code>P:Fluent.Pdf.PdfPageTemplateResult.Decorations</code>

Gets the final artifact decorations in physical page order.

### <code>P:Fluent.Pdf.PdfPageTemplateResult.Entries</code>

Gets the resolved body heading destinations.

### <code>P:Fluent.Pdf.PdfPageTemplateResult.OperationReport</code>

Gets the completed bounded layout report.

### <code>P:Fluent.Pdf.PdfPageTemplateResult.Passes</code>

Gets the number of body layout passes.

### <code>P:Fluent.Pdf.PdfPageTemplateResult.Template</code>

Gets the template configuration.

### <code>P:Fluent.Pdf.PdfPageTransition.Dimension</code>

Gets the optional dimension.

### <code>P:Fluent.Pdf.PdfPageTransition.Direction</code>

Gets the optional angle or nondirectional Fly marker.

### <code>P:Fluent.Pdf.PdfPageTransition.Duration</code>

Gets the finite transition duration in seconds.

### <code>P:Fluent.Pdf.PdfPageTransition.Motion</code>

Gets the optional inward/outward motion.

### <code>P:Fluent.Pdf.PdfPageTransition.Rectangular</code>

Gets the optional Fly rectangular-area setting.

### <code>P:Fluent.Pdf.PdfPageTransition.Scale</code>

Gets the optional Fly scale.

### <code>P:Fluent.Pdf.PdfPageTransition.Style</code>

Gets the standard transition style.

### <code>P:Fluent.Pdf.PdfPaint.Black</code>

Gets immutable device black.

### <code>P:Fluent.Pdf.PdfPaint.Color</code>

Gets the exact solid color or uncolored pattern base color.

### <code>P:Fluent.Pdf.PdfPaint.Pattern</code>

Gets the optional exact pattern.

### <code>P:Fluent.Pdf.PdfPasswordProtection.Cipher</code>

Gets the explicit output cipher.

### <code>P:Fluent.Pdf.PdfPasswordProtection.EncryptMetadata</code>

Gets whether metadata stream bytes are encrypted.

### <code>P:Fluent.Pdf.PdfPasswordProtection.OwnerPassword</code>

Gets the caller-owned prepared owner credential.

### <code>P:Fluent.Pdf.PdfPasswordProtection.Permissions</code>

Gets the requested user permission flags.

### <code>P:Fluent.Pdf.PdfPasswordProtection.UserPassword</code>

Gets the caller-owned prepared user credential.

### <code>P:Fluent.Pdf.PdfPath.OperationReport</code>

Gets the successful bounded construction report.

### <code>P:Fluent.Pdf.PdfPath.Operations</code>

Gets validated path construction operators.

### <code>P:Fluent.Pdf.PdfPath.Segments</code>

Gets exact authored segments.

### <code>P:Fluent.Pdf.PdfPathSegment.Coordinates</code>

Gets immutable finite coordinates.

### <code>P:Fluent.Pdf.PdfPathSegment.Operation</code>

Gets its path construction operator.

### <code>P:Fluent.Pdf.PdfPattern.Colored</code>

Gets whether the pattern supplies its own colors.

### <code>P:Fluent.Pdf.PdfPattern.Resource</code>

Gets its reusable Pattern resource.

### <code>P:Fluent.Pdf.PdfPoint.X</code>

Gets horizontal position.

### <code>P:Fluent.Pdf.PdfPoint.Y</code>

Gets vertical position.

### <code>P:Fluent.Pdf.PdfPreflightFinding.Code</code>

Gets stable finding code.

### <code>P:Fluent.Pdf.PdfPreflightFinding.Location</code>

Gets physical object/key provenance.

### <code>P:Fluent.Pdf.PdfPreflightFinding.LossDescription</code>

Gets the fixed profile's declared loss, or null when no fix is implemented.

### <code>P:Fluent.Pdf.PdfPreflightFinding.RemediationCode</code>

Gets an available explicit fix code, or null when no fix is implemented.

### <code>P:Fluent.Pdf.PdfPreflightFinding.Severity</code>

Gets finding severity, not a conformance verdict.

### <code>P:Fluent.Pdf.PdfPreflightReport.AvailableCapabilities</code>

Gets only explicitly supplied provider capability declarations, not qualification or implicit discovery.

### <code>P:Fluent.Pdf.PdfPreflightReport.ConformanceValidated</code>

Gets false: this structural source profile does not replace a complete independent conformance validator.

### <code>P:Fluent.Pdf.PdfPreflightReport.Findings</code>

Gets immutable stable findings.

### <code>P:Fluent.Pdf.PdfPreflightReport.HasSignatures</code>

Gets whether signature byte-range structures are present; validity is not inferred.

### <code>P:Fluent.Pdf.PdfPreflightReport.OperationReport</code>

Gets the complete shared operation report.

### <code>P:Fluent.Pdf.PdfPreflightReport.SourceSha256</code>

Gets the exact captured input SHA256.

### <code>P:Fluent.Pdf.PdfPresentationSet.Catalog</code>

Gets preserved catalog presentation settings, including unknown preference values and initial actions.

### <code>P:Fluent.Pdf.PdfPresentationSet.Numbering</code>

Gets a bounded standard label formatter, or null when raw ranges require another profile.

### <code>P:Fluent.Pdf.PdfPresentationSet.OperationReport</code>

Gets the inspection diagnostics and resource report.

### <code>P:Fluent.Pdf.PdfPresentationSet.Outlines</code>

Gets the ordered nested outlines.

### <code>P:Fluent.Pdf.PdfPresentationSet.PageLabels</code>

Gets preserved page-label range dictionaries by physical start index.

### <code>P:Fluent.Pdf.PdfPresentationSet.Pages</code>

Gets page transition/display-duration records.

### <code>P:Fluent.Pdf.PdfPresentationSet.Source</code>

Gets the detached input snapshot.

### <code>P:Fluent.Pdf.PdfPrintJob.JobId</code>

Gets spooler-assigned job identity.

### <code>P:Fluent.Pdf.PdfPrintJob.Printer</code>

Gets exact printer identity.

### <code>P:Fluent.Pdf.PdfPrintJob.State</code>

Gets observed state.

### <code>P:Fluent.Pdf.PdfPrintOptions.Copies</code>

Gets the number of copies.

### <code>P:Fluent.Pdf.PdfPrintOptions.Duplex</code>

Gets duplex policy.

### <code>P:Fluent.Pdf.PdfPrintOptions.FirstPage</code>

Gets the first one-based page.

### <code>P:Fluent.Pdf.PdfPrintOptions.LastPage</code>

Gets the inclusive last page, or zero for the final page.

### <code>P:Fluent.Pdf.PdfPrintOptions.NumberUp</code>

Gets logical pages per sheet.

### <code>P:Fluent.Pdf.PdfPrintOptions.Printer</code>

Gets the exact selected printer identity.

### <code>P:Fluent.Pdf.PdfPrintOptions.Scaling</code>

Gets physical scaling policy.

### <code>P:Fluent.Pdf.PdfProcessingJob.Context</code>

Gets the shared context required by optional providers and bounded work.

### <code>P:Fluent.Pdf.PdfProcessingJob.Document</code>

Gets the job's private mutable document, valid only on the callback thread.

### <code>P:Fluent.Pdf.PdfProcessingJob.Input</code>

Gets the expiring read-only input lease. Input reads charge the shared operation budget.

### <code>P:Fluent.Pdf.PdfProcessingOptions.MaximumQueuedJobs</code>

Gets the maximum number of waiting jobs.

### <code>P:Fluent.Pdf.PdfProcessingOptions.MaximumResidentInputBytes</code>

Gets the maximum copied input bytes retained by all admitted jobs.

### <code>P:Fluent.Pdf.PdfProcessingOptions.MaximumWorkers</code>

Gets the fixed maximum number of concurrently executing job callbacks.

### <code>P:Fluent.Pdf.PdfProcessingPool.Completion</code>

Gets completion of all owned workers after draining or shutdown. Completion never precedes active callback cleanup.

### <code>P:Fluent.Pdf.PdfProcessingPool.Options</code>

Gets immutable pool admission limits.

### <code>P:Fluent.Pdf.PdfProcessingPool.Progress</code>

Gets a synchronized bounded aggregate snapshot.

### <code>P:Fluent.Pdf.PdfProcessingProgress.Canceled</code>

Gets the canceled job count.

### <code>P:Fluent.Pdf.PdfProcessingProgress.Failed</code>

Gets the failed job count.

### <code>P:Fluent.Pdf.PdfProcessingProgress.Queued</code>

Gets the waiting job count.

### <code>P:Fluent.Pdf.PdfProcessingProgress.ResidentInputBytes</code>

Gets copied input bytes retained by waiting and executing jobs.

### <code>P:Fluent.Pdf.PdfProcessingProgress.Running</code>

Gets the executing job count.

### <code>P:Fluent.Pdf.PdfProcessingProgress.Succeeded</code>

Gets the successfully completed job count.

### <code>P:Fluent.Pdf.PdfProfileOptions.AllowAdvancedContentLoss</code>

Gets explicit authority for a rewrite whose advanced-content semantics cannot be proven retained by this source gate. Strict loss policy still rejects it.

### <code>P:Fluent.Pdf.PdfProfileOptions.AllowSignatureInvalidation</code>

Gets explicit signature rewrite authority; strict loss policy still applies.

### <code>P:Fluent.Pdf.PdfProfileOptions.OutputColorSpace</code>

Gets the complete explicit PDF/X ICC output intent, or null for PDF/E.

### <code>P:Fluent.Pdf.PdfProfileOptions.OutputCondition</code>

Gets the literal print condition identifier.

### <code>P:Fluent.Pdf.PdfProfileOptions.Profile</code>

Gets the exact requested profile.

### <code>P:Fluent.Pdf.PdfProfileValidation.Completed</code>

Gets whether complete profile validation finished.

### <code>P:Fluent.Pdf.PdfProfileValidation.Findings</code>

Gets immutable rule findings.

### <code>P:Fluent.Pdf.PdfProfileValidation.IsCompliant</code>

Gets the independent observation, not SDK production qualification.

### <code>P:Fluent.Pdf.PdfProfileValidation.OperationReport</code>

Gets the completed public operation report; null on a provider-only result.

### <code>P:Fluent.Pdf.PdfProfileValidation.Profile</code>

Gets exact profile identity.

### <code>P:Fluent.Pdf.PdfProfileValidation.RequestSha256</code>

Gets the SDK-supplied exact request binding digest.

### <code>P:Fluent.Pdf.PdfProfileValidation.SourceSha256</code>

Gets exact validated byte digest.

### <code>P:Fluent.Pdf.PdfProgressiveOptions.BlockSize</code>

Gets the maximum single range size.

### <code>P:Fluent.Pdf.PdfProgressiveOptions.MaximumCacheBytes</code>

Gets the hard resident payload cache ceiling. Bookkeeping is also charged to the operation budget.

### <code>P:Fluent.Pdf.PdfProgressivePage.Dictionary</code>

Gets the detached page dictionary, including effective Resources, MediaBox, CropBox and Rotate declarations.

### <code>P:Fluent.Pdf.PdfProgressivePage.ObjectId</code>

Gets the page's indirect identity.

### <code>P:Fluent.Pdf.PdfProgressivePage.OperationReport</code>

Gets the completed selective-read report.

### <code>P:Fluent.Pdf.PdfProgressivePage.PageCount</code>

Gets the declared root page count. This selective inspection does not validate unvisited subtrees.

### <code>P:Fluent.Pdf.PdfProgressivePage.PageIndex</code>

Gets the zero-based requested page index.

### <code>P:Fluent.Pdf.PdfProviderDescriptor.Capabilities</code>

Gets a defensive, read-only capability snapshot.

### <code>P:Fluent.Pdf.PdfProviderDescriptor.Id</code>

Gets the provider identifier; registration compares identifiers ordinally without case.

### <code>P:Fluent.Pdf.PdfProviderDescriptor.Version</code>

Gets the explicitly supplied provider version, without resolving or downloading an engine.

### <code>P:Fluent.Pdf.PdfProviderRegistry.Capabilities</code>

Gets the declared capability inventory in enum order.

### <code>P:Fluent.Pdf.PdfProviderRegistry.Default</code>

Gets the shared original managed reader/writer registry. Optional native/image services remain explicitly configured.

### <code>P:Fluent.Pdf.PdfProviderRegistry.Descriptors</code>

Gets captured provider metadata in registration order.

### <code>P:Fluent.Pdf.PdfProviderRegistry.Empty</code>

Gets the shared empty immutable registry.

### <code>P:Fluent.Pdf.PdfQuad.Bounds</code>

Gets its finite axis-aligned bounding rectangle.

### <code>P:Fluent.Pdf.PdfQuad.P0</code>

Gets the first transformed corner.

### <code>P:Fluent.Pdf.PdfQuad.P1</code>

Gets the second transformed corner.

### <code>P:Fluent.Pdf.PdfQuad.P2</code>

Gets the third transformed corner.

### <code>P:Fluent.Pdf.PdfQuad.P3</code>

Gets the fourth transformed corner.

### <code>P:Fluent.Pdf.PdfRangeIdentity.Length</code>

Gets the complete source length.

### <code>P:Fluent.Pdf.PdfRangeIdentity.Version</code>

Gets the immutable nonsecret revision binding.

### <code>P:Fluent.Pdf.PdfRangeResponse.Body</code>

Gets the SDK-owned body. Providers must not retain or concurrently use it.

### <code>P:Fluent.Pdf.PdfRangeResponse.Identity</code>

Gets the actual response revision and total length.

### <code>P:Fluent.Pdf.PdfRangeResponse.Length</code>

Gets the exact body byte count.

### <code>P:Fluent.Pdf.PdfRangeResponse.Offset</code>

Gets the response's absolute start offset.

### <code>P:Fluent.Pdf.PdfRaster.Report</code>

Gets operation diagnostics and resource accounting.

### <code>P:Fluent.Pdf.PdfRaster.Request</code>

Gets exact pixel layout, effective DPI and snapped region.

### <code>P:Fluent.Pdf.PdfRasterExportOptions.BitsPerComponent</code>

Gets eight bits per color channel; no quantization or lossy conversion is performed.

### <code>P:Fluent.Pdf.PdfRasterExportOptions.ColorManagement</code>

Gets explicit color transform/profile, or null for device-color output.

### <code>P:Fluent.Pdf.PdfRasterExportOptions.Format</code>

Gets selected container.

### <code>P:Fluent.Pdf.PdfRasterExportOptions.Rendering</code>

Gets explicit rasterization choices.

### <code>P:Fluent.Pdf.PdfRasterFileInfo.Frames</code>

Gets immutable frame descriptors.

### <code>P:Fluent.Pdf.PdfRasterFileInfo.IsAnimation</code>

Gets whether the container is an animation; selection then requires explicit authority.

### <code>P:Fluent.Pdf.PdfRasterFrameInfo.ColorSpace</code>

Gets sample color interpretation; no implicit conversion occurs.

### <code>P:Fluent.Pdf.PdfRasterFrameInfo.Metadata</code>

Gets exact declared orientation/density.

### <code>P:Fluent.Pdf.PdfRasterFrameInfo.Samples</code>

Gets complete packed RGBA8 geometry.

### <code>P:Fluent.Pdf.PdfRasterImportOptions.FallbackDpi</code>

Gets explicit fallback samples per inch.

### <code>P:Fluent.Pdf.PdfRasterImportOptions.ImageOptions</code>

Gets PNG/JPEG encoded-color/orientation/lossless-transcode choices.

### <code>P:Fluent.Pdf.PdfRasterImportOptions.Margin</code>

Gets physical margin in points.

### <code>P:Fluent.Pdf.PdfRasterImportOptions.MaximumPages</code>

Gets selected-page ceiling.

### <code>P:Fluent.Pdf.PdfRasterImportOptions.PageSize</code>

Gets fixed page geometry or null for natural physical size.

### <code>P:Fluent.Pdf.PdfRasterInput.AllowAnimationFrames</code>

Gets whether explicitly selected animation frames may be used.

### <code>P:Fluent.Pdf.PdfRasterInput.Format</code>

Gets declared content format, never inferred from a filename.

### <code>P:Fluent.Pdf.PdfRasterInput.Frames</code>

Gets ordered zero-based frames, including explicit repetitions.

### <code>P:Fluent.Pdf.PdfRasterRequest.ByteLength</code>

Gets exact BGRA sample extent.

### <code>P:Fluent.Pdf.PdfRasterRequest.EffectiveDpiX</code>

Gets actual horizontal DPI after full-page integer ceiling.

### <code>P:Fluent.Pdf.PdfRasterRequest.EffectiveDpiY</code>

Gets actual vertical DPI after full-page integer ceiling.

### <code>P:Fluent.Pdf.PdfRasterRequest.FullHeight</code>

Gets full rotated CropBox grid height.

### <code>P:Fluent.Pdf.PdfRasterRequest.FullWidth</code>

Gets full rotated CropBox grid width.

### <code>P:Fluent.Pdf.PdfRasterRequest.Height</code>

Gets output height.

### <code>P:Fluent.Pdf.PdfRasterRequest.Options</code>

Gets immutable original choices.

### <code>P:Fluent.Pdf.PdfRasterRequest.Page</code>

Gets the exact immutable prepared page binding corresponding to PageIndex.

### <code>P:Fluent.Pdf.PdfRasterRequest.PageIndex</code>

Gets the zero-based page index in the prepared document.

### <code>P:Fluent.Pdf.PdfRasterRequest.Region</code>

Gets the effective outward-snapped region in displayed physical points.

### <code>P:Fluent.Pdf.PdfRasterRequest.Stride</code>

Gets tightly packed row size in bytes.

### <code>P:Fluent.Pdf.PdfRasterRequest.Width</code>

Gets output width.

### <code>P:Fluent.Pdf.PdfRasterRequest.X</code>

Gets the window's left pixel on the full-page grid.

### <code>P:Fluent.Pdf.PdfRasterRequest.Y</code>

Gets the window's top pixel on the full-page grid.

### <code>P:Fluent.Pdf.PdfRect.Height</code>

Gets nonnegative height.

### <code>P:Fluent.Pdf.PdfRect.Right</code>

Gets the right bound.

### <code>P:Fluent.Pdf.PdfRect.Top</code>

Gets the top bound.

### <code>P:Fluent.Pdf.PdfRect.Width</code>

Gets nonnegative width.

### <code>P:Fluent.Pdf.PdfRect.X</code>

Gets its lower-left horizontal position.

### <code>P:Fluent.Pdf.PdfRect.Y</code>

Gets its lower-left vertical position.

### <code>P:Fluent.Pdf.PdfRedactionAudit.Coverage</code>

Gets requested/actual removal coverage, containing no source text, names, URIs or removed-content hashes.

### <code>P:Fluent.Pdf.PdfRedactionAudit.FullRewrite</code>

Gets true: output has one fresh full revision and no original byte prefix/history.

### <code>P:Fluent.Pdf.PdfRedactionAudit.MaskedPixels</code>

Gets unique replacing pixels, without double-counting overlapping rectangles.

### <code>P:Fluent.Pdf.PdfRedactionAudit.OriginalGraphDiscarded</code>

Gets true: this profile builds a fresh graph solely from masked RGB samples and fixed structural names.

### <code>P:Fluent.Pdf.PdfRedactionAudit.OutputSha256</code>

Gets the exact newly serialized output hash, never a removed-payload digest.

### <code>P:Fluent.Pdf.PdfRedactionAudit.PageCount</code>

Gets output page count.

### <code>P:Fluent.Pdf.PdfRedactionAudit.SourceHadSignatures</code>

Gets whether original signature byte ranges were detected and explicitly discarded.

### <code>P:Fluent.Pdf.PdfRedactionAudit.SourceRevisionCount</code>

Gets revision count across distinct original source graphs, all discarded rather than copied.

### <code>P:Fluent.Pdf.PdfRedactionCoverage.PageIndex</code>

Gets source/output page index.

### <code>P:Fluent.Pdf.PdfRedactionCoverage.PixelBounds</code>

Gets actual clipped outward mask in top-left output-raster pixels.

### <code>P:Fluent.Pdf.PdfRedactionCoverage.RequestedBounds</code>

Gets caller-requested displayed physical-point bounds.

### <code>P:Fluent.Pdf.PdfRedactionRasterOptions.AllowSignatureInvalidation</code>

Gets explicit authority to discard existing signatures; false rejects signed sources before rendering.

### <code>P:Fluent.Pdf.PdfRedactionRasterOptions.BackgroundRgb</code>

Gets explicit opaque page matte.

### <code>P:Fluent.Pdf.PdfRedactionRasterOptions.Dpi</code>

Gets explicitly requested raster DPI.

### <code>P:Fluent.Pdf.PdfRedactionRasterOptions.FillRgb</code>

Gets replacing RGB mask color; original selected samples are not encoded.

### <code>P:Fluent.Pdf.PdfRedactionRasterOptions.IncludeAnnotationAppearances</code>

Gets whether visible annotation/widget appearances are explicitly adopted as static pixels. All original annotation/form objects are discarded either way.

### <code>P:Fluent.Pdf.PdfRedactionRasterOptions.MaximumMaskWork</code>

Gets cumulative pixel/scanline/interval comparison work ceiling.

### <code>P:Fluent.Pdf.PdfRedactionRasterOptions.MaximumRasterBytes</code>

Gets maximum BGRA extent per page, checked before invoking a renderer.

### <code>P:Fluent.Pdf.PdfRedactionRasterOptions.MaximumRegions</code>

Gets finite removal-rectangle count.

### <code>P:Fluent.Pdf.PdfRedactionRasterOptions.PaddingPixels</code>

Gets extra outward pixel coverage around each requested rectangle.

### <code>P:Fluent.Pdf.PdfRedactionRegion.Bounds</code>

Gets displayed top-left physical-point removal bounds.

### <code>P:Fluent.Pdf.PdfRedactionRegion.PageIndex</code>

Gets the zero-based source page.

### <code>P:Fluent.Pdf.PdfRedactionResult.Audit</code>

Gets safe structural/count/coverage audit data.

### <code>P:Fluent.Pdf.PdfRedactionResult.ByteLength</code>

Gets exact complete sanitized PDF extent.

### <code>P:Fluent.Pdf.PdfRedactionResult.OperationReport</code>

Gets the complete creation root report.

### <code>P:Fluent.Pdf.PdfRemediationAudit.After</code>

Gets exact output binding and post-fix findings; other unresolved findings remain visible.

### <code>P:Fluent.Pdf.PdfRemediationAudit.Before</code>

Gets exact source binding and findings.

### <code>P:Fluent.Pdf.PdfRemediationAudit.OperationReport</code>

Gets complete rewrite/post-validation diagnostics.

### <code>P:Fluent.Pdf.PdfRemediationAudit.Plan</code>

Gets the exact explicit plan that was applied.

### <code>P:Fluent.Pdf.PdfRemediationPlan.AllowSignatureInvalidation</code>

Gets explicit authority for invalidating source signature bytes.

### <code>P:Fluent.Pdf.PdfRemediationPlan.Fixes</code>

Gets explicit stable fix codes.

### <code>P:Fluent.Pdf.PdfRemediationPlan.SourceSha256</code>

Gets exact inspected source SHA256.

### <code>P:Fluent.Pdf.PdfRenderOptions.Annotations</code>

Gets whether annotation and existing widget appearances are painted.

### <code>P:Fluent.Pdf.PdfRenderOptions.BackgroundRgb</code>

Gets opaque 0xRRGGBB matte, or null for transparency.

### <code>P:Fluent.Pdf.PdfRenderOptions.Color</code>

Gets device-color selection.

### <code>P:Fluent.Pdf.PdfRenderOptions.DpiX</code>

Gets requested horizontal dots per physical inch.

### <code>P:Fluent.Pdf.PdfRenderOptions.DpiY</code>

Gets requested vertical dots per physical inch.

### <code>P:Fluent.Pdf.PdfRenderOptions.Print</code>

Gets print intent, including print visibility and optional-content usage.

### <code>P:Fluent.Pdf.PdfRenderOptions.Region</code>

Gets the optional region before outward pixel snapping.

### <code>P:Fluent.Pdf.PdfRepairOptions.ActiveObjects</code>

Gets a complete caller-authorized active identity inventory, or null to require recoverable xref status rows.

### <code>P:Fluent.Pdf.PdfRepairOptions.AllowSignatureInvalidation</code>

Gets whether an explicitly reported signature-invalidating full rewrite is authorized.

### <code>P:Fluent.Pdf.PdfRepairOptions.Orphans</code>

Gets the explicit orphan policy.

### <code>P:Fluent.Pdf.PdfRepairOptions.RejectDanglingReferences</code>

Gets whether missing/generation-mismatched referenced objects block recovery.

### <code>P:Fluent.Pdf.PdfRepairOptions.Root</code>

Gets an explicit root, or null to retain the recovered trailer/infer a unique active Catalog.

### <code>P:Fluent.Pdf.PdfRepairOptions.StreamLengths</code>

Gets exact caller-authorized stream payload lengths.

### <code>P:Fluent.Pdf.PdfRepairRecord.Code</code>

Gets the stable machine-readable record code.

### <code>P:Fluent.Pdf.PdfRepairRecord.Message</code>

Gets a bounded description without imported payload content.

### <code>P:Fluent.Pdf.PdfRepairRecord.ObjectId</code>

Gets the affected exact object identity, when applicable.

### <code>P:Fluent.Pdf.PdfRepairRecord.SourceOffset</code>

Gets an exact original byte offset, or null for a graph-level observation.

### <code>P:Fluent.Pdf.PdfRepairResult.Ledger</code>

Gets every recovered/repaired/discarded-object and structural decision.

### <code>P:Fluent.Pdf.PdfRepairResult.OperationReport</code>

Gets operation diagnostics and shared budget usage.

### <code>P:Fluent.Pdf.PdfRepairResult.OriginalSha256</code>

Gets the untouched source digest.

### <code>P:Fluent.Pdf.PdfRepairResult.RepairedSha256</code>

Gets the exact recovered output digest.

### <code>P:Fluent.Pdf.PdfRepairStreamLength.Length</code>

Gets the explicitly authorized raw payload length.

### <code>P:Fluent.Pdf.PdfRepairStreamLength.ObjectId</code>

Gets the exact stream object identity.

### <code>P:Fluent.Pdf.PdfResourceBinding.Name</code>

Gets its exact name, without a slash.

### <code>P:Fluent.Pdf.PdfResourceBinding.Resource</code>

Gets its immutable resource.

### <code>P:Fluent.Pdf.PdfResourceContent.ContainerObjectId</code>

Gets the page/Form container identity.

### <code>P:Fluent.Pdf.PdfResourceContent.Program</code>

Gets the bounded parsed program; unknown operators are preserved only when explicitly requested.

### <code>P:Fluent.Pdf.PdfResourceContent.Resources</code>

Gets the effective original resource dictionary.

### <code>P:Fluent.Pdf.PdfResourceDeclaration.Category</code>

Gets the original resource dictionary category.

### <code>P:Fluent.Pdf.PdfResourceDeclaration.ContainerObjectId</code>

Gets the captured page/Form declaring the effective scope.

### <code>P:Fluent.Pdf.PdfResourceDeclaration.Name</code>

Gets the original byte-valued resource name.

### <code>P:Fluent.Pdf.PdfResourceDeclaration.ResourceIndex</code>

Gets its manifest resource index.

### <code>P:Fluent.Pdf.PdfResourceLimits.MaximumDepth</code>

Gets the maximum simultaneous recursion scopes.

### <code>P:Fluent.Pdf.PdfResourceLimits.MaximumDiagnostics</code>

Gets the maximum retained diagnostic count; exhaustion fails instead of dropping records.

### <code>P:Fluent.Pdf.PdfResourceLimits.MaximumElapsedTime</code>

Gets the cooperative operation deadline duration.

### <code>P:Fluent.Pdf.PdfResourceLimits.MaximumExpandedBytes</code>

Gets the cumulative expanded/decoded byte limit, charged before allocation by providers.

### <code>P:Fluent.Pdf.PdfResourceLimits.MaximumInputBytes</code>

Gets the maximum input bytes delivered, with at most one additional EOF-probe byte consumed on a rejected read.

### <code>P:Fluent.Pdf.PdfResourceLimits.MaximumObjects</code>

Gets the cumulative parsed/generated object limit.

### <code>P:Fluent.Pdf.PdfResourceLimits.MaximumOutputBytes</code>

Gets the cumulative output-write/growth and relative output-extent limit.

### <code>P:Fluent.Pdf.PdfResourceLimits.MaximumPixels</code>

Gets the cumulative decoded/rendered pixel limit.

### <code>P:Fluent.Pdf.PdfResourceLimits.MaximumWorkers</code>

Gets the maximum simultaneous worker scopes.

### <code>P:Fluent.Pdf.PdfResourceOccurrence.IsArtifact</code>

Gets whether the use is within an explicit inherited Artifact scope.

### <code>P:Fluent.Pdf.PdfResourceOccurrence.ResourceIndex</code>

Gets the shared resource index.

### <code>P:Fluent.Pdf.PdfResourceOccurrence.Source</code>

Gets exact source provenance.

### <code>P:Fluent.Pdf.PdfResourceOccurrence.Transform</code>

Gets the accumulated transform into native page user coordinates, including a Form's own matrix.

### <code>P:Fluent.Pdf.PdfResourceOccurrence.Usage</code>

Gets the operator role, such as Do, Tf, sh, scn or inline image.

### <code>P:Fluent.Pdf.PdfResourceSet.Bindings</code>

Gets the explicit captured bindings.

### <code>P:Fluent.Pdf.PdfResourceSet.Empty</code>

Gets a shared empty immutable scope.

### <code>P:Fluent.Pdf.PdfResourceSource.ContainerObjectId</code>

Gets the captured page or Form object identity.

### <code>P:Fluent.Pdf.PdfResourceSource.FormInvocationIndices</code>

Gets each Form call's operator index in its parent container, distinguishing repeated invocations of the same shared Form.

### <code>P:Fluent.Pdf.PdfResourceSource.FormPath</code>

Gets the ordered indirect Form invocation path; direct Forms have no indirect identity to append.

### <code>P:Fluent.Pdf.PdfResourceSource.OperationIndex</code>

Gets the zero-based operator index within the declared container program.

### <code>P:Fluent.Pdf.PdfResourceSource.PageIndex</code>

Gets the zero-based current page.

### <code>P:Fluent.Pdf.PdfResourceUsage.Consumed</code>

Gets cumulative use, or current outstanding scopes for Depth/Workers.

### <code>P:Fluent.Pdf.PdfResourceUsage.Peak</code>

Gets the highest counter value during the operation.

### <code>P:Fluent.Pdf.PdfResourceUsage.Resource</code>

Gets the resource kind.

### <code>P:Fluent.Pdf.PdfRevision.Entries</code>

Gets entries sorted by object number.

### <code>P:Fluent.Pdf.PdfRevision.IsHybrid</code>

Gets whether a classic table was supplemented by an XRefStm.

### <code>P:Fluent.Pdf.PdfRevision.StartXref</code>

Gets this revision's startxref offset.

### <code>P:Fluent.Pdf.PdfRevision.Trailer</code>

Gets its preserved trailer dictionary or cross-reference stream dictionary.

### <code>P:Fluent.Pdf.PdfRevisionAuditRecord.Changes</code>

Gets declared object changes, without an inferred semantic classification.

### <code>P:Fluent.Pdf.PdfRevisionAuditRecord.RevisionIndex</code>

Gets source revision index, newest first.

### <code>P:Fluent.Pdf.PdfRevisionAuditRecord.Signatures</code>

Gets original signatures, including those hidden or deleted by later revisions. Integrity never implies later-edit permission.

### <code>P:Fluent.Pdf.PdfRevisionAuditRecord.StartXref</code>

Gets linked cross-reference offset.

### <code>P:Fluent.Pdf.PdfRevisionChange.Kind</code>

Gets free, direct or compressed declaration.

### <code>P:Fluent.Pdf.PdfRevisionChange.ObjectId</code>

Gets exact declared object identity.

### <code>P:Fluent.Pdf.PdfRevisionChange.PreviouslyDefined</code>

Gets whether an older revision declared this object number.

### <code>P:Fluent.Pdf.PdfScanDewarpGrid.Columns</code>

Gets control-column count.

### <code>P:Fluent.Pdf.PdfScanDewarpGrid.OutputHeight</code>

Gets corrected output height.

### <code>P:Fluent.Pdf.PdfScanDewarpGrid.OutputWidth</code>

Gets corrected output width.

### <code>P:Fluent.Pdf.PdfScanDewarpGrid.Rows</code>

Gets control-row count.

### <code>P:Fluent.Pdf.PdfScanDewarpGrid.SourceHeight</code>

Gets exact original source height.

### <code>P:Fluent.Pdf.PdfScanDewarpGrid.SourcePoints</code>

Gets immutable row-major original-raster edge coordinates.

### <code>P:Fluent.Pdf.PdfScanDewarpGrid.SourceWidth</code>

Gets exact original source width.

### <code>P:Fluent.Pdf.PdfScanGeometry.Dewarp</code>

Gets explicit nonlinear dewarp data, or null for a fully affine mapping.

### <code>P:Fluent.Pdf.PdfScanGeometry.OriginalHeight</code>

Gets original raster height.

### <code>P:Fluent.Pdf.PdfScanGeometry.OriginalWidth</code>

Gets original raster width.

### <code>P:Fluent.Pdf.PdfScanGeometry.ProcessedHeight</code>

Gets corrected raster height.

### <code>P:Fluent.Pdf.PdfScanGeometry.ProcessedToDewarped</code>

Gets the affine inverse of rotation/deskew, before optional nonlinear dewarping.

### <code>P:Fluent.Pdf.PdfScanGeometry.ProcessedWidth</code>

Gets corrected raster width.

### <code>P:Fluent.Pdf.PdfScanLayoutOptions.InferColumns</code>

Gets whether whitespace-based prose-column inference is requested.

### <code>P:Fluent.Pdf.PdfScanLayoutOptions.LineTolerancePixels</code>

Gets geometric line-center tolerance.

### <code>P:Fluent.Pdf.PdfScanLayoutOptions.MaximumAnalysisWork</code>

Gets finite pixel/geometry analysis work ceiling.

### <code>P:Fluent.Pdf.PdfScanLayoutOptions.MaximumColumns</code>

Gets maximum inferred column count.

### <code>P:Fluent.Pdf.PdfScanLayoutOptions.MaximumWords</code>

Gets finite recognized-word ceiling.

### <code>P:Fluent.Pdf.PdfScanLayoutOptions.MinimumColumnGapPixels</code>

Gets minimum low-density vertical gutter width.

### <code>P:Fluent.Pdf.PdfScanLayoutOptions.MinimumRuleLengthPixels</code>

Gets minimum dark run used for optional literal closed-grid detection.

### <code>P:Fluent.Pdf.PdfScanLayoutOptions.MinimumWordsPerColumn</code>

Gets minimum words required in each proposed column.

### <code>P:Fluent.Pdf.PdfScanLayoutOptions.Regions</code>

Gets explicit semantic-region hints in caller order.

### <code>P:Fluent.Pdf.PdfScanLayoutOptions.RightToLeftColumns</code>

Gets explicit column direction.

### <code>P:Fluent.Pdf.PdfScanLayoutOptions.RightToLeftWords</code>

Gets explicit geometric word order within inferred lines.

### <code>P:Fluent.Pdf.PdfScanLayoutOptions.SpanningHeightFraction</code>

Gets tolerated spanning-word vertical coverage when finding gutters.

### <code>P:Fluent.Pdf.PdfScanLayoutRegion.Bounds</code>

Gets processed-raster pixel bounds.

### <code>P:Fluent.Pdf.PdfScanLayoutRegion.ColumnIndex</code>

Gets left-to-right geometric column index, or null for spanning/nonprose regions.

### <code>P:Fluent.Pdf.PdfScanLayoutRegion.Confidence</code>

Gets a declared or heuristic evidence score, never calibrated correctness.

### <code>P:Fluent.Pdf.PdfScanLayoutRegion.Kind</code>

Gets declared or observed region kind.

### <code>P:Fluent.Pdf.PdfScanLayoutRegion.Provenance</code>

Gets explicit authority: caller-declared, literal-rulings, or whitespace.

### <code>P:Fluent.Pdf.PdfScanLayoutRegion.WordIndices</code>

Gets original recognition word indices, without replacing raw engine order.

### <code>P:Fluent.Pdf.PdfScanLayoutResult.AnalysisWork</code>

Gets finite analysis work consumed.

### <code>P:Fluent.Pdf.PdfScanLayoutResult.NonProseWordIndices</code>

Gets table/form/closed-grid word indices retained in raw engine order without a semantic ordering claim.

### <code>P:Fluent.Pdf.PdfScanLayoutResult.OperationReport</code>

Gets root operation diagnostics.

### <code>P:Fluent.Pdf.PdfScanLayoutResult.ProseReadingOrder</code>

Gets inferred prose word indices, including spanning headings. It does not order table/form cells.

### <code>P:Fluent.Pdf.PdfScanLayoutResult.Recognition</code>

Gets unchanged raw engine recognition order/provenance/confidence.

### <code>P:Fluent.Pdf.PdfScanLayoutResult.Regions</code>

Gets declared/observed regions; collection order does not assert document reading order.

### <code>P:Fluent.Pdf.PdfScanMappedBoundary.Bounds</code>

Gets a conservative original-raster bounding rectangle including the error envelope.

### <code>P:Fluent.Pdf.PdfScanMappedBoundary.MaximumErrorPixels</code>

Gets the certified maximum original-pixel distance from each exact quadratic edge piece to its chord.

### <code>P:Fluent.Pdf.PdfScanMappedBoundary.OperationReport</code>

Gets root diagnostics for public mapping, or null for same-context helpers.

### <code>P:Fluent.Pdf.PdfScanMappedBoundary.Points</code>

Gets ordered boundary vertices, including the repeated closing point.

### <code>P:Fluent.Pdf.PdfScanOrientationScore.EngineConfidence</code>

Gets nullable engine mean confidence0–100.

### <code>P:Fluent.Pdf.PdfScanOrientationScore.Orientation</code>

Gets the tested clockwise quarter-turn.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.AnalysisStride</code>

Gets analysis pixel stride; output resampling remains full resolution.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.BinaryThreshold</code>

Gets optional final binary threshold, with values below it mapped to zero.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.ContrastHigh</code>

Gets the gray value mapped to255.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.ContrastLow</code>

Gets the gray value mapped to zero.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.DetectSkew</code>

Gets whether bounded row-projection skew analysis is requested.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.Dewarp</code>

Gets explicit vertical-curvature dewarping control data, applied before orientation/skew analysis.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.MaximumPixelVisits</code>

Gets cumulative finite managed pixel-visit ceiling.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.MaximumSkewDegrees</code>

Gets absolute skew search extent.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.MedianDenoise</code>

Gets whether a replicated-edge3x3 median filter is requested.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.MinimumOrientationMargin</code>

Gets minimum best-versus-second engine-confidence gap divided by100. It is not correctness probability.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.MinimumSkewMargin</code>

Gets minimum relative best-versus-second projection-score gap. It is not correctness probability.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.Orientation</code>

Gets explicit orientation policy.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.SkewDegrees</code>

Gets explicitly measured source skew; correction rotates by its negative.

### <code>P:Fluent.Pdf.PdfScanPreprocessingOptions.SkewStepDegrees</code>

Gets reproducible search increment.

### <code>P:Fluent.Pdf.PdfScanPreprocessingResult.AppliedOrientation</code>

Gets selected explicit or observed orientation.

### <code>P:Fluent.Pdf.PdfScanPreprocessingResult.Geometry</code>

Gets the processed-to-original geometry map.

### <code>P:Fluent.Pdf.PdfScanPreprocessingResult.OperationReport</code>

Gets the complete root operation report.

### <code>P:Fluent.Pdf.PdfScanPreprocessingResult.Options</code>

Gets exact captured parameters.

### <code>P:Fluent.Pdf.PdfScanPreprocessingResult.OrientationMargin</code>

Gets nullable orientation score margin, never an accuracy probability.

### <code>P:Fluent.Pdf.PdfScanPreprocessingResult.OrientationScores</code>

Gets four orientation observations when requested, otherwise empty.

### <code>P:Fluent.Pdf.PdfScanPreprocessingResult.Original</code>

Gets the exact original immutable raster; it is never overwritten.

### <code>P:Fluent.Pdf.PdfScanPreprocessingResult.PixelVisits</code>

Gets managed pixel visits consumed by this operation.

### <code>P:Fluent.Pdf.PdfScanPreprocessingResult.Processed</code>

Gets corrected top-left Gray8 pixels, including explicit white padding after deskew.

### <code>P:Fluent.Pdf.PdfScanPreprocessingResult.SkewMargin</code>

Gets nullable skew score margin, never an accuracy probability.

### <code>P:Fluent.Pdf.PdfScanPreprocessingResult.SourceSkewDegrees</code>

Gets explicit or measured source skew, corrected by its negative.

### <code>P:Fluent.Pdf.PdfScanRecognitionResult.Layout</code>

Gets separate prose layout inference and unresolved nonprose order.

### <code>P:Fluent.Pdf.PdfScanRecognitionResult.OperationReport</code>

Gets the complete shared root report.

### <code>P:Fluent.Pdf.PdfScanRecognitionResult.Recognition</code>

Gets raw engine recognition in processed-raster coordinates.

### <code>P:Fluent.Pdf.PdfScanRecognitionResult.Scan</code>

Gets preserved original, processed samples and inverse transforms.

### <code>P:Fluent.Pdf.PdfScanRegionHint.Bounds</code>

Gets processed-raster pixel bounds.

### <code>P:Fluent.Pdf.PdfScanRegionHint.Confidence</code>

Gets caller-declared confidence, never inferred correctness.

### <code>P:Fluent.Pdf.PdfScanRegionHint.Kind</code>

Gets caller-declared table/form kind.

### <code>P:Fluent.Pdf.PdfScriptRecord.IsAttachment</code>

Gets whether this observation is an attachment edge.

### <code>P:Fluent.Pdf.PdfScriptRecord.Location</code>

Gets exact object/attachment path, including trigger keys when known.

### <code>P:Fluent.Pdf.PdfScriptRecord.Source</code>

Gets decoded source without executing it.

### <code>P:Fluent.Pdf.PdfSecurityResult.Cipher</code>

Gets the selected cipher identity, without key material.

### <code>P:Fluent.Pdf.PdfSecurityResult.EncryptMetadata</code>

Gets the metadata stream encryption choice.

### <code>P:Fluent.Pdf.PdfSecurityResult.Permissions</code>

Gets standard user or recipient permissions. Owner authority permits all document operations independently of these flags.

### <code>P:Fluent.Pdf.PdfSecurityResult.Report</code>

Gets resource accounting and nonsecret diagnostics.

### <code>P:Fluent.Pdf.PdfSecurityResult.Revision</code>

Gets the standard handler revision, or zero for a public-key handler.

### <code>P:Fluent.Pdf.PdfSecurityResult.Role</code>

Gets the successful authentication path.

### <code>P:Fluent.Pdf.PdfSepaCreditTransfer.Amount</code>

Gets exact positive EUR amount.

### <code>P:Fluent.Pdf.PdfSepaCreditTransfer.Beneficiary</code>

Gets literal creditor name.

### <code>P:Fluent.Pdf.PdfSepaCreditTransfer.Bic</code>

Gets explicit syntax-validated BIC.

### <code>P:Fluent.Pdf.PdfSepaCreditTransfer.CreditorReference</code>

Gets canonical RF reference or empty.

### <code>P:Fluent.Pdf.PdfSepaCreditTransfer.Iban</code>

Gets canonical checksum-validated IBAN; account existence is not established.

### <code>P:Fluent.Pdf.PdfSepaCreditTransfer.Information</code>

Gets literal optional beneficiary-to-originator information.

### <code>P:Fluent.Pdf.PdfSepaCreditTransfer.Payload</code>

Gets canonical LF-separated complete payment text; no transfer is submitted.

### <code>P:Fluent.Pdf.PdfSepaCreditTransfer.Purpose</code>

Gets the literal optional purpose code syntax.

### <code>P:Fluent.Pdf.PdfSepaCreditTransfer.Remittance</code>

Gets unstructured remittance or empty.

### <code>P:Fluent.Pdf.PdfShading.ColorSpace</code>

Gets its exact color space.

### <code>P:Fluent.Pdf.PdfShading.Resource</code>

Gets its reusable Shading resource.

### <code>P:Fluent.Pdf.PdfShading.ShadingType</code>

Gets its standard shading type, one through seven.

### <code>P:Fluent.Pdf.PdfShapedGlyph.AdvanceX</code>

Gets horizontal progression.

### <code>P:Fluent.Pdf.PdfShapedGlyph.AdvanceY</code>

Gets vertical progression.

### <code>P:Fluent.Pdf.PdfShapedGlyph.Cluster</code>

Gets its original UTF-16 cluster offset.

### <code>P:Fluent.Pdf.PdfShapedGlyph.GlyphIndex</code>

Gets the face's glyph identity.

### <code>P:Fluent.Pdf.PdfShapedGlyph.OffsetX</code>

Gets horizontal placement adjustment.

### <code>P:Fluent.Pdf.PdfShapedGlyph.OffsetY</code>

Gets vertical placement adjustment.

### <code>P:Fluent.Pdf.PdfShapedRun.EmbeddingLevel</code>

Gets the run's resolved bidi level.

### <code>P:Fluent.Pdf.PdfShapedRun.Glyphs</code>

Gets positioned glyphs in visual order.

### <code>P:Fluent.Pdf.PdfShapedRun.ParagraphIndex</code>

Gets its logical paragraph index.

### <code>P:Fluent.Pdf.PdfShapedRun.Request</code>

Gets the exact itemization request.

### <code>P:Fluent.Pdf.PdfShapedText.OperationReport</code>

Gets the successful shaping report.

### <code>P:Fluent.Pdf.PdfShapedText.Options</code>

Gets captured text settings.

### <code>P:Fluent.Pdf.PdfShapedText.Runs</code>

Gets paragraph runs in visual order.

### <code>P:Fluent.Pdf.PdfShapedText.Unicode</code>

Gets unchanged logical Unicode and grapheme/bidi analysis.

### <code>P:Fluent.Pdf.PdfShapingRequest.Direction</code>

Gets glyph progression.

### <code>P:Fluent.Pdf.PdfShapingRequest.Features</code>

Gets explicit feature overrides in caller order.

### <code>P:Fluent.Pdf.PdfShapingRequest.Font</code>

Gets the exact selected immutable face.

### <code>P:Fluent.Pdf.PdfShapingRequest.Language</code>

Gets the explicit BCP 47 language, or und.

### <code>P:Fluent.Pdf.PdfShapingRequest.Length</code>

Gets its UTF-16 run length.

### <code>P:Fluent.Pdf.PdfShapingRequest.ScriptTag</code>

Gets the ISO 15924 script tag in big-endian byte order.

### <code>P:Fluent.Pdf.PdfShapingRequest.Start</code>

Gets its inclusive UTF-16 run start.

### <code>P:Fluent.Pdf.PdfShapingRequest.Text</code>

Gets unchanged full logical text, including context outside this run.

### <code>P:Fluent.Pdf.PdfSignatureFieldDefinition.Appearance</code>

Gets explicit visible appearance, or null for an invisible signature.

### <code>P:Fluent.Pdf.PdfSignatureFieldDefinition.Bounds</code>

Gets native unrotated PDF bounds.

### <code>P:Fluent.Pdf.PdfSignatureFieldDefinition.Name</code>

Gets exact flat field name.

### <code>P:Fluent.Pdf.PdfSignatureFieldDefinition.PageIndex</code>

Gets zero-based page.

### <code>P:Fluent.Pdf.PdfSignatureFieldLock.Action</code>

Gets lock selection.

### <code>P:Fluent.Pdf.PdfSignatureFieldLock.Fields</code>

Gets immutable ordinal names.

### <code>P:Fluent.Pdf.PdfSignatureObservation.ByteRange</code>

Gets exact two-pair byte range.

### <code>P:Fluent.Pdf.PdfSignatureObservation.CryptographicallyValid</code>

Gets detached CMS integrity, or null for an unsupported profile; does not establish trust or permitted later edits.

### <code>P:Fluent.Pdf.PdfSignatureObservation.ObjectId</code>

Gets signature object identity.

### <code>P:Fluent.Pdf.PdfSignatureObservation.SignedRevisionLength</code>

Gets covered revision extent.

### <code>P:Fluent.Pdf.PdfSignatureObservation.TrustEvaluated</code>

Gets false: trust/revocation validation is a separate explicitly qualified service.

### <code>P:Fluent.Pdf.PdfSignatureObservation.UnsupportedReason</code>

Gets a stable diagnostic code when this signature profile cannot be evaluated; null otherwise.

### <code>P:Fluent.Pdf.PdfSignatureOptions.Certification</code>

Gets document certification authority.

### <code>P:Fluent.Pdf.PdfSignatureOptions.FieldLock</code>

Gets optional field lock authority.

### <code>P:Fluent.Pdf.PdfSignatureOptions.FieldName</code>

Gets exact pre-created field name.

### <code>P:Fluent.Pdf.PdfSignatureOptions.Reason</code>

Gets optional caller reason.

### <code>P:Fluent.Pdf.PdfSignatureOptions.ReservedCmsBytes</code>

Gets finite CMS byte reservation.

### <code>P:Fluent.Pdf.PdfSignatureOptions.SigningTime</code>

Gets optional caller timestamp; it is not a trusted timestamp token.

### <code>P:Fluent.Pdf.PdfSignatureTrustDecision.Accepted</code>

Gets whether all identity, time and fresh revocation checks pass.

### <code>P:Fluent.Pdf.PdfSignatureTrustDecision.CertificateSha256</code>

Gets the exact signer certificate fingerprint.

### <code>P:Fluent.Pdf.PdfSignatureTrustDecision.IdentityTrusted</code>

Gets explicit identity trust.

### <code>P:Fluent.Pdf.PdfSignatureTrustDecision.Revocation</code>

Gets revocation status, including unknown/offline.

### <code>P:Fluent.Pdf.PdfSignatureTrustDecision.TimeValid</code>

Gets whether the certificate is valid at the requested instant.

### <code>P:Fluent.Pdf.PdfSignatureTrustOptions.EvaluationTime</code>

Gets the evaluation instant; a PDF signing-time string does not supply this authority.

### <code>P:Fluent.Pdf.PdfSignatureVerification.Accepted</code>

Gets complete integrity and identity acceptance; does not authorize later modifications.

### <code>P:Fluent.Pdf.PdfSignatureVerification.LaterByteCount</code>

Gets bytes after the signed revision. Their presence does not establish permission or tampering.

### <code>P:Fluent.Pdf.PdfSignatureVerification.Signature</code>

Gets structural/range and mathematical observations.

### <code>P:Fluent.Pdf.PdfSignatureVerification.Trust</code>

Gets explicit trust, or null when no supported CMS could be verified.

### <code>P:Fluent.Pdf.PdfSignedEditPlan.Allowed</code>

Gets whether this exact requested profile passes the source-policy barrier.

### <code>P:Fluent.Pdf.PdfSignedEditPlan.PreservesOriginalPrefix</code>

Gets whether an allowed operation retains every original byte. This alone is not cryptographic, trust or permitted-edit validation.

### <code>P:Fluent.Pdf.PdfSignedEditPlan.Reason</code>

Gets the stable policy reason.

### <code>P:Fluent.Pdf.PdfSignedResult.ByteLength</code>

Gets exact signed extent.

### <code>P:Fluent.Pdf.PdfSignedResult.OperationReport</code>

Gets creation report.

### <code>P:Fluent.Pdf.PdfSimpleTextShaper.Descriptor</code>

Gets the narrowly scoped managed capability declaration.

### <code>P:Fluent.Pdf.PdfSoftMask.Backdrop</code>

Gets the explicitly selected luminosity backdrop.

### <code>P:Fluent.Pdf.PdfSoftMask.Group</code>

Gets its exact vector transparency group.

### <code>P:Fluent.Pdf.PdfSoftMask.Kind</code>

Gets the alpha or luminosity method.

### <code>P:Fluent.Pdf.PdfSoftMask.Transfer</code>

Gets the optional unary transfer function.

### <code>P:Fluent.Pdf.PdfStampContent.Kind</code>

Gets the explicit source classification.

### <code>P:Fluent.Pdf.PdfStampContent.OperationReport</code>

Gets the successful source-capture report.

### <code>P:Fluent.Pdf.PdfStampInfo.ContentKind</code>

Gets the recorded source classification.

### <code>P:Fluent.Pdf.PdfStampInfo.Identifier</code>

Gets the exact caller-supplied ASCII identifier.

### <code>P:Fluent.Pdf.PdfStampInfo.Options</code>

Gets the captured requested placement. Subsequent page geometry edits can change its displayed position.

### <code>P:Fluent.Pdf.PdfStampInfo.PageIndex</code>

Gets the current zero-based page index.

### <code>P:Fluent.Pdf.PdfStampInspection.OperationReport</code>

Gets the complete bounded inspection report.

### <code>P:Fluent.Pdf.PdfStampInspection.Stamps</code>

Gets stamps in page/content order.

### <code>P:Fluent.Pdf.PdfStampOptions.Artifact</code>

Gets whether the complete stamp invocation is decoration marked as Artifact.

### <code>P:Fluent.Pdf.PdfStampOptions.Columns</code>

Gets the horizontal repetition count.

### <code>P:Fluent.Pdf.PdfStampOptions.Destination</code>

Gets the first displayed destination frame.

### <code>P:Fluent.Pdf.PdfStampOptions.Fit</code>

Gets explicit content fitting.

### <code>P:Fluent.Pdf.PdfStampOptions.HorizontalGap</code>

Gets additional horizontal spacing in points.

### <code>P:Fluent.Pdf.PdfStampOptions.Layer</code>

Gets foreground/background placement.

### <code>P:Fluent.Pdf.PdfStampOptions.Opacity</code>

Gets explicit fill and stroke opacity.

### <code>P:Fluent.Pdf.PdfStampOptions.Rotation</code>

Gets clockwise rotation about each destination's center.

### <code>P:Fluent.Pdf.PdfStampOptions.Rows</code>

Gets the vertical repetition count.

### <code>P:Fluent.Pdf.PdfStampOptions.VerticalGap</code>

Gets additional vertical spacing in points.

### <code>P:Fluent.Pdf.PdfStructuralInspection.Findings</code>

Gets missing-reference, orphan and structural observations.

### <code>P:Fluent.Pdf.PdfStructuralInspection.OperationReport</code>

Gets diagnostics and shared resource usage.

### <code>P:Fluent.Pdf.PdfStructuralInspection.Syntax</code>

Gets the unmodified syntax graph and exact original-source view.

### <code>P:Fluent.Pdf.PdfStructureContent.AnnotationId</code>

Gets the annotation identity, or null for marked content.

### <code>P:Fluent.Pdf.PdfStructureContent.Mcid</code>

Gets the MCID or minus one for an annotation.

### <code>P:Fluent.Pdf.PdfStructureContent.PageIndex</code>

Gets the source page index.

### <code>P:Fluent.Pdf.PdfStructureContent.StreamId</code>

Gets the explicit Form stream identity; null selects page content.

### <code>P:Fluent.Pdf.PdfStructureElement.ActualText</code>

Gets optional exact logical replacement text.

### <code>P:Fluent.Pdf.PdfStructureElement.AlternativeText</code>

Gets meaningful author-provided alternative text.

### <code>P:Fluent.Pdf.PdfStructureElement.Identifier</code>

Gets optional unique element ID.

### <code>P:Fluent.Pdf.PdfStructureElement.Items</code>

Gets author-defined reading order across elements and content.

### <code>P:Fluent.Pdf.PdfStructureElement.Language</code>

Gets optional explicit element language.

### <code>P:Fluent.Pdf.PdfStructureElement.Role</code>

Gets the explicit standard role.

### <code>P:Fluent.Pdf.PdfStructureElement.Table</code>

Gets explicit table attributes.

### <code>P:Fluent.Pdf.PdfStructureElementInfo.ActualText</code>

Gets logical replacement text.

### <code>P:Fluent.Pdf.PdfStructureElementInfo.AlternativeText</code>

Gets alternative text.

### <code>P:Fluent.Pdf.PdfStructureElementInfo.Children</code>

Gets exact ordered child values; arrays are expanded one level.

### <code>P:Fluent.Pdf.PdfStructureElementInfo.Dictionary</code>

Gets the immutable raw element dictionary, including unknown semantic metadata.

### <code>P:Fluent.Pdf.PdfStructureElementInfo.Language</code>

Gets the declared element language.

### <code>P:Fluent.Pdf.PdfStructureElementInfo.ObjectId</code>

Gets the exact element identity in the inspection source.

### <code>P:Fluent.Pdf.PdfStructureElementInfo.Role</code>

Gets the raw declared role.

### <code>P:Fluent.Pdf.PdfStructurePlan.Language</code>

Gets the explicit document language.

### <code>P:Fluent.Pdf.PdfStructurePlan.Profile</code>

Gets the requested validation profile.

### <code>P:Fluent.Pdf.PdfStructurePlan.ReplaceExisting</code>

Gets whether existing structure replacement is explicitly allowed and diagnosed.

### <code>P:Fluent.Pdf.PdfStructurePlan.Root</code>

Gets the author-defined Document root.

### <code>P:Fluent.Pdf.PdfStructureTableAttributes.ColumnSpan</code>

Gets the explicit cell column span.

### <code>P:Fluent.Pdf.PdfStructureTableAttributes.HeaderScope</code>

Gets optional standard header scope.

### <code>P:Fluent.Pdf.PdfStructureTableAttributes.Headers</code>

Gets immutable header element IDs.

### <code>P:Fluent.Pdf.PdfStructureTableAttributes.RowSpan</code>

Gets the explicit cell row span.

### <code>P:Fluent.Pdf.PdfStructureTableAttributes.Summary</code>

Gets author-supplied table summary.

### <code>P:Fluent.Pdf.PdfSyntaxDocument.OperationReport</code>

Gets the source-read operation report. Internal provider reads use their enclosing document report.

### <code>P:Fluent.Pdf.PdfSyntaxDocument.Revisions</code>

Gets immutable revisions, newest first.

### <code>P:Fluent.Pdf.PdfSyntaxDocument.Version</code>

Gets the header version, from 1.2 through 1.7 or 2.0.

### <code>P:Fluent.Pdf.PdfTable.AllowRowSplit</code>

Gets whether explicitly splittable rows may cross page boundaries.

### <code>P:Fluent.Pdf.PdfTable.Columns</code>

Gets explicit immutable column definitions.

### <code>P:Fluent.Pdf.PdfTable.HeaderRows</code>

Gets the count of complete leading rows repeated on every fragment page.

### <code>P:Fluent.Pdf.PdfTable.OperationReport</code>

Gets the successful bounded grid-construction or data-binding report.

### <code>P:Fluent.Pdf.PdfTable.Rows</code>

Gets immutable rows in logical source order.

### <code>P:Fluent.Pdf.PdfTableBorder.Color</code>

Gets explicit paint color.

### <code>P:Fluent.Pdf.PdfTableBorder.None</code>

Gets an immutable disabled border.

### <code>P:Fluent.Pdf.PdfTableBorder.Width</code>

Gets physical width.

### <code>P:Fluent.Pdf.PdfTableCell.ColumnSpan</code>

Gets the horizontal grid span.

### <code>P:Fluent.Pdf.PdfTableCell.Content</code>

Gets logical content blocks, including nested immutable tables.

### <code>P:Fluent.Pdf.PdfTableCell.RowSpan</code>

Gets the vertical grid span.

### <code>P:Fluent.Pdf.PdfTableCell.Style</code>

Gets immutable cell style.

### <code>P:Fluent.Pdf.PdfTableCellFragment.Bounds</code>

Gets the visible cell rectangle relative to the page content origin, in physical points.

### <code>P:Fluent.Pdf.PdfTableCellFragment.Cell</code>

Gets the immutable cell, including its row/column spans.

### <code>P:Fluent.Pdf.PdfTableCellFragment.Column</code>

Gets the original zero-based column origin.

### <code>P:Fluent.Pdf.PdfTableCellFragment.IsContinuation</code>

Gets whether an earlier body fragment contains this merged cell's beginning.

### <code>P:Fluent.Pdf.PdfTableCellFragment.IsRepeatedHeader</code>

Gets whether this is a repeated header instance.

### <code>P:Fluent.Pdf.PdfTableCellFragment.Row</code>

Gets the original zero-based row origin.

### <code>P:Fluent.Pdf.PdfTableCellStyle.Background</code>

Gets the optional explicit background.

### <code>P:Fluent.Pdf.PdfTableCellStyle.Bottom</code>

Gets the bottom border.

### <code>P:Fluent.Pdf.PdfTableCellStyle.Left</code>

Gets the left border.

### <code>P:Fluent.Pdf.PdfTableCellStyle.Padding</code>

Gets physical cell padding.

### <code>P:Fluent.Pdf.PdfTableCellStyle.Right</code>

Gets the right border.

### <code>P:Fluent.Pdf.PdfTableCellStyle.Top</code>

Gets the top border.

### <code>P:Fluent.Pdf.PdfTableCellStyle.VerticalAlignment</code>

Gets vertical content placement inside the completed merged rectangle.

### <code>P:Fluent.Pdf.PdfTableColumn.IsFixed</code>

Gets whether the width is fixed.

### <code>P:Fluent.Pdf.PdfTableColumn.Maximum</code>

Gets the finite maximum width in physical points.

### <code>P:Fluent.Pdf.PdfTableColumn.Minimum</code>

Gets the minimum width in physical points.

### <code>P:Fluent.Pdf.PdfTableColumn.Weight</code>

Gets the positive distribution weight for remaining automatic width.

### <code>P:Fluent.Pdf.PdfTableDataColumn`1.CellStyle</code>

Gets immutable cell style.

### <code>P:Fluent.Pdf.PdfTableDataColumn`1.Format</code>

Gets the optional scalar format string.

### <code>P:Fluent.Pdf.PdfTableDataColumn`1.NullText</code>

Gets explicit text for null values and null records.

### <code>P:Fluent.Pdf.PdfTableDataColumn`1.ParagraphStyle</code>

Gets immutable paragraph alignment, spacing and line constraints inside the bound cell.

### <code>P:Fluent.Pdf.PdfTableDataColumn`1.TextStyle</code>

Gets immutable text style.

### <code>P:Fluent.Pdf.PdfTableDataColumn`1.Value</code>

Gets the explicit scalar selector; the SDK never reflects over records.

### <code>P:Fluent.Pdf.PdfTableDataColumn`1.Width</code>

Gets the immutable width definition.

### <code>P:Fluent.Pdf.PdfTableExtractionOptions.HeaderRows</code>

Gets the explicit leading header-row count used for exact repeated-header comparison.

### <code>P:Fluent.Pdf.PdfTableExtractionOptions.JoinConsecutivePages</code>

Gets whether consecutive selected pages with matching columns and explicit headers may form a sequence.

### <code>P:Fluent.Pdf.PdfTableExtractionOptions.MaximumCells</code>

Gets the maximum primitive cells per candidate, checked before allocation.

### <code>P:Fluent.Pdf.PdfTableExtractionOptions.MinimumColumnGap</code>

Gets the displayed-point gutter separating borderless cells.

### <code>P:Fluent.Pdf.PdfTableExtractionOptions.MinimumColumns</code>

Gets the minimum accepted columns.

### <code>P:Fluent.Pdf.PdfTableExtractionOptions.MinimumRows</code>

Gets the minimum accepted rows.

### <code>P:Fluent.Pdf.PdfTableExtractionOptions.Mode</code>

Gets the explicit inference profile.

### <code>P:Fluent.Pdf.PdfTableExtractionOptions.NumericCulture</code>

Gets the explicit numeric culture, or null for text-only cells. Empty explicitly selects invariant culture.

### <code>P:Fluent.Pdf.PdfTableExtractionOptions.Region</code>

Gets the optional displayed top-left table region, applied independently to selected pages.

### <code>P:Fluent.Pdf.PdfTableExtractionOptions.Tolerance</code>

Gets the displayed-point alignment/rule tolerance.

### <code>P:Fluent.Pdf.PdfTableExtractionResult.OperationReport</code>

Gets inference diagnostics and resource observations.

### <code>P:Fluent.Pdf.PdfTableExtractionResult.Sequences</code>

Gets explicit page-continuation groups.

### <code>P:Fluent.Pdf.PdfTableExtractionResult.Source</code>

Gets the immutable original captured snapshot for independent source inspection.

### <code>P:Fluent.Pdf.PdfTableExtractionResult.Tables</code>

Gets per-page tables; zero candidates is explicit and diagnosed.

### <code>P:Fluent.Pdf.PdfTableFragment.BlockIndex</code>

Gets the zero-based source flow block index.

### <code>P:Fluent.Pdf.PdfTableFragment.BodyEnd</code>

Gets the body slice end in the complete table's vertical sheet coordinates.

### <code>P:Fluent.Pdf.PdfTableFragment.BodyStart</code>

Gets the body slice start in the complete table's vertical sheet coordinates.

### <code>P:Fluent.Pdf.PdfTableFragment.Bounds</code>

Gets the displayed table rectangle relative to the page content origin, in physical points.

### <code>P:Fluent.Pdf.PdfTableFragment.Cells</code>

Gets visible cell rectangles with original merged-grid identities.

### <code>P:Fluent.Pdf.PdfTableFragment.IsRepeatedHeader</code>

Gets whether this fragment repeats the leading header rows after an earlier fragment.

### <code>P:Fluent.Pdf.PdfTableFragment.NestedHeaderBandHeight</code>

Gets common physical band height reserved for nested continuation headers.

### <code>P:Fluent.Pdf.PdfTableFragment.NestedHeaders</code>

Gets explicit nested headers repeated after this table's own header rows.

### <code>P:Fluent.Pdf.PdfTableFragment.Table</code>

Gets the original immutable table.

### <code>P:Fluent.Pdf.PdfTableHeaderRepeat.Bounds</code>

Gets the reserved header rectangle relative to the page content origin, in physical points.

### <code>P:Fluent.Pdf.PdfTableHeaderRepeat.Table</code>

Gets the original nested table whose complete header rows are repeated.

### <code>P:Fluent.Pdf.PdfTableRow.AllowSplit</code>

Gets whether safe text-line boundaries may split this row.

### <code>P:Fluent.Pdf.PdfTableRow.Cells</code>

Gets cells whose origins occur on this row; spans from earlier rows occupy their grid slots.

### <code>P:Fluent.Pdf.PdfTableRow.MinimumHeight</code>

Gets physical minimum height.

### <code>P:Fluent.Pdf.PdfTextDrawing.Content</code>

Gets exact positioned glyph operators wrapped in logical ActualText.

### <code>P:Fluent.Pdf.PdfTextDrawing.Fonts</code>

Gets all named embedded resources required by the program.

### <code>P:Fluent.Pdf.PdfTextDrawing.OperationReport</code>

Gets the successful bounded construction report.

### <code>P:Fluent.Pdf.PdfTextExtractionOptions.IncludeHiddenLayers</code>

Gets whether optional-content-hidden characters are retained and flagged.

### <code>P:Fluent.Pdf.PdfTextExtractionOptions.IncludeInvisibleText</code>

Gets whether nonpainting text modes, including searchable OCR text, are retained.

### <code>P:Fluent.Pdf.PdfTextExtractionOptions.LayerConfiguration</code>

Gets the optional source configuration; null selects the default.

### <code>P:Fluent.Pdf.PdfTextExtractionOptions.LayerUsage</code>

Gets the explicit optional-content usage event.

### <code>P:Fluent.Pdf.PdfTextExtractionOptions.LineTolerance</code>

Gets the displayed baseline grouping tolerance in points.

### <code>P:Fluent.Pdf.PdfTextExtractionOptions.WordGapRatio</code>

Gets the font-size-relative gap used to infer word breaks.

### <code>P:Fluent.Pdf.PdfTextExtractionResult.ActualTextSpans</code>

Gets source-declared logical replacements without inventing per-character geometry.

### <code>P:Fluent.Pdf.PdfTextExtractionResult.Characters</code>

Gets raw character-code execution order.

### <code>P:Fluent.Pdf.PdfTextExtractionResult.Geometry</code>

Gets immutable boxes, viewing rotation and UserUnit.

### <code>P:Fluent.Pdf.PdfTextExtractionResult.LayoutText</code>

Gets a separate geometric layout inference; it is not a claim of original logical reading order.

### <code>P:Fluent.Pdf.PdfTextExtractionResult.Lines</code>

Gets inferred geometric line order.

### <code>P:Fluent.Pdf.PdfTextExtractionResult.OperationReport</code>

Gets diagnostics and resource usage.

### <code>P:Fluent.Pdf.PdfTextExtractionResult.PageIndex</code>

Gets the zero-based physical page index.

### <code>P:Fluent.Pdf.PdfTextExtractionResult.Paragraphs</code>

Gets inferred paragraph grouping.

### <code>P:Fluent.Pdf.PdfTextExtractionResult.RawText</code>

Gets decoded cluster text in source order without synthetic spacing or ActualText substitution.

### <code>P:Fluent.Pdf.PdfTextExtractionResult.Text</code>

Gets source-order text with ActualText replacements and explicitly inferred positional separators.

### <code>P:Fluent.Pdf.PdfTextExtractionResult.Words</code>

Gets inferred word segmentation in raw order.

### <code>P:Fluent.Pdf.PdfTextFeature.End</code>

Gets its exclusive logical end.

### <code>P:Fluent.Pdf.PdfTextFeature.Start</code>

Gets its inclusive logical start.

### <code>P:Fluent.Pdf.PdfTextFeature.Tag</code>

Gets the OpenType tag.

### <code>P:Fluent.Pdf.PdfTextFeature.Value</code>

Gets its value.

### <code>P:Fluent.Pdf.PdfTextMatch.Bounds</code>

Gets their displayed advance-cell union, or null for geometry-free logical text.

### <code>P:Fluent.Pdf.PdfTextMatch.Characters</code>

Gets covered source-code records, without inventing per-scalar ligature geometry.

### <code>P:Fluent.Pdf.PdfTextMatch.IsPartialCluster</code>

Gets whether a match cuts a Unicode cluster or ActualText replacement.

### <code>P:Fluent.Pdf.PdfTextMatch.Start</code>

Gets the UTF-16 start in the result's semantic search text.

### <code>P:Fluent.Pdf.PdfTextMatch.Text</code>

Gets the exact matched Unicode.

### <code>P:Fluent.Pdf.PdfTextMatch.UsesActualText</code>

Gets whether explicit source ActualText contributes to the match.

### <code>P:Fluent.Pdf.PdfTextOptions.Features</code>

Gets immutable feature overrides.

### <code>P:Fluent.Pdf.PdfTextOptions.Language</code>

Gets the explicit language.

### <code>P:Fluent.Pdf.PdfTextOptions.ParagraphDirection</code>

Gets the base-direction policy.

### <code>P:Fluent.Pdf.PdfTextOptions.Vertical</code>

Gets whether vertical top-to-bottom shaping was requested.

### <code>P:Fluent.Pdf.PdfTextQuery.FontName</code>

Gets the optional exact declared PDF font name, applied to all covered source characters.

### <code>P:Fluent.Pdf.PdfTextQuery.IgnoreCase</code>

Gets ordinal/invariant case-insensitive selection.

### <code>P:Fluent.Pdf.PdfTextQuery.IsRegex</code>

Gets whether regex syntax is explicitly requested.

### <code>P:Fluent.Pdf.PdfTextQuery.MaximumFontSize</code>

Gets the optional maximum displayed font size.

### <code>P:Fluent.Pdf.PdfTextQuery.MaximumMatches</code>

Gets the maximum accepted matches; overflow fails rather than truncating.

### <code>P:Fluent.Pdf.PdfTextQuery.MinimumFontSize</code>

Gets the optional minimum displayed font size.

### <code>P:Fluent.Pdf.PdfTextQuery.Pattern</code>

Gets the exact literal or regex pattern.

### <code>P:Fluent.Pdf.PdfTextQuery.RegexTimeoutMilliseconds</code>

Gets the finite timeout for each regex match call.

### <code>P:Fluent.Pdf.PdfTextQuery.Region</code>

Gets an optional displayed top-left region; all covered advance cells must be contained.

### <code>P:Fluent.Pdf.PdfTextReplacementOptions.FontSize</code>

Gets the explicit maximum font size.

### <code>P:Fluent.Pdf.PdfTextReplacementOptions.Overflow</code>

Gets the explicit overflow behavior.

### <code>P:Fluent.Pdf.PdfTextSearchResult.Matches</code>

Gets nonoverlapping selected matches, including explicit zero-length regex matches.

### <code>P:Fluent.Pdf.PdfTextSearchResult.OperationReport</code>

Gets diagnostics and resource observations.

### <code>P:Fluent.Pdf.PdfTextSearchResult.PageIndex</code>

Gets the selected page index.

### <code>P:Fluent.Pdf.PdfTextSearchResult.Text</code>

Gets source-order semantic text without inferred spaces or paragraph separators.

### <code>P:Fluent.Pdf.PdfTextString.Encoding</code>

Gets the explicit detected/selected encoding.

### <code>P:Fluent.Pdf.PdfTextString.MinimumVersion</code>

Gets the encoding's output version floor.

### <code>P:Fluent.Pdf.PdfTextString.OperationReport</code>

Gets the successful explicit string operation report.

### <code>P:Fluent.Pdf.PdfTextString.Text</code>

Gets decoded logical Unicode.

### <code>P:Fluent.Pdf.PdfTextString.Value</code>

Gets the exact immutable PDF string bytes.

### <code>P:Fluent.Pdf.PdfTransaction.LastOperationReport</code>

Gets the last save report, including a failed save. Remains readable after disposal.

### <code>P:Fluent.Pdf.PdfTransaction.Source</code>

Gets the immutable original graph, or null for a new file.

### <code>P:Fluent.Pdf.PdfTransaction.Trailer</code>

Gets or sets a direct trailer dictionary. Structural xref keys are generated by the writer.

### <code>P:Fluent.Pdf.PdfTransparencyGroup.ColorSpace</code>

Gets the exact blending color space.

### <code>P:Fluent.Pdf.PdfTransparencyGroup.Isolated</code>

Gets the isolated-group choice.

### <code>P:Fluent.Pdf.PdfTransparencyGroup.Knockout</code>

Gets the knockout-group choice.

### <code>P:Fluent.Pdf.PdfTransparencyGroup.Resource</code>

Gets the reusable vector form resource.

### <code>P:Fluent.Pdf.PdfType3Glyph.Name</code>

Gets the unique glyph name.

### <code>P:Fluent.Pdf.PdfType3Glyph.Program</code>

Gets the immutable glyph procedure.

### <code>P:Fluent.Pdf.PdfType3Glyph.UnicodeText</code>

Gets the exact logical Unicode sequence.

### <code>P:Fluent.Pdf.PdfUnicode.Version</code>

Gets the pinned Unicode data version.

### <code>P:Fluent.Pdf.PdfUnicodeAnalysis.Graphemes</code>

Gets extended grapheme boundaries.

### <code>P:Fluent.Pdf.PdfUnicodeAnalysis.OperationReport</code>

Gets the successful bounded analysis report.

### <code>P:Fluent.Pdf.PdfUnicodeAnalysis.Paragraphs</code>

Gets paragraph ranges/base levels.

### <code>P:Fluent.Pdf.PdfUnicodeAnalysis.Scalars</code>

Gets logical scalars and their resolved levels.

### <code>P:Fluent.Pdf.PdfUnicodeAnalysis.Text</code>

Gets unchanged logical Unicode text.

### <code>P:Fluent.Pdf.PdfUnicodeAnalysis.VisualScalarOrder</code>

Gets UAX #9 visual scalar indices, with X9 format characters excluded.

### <code>P:Fluent.Pdf.PdfUnicodeMap.Count</code>

Gets the number of explicit code-to-Unicode mappings.

### <code>P:Fluent.Pdf.PdfUnicodeMap.OperationReport</code>

Gets the successful bounded parsing report.

### <code>P:Fluent.Pdf.PdfUnicodeScalar.BidiClass</code>

Gets the original Unicode bidi class.

### <code>P:Fluent.Pdf.PdfUnicodeScalar.DefaultIgnorable</code>

Gets its normative default-ignorable property.

### <code>P:Fluent.Pdf.PdfUnicodeScalar.EmbeddingLevel</code>

Gets the resolved level, or -1 for a format character removed by UAX #9 rule X9.

### <code>P:Fluent.Pdf.PdfUnicodeScalar.ScriptTag</code>

Gets its ISO 15924 script tag packed in big-endian byte order.

### <code>P:Fluent.Pdf.PdfUnicodeScalar.Utf16Length</code>

Gets its UTF-16 extent.

### <code>P:Fluent.Pdf.PdfUnicodeScalar.Utf16Offset</code>

Gets its original UTF-16 offset.

### <code>P:Fluent.Pdf.PdfUnicodeScalar.Value</code>

Gets the Unicode scalar value.

### <code>P:Fluent.Pdf.PdfValue.BooleanValue</code>

Gets a Boolean; throws if the kind differs.

### <code>P:Fluent.Pdf.PdfValue.Entries</code>

Gets immutable dictionary entries for dictionaries and streams.

### <code>P:Fluent.Pdf.PdfValue.Items</code>

Gets the immutable array elements.

### <code>P:Fluent.Pdf.PdfValue.Kind</code>

Gets the lexical kind.

### <code>P:Fluent.Pdf.PdfValue.NameValue</code>

Gets the decoded byte-valued name as Latin-1 characters.

### <code>P:Fluent.Pdf.PdfValue.Null</code>

Gets the shared immutable null value.

### <code>P:Fluent.Pdf.PdfValue.NumberText</code>

Gets the exact decimal spelling; throws if the kind differs.

### <code>P:Fluent.Pdf.PdfValue.NumberValue</code>

Gets a finite approximate number; throws for a different kind or a value outside Double range.

### <code>P:Fluent.Pdf.PdfValue.RawStreamLength</code>

Gets the encoded stream length.

### <code>P:Fluent.Pdf.PdfValue.ReferenceId</code>

Gets the indirect reference identity.

### <code>P:Fluent.Pdf.PdfValue.StringBytes</code>

Gets immutable byte-string contents. Interpretation and decryption belong to the owning feature.

### <code>P:Fluent.Pdf.PdfViewSession.CachedBytes</code>

Gets retained sample bytes, excluding caller-owned returned rasters and in-flight bounded operations.

### <code>P:Fluent.Pdf.PdfViewSession.CachedRasterCount</code>

Gets number of retained rasters.

### <code>P:Fluent.Pdf.PdfWordConversionOptions.MaximumArchiveEntries</code>

Gets the archive-entry ceiling.

### <code>P:Fluent.Pdf.PdfWordConversionOptions.MaximumCharacters</code>

Gets the reconstructed/imported character ceiling.

### <code>P:Fluent.Pdf.PdfWordConversionOptions.MaximumDepth</code>

Gets the dependency nesting ceiling, additionally bounded by root depth.

### <code>P:Fluent.Pdf.PdfWordConversionOptions.MaximumInputBytes</code>

Gets the source-byte ceiling.

### <code>P:Fluent.Pdf.PdfWordConversionOptions.MaximumOutputBytes</code>

Gets the converted-file ceiling.

### <code>P:Fluent.Pdf.PdfWordConversionOptions.MaximumPages</code>

Gets the physical page ceiling.

### <code>P:Fluent.Pdf.PdfWordConversionOptions.MaximumProviderExpandedBytes</code>

Gets one opaque provider phase's expansion envelope; each phase must charge it before entering the dependency.

### <code>P:Fluent.Pdf.PdfWordConversionOptions.RasterDpi</code>

Gets explicitly selected fixed-layout raster DPI.

### <code>P:Fluent.Pdf.PdfWordConversionOptions.Reconstruction</code>

Gets the explicit PDF reconstruction intent.

### <code>P:Fluent.Pdf.PdfWriteOptions.AllowSignatureInvalidation</code>

Gets explicit permission for a full rewrite of a document containing signature dictionaries.

### <code>P:Fluent.Pdf.PdfWriteOptions.CrossReferenceFormat</code>

Gets the representation of newly generated cross references.

### <code>P:Fluent.Pdf.PdfWriteOptions.Default</code>

Gets shared full-rewrite defaults.

### <code>P:Fluent.Pdf.PdfWriteOptions.Extensions</code>

Gets explicit developer extension requirements.

### <code>P:Fluent.Pdf.PdfWriteOptions.MinimumVersion</code>

Gets an explicitly declared feature version floor, or null.

### <code>P:Fluent.Pdf.PdfWriteOptions.Mode</code>

Gets the physical revision strategy.

### <code>P:Fluent.Pdf.PdfWriteOptions.UnknownExtensions</code>

Gets the source developer-extension preservation policy.

### <code>P:Fluent.Pdf.PdfWriteOptions.Version</code>

Gets an exact target version, or null to retain/negotiate the source version.

### <code>P:Fluent.Pdf.PdfXfaPacket.Name</code>

Gets exact packet name.

### <code>P:Fluent.Pdf.PdfXfaPacket.ObjectId</code>

Gets original indirect stream identity when present.

### <code>P:Fluent.Pdf.PdfXfaPacket.Xml</code>

Gets decoded XML without executing scripts, resolving resources or laying out dynamic forms.

### <code>P:Fluent.Pdf.PdfXfaSet.Data</code>

Gets exact dot-separated scalar XML element paths below datasets/data.

### <code>P:Fluent.Pdf.PdfXfaSet.OperationReport</code>

Gets bounded inspection report.

### <code>P:Fluent.Pdf.PdfXfaSet.Packets</code>

Gets preserved packet order.

### <code>T:Fluent.Pdf.IPdfAccessibilityValidator</code>

An explicitly supplied complete independent PDF/UA validator; metadata-only implementations may not declare compliance.

### <code>T:Fluent.Pdf.IPdfArchivalValidator</code>

Optional complete independent ISO 19005 validation of exact borrowed input. No metadata-only implementation may declare compliance.

### <code>T:Fluent.Pdf.IPdfAsyncSignatureProvider</code>

An explicitly asynchronous detached signer using the same cryptographic verifier. PdfSignatures synchronous entry points reject this interface; callers must use SignAsync or SignFileAsync.

### <code>T:Fluent.Pdf.IPdfBarcodeDecoder</code>

Explicit independently implemented caller-owned decoder. It must honor the mandatory context and must not delegate generation verification back to the encoder.

### <code>T:Fluent.Pdf.IPdfCertificateStatusProvider</code>

Optional caller-configured certificate status authority; unknown must remain observable.

### <code>T:Fluent.Pdf.IPdfCidFontProgram</code>

Optional original CID-to-glyph lookup for a CID-keyed embedded font program without a Unicode cmap.

### <code>T:Fluent.Pdf.IPdfCmsEnvelopeProvider</code>

An optional offline CMS recipient-envelope boundary using mandatory shared budgets and borrowed streams.

### <code>T:Fluent.Pdf.IPdfColorManagedPageRenderer</code>

Optional ICC-aware renderer, independent of GUI/platform types. It must honor embedded profiles/output intents, requested output transform and print/screen policies, or reject the unsupported request.

### <code>T:Fluent.Pdf.IPdfDocumentReader</code>

Populates an empty document synchronously; implementations must honor cancellation and supplied options.

### <code>T:Fluent.Pdf.IPdfDocumentWriter</code>

Writes an immutable document snapshot synchronously.

### <code>T:Fluent.Pdf.IPdfExternalSigningKey</code>

A caller-owned external key/session factory. Authentication and key identity stay inside the provider.

### <code>T:Fluent.Pdf.IPdfExternalSigningSession</code>

One external RSA-SHA256-PKCS1 signing session; disposal must release the hardware/remote lease.

### <code>T:Fluent.Pdf.IPdfFontEncodingMetricsProvider</code>

Optional deterministic source-code advances for standard simple fonts without a Widths array.

### <code>T:Fluent.Pdf.IPdfFontEncodingProvider</code>

Optional deterministic PDF simple-font encoding and glyph-name mapping; no machine encoding fallback is permitted.

### <code>T:Fluent.Pdf.IPdfFontProgram</code>

A caller-owned immutable font face. Methods must honor the supplied operation context and never search machine fonts.

### <code>T:Fluent.Pdf.IPdfFontProvider</code>

Explicit offline font parsing and deterministic standard-font selection.

### <code>T:Fluent.Pdf.IPdfFontShapingSource</code>

Optional immutable original OpenType source for shaping; copying does not grant embedding permission.

### <code>T:Fluent.Pdf.IPdfHtmlRenderer</code>

An explicitly registered optional HTML engine. Implementations must use a private job, supplied resources and the mandatory shared context.

### <code>T:Fluent.Pdf.IPdfImageCodecProvider</code>

An explicitly registered image codec boundary. Stream leases expire on return and must never be retained.

### <code>T:Fluent.Pdf.IPdfInvoiceProducer</code>

Explicit caller-owned complete invoice PDF/A-3 producer. All work must use the supplied operation context and separately qualified offline engine assets/host.

### <code>T:Fluent.Pdf.IPdfInvoiceValidator</code>

Complete independent schema/business-rule/invoice-package validator, including readable PDF versus XML/model reconciliation.

### <code>T:Fluent.Pdf.IPdfLinearizationProducer</code>

Explicit offline caller-owned linearization engine with a pinned identity and qualified bounded isolation for native implementations.

### <code>T:Fluent.Pdf.IPdfLinearizationValidator</code>

Separately configured complete independent hint/offset validator. A marker check or the producer under another name is insufficient.

### <code>T:Fluent.Pdf.IPdfOcrProvider</code>

An explicitly configured OCR provider. Native/model paths, provenance and deployment are provider choices with no default discovery.

### <code>T:Fluent.Pdf.IPdfPadesProvider</code>

Explicit optional incremental signature-profile producer. Endpoint, credential and timeouts belong to its explicit configuration.

### <code>T:Fluent.Pdf.IPdfPadesValidator</code>

Explicit independent validator with caller-configured trust and network authority; no engine is discovered.

### <code>T:Fluent.Pdf.IPdfPageRenderer</code>

An explicit optional headless renderer. Implementations remain responsible for declaring their supported content profile.

### <code>T:Fluent.Pdf.IPdfPrintProvider</code>

Explicit optional print-system boundary. Implementations must reject unsupported choices, observe the context and never retry submission implicitly.

### <code>T:Fluent.Pdf.IPdfProfileProducer</code>

Explicit caller-owned profile conversion engine. No metadata-only implementation can claim complete conversion.

### <code>T:Fluent.Pdf.IPdfProfileValidator</code>

Separately configured complete independent standard validator, not a metadata marker or SDK structural check.

### <code>T:Fluent.Pdf.IPdfProvider</code>

An explicitly configured, caller-owned provider. Documents never dispose shared providers.

### <code>T:Fluent.Pdf.IPdfRangeSource</code>

Explicit caller-owned random-access transport. Authentication, origins, TLS and conditional requests belong to this provider; no network is enabled implicitly.

### <code>T:Fluent.Pdf.IPdfRasterDocumentDecoder</code>

Explicit optional file codec. Native decoders require an appropriately isolated provisioned host; no codec may discover/download dependencies.

### <code>T:Fluent.Pdf.IPdfRevisionWriter</code>

A document writer that explicitly honors revision, xref, version and signature rewrite choices.

### <code>T:Fluent.Pdf.IPdfSignatureProvider</code>

Optional detached CMS signer/verifier with mandatory shared context and borrowed exact-byte streams. Trust/revocation/network services are never inferred.

### <code>T:Fluent.Pdf.IPdfSignatureTrustProvider</code>

Explicit optional trust service. It receives the exact CMS already selected by the range validator; it must not infer trust from mathematical validity.

### <code>T:Fluent.Pdf.IPdfTemplateHtmlProvider</code>

Explicit optional HTML fragment service. The core supplies no HTML/browser engine.

### <code>T:Fluent.Pdf.IPdfTextShaper</code>

An explicit optional shaper. Results use visual glyph order and monotone grapheme cluster offsets for each run.

### <code>T:Fluent.Pdf.IPdfVerticalFontProgram</code>

An optional exact vertical metric capability; callers must check support for the selected face.

### <code>T:Fluent.Pdf.IPdfWordConverter</code>

An explicit optional Word converter. Implementations retain borrowed stream ownership and use the mandatory context across every SDK/provider phase.

### <code>T:Fluent.Pdf.ManagedPdfProvider</code>

The original managed PDF object/page reader and full/incremental writer. No document actions are executed.

### <code>T:Fluent.Pdf.Pdf</code>

The document creation/opening facade. No provider is discovered or downloaded implicitly.

### <code>T:Fluent.Pdf.PdfAccessibility</code>

Explicit logical structure authoring, passive inspection and identity-preserving semantic remediation.

### <code>T:Fluent.Pdf.PdfAccessibilityFinding</code>

A bounded automated or independent accessibility rule finding, without an implied conformance claim.

### <code>T:Fluent.Pdf.PdfAccessibilityHumanReview</code>

An explicit author/reviewer attestation bound to exact bytes, kept separate from all automated checks.

### <code>T:Fluent.Pdf.PdfAccessibilityInspection</code>

Managed structural observations, with automated findings separate from always-required human semantic review.

### <code>T:Fluent.Pdf.PdfAccessibilityProfile</code>

The requested accessibility validation standard; selection is not a conformance claim.

### <code>T:Fluent.Pdf.PdfAccessibilityValidation</code>

Complete independent exact-input PDF/UA observations. It does not establish human semantic review or SDK production qualification.

### <code>T:Fluent.Pdf.PdfAction</code>

A passive immutable action description. Creating or inspecting it never executes it.

### <code>T:Fluent.Pdf.PdfActionPolicy</code>

Explicit permission for emitted and retained actions. Permissions never execute actions or fetch resources.

### <code>T:Fluent.Pdf.PdfActionRecord</code>

A passive action or opaque active-content observation at a physical dictionary/array location.

### <code>T:Fluent.Pdf.PdfAnnotation</code>

A detached immutable authored annotation with an explicit normal Form appearance.

### <code>T:Fluent.Pdf.PdfAnnotationKind</code>

Standard authored comment/markup/media annotation kinds. Imported unknown kinds remain inspectable.

### <code>T:Fluent.Pdf.PdfAnnotationMetadata</code>

Immutable comment metadata and stable reply identity. Dates retain their explicit offsets.

### <code>T:Fluent.Pdf.PdfAnnotationRecord</code>

An immutable original annotation entry. Unknown keys and appearance states remain in its source-owned definition.

### <code>T:Fluent.Pdf.PdfAnnotationSet</code>

A detached annotation manifest; referenced bytes survive source disposal and remain passive.

### <code>T:Fluent.Pdf.PdfAnnotationView</code>

The explicit visibility rules used for static appearance flattening.

### <code>T:Fluent.Pdf.PdfAnnotations</code>

Managed annotation authoring, preserving passive content and explicit Form appearances.

### <code>T:Fluent.Pdf.PdfArchival</code>

Explicit archival preflight and full rewrites gated on complete independent validation of the exact result.

### <code>T:Fluent.Pdf.PdfArchivalConversion</code>

A completed archival rewrite and independent exact-byte observation.

### <code>T:Fluent.Pdf.PdfArchivalFinding</code>

A detached validation/preflight rule finding, without an implied conformance claim.

### <code>T:Fluent.Pdf.PdfArchivalInspection</code>

A managed structural preflight inventory; zero findings is not a conformance result.

### <code>T:Fluent.Pdf.PdfArchivalOptions</code>

Explicit full-rewrite archival choices. No fonts, color profiles or validation engines are discovered.

### <code>T:Fluent.Pdf.PdfArchivalProfile</code>

The explicitly requested ISO 19005 profile; selection alone makes no conformance claim.

### <code>T:Fluent.Pdf.PdfArchivalValidation</code>

A complete or incomplete independent validation observation bound to exact bytes and profile.

### <code>T:Fluent.Pdf.PdfAttachment</code>

A detached passive file specification, including all embedded platform variants and raw custom values.

### <code>T:Fluent.Pdf.PdfAttachmentOptions</code>

Explicit embedded-file metadata. The SDK never reads a file clock or executes payloads.

### <code>T:Fluent.Pdf.PdfAttachmentSet</code>

A passive all-active-object file inventory and raw portable-collection dictionary.

### <code>T:Fluent.Pdf.PdfAttachments</code>

Passive managed file embedding/extraction and full-rewrite removal. Payloads are never launched.

### <code>T:Fluent.Pdf.PdfAuthenticationRole</code>

The successful authentication path; owner authority overrides user permission restrictions.

### <code>T:Fluent.Pdf.PdfBarcodeDetection</code>

Detached bounded detection result, with native PDF coordinates only when PageIndex has a value.

### <code>T:Fluent.Pdf.PdfBarcodeErrorCorrection</code>

Explicit QR error correction; Code128 uses None and its mandatory checksum.

### <code>T:Fluent.Pdf.PdfBarcodeObservation</code>

One independent decoder observation. Text is decoded Unicode; this does not assert the original QR byte encoding or expose arbitrary binary payloads.

### <code>T:Fluent.Pdf.PdfBarcodeOptions</code>

Immutable finite generation settings. Generated symbols require an explicitly configured independent decoder.

### <code>T:Fluent.Pdf.PdfBarcodeSymbol</code>

A generated immutable symbol accepted only after independent decoding of its actual canonical pixels.

### <code>T:Fluent.Pdf.PdfBarcodeSymbology</code>

Explicit supported symbol profiles.

### <code>T:Fluent.Pdf.PdfBarcodes</code>

Original bounded symbol encoding, mandatory independent decoding and explicit page/region integration.

### <code>T:Fluent.Pdf.PdfBidiClass</code>

Unicode bidirectional character classes, independent of the installed runtime's Unicode tables.

### <code>T:Fluent.Pdf.PdfBidiParagraph</code>

A paragraph range and resolved base embedding level.

### <code>T:Fluent.Pdf.PdfBookmark</code>

An immutable nested bookmark with an optional local destination or passive action.

### <code>T:Fluent.Pdf.PdfCalculationResult</code>

One finite decimal result with resource accounting; applying values/appearances remains an explicit form operation.

### <code>T:Fluent.Pdf.PdfCanvas</code>

A bounded synchronous canvas builder. Use only on its callback thread; completed drawings and placements are immutable.

### <code>T:Fluent.Pdf.PdfCanvasAnchor</code>

Displayed alignment of a fitted object inside its destination box.

### <code>T:Fluent.Pdf.PdfCanvasApplyMode</code>

How a completed canvas combines with a page's existing content.

### <code>T:Fluent.Pdf.PdfCanvasFit</code>

Explicit placement-box fitting policy.

### <code>T:Fluent.Pdf.PdfCanvasOptions</code>

Immutable explicit canvas coordinate and clipping settings.

### <code>T:Fluent.Pdf.PdfCanvasOrigin</code>

Canvas axes relative to the displayed selected box.

### <code>T:Fluent.Pdf.PdfCanvasPageLayer</code>

An immutable generated page-content layer, exposed in writer snapshots.

### <code>T:Fluent.Pdf.PdfCanvasPlacement</code>

An immutable painted-layer frame. Hit testing measures its transformed bounding frame and captured clips, rather than glyph ink or image alpha.

### <code>T:Fluent.Pdf.PdfCanvasResult</code>

A complete immutable canvas, independent of subsequent page edits and builder lifetime.

### <code>T:Fluent.Pdf.PdfCanvasStyle</code>

Immutable vector paint/stroke choices in canvas units.

### <code>T:Fluent.Pdf.PdfCapability</code>

A named service advertised by an explicitly registered provider; this is not production qualification.

### <code>T:Fluent.Pdf.PdfCertificateCredential</code>

An explicit public certificate snapshot paired with a caller-owned private RSA handle.

Remarks: The SDK never searches certificate stores or exports the private key. Keep the key alive and serialize operations if its provider requires that. Hardware/PIN behavior belongs to the explicitly supplied key provider.

### <code>T:Fluent.Pdf.PdfCertificateProtection</code>

Immutable recipient and file-cipher choices. All recipients and certificate trust decisions are explicit.

### <code>T:Fluent.Pdf.PdfCertificateRecipient</code>

An immutable public certificate snapshot and its requested PDF permissions.

### <code>T:Fluent.Pdf.PdfCertificationPermission</code>

Explicit certification permissions; unsupported edit classes reject conservatively.

### <code>T:Fluent.Pdf.PdfCmsKeyTransport</code>

Explicit RSA key transport for a PDF recipient envelope.

### <code>T:Fluent.Pdf.PdfColor</code>

Finite exact channel values in an explicitly selected immutable color space.

### <code>T:Fluent.Pdf.PdfColorRenderOptions</code>

Immutable color-management authority, distinct from ordinary device-color screen preview.

### <code>T:Fluent.Pdf.PdfColorRendering</code>

Explicit color-managed rendering; ordinary page renderers are never silently substituted.

### <code>T:Fluent.Pdf.PdfColorRenderingIntent</code>

Standard ICC rendering intent.

### <code>T:Fluent.Pdf.PdfColorSpace</code>

An immutable explicitly selected PDF color space and exact channel bounds.

### <code>T:Fluent.Pdf.PdfColorTransformation</code>

A detached explicit RGB ICC matrix/TRC color conversion and its bounded operation report.

### <code>T:Fluent.Pdf.PdfComparison</code>

Bounded original ordinal-page text, geometry, metadata and optional raster comparison.

### <code>T:Fluent.Pdf.PdfComparisonOptions</code>

Explicit ordinal-page comparison choices. No page matching/reflow inference is implied.

### <code>T:Fluent.Pdf.PdfComparisonResult</code>

A detached comparison report. Zero differences only covers the selected dimensions and explicit tolerances.

### <code>T:Fluent.Pdf.PdfCompatibility</code>

Passive format-version and extension inventory using the normal managed object graph.

### <code>T:Fluent.Pdf.PdfCompatibilityInspection</code>

Detached passive version and raw catalog compatibility inventory. This is not PDF conformance validation.

### <code>T:Fluent.Pdf.PdfConformanceProfile</code>

Exact standard editions. Selecting a profile is not a conformance claim.

### <code>T:Fluent.Pdf.PdfContentKind</code>

The context in which a complete content program executes.

### <code>T:Fluent.Pdf.PdfContentOperation</code>

An immutable typed PDF operator and its direct, uninterpreted operands.

### <code>T:Fluent.Pdf.PdfContentProgram</code>

An immutable validated content program. Encoded text bytes and named resources remain uninterpreted.

### <code>T:Fluent.Pdf.PdfContentsEntry</code>

A contents/index link resolved to a final physical destination.

### <code>T:Fluent.Pdf.PdfCrossReference</code>

An immutable cross-reference observation.

### <code>T:Fluent.Pdf.PdfCrossReferenceFormat</code>

The representation of newly generated cross references.

### <code>T:Fluent.Pdf.PdfCrossReferenceKind</code>

A cross-reference entry kind.

### <code>T:Fluent.Pdf.PdfDataGeneration</code>

Typed offline generation with shared binding/layout/provider/write budgets and ordered precommit hooks.

### <code>T:Fluent.Pdf.PdfDecodedData</code>

Immutable eagerly decoded data and its operation report; no caller handle or operation context is retained.

### <code>T:Fluent.Pdf.PdfDecodedText</code>

Exact decoded logical text and its operation report, including any explicitly permitted substitutions.

### <code>T:Fluent.Pdf.PdfDestination</code>

An immutable destination captured against a physical page when applied to a snapshot.

### <code>T:Fluent.Pdf.PdfDestinationView</code>

Standard local destination view modes, in native unrotated page coordinates.

### <code>T:Fluent.Pdf.PdfDiagnostic</code>

An immutable, provider-neutral failure description with a stable code.

### <code>T:Fluent.Pdf.PdfDiagnosticSeverity</code>

Diagnostic severity.

### <code>T:Fluent.Pdf.PdfDifference</code>

One detached difference with exact page/object provenance on both inputs.

### <code>T:Fluent.Pdf.PdfDifferenceKind</code>

Independent comparison dimensions; equality in one dimension never implies equality in another.

### <code>T:Fluent.Pdf.PdfDocument</code>

An owned in-memory editing session with explicit provider-backed I/O.

Remarks: Single-document mutation and I/O must be serialized by the caller. Provider instances remain caller-owned.

### <code>T:Fluent.Pdf.PdfDocumentPermissions</code>

Document permission bits carried by standard PDF security handlers.

### <code>T:Fluent.Pdf.PdfDocumentSnapshot</code>

An immutable, detached page-model snapshot passed to writers.

Remarks: Snapshots remain valid after the source document changes or is disposed. They are not PDF bytes.

### <code>T:Fluent.Pdf.PdfDrawing</code>

A self-contained vector drawing with a private graphics-state scope and explicit immutable resources.

### <code>T:Fluent.Pdf.PdfEngineering</code>

Passive cross-platform advanced-content inspection and exact-byte preservation. No U3D/PRC/media/geospatial renderer is bundled.

### <code>T:Fluent.Pdf.PdfEngineeringContentKind</code>

Passive engineering content families; no record implies execution or rendering support.

### <code>T:Fluent.Pdf.PdfEngineeringInspection</code>

Read-only passive inventory; zero records never establishes PDF/E conformance.

### <code>T:Fluent.Pdf.PdfEngineeringPreservation</code>

Exact-byte preserve-only evidence; not a signature, media rendering or conformance verdict.

### <code>T:Fluent.Pdf.PdfEngineeringRecord</code>

Immutable physical graph observation without loading a media engine, decoding payloads or running actions.

### <code>T:Fluent.Pdf.PdfException</code>

A failure with a typed, provider-neutral diagnostic.

### <code>T:Fluent.Pdf.PdfExtensionRequirement</code>

A caller-declared developer extension requirement; this declares a format dependency without implementing its semantics.

### <code>T:Fluent.Pdf.PdfExtractedArtifact</code>

An explicit source Artifact scope, distinct from inferred decorations or authenticated ownership.

### <code>T:Fluent.Pdf.PdfExtractedCharacter</code>

A decoded source character code and its Unicode cluster. Ligatures may map to multiple scalars sharing one advance cell.

### <code>T:Fluent.Pdf.PdfExtractedLine</code>

A geometrically inferred line, separate from source execution order.

### <code>T:Fluent.Pdf.PdfExtractedParagraph</code>

A geometric paragraph inference; it is not an accessibility/structure-tree assertion.

### <code>T:Fluent.Pdf.PdfExtractedPath</code>

A literal source path ending in a paint/end operator; this does not claim rendered ink or effective clipping geometry.

### <code>T:Fluent.Pdf.PdfExtractedPathElement</code>

One path-construction operator and its construction-time CTM, preserving mixed-transform paths.

### <code>T:Fluent.Pdf.PdfExtractedResource</code>

An immutable resource definition; shared indirect identities are separate from occurrences. Raw bytes are PDF stream bytes, not a reconstructed PNG or original container.

### <code>T:Fluent.Pdf.PdfExtractedResourceKind</code>

The declared role of an immutable captured PDF resource. Unknown roles remain observable.

### <code>T:Fluent.Pdf.PdfExtractedSpan</code>

A source ActualText replacement and its covered raw character range.

### <code>T:Fluent.Pdf.PdfExtractedTable</code>

One inferred table on one source page, retained independently of continuation decisions.

### <code>T:Fluent.Pdf.PdfExtractedTableCell</code>

An inferred rectangular cell with original source characters, rule provenance and optional explicit-culture decimal typing.

### <code>T:Fluent.Pdf.PdfExtractedTableSequence</code>

An explicit heuristic continuation grouping, without deleting repeated source rows.

### <code>T:Fluent.Pdf.PdfExtractedWord</code>

An inferred geometric word retaining its raw character records.

### <code>T:Fluent.Pdf.PdfFieldCollisionPolicy</code>

Explicit handling of colliding root form-field names.

### <code>T:Fluent.Pdf.PdfFilter</code>

An explicit PDF decoding filter and immutable direct decode parameters.

### <code>T:Fluent.Pdf.PdfFilters</code>

Bounded original managed filter chains. Image codecs are supplied through optional providers.

### <code>T:Fluent.Pdf.PdfFlow</code>

Bounded offline paragraph layout and deterministic pagination using explicit shaping providers.

### <code>T:Fluent.Pdf.PdfFlowAlignment</code>

Horizontal line alignment within the available flow interval.

### <code>T:Fluent.Pdf.PdfFlowBlock</code>

Base of immutable managed flow blocks.

### <code>T:Fluent.Pdf.PdfFlowClear</code>

Which preceding floats a block must pass.

### <code>T:Fluent.Pdf.PdfFlowColumnOrder</code>

Physical ordering of logical columns.

### <code>T:Fluent.Pdf.PdfFlowColumnPlacement</code>

An immutable logical column instance in a physical point rectangle.

### <code>T:Fluent.Pdf.PdfFlowColumns</code>

A fixed-height column region; logical content fills columns in the explicit physical order.

### <code>T:Fluent.Pdf.PdfFlowComponent</code>

Reusable immutable content. Kept components use an unsplittable borderless cell.

### <code>T:Fluent.Pdf.PdfFlowContents</code>

A contents placeholder resolved by the bounded multi-pass template planner.

### <code>T:Fluent.Pdf.PdfFlowFloat</code>

A reusable vector float excluding subsequent text from its rectangle.

### <code>T:Fluent.Pdf.PdfFlowFloatPlacement</code>

An immutable float placement in displayed physical point coordinates.

### <code>T:Fluent.Pdf.PdfFlowFloatSide</code>

Physical side of a floating rectangle.

### <code>T:Fluent.Pdf.PdfFlowHeading</code>

An explicit body heading and stable destination ID.

### <code>T:Fluent.Pdf.PdfFlowHeadingPlacement</code>

A resolved physical heading destination.

### <code>T:Fluent.Pdf.PdfFlowIndex</code>

Explicit index locators, sorted with the supplied culture; no implicit text search occurs.

### <code>T:Fluent.Pdf.PdfFlowIndexTerm</code>

An explicitly indexed term and its stable body destination IDs.

### <code>T:Fluent.Pdf.PdfFlowLine</code>

An immutable line with exact logical paragraph provenance and physical point geometry.

### <code>T:Fluent.Pdf.PdfFlowList</code>

An immutable list whose counters are fixed before pagination.

### <code>T:Fluent.Pdf.PdfFlowListItem</code>

Immutable content with an optional local counter restart.

### <code>T:Fluent.Pdf.PdfFlowListKind</code>

Invariant list marker notation.

### <code>T:Fluent.Pdf.PdfFlowNote</code>

An immutable note definition, reusable by multiple explicit references.

### <code>T:Fluent.Pdf.PdfFlowNotePlacement</code>

A note's physical placement and source references on a flow page.

### <code>T:Fluent.Pdf.PdfFlowNoteReference</code>

A paragraph reference with an automatically appended raised note label.

### <code>T:Fluent.Pdf.PdfFlowOptions</code>

Immutable page frame and explicit overflow/tab settings for a complete layout operation.

### <code>T:Fluent.Pdf.PdfFlowOverflow</code>

Explicit handling of a word wider than an empty available interval.

### <code>T:Fluent.Pdf.PdfFlowPage</code>

An immutable page with line/float geometry and its complete reusable canvas.

### <code>T:Fluent.Pdf.PdfFlowPageBreak</code>

An explicit page boundary, including an intentional blank page when already at a page start.

### <code>T:Fluent.Pdf.PdfFlowParagraph</code>

A paragraph with logical run order and explicit pagination rules.

### <code>T:Fluent.Pdf.PdfFlowParagraphStyle</code>

Immutable paragraph geometry and pagination constraints. Distances are physical points.

### <code>T:Fluent.Pdf.PdfFlowResult</code>

A detached deterministic layout, ready for independent page inspection and managed writing.

### <code>T:Fluent.Pdf.PdfFlowRun</code>

A logical UTF-16 run. A style boundary may not split an extended grapheme.

### <code>T:Fluent.Pdf.PdfFlowSectionBreak</code>

An explicit continuous/new/odd/even section boundary.

### <code>T:Fluent.Pdf.PdfFlowSectionStart</code>

An explicit section boundary without changing page geometry.

### <code>T:Fluent.Pdf.PdfFlowTextStyle</code>

Immutable shaping, font size and color for a logical text run.

### <code>T:Fluent.Pdf.PdfFont</code>

An immutable, explicitly loaded font face with fresh budgets for each public operation.

### <code>T:Fluent.Pdf.PdfFontEmbeddingRights</code>

Embedding restrictions declared by the font's license metadata; zero means installable embedding.

### <code>T:Fluent.Pdf.PdfFontFallback</code>

An immutable caller-ordered font fallback list; resolution never consults operating-system font order.

### <code>T:Fluent.Pdf.PdfFontFormat</code>

The outline/container representation supplied by a font provider.

### <code>T:Fluent.Pdf.PdfFontGlyph</code>

An exact font/glyph mapping and its unscaled advances; zero is the explicit missing-glyph identity.

### <code>T:Fluent.Pdf.PdfFontInfo</code>

An immutable font identity and license/metric declaration from an explicitly selected provider.

### <code>T:Fluent.Pdf.PdfFontMetrics</code>

Immutable font-unit metrics. Coordinates use the font's unscaled design space.

### <code>T:Fluent.Pdf.PdfFontProgramWriteResult</code>

Format details for an eagerly written embedded font program.

### <code>T:Fluent.Pdf.PdfFontResource</code>

An immutable eagerly embedded font resource and its encoded show-text operand.

### <code>T:Fluent.Pdf.PdfFontVerticalMetrics</code>

Exact optional vertical design-space metrics for an explicitly supported face.

### <code>T:Fluent.Pdf.PdfFontWriteResult</code>

Emitted font program details and the complete successful operation report.

### <code>T:Fluent.Pdf.PdfFormChoice</code>

One immutable choice export/display pair.

### <code>T:Fluent.Pdf.PdfFormData</code>

Immutable bounded field-value data. Parsing never imports actions, opens file references or executes scripts.

### <code>T:Fluent.Pdf.PdfFormDataField</code>

One exact qualified field name and detached current value.

### <code>T:Fluent.Pdf.PdfFormDataFormat</code>

Explicit field-value interchange formats. XML/JSON use the documented version-one SDK data schema.

### <code>T:Fluent.Pdf.PdfFormField</code>

A logical field with one or more valid same-name terminal representations.

### <code>T:Fluent.Pdf.PdfFormFieldDefinition</code>

An immutable terminal field definition. Partial names are separated by periods in Name.

### <code>T:Fluent.Pdf.PdfFormFieldFlags</code>

Common and kind-specific standard field behavior flags. Kind discriminator bits are derived from the field kind.

### <code>T:Fluent.Pdf.PdfFormFieldKind</code>

The standard AcroForm terminal field kind.

### <code>T:Fluent.Pdf.PdfFormSet</code>

A passive AcroForm inventory and operation report.

### <code>T:Fluent.Pdf.PdfFormTabOrder</code>

Standard page widget traversal order.

### <code>T:Fluent.Pdf.PdfFormTextStyle</code>

Explicit offline text appearance resources and native-unit geometry choices.

### <code>T:Fluent.Pdf.PdfFormUpdate</code>

An immutable value update with explicit appearance generation or exact per-widget private Forms.

### <code>T:Fluent.Pdf.PdfFormWidget</code>

A detached passive widget record.

### <code>T:Fluent.Pdf.PdfFormWidgetDefinition</code>

An authored widget with caller-selected appearance data and explicit page ownership.

### <code>T:Fluent.Pdf.PdfForms</code>

Managed passive AcroForm creation, field/widget editing and calculation metadata.

### <code>T:Fluent.Pdf.PdfFunction</code>

An immutable PDF interpolation/tint function, preserved as vector resource data without executing document code.

### <code>T:Fluent.Pdf.PdfGenerationContext</code>

A callback-thread generation builder. Mutation is permitted only inside an active template invocation; completed contexts cannot be reused.

### <code>T:Fluent.Pdf.PdfGenerationEvent</code>

An immutable hook view without a live editable document or output stream.

### <code>T:Fluent.Pdf.PdfGenerationEventKind</code>

Ordered synchronous preparation phases. Every hook runs before successful return or atomic file publication.

### <code>T:Fluent.Pdf.PdfGenerationOptions</code>

Immutable explicit layout/culture/template/hook and finite data-generation ceilings.

### <code>T:Fluent.Pdf.PdfGenerationPage</code>

Immutable final-page observations without a live canvas, mutable model or unfinished operation report.

### <code>T:Fluent.Pdf.PdfGenerationTemplate`1</code>

An immutable typed reusable renderer. Call nested templates through the generation context for cycle/depth accounting.

### <code>T:Fluent.Pdf.PdfGlyphMapping</code>

One actual glyph and its exact logical Unicode text, including multi-scalar ligatures.

### <code>T:Fluent.Pdf.PdfGraphemeCluster</code>

An extended grapheme cluster with exact scalar and UTF-16 extents.

### <code>T:Fluent.Pdf.PdfGraphicsObjects</code>

Original form/pattern construction preserving vector operators and resource scopes.

### <code>T:Fluent.Pdf.PdfGraphicsOccurrence</code>

One immutable invocation, retaining its source scope and exact encoded bytes; clipping remains in the surrounding content.

### <code>T:Fluent.Pdf.PdfGraphicsOccurrenceKind</code>

The kind of an actually invoked page graphics object.

### <code>T:Fluent.Pdf.PdfGraphicsResource</code>

A detached immutable resource graph. Nested streams become indirect objects when saved.

### <code>T:Fluent.Pdf.PdfGraphicsResourceCategory</code>

The standard resource dictionary containing a named reusable graphics object.

### <code>T:Fluent.Pdf.PdfGraphicsState</code>

An immutable explicit extended graphics state with standard compositing behavior.

### <code>T:Fluent.Pdf.PdfHtml</code>

Explicit optional HTML conversion importing the result through the ordinary managed reader into the editable document model.

### <code>T:Fluent.Pdf.PdfHtmlMedia</code>

Explicit CSS media for browser document preparation and printing.

### <code>T:Fluent.Pdf.PdfHtmlOptions</code>

Explicit bounded browser layout/print choices. Javascript is disabled unless the caller enables it for this conversion.

### <code>T:Fluent.Pdf.PdfHtmlPrintOptions</code>

Immutable browser print policy. Geometry is requested in PDF points; actual engine page boxes are retained.

### <code>T:Fluent.Pdf.PdfHtmlReadinessOptions</code>

Immutable bounded main-document readiness policy. Caller JavaScript is explicit and requires source script execution to be enabled.

### <code>T:Fluent.Pdf.PdfHtmlResource</code>

An immutable explicitly supplied browser resource. No path or URL is fetched by creating it.

### <code>T:Fluent.Pdf.PdfHtmlSource</code>

Captured markup or an explicitly addressed main resource and immutable local resource map. It authorizes no network or filesystem access.

### <code>T:Fluent.Pdf.PdfImageAsset</code>

A detached immutable image, exact encoded samples and explicit display metadata.

### <code>T:Fluent.Pdf.PdfImageCodec</code>

An explicitly selected optional image stream codec.

### <code>T:Fluent.Pdf.PdfImageDeduplicationResult</code>

An immutable per-operation image interning result; original assets and their reports remain unchanged.

### <code>T:Fluent.Pdf.PdfImageInfo</code>

Immutable interleaved image geometry. Samples are packed most-significant bit first, with each row byte-aligned.

### <code>T:Fluent.Pdf.PdfImageMetadata</code>

Immutable explicit image orientation, pixel aspect and optional physical resolution.

### <code>T:Fluent.Pdf.PdfImageOrientation</code>

The eight standard TIFF/Exif display orientations.

### <code>T:Fluent.Pdf.PdfImageReadOptions</code>

Immutable image-file import choices; preserving encoded data never silently transcodes it.

### <code>T:Fluent.Pdf.PdfImages</code>

Bounded image construction and byte-preserving packaging, independent of platform graphics APIs.

### <code>T:Fluent.Pdf.PdfInputConversion</code>

Original bounded offline Unicode/Markdown/JSON/XML conversion through shared-context generation.

### <code>T:Fluent.Pdf.PdfInputConversionOptions</code>

Immutable explicit generation, font/style and named-resource choices for source conversion.

### <code>T:Fluent.Pdf.PdfInputDrawing</code>

An explicitly supplied reusable drawing; names in source refer only to these captured resources.

### <code>T:Fluent.Pdf.PdfInputFormat</code>

Explicit offline source syntax. None performs resource discovery or executes document actions.

### <code>T:Fluent.Pdf.PdfInvoiceData</code>

Immutable source model with explicit edition, parties, UTC issue time, currency and rounding. No discounts, credit notes, prepayments or inferred tax jurisdiction are supported in this source profile.

### <code>T:Fluent.Pdf.PdfInvoiceLine</code>

Immutable literal invoice line used by the same calculation for human-readable totals and CII header amounts.

### <code>T:Fluent.Pdf.PdfInvoiceProfile</code>

Exact requested invoice version/profile, not an automatic standards support claim.

### <code>T:Fluent.Pdf.PdfInvoiceTaxCategory</code>

Immutable tax breakdown for one explicit percentage rate. No tax exemption or jurisdiction is inferred.

### <code>T:Fluent.Pdf.PdfInvoiceValidation</code>

Complete/incomplete exact-byte independent invoice observation; not SDK production qualification.

### <code>T:Fluent.Pdf.PdfInvoices</code>

One-source invoice calculation, CII/human composition and mandatory complete independent PDF/A/invoice validation gates.

### <code>T:Fluent.Pdf.PdfLayerGroup</code>

An immutable registered optional-content group and its passive source dictionary.

### <code>T:Fluent.Pdf.PdfLayerSet</code>

A detached layer/configuration inventory.

### <code>T:Fluent.Pdf.PdfLayerUsage</code>

The explicit optional-content usage event.

### <code>T:Fluent.Pdf.PdfLayerVisibility</code>

An explicit visibility decision and its operation report.

### <code>T:Fluent.Pdf.PdfLayers</code>

Managed passive optional-content authoring, configuration evaluation and explicit flattening.

### <code>T:Fluent.Pdf.PdfLengthUnit</code>

Explicit lengths used in a canvas coordinate frame.

### <code>T:Fluent.Pdf.PdfLineCap</code>

Standard PDF line cap behavior.

### <code>T:Fluent.Pdf.PdfLineJoin</code>

Standard PDF line join behavior.

### <code>T:Fluent.Pdf.PdfLinearization</code>

Staged exact-byte linearization gates and passive dictionary inspection. The core does not bundle a complete producer or independent hint validator.

### <code>T:Fluent.Pdf.PdfLinearizationInfo</code>

Structural linearization dictionary observations. These bounds alone do not establish hint-table validity or web optimization.

### <code>T:Fluent.Pdf.PdfLinearizationOptions</code>

Immutable web-optimization request. Full rewrites discard existing linearization and must rebuild it for the exact resulting bytes.

### <code>T:Fluent.Pdf.PdfLinearizationValidation</code>

Independent complete linearization verdict bound to exact candidate and request bytes.

### <code>T:Fluent.Pdf.PdfLossCategory</code>

The kind of loss explicitly reported by a provider.

### <code>T:Fluent.Pdf.PdfMailMerge</code>

Closed native PDF merge programs. Evaluation, image/table binding, layout, providers and writing use one shared root context.

### <code>T:Fluent.Pdf.PdfMailMergeOptions</code>

Native template generation with explicit layout/culture/styles and closed finite evaluation.

### <code>T:Fluent.Pdf.PdfMargins</code>

Finite nonnegative content margins, expressed in the canvas unit.

### <code>T:Fluent.Pdf.PdfMatrix</code>

A finite affine PDF matrix: x'=A*x+C*y+E, y'=B*x+D*y+F.

### <code>T:Fluent.Pdf.PdfMatrixIccTransform</code>

Original media-relative ICC RGB matrix/TRC conversion in D50 XYZ PCS. LUT, absolute/perceptual gamut mapping, black-point compensation and separations require a separately qualified provider.

### <code>T:Fluent.Pdf.PdfMergeBlock</code>

A finite closed merge instruction. No node contains executable callbacks, reflected member access or resource resolution.

### <code>T:Fluent.Pdf.PdfMergeCondition</code>

A boolean-only branch. No truthiness coercion or expression execution occurs.

### <code>T:Fluent.Pdf.PdfMergeEvaluationOptions</code>

Closed template evaluation policy, with explicit formatting culture, missing fields and finite visit/record ceilings.

### <code>T:Fluent.Pdf.PdfMergeImage</code>

Explicit immutable PNG bytes and physical geometry for both native PDF and DOCX merge sources.

### <code>T:Fluent.Pdf.PdfMergeImageBlock</code>

An explicit captured image field. Native output places it before subsequent text; source bytes and geometry remain caller-selected.

### <code>T:Fluent.Pdf.PdfMergeMissingField</code>

Explicit handling of an absent field; a present null scalar is distinct.

### <code>T:Fluent.Pdf.PdfMergeParagraph</code>

A native paragraph with finite literal/field runs and explicit paragraph geometry.

### <code>T:Fluent.Pdf.PdfMergeRecord</code>

An ordinal immutable merge field collection. Nested values are already immutable and contain no callbacks.

### <code>T:Fluent.Pdf.PdfMergeRepeat</code>

A finite record repetition; child paths are relative to each immutable row, with root access explicitly prefixed by root.

### <code>T:Fluent.Pdf.PdfMergeRun</code>

An immutable literal or scalar-field run with an explicitly named text style.

### <code>T:Fluent.Pdf.PdfMergeTable</code>

A finite native table bound to a closed record collection.

### <code>T:Fluent.Pdf.PdfMergeTableColumn</code>

An explicit data-table column, with closed scalar/literal cell runs and optional header text.

### <code>T:Fluent.Pdf.PdfMergeTemplate</code>

A finite immutable native PDF template program.

### <code>T:Fluent.Pdf.PdfMergeValue</code>

A closed immutable merge value: scalar, record, finite record collection or explicitly captured PNG image. No reflected properties or expressions are evaluated.

### <code>T:Fluent.Pdf.PdfMergedCellCsvPolicy</code>

An explicit lossy flattening decision for CSV merged cells.

### <code>T:Fluent.Pdf.PdfMetadata</code>

Managed passive metadata editing with explicit clocks, identifiers and synchronization authority.

### <code>T:Fluent.Pdf.PdfMetadataEditor</code>

A synchronous, operation-owned metadata editor. It expires when its callback returns.

### <code>T:Fluent.Pdf.PdfMetadataEmbeddedFilePolicy</code>

How document metadata removal handles files whose internal metadata is outside the PDF graph.

### <code>T:Fluent.Pdf.PdfMetadataRemovalOptions</code>

Explicit metadata-removal decisions. Removal always rewrites the active graph without prior revisions.

### <code>T:Fluent.Pdf.PdfMetadataSet</code>

A detached passive document metadata inventory, including raw values and parsing diagnostics.

### <code>T:Fluent.Pdf.PdfMetadataSynchronization</code>

The explicit authority used when synchronizing equivalent Info and XMP properties.

### <code>T:Fluent.Pdf.PdfMissingDestinationPolicy</code>

Explicit handling of navigation destinations excluded by a selection.

### <code>T:Fluent.Pdf.PdfNamedDestination</code>

A named destination and its resolved local page identity.

### <code>T:Fluent.Pdf.PdfNativeAsset</code>

A pinned local asset. The inspector never downloads or loads this file.

### <code>T:Fluent.Pdf.PdfNativeDeployment</code>

An immutable offline provisioning description; it grants no execution capability.

### <code>T:Fluent.Pdf.PdfNativeDeploymentInspector</code>

Inspects explicitly provisioned local files without loading libraries, executing code or searching PATH.

### <code>T:Fluent.Pdf.PdfNativeDeploymentReport</code>

A detached integrity preflight result, not permission to execute or proof of ABI/sandbox compatibility.

### <code>T:Fluent.Pdf.PdfNavigation</code>

Original managed navigation authoring, inspection and explicit action-policy sanitization.

### <code>T:Fluent.Pdf.PdfNavigationSet</code>

A bounded passive inspection of all active objects, including orphan action definitions and physical attachment edges.

### <code>T:Fluent.Pdf.PdfObjectId</code>

An indirect object number and generation. Object zero is the null reference.

### <code>T:Fluent.Pdf.PdfOcr</code>

Explicit bounded OCR with shared provider contexts and no implicit rasterizer, native library or model installation.

### <code>T:Fluent.Pdf.PdfOcrAccuracy</code>

Original bounded Unicode-scalar CER and ordinal whitespace-word WER, independent of recognition confidence.

### <code>T:Fluent.Pdf.PdfOcrAccuracyGroup</code>

A per-language/model error observation; passing thresholds is not production or model qualification.

### <code>T:Fluent.Pdf.PdfOcrAccuracyOptions</code>

Explicit bounded accuracy evaluation with thresholds fixed by the caller before observations are measured.

### <code>T:Fluent.Pdf.PdfOcrAccuracyReport</code>

An immutable corpus measurement bound to exact reference/recognized data digests, without a qualification claim.

### <code>T:Fluent.Pdf.PdfOcrAccuracySample</code>

An explicit labeled recognition observation. Reference text must come from a separately retained corpus, not generated implementation output.

### <code>T:Fluent.Pdf.PdfOcrAccuracyThresholds</code>

Explicit predeclared per-language error thresholds, separate from engine confidence and SDK qualification.

### <code>T:Fluent.Pdf.PdfOcrExistingTextPolicy</code>

Explicit handling of sufficiently overlapping, mapped existing digital words.

### <code>T:Fluent.Pdf.PdfOcrLanguageModel</code>

Explicit immutable metadata for one locally provisioned printed-text language/script model. Declarations do not qualify accuracy or licensing.

### <code>T:Fluent.Pdf.PdfOcrModelCatalog</code>

An immutable caller-selected catalog of at most32 explicit local models. It never searches directories or downloads a model.

### <code>T:Fluent.Pdf.PdfOcrModelSelection</code>

An immutable exact model/version/content selection, independent of accuracy or publication qualification.

### <code>T:Fluent.Pdf.PdfOcrOptions</code>

Explicit recognition language and finite output ceilings. A provider must reject unsupported languages rather than substitute a model.

### <code>T:Fluent.Pdf.PdfOcrOutput</code>

Bounded OCR interchange. Geometry is always top-left raster pixels, never implicitly PDF coordinates.

### <code>T:Fluent.Pdf.PdfOcrOutputFormat</code>

Explicit original OCR interchange profiles, all UTF8 without BOM.

### <code>T:Fluent.Pdf.PdfOcrOverlayOptions</code>

Finite invisible-text placement and explicit existing-layer policy. No OCR confidence establishes digital-text adequacy.

### <code>T:Fluent.Pdf.PdfOcrPageFrame</code>

An explicit source-raster pixel frame bound to exact PDF crop, rotation and UserUnit geometry.

### <code>T:Fluent.Pdf.PdfOcrPixelFormat</code>

Explicit packed OCR raster byte order; no image format or alpha interpretation is guessed.

### <code>T:Fluent.Pdf.PdfOcrRaster</code>

An immutable explicitly captured top-left-origin raster, independent of a rendering engine or implicit image decoder.

### <code>T:Fluent.Pdf.PdfOcrResult</code>

Immutable recognized Unicode and engine-order word geometry. It does not establish language accuracy, reading order or production qualification.

### <code>T:Fluent.Pdf.PdfOcrSearchable</code>

Original transactional searchable-text overlays. Existing raster/content bytes remain intact; text renders with mode3.

### <code>T:Fluent.Pdf.PdfOcrSegmentation</code>

Explicit local segmentation without implicit orientation/script-model discovery.

### <code>T:Fluent.Pdf.PdfOcrWord</code>

An immutable engine-recognized word, with source-raster geometry and confidence distinct from correctness.

### <code>T:Fluent.Pdf.PdfOperationContext</code>

A synchronized per-operation policy, diagnostic and budget boundary, owned by the SDK.

Remarks: Providers must charge resources before allocating, poll checkpoints, and finish all workers/scopes before returning.

### <code>T:Fluent.Pdf.PdfOperationPolicy</code>

Immutable operation behavior and resource ceilings. There is no global mutable policy.

### <code>T:Fluent.Pdf.PdfOperationReport</code>

A detached immutable result containing every accepted diagnostic and resource usage.

### <code>T:Fluent.Pdf.PdfOperator</code>

Standard PDF content-stream operators; unknown tokens require an explicit preservation policy.

### <code>T:Fluent.Pdf.PdfOptimization</code>

Original bounded PDF graph optimization with atomic output, exact resource sharing and opt-in image resampling.

### <code>T:Fluent.Pdf.PdfOptimizationOptions</code>

Immutable full-rewrite choices. Signature invalidation and image fidelity loss require separate explicit authority.

### <code>T:Fluent.Pdf.PdfOptimizationPreset</code>

Bounded explicit optimization presets; no renderer or codec is selected implicitly.

### <code>T:Fluent.Pdf.PdfOptimizationResult</code>

Completed immutable optimized bytes and measured transformation counts, without a standards/fidelity qualification claim.

### <code>T:Fluent.Pdf.PdfOptions</code>

Immutable options captured when a document session is created.

Remarks: Page limits apply to the model; the immutable operation policy creates independent read/save budgets.

### <code>T:Fluent.Pdf.PdfOrganizationOptions</code>

Immutable organization choices. Encryption requires an explicit decryption provider before import.

### <code>T:Fluent.Pdf.PdfOutlineRecord</code>

A passive outline observation preserving unknown source keys.

### <code>T:Fluent.Pdf.PdfPades</code>

Fail-closed incremental PAdES orchestration with exact-byte independent validation before publication.

### <code>T:Fluent.Pdf.PdfPadesProfile</code>

Requested ETSI baseline signature profile. Selecting a value does not establish compliance.

### <code>T:Fluent.Pdf.PdfPadesValidation</code>

Complete independent PAdES validation bound to exact bytes and requested profile.

### <code>T:Fluent.Pdf.PdfPage</code>

A page owned by one document session; removed or disposed pages cannot be reused.

### <code>T:Fluent.Pdf.PdfPageBox</code>

The five standard page box selections.

### <code>T:Fluent.Pdf.PdfPageBoxes</code>

Immutable finite standard page boxes in native PDF user coordinates.

### <code>T:Fluent.Pdf.PdfPageCollection</code>

A zero-based editable page collection. Document mutation is not thread-safe.

### <code>T:Fluent.Pdf.PdfPageDecoration</code>

Immutable explicit flow, vector/image or optional HTML decoration.

### <code>T:Fluent.Pdf.PdfPageFont</code>

A detached page font binding for immutable source inspection, binary extraction and exact Unicode decoding.

### <code>T:Fluent.Pdf.PdfPageGeometry</code>

Immutable page boxes, native user-unit scale and clockwise viewing rotation.

### <code>T:Fluent.Pdf.PdfPageGraphics</code>

An immutable page graphics walk. The captured graph remains valid after source disposal; no document actions or optional codecs run.

### <code>T:Fluent.Pdf.PdfPageImposition</code>

Managed vector N-up, booklet and tiled page imposition with explicit sheet geometry.

### <code>T:Fluent.Pdf.PdfPageInput</code>

A detached document snapshot and an optional ordered selection.

### <code>T:Fluent.Pdf.PdfPageLabelStyle</code>

Standard page-label numbering styles.

### <code>T:Fluent.Pdf.PdfPageLayout</code>

Standard catalog page layouts.

### <code>T:Fluent.Pdf.PdfPageLinkSnapshot</code>

An immutable passive internal or URI page link in native PDF user coordinates.

### <code>T:Fluent.Pdf.PdfPageMode</code>

Standard initial viewer page modes.

### <code>T:Fluent.Pdf.PdfPageNumberRange</code>

An immutable numbering restart at a zero-based physical page.

### <code>T:Fluent.Pdf.PdfPageNumbering</code>

Immutable physical-page numbering ranges with invariant formatting.

### <code>T:Fluent.Pdf.PdfPageOrganization</code>

Bounded managed page import, extraction and composition; source sessions and streams remain caller-owned.

### <code>T:Fluent.Pdf.PdfPagePresentationRecord</code>

One page's passive transition and display-duration settings.

### <code>T:Fluent.Pdf.PdfPageResourceManifest</code>

A passive source inventory. All conditional/layer content is inventoried; actual visibility and rendered fidelity require explicit evaluation/rendering.

### <code>T:Fluent.Pdf.PdfPageRotation</code>

Clockwise page display rotation, independent of the physical page size.

### <code>T:Fluent.Pdf.PdfPageSelection</code>

An immutable ordered zero-based selection; duplicate indices request independent page copies.

### <code>T:Fluent.Pdf.PdfPageSize</code>

Immutable physical page dimensions in points (72 points per inch).

### <code>T:Fluent.Pdf.PdfPageSnapshot</code>

A detached, immutable page geometry value.

### <code>T:Fluent.Pdf.PdfPageSource</code>

An immutable original page binding, including effective standard inheritable attributes.

### <code>T:Fluent.Pdf.PdfPageTemplate</code>

Finite reserved header/footer regions selected from final page context.

### <code>T:Fluent.Pdf.PdfPageTemplateContext</code>

Final stable page data for one synchronous header/footer callback.

### <code>T:Fluent.Pdf.PdfPageTemplateResult</code>

Final body/decoration output with resolved page labels and contents/index destinations.

### <code>T:Fluent.Pdf.PdfPageTransition</code>

An immutable transition request; authoring never runs an animation.

### <code>T:Fluent.Pdf.PdfPaint</code>

An explicit solid color or colored/uncolored pattern paint.

### <code>T:Fluent.Pdf.PdfParagraphDirection</code>

Explicit paragraph base direction, or automatic first-strong resolution.

### <code>T:Fluent.Pdf.PdfPassword</code>

An explicitly owned, disposable copy of standard-prepared password bytes.

Remarks: Revision 6 uses UTF-8 bytes after the standard's password preparation; legacy handlers use their specified eight-bit bytes. This byte API performs no implicit Unicode preparation. Dispose after all operations have acquired their private copies.

### <code>T:Fluent.Pdf.PdfPasswordCipher</code>

Standard password encryption selected explicitly for new output.

### <code>T:Fluent.Pdf.PdfPasswordProtection</code>

Immutable password protection choices. Credentials remain caller-owned and are never added to document snapshots or reports.

### <code>T:Fluent.Pdf.PdfPath</code>

An immutable path that retains vector segments and can be edited without changing another drawing.

### <code>T:Fluent.Pdf.PdfPathSegment</code>

A typed immutable path segment in PDF user coordinates.

### <code>T:Fluent.Pdf.PdfPattern</code>

An immutable tiling or shading pattern preserving vector paint semantics.

### <code>T:Fluent.Pdf.PdfPoint</code>

A finite point in PDF Cartesian user coordinates.

### <code>T:Fluent.Pdf.PdfPreflight</code>

Passive source preflight and explicit full-rewrite remediation with exact source binding, post-fix verification and machine-readable audit.

### <code>T:Fluent.Pdf.PdfPreflightFinding</code>

Stable passive finding with physical provenance and an explicitly bounded optional fix.

### <code>T:Fluent.Pdf.PdfPreflightReport</code>

Immutable read-only bounded structural/security inventory. Absence of findings is not standards compliance.

### <code>T:Fluent.Pdf.PdfPresentation</code>

Managed outline, label, viewer-preference and presentation editing without executing actions.

### <code>T:Fluent.Pdf.PdfPresentationSet</code>

A passive presentation inventory retaining unsupported settings and diagnostics.

### <code>T:Fluent.Pdf.PdfPrintDuplex</code>

Explicit physical sheet duplex policy; no printer defaults are inferred.

### <code>T:Fluent.Pdf.PdfPrintJob</code>

One bounded spool observation. No output success is inferred from an accepted job.

### <code>T:Fluent.Pdf.PdfPrintJobState</code>

Observed spool state. Submission does not prove physical printing.

### <code>T:Fluent.Pdf.PdfPrintOptions</code>

Immutable explicit printer and sheet choices. Page numbers are one-based and inclusive.

### <code>T:Fluent.Pdf.PdfPrintScaling</code>

Explicit print scaling.

### <code>T:Fluent.Pdf.PdfPrinting</code>

Explicit headless printing; inspection/opening never selects a printer or submits a job.

### <code>T:Fluent.Pdf.PdfProcessingJob</code>

An expiring synchronous worker callback boundary owning one private document, input lease and shared operation budget.

Remarks: Use Save/SaveFile on this boundary. Live model references cannot be used on other threads or after completion.

### <code>T:Fluent.Pdf.PdfProcessingMode</code>

Controls whether explicitly reported losses can produce successful output.

### <code>T:Fluent.Pdf.PdfProcessingOptions</code>

Immutable limits on pool workers, waiting jobs and copied input bytes across waiting and running jobs.

### <code>T:Fluent.Pdf.PdfProcessingPool</code>

A fixed-worker asynchronous boundary with bounded, nonblocking admission and privately owned documents.

Remarks: Callbacks execute synchronously on owned workers and must cooperate with their context. No arbitrary managed callback can be forcibly terminated safely. Provider instances and captured callback state remain caller-owned.

### <code>T:Fluent.Pdf.PdfProcessingProgress</code>

A detached aggregate progress snapshot with no document data or unbounded event queue.

### <code>T:Fluent.Pdf.PdfProfileConversions</code>

Explicit exact-byte profile gates. No bundled complete PDF/X producer or validator is implied.

### <code>T:Fluent.Pdf.PdfProfileOptions</code>

Explicit profile, output intent and signature authority. Providers must report every color/flattening/content loss through the shared context.

### <code>T:Fluent.Pdf.PdfProfileValidation</code>

Complete or incomplete independent observation bound to exact candidate bytes and request settings.

### <code>T:Fluent.Pdf.PdfProgressiveDocument</code>

A caller-owned, nonconcurrent range session. Syntax indexes and requested objects are parsed per operation; bounded transport blocks persist until disposal. No whole-file snapshot or document edit model is implied.

### <code>T:Fluent.Pdf.PdfProgressiveOptions</code>

Finite session cache settings; the cache is private to one source and revision.

### <code>T:Fluent.Pdf.PdfProgressivePage</code>

Detached page dictionary with the four inheritable attributes applied. Indirect resources remain lazy references; no actions execute.

### <code>T:Fluent.Pdf.PdfProviderDescriptor</code>

Immutable identity and advertised services captured at registration.

### <code>T:Fluent.Pdf.PdfProviderRegistry</code>

An immutable, explicit registry with at most 64 caller-owned provider instances.

Remarks: Duplicate identities/services are rejected. There is no global discovery, automatic download or implicit fallback.

### <code>T:Fluent.Pdf.PdfProviderUnavailableException</code>

A requested optional capability has no registered provider.

### <code>T:Fluent.Pdf.PdfPublicKeyCipher</code>

Explicit file cipher for certificate security handlers.

### <code>T:Fluent.Pdf.PdfQuad</code>

Four ordered corners of an affine rectangle, retaining its orientation under transforms.

### <code>T:Fluent.Pdf.PdfRangeIdentity</code>

Immutable source identity. Version must be an opaque nonsecret immutable revision identifier, never a credential or URL.

### <code>T:Fluent.Pdf.PdfRangeResponse</code>

A range response whose body ownership transfers to the SDK. Disposal closes the body.

### <code>T:Fluent.Pdf.PdfRaster</code>

Detached immutable top-down straight-alpha BGRA samples. Transparent pixels have zero RGB.

### <code>T:Fluent.Pdf.PdfRasterColor</code>

Device-color output selection; grayscale retains three equal color channels.

### <code>T:Fluent.Pdf.PdfRasterDocuments</code>

Original bounded raster-document composition with native PNG/JPEG packaging and explicit optional frame codecs.

### <code>T:Fluent.Pdf.PdfRasterExport</code>

Bounded original PNG/BMP/multipage TIFF export, rendering one frame at a time into private staged output.

### <code>T:Fluent.Pdf.PdfRasterExportOptions</code>

Explicit page/region raster export. Render choices include DPI, alpha/matte, annotations, print/layer use and color.

### <code>T:Fluent.Pdf.PdfRasterFileFormat</code>

Explicit raster-file families. Optional codec availability is checked before source conversion.

### <code>T:Fluent.Pdf.PdfRasterFileInfo</code>

Finite inspected file frames, before sample allocation.

### <code>T:Fluent.Pdf.PdfRasterFormat</code>

Original lossless raster container profiles.

### <code>T:Fluent.Pdf.PdfRasterFrameInfo</code>

One complete unassociated RGBA8 frame with explicit orientation, density and RGB/ICC interpretation.

### <code>T:Fluent.Pdf.PdfRasterImportOptions</code>

Explicit physical page, fallback density, margins and managed PNG/JPEG packaging policy.

### <code>T:Fluent.Pdf.PdfRasterInput</code>

One borrowed file and an explicit ordered frame selection. The caller retains stream ownership until import completes.

### <code>T:Fluent.Pdf.PdfRasterRequest</code>

An SDK-validated integer raster plan. Providers paint only the requested window of the full-page pixel grid.

### <code>T:Fluent.Pdf.PdfRect</code>

A finite nonnegative Cartesian rectangle with ordered bounds.

### <code>T:Fluent.Pdf.PdfRedaction</code>

Actual sample replacement and comprehensive fresh-graph raster sanitization. A painted overlay is never used as a removal mechanism.

### <code>T:Fluent.Pdf.PdfRedactionAudit</code>

A payload-free transformation audit. It is not independent recovery/renderer qualification.

### <code>T:Fluent.Pdf.PdfRedactionCoverage</code>

Audited requested and actual outward-snapped removal coverage, without removed payloads or their hashes.

### <code>T:Fluent.Pdf.PdfRedactionRasterOptions</code>

Explicit destructive raster/full-rewrite policy. All original object programs, tags, forms, metadata, actions, attachments, hidden layers and revision bytes are discarded; unmasked page content is retained only as RGB pixels.

### <code>T:Fluent.Pdf.PdfRedactionRegion</code>

An explicit removal rectangle in displayed top-left CropBox physical points.

### <code>T:Fluent.Pdf.PdfRedactionResult</code>

An immutable serialized redaction result. Saving always copies these exact fresh bytes, with no incremental/old-history option.

### <code>T:Fluent.Pdf.PdfRemediationAudit</code>

Immutable source/output binding and before/after findings after verified explicit remediation.

### <code>T:Fluent.Pdf.PdfRemediationPlan</code>

Explicit immutable fix selection bound to exact inspected bytes. No automatic or font/renderer-based repairs occur.

### <code>T:Fluent.Pdf.PdfRenderOptions</code>

Immutable headless rendering choices in displayed CropBox coordinates.

### <code>T:Fluent.Pdf.PdfRendering</code>

Bounded optional-provider page and region rasterization.

### <code>T:Fluent.Pdf.PdfRepair</code>

Explicit bounded recovery with an untouched source view, complete object ledger and strict post-write structural parsing.

### <code>T:Fluent.Pdf.PdfRepairOptions</code>

Explicit recovery authority. Normal parsing never uses these options.

### <code>T:Fluent.Pdf.PdfRepairOrphanPolicy</code>

Explicit treatment of indexed objects unreachable from the recovered trailer.

### <code>T:Fluent.Pdf.PdfRepairRecord</code>

A deterministic recovery/validation ledger record. Messages contain no document strings or stream payloads.

### <code>T:Fluent.Pdf.PdfRepairResult</code>

A detached explicit recovery result with both exact source/output views and a complete repair ledger.

### <code>T:Fluent.Pdf.PdfRepairStreamLength</code>

A caller-authorized exact raw stream length; recovery never guesses an endstream marker inside payload bytes.

### <code>T:Fluent.Pdf.PdfResourceBinding</code>

One immutable exact resource name and object binding.

### <code>T:Fluent.Pdf.PdfResourceContent</code>

A captured content program with effective resources for operator cross-checks. References belong to the manifest snapshot.

### <code>T:Fluent.Pdf.PdfResourceDeclaration</code>

One resource binding, separate from resource identity and actual use.

### <code>T:Fluent.Pdf.PdfResourceExtraction</code>

Passive source resource/path/artifact inventory without rendering, font discovery or document action execution.

### <code>T:Fluent.Pdf.PdfResourceKind</code>

A resource accounted by the operation context.

### <code>T:Fluent.Pdf.PdfResourceLimits</code>

Immutable positive resource ceilings for one operation. Byte and object counters use 64-bit arithmetic.

### <code>T:Fluent.Pdf.PdfResourceOccurrence</code>

An actual invocation or selection. Visibility, clipping, color and glyph ink are not inferred from this inventory.

### <code>T:Fluent.Pdf.PdfResourceSet</code>

An explicit immutable resource scope. Duplicate names in one category are rejected.

### <code>T:Fluent.Pdf.PdfResourceSource</code>

An exact container/operator location. Page indices refer to concatenated page Contents; Form indices refer to that Form's program.

### <code>T:Fluent.Pdf.PdfResourceUsage</code>

An immutable resource counter snapshot.

### <code>T:Fluent.Pdf.PdfRevision</code>

A revision's trailer and cross-reference entries. Revisions are ordered newest first.

### <code>T:Fluent.Pdf.PdfRevisionAuditRecord</code>

One immutable revision inventory with structural changes and signatures physically introduced in it.

### <code>T:Fluent.Pdf.PdfRevisionChange</code>

A cross-reference change declaration. It does not infer semantic or certification permission.

### <code>T:Fluent.Pdf.PdfRevocationStatus</code>

Explicit certificate revocation observation, never inferred from offline success.

### <code>T:Fluent.Pdf.PdfScanDewarpGrid</code>

Explicit bounded vertical-curvature dewarping. Control columns have fixed increasing source X; source Y increases down each column, preventing folds.

### <code>T:Fluent.Pdf.PdfScanGeometry</code>

Complete inverse mapping from processed pixel edges through deskew/orientation and optional explicit dewarping.

### <code>T:Fluent.Pdf.PdfScanLayout</code>

Original bounded geometric scan-layout analysis, with table/form semantics kept distinct from prose-column inference.

### <code>T:Fluent.Pdf.PdfScanLayoutOptions</code>

Finite opt-in whitespace column/ruling analysis and explicit semantic-region authority.

### <code>T:Fluent.Pdf.PdfScanLayoutRegion</code>

A region observation retaining source word identities and explicit confidence provenance.

### <code>T:Fluent.Pdf.PdfScanLayoutResult</code>

Prose reading order is separate from unresolved table/form order and immutable raw recognition data.

### <code>T:Fluent.Pdf.PdfScanMappedBoundary</code>

A bounded mapped word boundary in original-raster pixels, with an explicit curve approximation error.

### <code>T:Fluent.Pdf.PdfScanOrientation</code>

Explicit opt-in scan orientation. Automatic compares four explicit OCR observations; it does not discover an orientation model.

### <code>T:Fluent.Pdf.PdfScanOrientationScore</code>

One exact requested orientation observation, distinct from recognition correctness.

### <code>T:Fluent.Pdf.PdfScanPreprocessing</code>

Original bounded opt-in gray scan correction, with immutable original preservation and complete inverse geometry.

### <code>T:Fluent.Pdf.PdfScanPreprocessingOptions</code>

Immutable reproducible scan parameters. Every correction is opt-in; original samples remain available.

### <code>T:Fluent.Pdf.PdfScanPreprocessingResult</code>

Original immutable scan, corrected raster and complete inverse geometry/provenance.

### <code>T:Fluent.Pdf.PdfScanRecognitionResult</code>

One shared-context preprocessing, recognition and layout result, without automatic language/accuracy/semantic qualification.

### <code>T:Fluent.Pdf.PdfScanRegionHint</code>

A finite caller declaration used to keep tables/forms out of prose-column inference.

### <code>T:Fluent.Pdf.PdfScanRegionKind</code>

Explicit or observed scan-region semantics, separate from prose column order.

### <code>T:Fluent.Pdf.PdfScriptRecord</code>

One passive script definition/attachment with trigger/path provenance.

### <code>T:Fluent.Pdf.PdfScripts</code>

Passive script inventory/authoring and an opt-in closed arithmetic calculation subset with no host capabilities.

### <code>T:Fluent.Pdf.PdfSecurity</code>

Explicit bounded full-rewrite security transformations. Credentials and plaintext staging remain in memory; caller streams remain owned by the caller.

### <code>T:Fluent.Pdf.PdfSecurityResult</code>

Detached nonsecret authentication and encryption metadata, with the operation report.

### <code>T:Fluent.Pdf.PdfSepaCreditTransfer</code>

Canonical twelve-line EPC002/UTF8 SCT payment data. This bounded profile requires an explicit BIC, EUR amount and supported country-length IBAN; it does not establish bank existence, account ownership or payment authorization.

### <code>T:Fluent.Pdf.PdfShading</code>

An immutable standard vector shading, retaining explicit color-space and interpolation data.

### <code>T:Fluent.Pdf.PdfShapedGlyph</code>

One positioned glyph in font design units with unchanged logical UTF-16 cluster provenance.

### <code>T:Fluent.Pdf.PdfShapedRun</code>

A visually ordered immutable run with exact font identity and logical provenance.

### <code>T:Fluent.Pdf.PdfShapedText</code>

Unicode analysis and immutable positioned runs. Wrapping and pagination use the layout service.

### <code>T:Fluent.Pdf.PdfShapingRequest</code>

An immutable bounded shaping request with full paragraph context and an explicit selected face.

### <code>T:Fluent.Pdf.PdfSignatureFieldDefinition</code>

A pre-created flat signature field and optional explicit visible Form appearance.

### <code>T:Fluent.Pdf.PdfSignatureFieldLock</code>

Immutable explicit field-lock authority.

### <code>T:Fluent.Pdf.PdfSignatureLockAction</code>

FieldMDP lock selection.

### <code>T:Fluent.Pdf.PdfSignatureObservation</code>

One structural and cryptographic signature observation, separate from certificate trust and permitted-edit validation.

### <code>T:Fluent.Pdf.PdfSignatureOptions</code>

Explicit finite detached CMS signing choices. No wall-clock timestamp is invented.

### <code>T:Fluent.Pdf.PdfSignatureTrustDecision</code>

Detached identity observations separate from mathematical CMS integrity and document edit permissions.

### <code>T:Fluent.Pdf.PdfSignatureTrustOptions</code>

A caller-selected trust instant. No machine clock or ambient trust store is consulted.

### <code>T:Fluent.Pdf.PdfSignatureVerification</code>

One signature's exact signed extent, integrity and separate identity decision.

### <code>T:Fluent.Pdf.PdfSignatures</code>

Exact-byte detached signatures, pre-created fields and conservative certification-aware writing. Certificate trust is separate.

### <code>T:Fluent.Pdf.PdfSignedEditPlan</code>

A conservative preflight decision for the existing writer/signing profiles.

### <code>T:Fluent.Pdf.PdfSignedResult</code>

An immutable complete signed file. Exact signed bytes are preserved by every save.

### <code>T:Fluent.Pdf.PdfSimpleTextShaper</code>

Explicit scalar-only Latin/Greek/Cyrillic shaping. Complex scripts, marks, feature overrides and vertical writing fail.

### <code>T:Fluent.Pdf.PdfSoftMask</code>

An immutable explicit vector soft mask; no rasterization occurs in the SDK.

### <code>T:Fluent.Pdf.PdfSoftMaskKind</code>

The alpha/luminosity method used to derive a soft mask from its transparency group.

### <code>T:Fluent.Pdf.PdfSpotColorPolicy</code>

Explicit spot-color handling; the provider must reject unhonored requests.

### <code>T:Fluent.Pdf.PdfStampContent</code>

An immutable detached stamp source with private resources. No browser, font, codec or file discovery is implicit.

### <code>T:Fluent.Pdf.PdfStampContentKind</code>

The explicitly captured source of an owned page-content stamp.

### <code>T:Fluent.Pdf.PdfStampInfo</code>

A passively inspected stamp invocation with a validated owned-stream marker. Ownership metadata is document provenance, not authentication.

### <code>T:Fluent.Pdf.PdfStampInspection</code>

A detached passive inventory of recognized owned stamp streams.

### <code>T:Fluent.Pdf.PdfStampLayer</code>

The owned stamp's position relative to existing page content.

### <code>T:Fluent.Pdf.PdfStampOptions</code>

Immutable explicit placement in displayed CropBox points, with a top-left origin.

### <code>T:Fluent.Pdf.PdfStamps</code>

Transactional owned page-content stamping. Removal affects recognized invocations, and is not secure redaction.

### <code>T:Fluent.Pdf.PdfStructuralInspection</code>

Passive strict-parser structural observations; syntax failures are never silently repaired.

### <code>T:Fluent.Pdf.PdfStructuralValidation</code>

Strict passive syntax/active-reference/orphan inspection, without recovery or action execution.

### <code>T:Fluent.Pdf.PdfStructureContent</code>

An explicit page/Form MCID or page annotation association. These references do not infer semantics from geometry.

### <code>T:Fluent.Pdf.PdfStructureElement</code>

An immutable author-supplied semantic element with explicitly ordered children/content.

### <code>T:Fluent.Pdf.PdfStructureElementInfo</code>

A detached source element and exact child inventory, preserving identity and reading order.

### <code>T:Fluent.Pdf.PdfStructureItem</code>

A closed immutable logical-structure item; order is explicitly supplied by the author.

### <code>T:Fluent.Pdf.PdfStructurePlan</code>

A complete explicitly ordered replacement/creation plan. It does not assert PDF/UA conformance.

### <code>T:Fluent.Pdf.PdfStructureTableAttributes</code>

Explicit standard table attributes. Header associations use bounded author-provided element IDs.

### <code>T:Fluent.Pdf.PdfSyntax</code>

Original managed syntax inspection. No provider, file repair, decryption or document action is invoked.

### <code>T:Fluent.Pdf.PdfSyntaxDocument</code>

An eagerly parsed immutable object graph and revision history. It owns no caller stream and executes no actions.

### <code>T:Fluent.Pdf.PdfTable</code>

An immutable validated grid, also usable as a flow block.

### <code>T:Fluent.Pdf.PdfTableBorder</code>

One immutable table border in physical points.

### <code>T:Fluent.Pdf.PdfTableCell</code>

Immutable content and spans for one grid-origin cell. Empty content is allowed.

### <code>T:Fluent.Pdf.PdfTableCellFragment</code>

A visible cell rectangle on one table fragment, retaining its original merged-grid identity.

### <code>T:Fluent.Pdf.PdfTableCellStyle</code>

Immutable cell padding, edge borders, background and vertical alignment.

### <code>T:Fluent.Pdf.PdfTableColumn</code>

An immutable fixed or automatically sized table column.

### <code>T:Fluent.Pdf.PdfTableData</code>

Bounded typed row binding with explicit culture and scalar conversion.

### <code>T:Fluent.Pdf.PdfTableDataColumn`1</code>

An explicit typed scalar selector, culture-aware format and cell style for data binding.

### <code>T:Fluent.Pdf.PdfTableDetectionMode</code>

An explicit table inference profile; neither mode asserts semantic structure or calibrated probability.

### <code>T:Fluent.Pdf.PdfTableExtraction</code>

Explicit bounded geometric table inference, separate from semantic structure and production reconstruction qualification.

### <code>T:Fluent.Pdf.PdfTableExtractionOptions</code>

Explicit bounds, tolerances, continuation and cultural-number policy.

### <code>T:Fluent.Pdf.PdfTableExtractionResult</code>

Immutable inferred tables/sequences with original snapshot and operation diagnostics.

### <code>T:Fluent.Pdf.PdfTableFragment</code>

An immutable repeated-header/body slice of a table on one flow page.

### <code>T:Fluent.Pdf.PdfTableHeaderRepeat</code>

An explicit nested-table header repeated in a reserved continuation band.

### <code>T:Fluent.Pdf.PdfTableRow</code>

Immutable grid-origin cells and row constraints.

### <code>T:Fluent.Pdf.PdfTableVerticalAlignment</code>

Vertical placement inside the padded cell rectangle.

### <code>T:Fluent.Pdf.PdfTemplateLayout</code>

Bounded multi-pass contents/index layout with final page decorations.

### <code>T:Fluent.Pdf.PdfText</code>

Explicit offline Unicode itemization, cluster-preserving font fallback and shaping.

### <code>T:Fluent.Pdf.PdfTextDirection</code>

Glyph progression for one fully itemized shaping run.

### <code>T:Fluent.Pdf.PdfTextDrawing</code>

An eagerly embedded positioned text program and its named font resources, ready for page/canvas composition.

### <code>T:Fluent.Pdf.PdfTextExtraction</code>

Managed passive page/Form text extraction with explicit font providers, mappings and geometric inferences.

### <code>T:Fluent.Pdf.PdfTextExtractionOptions</code>

Explicit text/layer selection and finite geometric grouping tolerances.

### <code>T:Fluent.Pdf.PdfTextExtractionResult</code>

Detached page text with raw, semantic and layout views and explicit mapping/geometry provenance.

### <code>T:Fluent.Pdf.PdfTextFeature</code>

One requested OpenType feature and its original UTF-16 range.

### <code>T:Fluent.Pdf.PdfTextMappingConfidence</code>

The provenance of an extracted Unicode value.

### <code>T:Fluent.Pdf.PdfTextMatch</code>

A source-order semantic match retaining exact source characters and advance-cell bounds.

### <code>T:Fluent.Pdf.PdfTextOptions</code>

Immutable paragraph direction, writing direction, language and feature choices.

### <code>T:Fluent.Pdf.PdfTextQuery</code>

An immutable bounded literal/regex query over source-order semantic text, without inferred separators.

### <code>T:Fluent.Pdf.PdfTextReplacementOptions</code>

Explicit single-line replacement policy; surrounding source advances remain unchanged.

### <code>T:Fluent.Pdf.PdfTextReplacementOverflow</code>

Explicit overflow policy for replacement in a source advance-cell frame.

### <code>T:Fluent.Pdf.PdfTextSearch</code>

Bounded passive semantic search and explicit source-position-preserving text replacement.

### <code>T:Fluent.Pdf.PdfTextSearchResult</code>

An immutable successful page search result.

### <code>T:Fluent.Pdf.PdfTextString</code>

Immutable exact encoded text, declared encoding and required PDF version. No implicit Unicode normalization occurs.

### <code>T:Fluent.Pdf.PdfTextStringEncoding</code>

Explicit encoding for human-readable PDF text strings, independent of font character-code strings.

### <code>T:Fluent.Pdf.PdfTransaction</code>

An isolated editable overlay over an immutable object graph; mutation and saving must be serialized by the caller.

### <code>T:Fluent.Pdf.PdfTransitionDimension</code>

Transition split/blind dimension.

### <code>T:Fluent.Pdf.PdfTransitionMotion</code>

Transition motion.

### <code>T:Fluent.Pdf.PdfTransitionStyle</code>

Standard passive page transition styles.

### <code>T:Fluent.Pdf.PdfTransparencyGroup</code>

An explicit Form XObject transparency group.

### <code>T:Fluent.Pdf.PdfType3Glyph</code>

A caller-authored Type 3 glyph with exact logical text and a validated glyph content program.

### <code>T:Fluent.Pdf.PdfUnicode</code>

Original managed Unicode analysis using explicitly pinned normative tables.

### <code>T:Fluent.Pdf.PdfUnicodeAnalysis</code>

An immutable Unicode analysis with logical provenance, grapheme boundaries and bidi visual indices.

### <code>T:Fluent.Pdf.PdfUnicodeMap</code>

An immutable bounded ToUnicode CMap. Its PostScript-style envelope is inspected without execution.

### <code>T:Fluent.Pdf.PdfUnicodeScalar</code>

One Unicode scalar and its UTF-16 provenance/resolved paragraph level.

### <code>T:Fluent.Pdf.PdfUnits</code>

Explicit physical length conversion; no display/device resolution is queried.

### <code>T:Fluent.Pdf.PdfUnknownExtensionPolicy</code>

The explicit policy for preserved source developer extensions.

### <code>T:Fluent.Pdf.PdfUnknownOperatorPolicy</code>

How a content parser handles unknown operator tokens.

### <code>T:Fluent.Pdf.PdfValue</code>

An immutable syntax value. Unknown keys and encoded bytes are retained; no action is executed.

### <code>T:Fluent.Pdf.PdfValueKind</code>

The lexical kind of an immutable PDF value.

### <code>T:Fluent.Pdf.PdfValues</code>

Bounded stream authoring and exact finite numeric conversion for immutable syntax values.

### <code>T:Fluent.Pdf.PdfViewSession</code>

A toolkit-independent viewer over one immutable snapshot, with bounded outstanding requests and byte-capped LRU samples. Dispose cancels pending requests without blocking provider callbacks.

### <code>T:Fluent.Pdf.PdfWordConversion</code>

Explicit provider-backed Word interchange, with shared-budget PDF import and atomic file export.

### <code>T:Fluent.Pdf.PdfWordConversionOptions</code>

Immutable direction-independent limits and PDF reconstruction intent. Provider asset/font choices remain explicit provider configuration.

### <code>T:Fluent.Pdf.PdfWordFormat</code>

An explicitly selected Word interchange format; availability is provider-specific.

### <code>T:Fluent.Pdf.PdfWordReconstruction</code>

Explicit PDF reconstruction intent, independent of whether original authoring structure can be recovered.

### <code>T:Fluent.Pdf.PdfWriteMode</code>

The physical revision strategy for a save.

### <code>T:Fluent.Pdf.PdfWriteOptions</code>

Immutable save choices. Signature validation/signing are separate provider capabilities.

### <code>T:Fluent.Pdf.PdfXfa</code>

Original passive XFA packet handling, bounded scalar filling and explicit static text-field conversion.

### <code>T:Fluent.Pdf.PdfXfaPacket</code>

One immutable original XFA XML packet and exact source stream identity.

### <code>T:Fluent.Pdf.PdfXfaSet</code>

Passive packet and scalar datasets inventory. Repeated/ambiguous data paths reject rather than guessing SOM indices.

