# Fluent.Pdf.Intelligence source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Intelligence.IPdfDocumentIntelligenceProvider
Fluent.Pdf.Intelligence.IPdfDocumentIntelligenceProvider.Model.get -> Fluent.Pdf.Intelligence.PdfIntelligenceModel!
Fluent.Pdf.Intelligence.IPdfDocumentIntelligenceProvider.Prepare(Fluent.Pdf.Intelligence.PdfIntelligenceJob! job, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Intelligence.PdfIntelligenceCall!
Fluent.Pdf.Intelligence.IPdfDocumentIntelligenceProvider.Supports(Fluent.Pdf.Intelligence.PdfIntelligenceTask task, bool requiresImages) -> bool
Fluent.Pdf.Intelligence.IPdfEmbeddingProvider
Fluent.Pdf.Intelligence.IPdfEmbeddingProvider.Model.get -> Fluent.Pdf.Intelligence.PdfEmbeddingModel!
Fluent.Pdf.Intelligence.IPdfEmbeddingProvider.Prepare(Fluent.Pdf.Intelligence.PdfEmbeddingRequest! request, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Intelligence.PdfEmbeddingCall!
Fluent.Pdf.Intelligence.IPdfIntelligenceTransport
Fluent.Pdf.Intelligence.IPdfIntelligenceTransport.OpenResponse(System.Uri! endpoint, System.IO.Stream! request, long requestLength, Fluent.Pdf.PdfOperationContext! context) -> System.IO.Stream!
Fluent.Pdf.Intelligence.IPdfVectorStore
Fluent.Pdf.Intelligence.IPdfVectorStore.Authorize(Fluent.Pdf.Intelligence.PdfRetrievalScope! scope, string! principal, bool write, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Intelligence.PdfVectorIndexInfo!
Fluent.Pdf.Intelligence.IPdfVectorStore.Remove(Fluent.Pdf.Intelligence.PdfRetrievalScope! scope, string! principal, long expectedVersion, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Intelligence.IPdfVectorStore.Replace(Fluent.Pdf.Intelligence.PdfVectorIndex! index, string! principal, long expectedVersion, Fluent.Pdf.PdfOperationContext! context) -> long
Fluent.Pdf.Intelligence.IPdfVectorStore.Search(Fluent.Pdf.Intelligence.PdfRetrievalScope! scope, string! principal, long expectedVersion, Fluent.Pdf.Intelligence.PdfEmbeddingModel! model, Fluent.Pdf.Intelligence.PdfEmbeddingVector! query, int maximumResults, Fluent.Pdf.PdfOperationContext! context) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Intelligence.PdfVectorMatch!>!
Fluent.Pdf.Intelligence.PdfChatDocumentModel
Fluent.Pdf.Intelligence.PdfChatDocumentModel.Model.get -> Fluent.Pdf.Intelligence.PdfIntelligenceModel!
Fluent.Pdf.Intelligence.PdfChatDocumentModel.PdfChatDocumentModel(Fluent.Pdf.Intelligence.PdfIntelligenceModel! model, Fluent.Pdf.Intelligence.IPdfIntelligenceTransport! transport, bool supportsImages = false, int maximumTokensPerUtf8Byte = 1) -> void
Fluent.Pdf.Intelligence.PdfChatDocumentModel.Prepare(Fluent.Pdf.Intelligence.PdfIntelligenceJob! job, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Intelligence.PdfIntelligenceCall!
Fluent.Pdf.Intelligence.PdfChatDocumentModel.Supports(Fluent.Pdf.Intelligence.PdfIntelligenceTask task, bool requiresImages) -> bool
Fluent.Pdf.Intelligence.PdfChatDocumentModel.SupportsImages.get -> bool
Fluent.Pdf.Intelligence.PdfDocumentIntelligence
Fluent.Pdf.Intelligence.PdfEmbeddingCall
Fluent.Pdf.Intelligence.PdfEmbeddingCall.ConfiguredCostCeiling.get -> decimal
Fluent.Pdf.Intelligence.PdfEmbeddingCall.InputTokenCeiling.get -> int
Fluent.Pdf.Intelligence.PdfEmbeddingCall.Model.get -> Fluent.Pdf.Intelligence.PdfEmbeddingModel!
Fluent.Pdf.Intelligence.PdfEmbeddingCall.PdfEmbeddingCall(Fluent.Pdf.Intelligence.PdfEmbeddingModel! model, string! requestSha256, int inputTokenCeiling, decimal configuredCostCeiling) -> void
Fluent.Pdf.Intelligence.PdfEmbeddingCall.RequestSha256.get -> string!
Fluent.Pdf.Intelligence.PdfEmbeddingModel
Fluent.Pdf.Intelligence.PdfEmbeddingModel.Dimensions.get -> int
Fluent.Pdf.Intelligence.PdfEmbeddingModel.Model.get -> Fluent.Pdf.Intelligence.PdfIntelligenceModel!
Fluent.Pdf.Intelligence.PdfEmbeddingModel.PdfEmbeddingModel(Fluent.Pdf.Intelligence.PdfIntelligenceModel! model, int dimensions) -> void
Fluent.Pdf.Intelligence.PdfEmbeddingRequest
Fluent.Pdf.Intelligence.PdfEmbeddingRequest.RequestSha256.get -> string!
Fluent.Pdf.Intelligence.PdfEmbeddingRequest.Texts.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.Intelligence.PdfEmbeddingResponse
Fluent.Pdf.Intelligence.PdfEmbeddingResponse.ExactModelVersion.get -> string!
Fluent.Pdf.Intelligence.PdfEmbeddingResponse.InputTokens.get -> int
Fluent.Pdf.Intelligence.PdfEmbeddingResponse.PdfEmbeddingResponse(string! requestSha256, string! exactModelVersion, System.Collections.Generic.IEnumerable<Fluent.Pdf.Intelligence.PdfEmbeddingVector!>! vectors, int inputTokens, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Intelligence.PdfEmbeddingResponse.RequestSha256.get -> string!
Fluent.Pdf.Intelligence.PdfEmbeddingResponse.Vectors.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Intelligence.PdfEmbeddingVector!>!
Fluent.Pdf.Intelligence.PdfEmbeddingVector
Fluent.Pdf.Intelligence.PdfEmbeddingVector.PdfEmbeddingVector(System.Collections.Generic.IEnumerable<double>! values, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Intelligence.PdfEmbeddingVector.Values.get -> System.Collections.Generic.IReadOnlyList<double>!
Fluent.Pdf.Intelligence.PdfIntelligenceCall
Fluent.Pdf.Intelligence.PdfIntelligenceCall.InputTokenCeiling.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceCall.MaximumOutputTokens.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceCall.Model.get -> Fluent.Pdf.Intelligence.PdfIntelligenceModel!
Fluent.Pdf.Intelligence.PdfIntelligenceCall.PdfIntelligenceCall(Fluent.Pdf.Intelligence.PdfIntelligenceModel! model, string! requestSha256, int inputTokenCeiling, int maximumOutputTokens, decimal reservedConfiguredCost) -> void
Fluent.Pdf.Intelligence.PdfIntelligenceCall.RequestSha256.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceCall.ReservedConfiguredCost.get -> decimal
Fluent.Pdf.Intelligence.PdfIntelligenceCitation
Fluent.Pdf.Intelligence.PdfIntelligenceCitation.OutputPath.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceCitation.PdfIntelligenceCitation(string! sourceId, string! outputPath, string! quote) -> void
Fluent.Pdf.Intelligence.PdfIntelligenceCitation.Quote.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceCitation.SourceId.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceEvidence
Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.CaptureRegion.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.OutputPath.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.PageIndex.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.Quote.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.SourceId.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.SourceSha256.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.TextOffset.get -> int?
Fluent.Pdf.Intelligence.PdfIntelligenceField
Fluent.Pdf.Intelligence.PdfIntelligenceField.Name.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceField.PdfIntelligenceField(string! name, Fluent.Pdf.Intelligence.PdfIntelligenceSchema! schema, bool required = true) -> void
Fluent.Pdf.Intelligence.PdfIntelligenceField.Required.get -> bool
Fluent.Pdf.Intelligence.PdfIntelligenceField.Schema.get -> Fluent.Pdf.Intelligence.PdfIntelligenceSchema!
Fluent.Pdf.Intelligence.PdfIntelligenceJob
Fluent.Pdf.Intelligence.PdfIntelligenceJob.Instruction.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceJob.Options.get -> Fluent.Pdf.Intelligence.PdfIntelligenceOptions!
Fluent.Pdf.Intelligence.PdfIntelligenceJob.RequestSha256.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceJob.Schema.get -> Fluent.Pdf.Intelligence.PdfIntelligenceSchema!
Fluent.Pdf.Intelligence.PdfIntelligenceJob.Sources.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Intelligence.PdfIntelligenceSource!>!
Fluent.Pdf.Intelligence.PdfIntelligenceJob.TargetLanguage.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceJob.Task.get -> Fluent.Pdf.Intelligence.PdfIntelligenceTask
Fluent.Pdf.Intelligence.PdfIntelligenceLocation
Fluent.Pdf.Intelligence.PdfIntelligenceLocation.Local = 0 -> Fluent.Pdf.Intelligence.PdfIntelligenceLocation
Fluent.Pdf.Intelligence.PdfIntelligenceLocation.Remote = 1 -> Fluent.Pdf.Intelligence.PdfIntelligenceLocation
Fluent.Pdf.Intelligence.PdfIntelligenceModel
Fluent.Pdf.Intelligence.PdfIntelligenceModel.Currency.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceModel.Endpoint.get -> System.Uri?
Fluent.Pdf.Intelligence.PdfIntelligenceModel.ExactModelVersion.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceModel.InputPricePerMillion.get -> decimal
Fluent.Pdf.Intelligence.PdfIntelligenceModel.Location.get -> Fluent.Pdf.Intelligence.PdfIntelligenceLocation
Fluent.Pdf.Intelligence.PdfIntelligenceModel.MaximumContextTokens.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceModel.OutputPricePerMillion.get -> decimal
Fluent.Pdf.Intelligence.PdfIntelligenceModel.PdfIntelligenceModel(string! providerId, string! exactModelVersion, Fluent.Pdf.Intelligence.PdfIntelligenceLocation location = Fluent.Pdf.Intelligence.PdfIntelligenceLocation.Local, System.Uri? endpoint = null, int maximumContextTokens = 128000, string! currency = "USD", decimal inputPricePerMillion = 0, decimal outputPricePerMillion = 0, decimal requestCharge = 0) -> void
Fluent.Pdf.Intelligence.PdfIntelligenceModel.ProviderId.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceModel.RequestCharge.get -> decimal
Fluent.Pdf.Intelligence.PdfIntelligenceOptions
Fluent.Pdf.Intelligence.PdfIntelligenceOptions.AllowImages.get -> bool
Fluent.Pdf.Intelligence.PdfIntelligenceOptions.AllowedRemoteOrigin.get -> System.Uri?
Fluent.Pdf.Intelligence.PdfIntelligenceOptions.CostCurrency.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceOptions.MaximumCitations.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceOptions.MaximumConfiguredCost.get -> decimal
Fluent.Pdf.Intelligence.PdfIntelligenceOptions.MaximumInputTokens.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceOptions.MaximumOutputCharacters.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceOptions.MaximumOutputTokens.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceOptions.MaximumSourceCharacters.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceOptions.PdfIntelligenceOptions(System.Uri? allowedRemoteOrigin = null, bool allowImages = false, int maximumSourceCharacters = 65536, int maximumInputTokens = 16384, int maximumOutputTokens = 2048, decimal maximumConfiguredCost = 0, string! costCurrency = "USD", int maximumOutputCharacters = 16384, int maximumCitations = 128) -> void
Fluent.Pdf.Intelligence.PdfIntelligenceResponse
Fluent.Pdf.Intelligence.PdfIntelligenceResponse.Citations.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Intelligence.PdfIntelligenceCitation!>!
Fluent.Pdf.Intelligence.PdfIntelligenceResponse.DataJson.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceResponse.PdfIntelligenceResponse(string! requestSha256, string! dataJson, System.Collections.Generic.IEnumerable<Fluent.Pdf.Intelligence.PdfIntelligenceCitation!>! citations, Fluent.Pdf.Intelligence.PdfIntelligenceUsage! usage) -> void
Fluent.Pdf.Intelligence.PdfIntelligenceResponse.RequestSha256.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceResponse.Usage.get -> Fluent.Pdf.Intelligence.PdfIntelligenceUsage!
Fluent.Pdf.Intelligence.PdfIntelligenceResult
Fluent.Pdf.Intelligence.PdfIntelligenceResult.DataJson.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceResult.Evidence.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Intelligence.PdfIntelligenceEvidence!>!
Fluent.Pdf.Intelligence.PdfIntelligenceResult.Model.get -> Fluent.Pdf.Intelligence.PdfIntelligenceModel!
Fluent.Pdf.Intelligence.PdfIntelligenceResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.Intelligence.PdfIntelligenceResult.RequestSha256.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceResult.Usage.get -> Fluent.Pdf.Intelligence.PdfIntelligenceUsage!
Fluent.Pdf.Intelligence.PdfIntelligenceSchema
Fluent.Pdf.Intelligence.PdfIntelligenceSchema.AllowedStrings.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Fields.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Intelligence.PdfIntelligenceField!>!
Fluent.Pdf.Intelligence.PdfIntelligenceSchema.ItemSchema.get -> Fluent.Pdf.Intelligence.PdfIntelligenceSchema?
Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Kind.get -> Fluent.Pdf.Intelligence.PdfIntelligenceValueKind
Fluent.Pdf.Intelligence.PdfIntelligenceSchema.MaximumLength.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceSchema.MaximumNumber.get -> decimal?
Fluent.Pdf.Intelligence.PdfIntelligenceSchema.MinimumLength.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceSchema.MinimumNumber.get -> decimal?
Fluent.Pdf.Intelligence.PdfIntelligenceSelection
Fluent.Pdf.Intelligence.PdfIntelligenceSelection.PageIndex.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceSelection.PdfIntelligenceSelection(int pageIndex, Fluent.Pdf.PdfRect? region = null) -> void
Fluent.Pdf.Intelligence.PdfIntelligenceSelection.Region.get -> Fluent.Pdf.PdfRect?
Fluent.Pdf.Intelligence.PdfIntelligenceSource
Fluent.Pdf.Intelligence.PdfIntelligenceSource.Id.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceSource.IsImage.get -> bool
Fluent.Pdf.Intelligence.PdfIntelligenceSource.OpenImage(Fluent.Pdf.PdfOperationContext! context) -> System.IO.Stream!
Fluent.Pdf.Intelligence.PdfIntelligenceSource.PageIndex.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceSource.Region.get -> Fluent.Pdf.PdfRect
Fluent.Pdf.Intelligence.PdfIntelligenceSource.Sha256.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceSource.Text.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceTask
Fluent.Pdf.Intelligence.PdfIntelligenceTask.DescribeImage = 3 -> Fluent.Pdf.Intelligence.PdfIntelligenceTask
Fluent.Pdf.Intelligence.PdfIntelligenceTask.Ocr = 4 -> Fluent.Pdf.Intelligence.PdfIntelligenceTask
Fluent.Pdf.Intelligence.PdfIntelligenceTask.QuestionAnswer = 1 -> Fluent.Pdf.Intelligence.PdfIntelligenceTask
Fluent.Pdf.Intelligence.PdfIntelligenceTask.StructuredExtraction = 5 -> Fluent.Pdf.Intelligence.PdfIntelligenceTask
Fluent.Pdf.Intelligence.PdfIntelligenceTask.Summarize = 0 -> Fluent.Pdf.Intelligence.PdfIntelligenceTask
Fluent.Pdf.Intelligence.PdfIntelligenceTask.Translate = 2 -> Fluent.Pdf.Intelligence.PdfIntelligenceTask
Fluent.Pdf.Intelligence.PdfIntelligenceUsage
Fluent.Pdf.Intelligence.PdfIntelligenceUsage.ConfiguredCost.get -> decimal
Fluent.Pdf.Intelligence.PdfIntelligenceUsage.Currency.get -> string!
Fluent.Pdf.Intelligence.PdfIntelligenceUsage.InputTokens.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceUsage.OutputTokens.get -> int
Fluent.Pdf.Intelligence.PdfIntelligenceUsage.PdfIntelligenceUsage(int inputTokens, int outputTokens, decimal configuredCost, string! currency) -> void
Fluent.Pdf.Intelligence.PdfIntelligenceValueKind
Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Array = 1 -> Fluent.Pdf.Intelligence.PdfIntelligenceValueKind
Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Boolean = 5 -> Fluent.Pdf.Intelligence.PdfIntelligenceValueKind
Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Integer = 4 -> Fluent.Pdf.Intelligence.PdfIntelligenceValueKind
Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Null = 6 -> Fluent.Pdf.Intelligence.PdfIntelligenceValueKind
Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Nullable = 7 -> Fluent.Pdf.Intelligence.PdfIntelligenceValueKind
Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Number = 3 -> Fluent.Pdf.Intelligence.PdfIntelligenceValueKind
Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Object = 0 -> Fluent.Pdf.Intelligence.PdfIntelligenceValueKind
Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.String = 2 -> Fluent.Pdf.Intelligence.PdfIntelligenceValueKind
Fluent.Pdf.Intelligence.PdfJsonEmbeddingProvider
Fluent.Pdf.Intelligence.PdfJsonEmbeddingProvider.Model.get -> Fluent.Pdf.Intelligence.PdfEmbeddingModel!
Fluent.Pdf.Intelligence.PdfJsonEmbeddingProvider.PdfJsonEmbeddingProvider(Fluent.Pdf.Intelligence.PdfEmbeddingModel! model, Fluent.Pdf.Intelligence.IPdfIntelligenceTransport! transport, int maximumTokensPerUtf8Byte = 1) -> void
Fluent.Pdf.Intelligence.PdfJsonEmbeddingProvider.Prepare(Fluent.Pdf.Intelligence.PdfEmbeddingRequest! request, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Intelligence.PdfEmbeddingCall!
Fluent.Pdf.Intelligence.PdfMemoryVectorStore
Fluent.Pdf.Intelligence.PdfMemoryVectorStore.Authorize(Fluent.Pdf.Intelligence.PdfRetrievalScope! scope, string! principal, bool write, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Intelligence.PdfVectorIndexInfo!
Fluent.Pdf.Intelligence.PdfMemoryVectorStore.PdfMemoryVectorStore(string! administrator, int maximumDocuments = 128, int maximumRecords = 65536, long maximumResidentBytes = 67108864) -> void
Fluent.Pdf.Intelligence.PdfMemoryVectorStore.Remove(Fluent.Pdf.Intelligence.PdfRetrievalScope! scope, string! principal, long expectedVersion, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Intelligence.PdfMemoryVectorStore.Replace(Fluent.Pdf.Intelligence.PdfVectorIndex! index, string! principal, long expectedVersion, Fluent.Pdf.PdfOperationContext! context) -> long
Fluent.Pdf.Intelligence.PdfMemoryVectorStore.Search(Fluent.Pdf.Intelligence.PdfRetrievalScope! scope, string! principal, long expectedVersion, Fluent.Pdf.Intelligence.PdfEmbeddingModel! model, Fluent.Pdf.Intelligence.PdfEmbeddingVector! query, int maximumResults, Fluent.Pdf.PdfOperationContext! context) -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Intelligence.PdfVectorMatch!>!
Fluent.Pdf.Intelligence.PdfRetrievalPermissions
Fluent.Pdf.Intelligence.PdfRetrievalPermissions.PdfRetrievalPermissions(string! policyVersion, string! writer, System.Collections.Generic.IEnumerable<string!>! readers) -> void
Fluent.Pdf.Intelligence.PdfRetrievalPermissions.PolicyVersion.get -> string!
Fluent.Pdf.Intelligence.PdfRetrievalPermissions.Readers.get -> System.Collections.Generic.IReadOnlyList<string!>!
Fluent.Pdf.Intelligence.PdfRetrievalPermissions.Writer.get -> string!
Fluent.Pdf.Intelligence.PdfRetrievalScope
Fluent.Pdf.Intelligence.PdfRetrievalScope.DocumentId.get -> string!
Fluent.Pdf.Intelligence.PdfRetrievalScope.DocumentSha256.get -> string!
Fluent.Pdf.Intelligence.PdfRetrievalScope.PdfRetrievalScope(string! tenantId, string! documentId, string! documentSha256) -> void
Fluent.Pdf.Intelligence.PdfRetrievalScope.TenantId.get -> string!
Fluent.Pdf.Intelligence.PdfSemanticChunk
Fluent.Pdf.Intelligence.PdfSemanticChunk.Characters.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.PdfExtractedCharacter!>!
Fluent.Pdf.Intelligence.PdfSemanticChunk.Index.get -> int
Fluent.Pdf.Intelligence.PdfSemanticChunk.PageIndex.get -> int
Fluent.Pdf.Intelligence.PdfSemanticChunk.Sha256.get -> string!
Fluent.Pdf.Intelligence.PdfSemanticChunk.TableIndex.get -> int?
Fluent.Pdf.Intelligence.PdfSemanticChunk.Text.get -> string!
Fluent.Pdf.Intelligence.PdfSemanticChunk.TextOffset.get -> int
Fluent.Pdf.Intelligence.PdfSemanticChunk.Utf8Bytes.get -> int
Fluent.Pdf.Intelligence.PdfSemanticChunkOptions
Fluent.Pdf.Intelligence.PdfSemanticChunkOptions.IncludeInvisibleText.get -> bool
Fluent.Pdf.Intelligence.PdfSemanticChunkOptions.MaximumCharacters.get -> int
Fluent.Pdf.Intelligence.PdfSemanticChunkOptions.MaximumChunks.get -> int
Fluent.Pdf.Intelligence.PdfSemanticChunkOptions.MaximumUtf8Bytes.get -> int
Fluent.Pdf.Intelligence.PdfSemanticChunkOptions.PdfSemanticChunkOptions(int maximumUtf8Bytes = 1024, int maximumChunks = 4096, int maximumCharacters = 1000000, bool includeInvisibleText = false, Fluent.Pdf.PdfTableExtractionOptions? tables = null) -> void
Fluent.Pdf.Intelligence.PdfSemanticChunkOptions.Tables.get -> Fluent.Pdf.PdfTableExtractionOptions?
Fluent.Pdf.Intelligence.PdfSemanticChunkResult
Fluent.Pdf.Intelligence.PdfSemanticChunkResult.CaptureSha256.get -> string!
Fluent.Pdf.Intelligence.PdfSemanticChunkResult.Chunks.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Intelligence.PdfSemanticChunk!>!
Fluent.Pdf.Intelligence.PdfSemanticChunkResult.DocumentSha256.get -> string!
Fluent.Pdf.Intelligence.PdfSemanticChunkResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.Intelligence.PdfSemanticChunkResult.PageIndices.get -> System.Collections.Generic.IReadOnlyList<int>!
Fluent.Pdf.Intelligence.PdfSemanticChunks
Fluent.Pdf.Intelligence.PdfVectorIndex
Fluent.Pdf.Intelligence.PdfVectorIndex.Capture.get -> Fluent.Pdf.Intelligence.PdfSemanticChunkResult!
Fluent.Pdf.Intelligence.PdfVectorIndex.Embeddings.get -> Fluent.Pdf.Intelligence.PdfEmbeddingResponse!
Fluent.Pdf.Intelligence.PdfVectorIndex.Model.get -> Fluent.Pdf.Intelligence.PdfEmbeddingModel!
Fluent.Pdf.Intelligence.PdfVectorIndex.Permissions.get -> Fluent.Pdf.Intelligence.PdfRetrievalPermissions!
Fluent.Pdf.Intelligence.PdfVectorIndex.Scope.get -> Fluent.Pdf.Intelligence.PdfRetrievalScope!
Fluent.Pdf.Intelligence.PdfVectorIndexInfo
Fluent.Pdf.Intelligence.PdfVectorIndexInfo.Model.get -> Fluent.Pdf.Intelligence.PdfEmbeddingModel?
Fluent.Pdf.Intelligence.PdfVectorIndexInfo.PdfVectorIndexInfo(long version, Fluent.Pdf.Intelligence.PdfEmbeddingModel? model, Fluent.Pdf.Intelligence.PdfRetrievalPermissions? permissions) -> void
Fluent.Pdf.Intelligence.PdfVectorIndexInfo.Permissions.get -> Fluent.Pdf.Intelligence.PdfRetrievalPermissions?
Fluent.Pdf.Intelligence.PdfVectorIndexInfo.Version.get -> long
Fluent.Pdf.Intelligence.PdfVectorIndexResult
Fluent.Pdf.Intelligence.PdfVectorIndexResult.Index.get -> Fluent.Pdf.Intelligence.PdfVectorIndex!
Fluent.Pdf.Intelligence.PdfVectorIndexResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
Fluent.Pdf.Intelligence.PdfVectorIndexResult.Version.get -> long
Fluent.Pdf.Intelligence.PdfVectorMatch
Fluent.Pdf.Intelligence.PdfVectorMatch.Chunk.get -> Fluent.Pdf.Intelligence.PdfSemanticChunk!
Fluent.Pdf.Intelligence.PdfVectorMatch.Index.get -> Fluent.Pdf.Intelligence.PdfVectorIndex!
Fluent.Pdf.Intelligence.PdfVectorMatch.PdfVectorMatch(Fluent.Pdf.Intelligence.PdfVectorIndex! index, int chunkIndex, double score) -> void
Fluent.Pdf.Intelligence.PdfVectorMatch.Score.get -> double
Fluent.Pdf.Intelligence.PdfVectorRetrieval
Fluent.Pdf.Intelligence.PdfVectorSearchResult
Fluent.Pdf.Intelligence.PdfVectorSearchResult.Matches.get -> System.Collections.Generic.IReadOnlyList<Fluent.Pdf.Intelligence.PdfVectorMatch!>!
Fluent.Pdf.Intelligence.PdfVectorSearchResult.OperationReport.get -> Fluent.Pdf.PdfOperationReport!
abstract Fluent.Pdf.Intelligence.PdfEmbeddingCall.Execute(Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Intelligence.PdfEmbeddingResponse!
abstract Fluent.Pdf.Intelligence.PdfIntelligenceCall.Execute(Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.Intelligence.PdfIntelligenceResponse!
static Fluent.Pdf.Intelligence.PdfDocumentIntelligence.Analyze(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<Fluent.Pdf.Intelligence.PdfIntelligenceSelection!>! selections, Fluent.Pdf.Intelligence.PdfIntelligenceTask task, Fluent.Pdf.Intelligence.IPdfDocumentIntelligenceProvider? provider = null, string! instruction = "", string! targetLanguage = "", Fluent.Pdf.Intelligence.PdfIntelligenceSchema? schema = null, Fluent.Pdf.Intelligence.PdfIntelligenceOptions? options = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfRenderOptions? render = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Intelligence.PdfIntelligenceResult!
static Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Array(Fluent.Pdf.Intelligence.PdfIntelligenceSchema! items, int minimumItems = 0, int maximumItems = 100) -> Fluent.Pdf.Intelligence.PdfIntelligenceSchema!
static Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Boolean() -> Fluent.Pdf.Intelligence.PdfIntelligenceSchema!
static Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Integer(long minimum = -9223372036854775808, long maximum = 9223372036854775807) -> Fluent.Pdf.Intelligence.PdfIntelligenceSchema!
static Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Null() -> Fluent.Pdf.Intelligence.PdfIntelligenceSchema!
static Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Nullable(Fluent.Pdf.Intelligence.PdfIntelligenceSchema! value) -> Fluent.Pdf.Intelligence.PdfIntelligenceSchema!
static Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Number(decimal? minimum = null, decimal? maximum = null) -> Fluent.Pdf.Intelligence.PdfIntelligenceSchema!
static Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Object(System.Collections.Generic.IEnumerable<Fluent.Pdf.Intelligence.PdfIntelligenceField!>! fields) -> Fluent.Pdf.Intelligence.PdfIntelligenceSchema!
static Fluent.Pdf.Intelligence.PdfIntelligenceSchema.String(int minimumLength = 0, int maximumLength = 16384, System.Collections.Generic.IEnumerable<string!>? allowedValues = null) -> Fluent.Pdf.Intelligence.PdfIntelligenceSchema!
static Fluent.Pdf.Intelligence.PdfSemanticChunks.Capture(Fluent.Pdf.PdfDocumentSnapshot! source, System.Collections.Generic.IEnumerable<int>! pages, Fluent.Pdf.Intelligence.PdfSemanticChunkOptions? options = null, Fluent.Pdf.PdfProviderRegistry? providers = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Intelligence.PdfSemanticChunkResult!
static Fluent.Pdf.Intelligence.PdfVectorRetrieval.Index(Fluent.Pdf.Intelligence.PdfSemanticChunkResult! capture, Fluent.Pdf.Intelligence.PdfRetrievalScope! scope, Fluent.Pdf.Intelligence.PdfRetrievalPermissions! permissions, string! principal, Fluent.Pdf.Intelligence.IPdfEmbeddingProvider! provider, Fluent.Pdf.Intelligence.IPdfVectorStore! store, Fluent.Pdf.Intelligence.PdfIntelligenceOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Intelligence.PdfVectorIndexResult!
static Fluent.Pdf.Intelligence.PdfVectorRetrieval.Remove(Fluent.Pdf.Intelligence.PdfRetrievalScope! scope, string! principal, Fluent.Pdf.Intelligence.IPdfVectorStore! store, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.PdfOperationReport!
static Fluent.Pdf.Intelligence.PdfVectorRetrieval.Search(Fluent.Pdf.Intelligence.PdfRetrievalScope! scope, string! principal, string! query, Fluent.Pdf.Intelligence.IPdfEmbeddingProvider! provider, Fluent.Pdf.Intelligence.IPdfVectorStore! store, int maximumResults = 10, Fluent.Pdf.Intelligence.PdfIntelligenceOptions? options = null, Fluent.Pdf.PdfOperationPolicy? policy = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> Fluent.Pdf.Intelligence.PdfVectorSearchResult!
```

## Member documentation

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceLocation.Local</code>

Caller-owned in-process model or explicit literal-loopback service.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceLocation.Remote</code>

Explicit remote HTTPS service requiring per-operation origin consent.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceTask.DescribeImage</code>

Describe explicitly selected rendered page regions.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceTask.Ocr</code>

Recognize explicitly selected rendered page regions.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceTask.QuestionAnswer</code>

Answer an explicit question from selected excerpts.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceTask.StructuredExtraction</code>

Extract data matching an explicit closed schema.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceTask.Summarize</code>

Summarize selected excerpts.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceTask.Translate</code>

Translate selected excerpts to an explicit language tag.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Array</code>

A bounded homogeneous array.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Boolean</code>

A boolean.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Integer</code>

A signed64-bit integer.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Null</code>

A null value.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Nullable</code>

A value matching one explicit nested schema, or null.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Number</code>

An exactly representable decimal.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.Object</code>

A JSON object with explicitly declared members.

### <code>F:Fluent.Pdf.Intelligence.PdfIntelligenceValueKind.String</code>

A Unicode scalar string.

### <code>M:Fluent.Pdf.Intelligence.IPdfDocumentIntelligenceProvider.Prepare(Fluent.Pdf.Intelligence.PdfIntelligenceJob,Fluent.Pdf.PdfOperationContext)</code>

Prepares one request and conservative configured token/cost bounds offline using only supplied sources and the mandatory context.

### <code>M:Fluent.Pdf.Intelligence.IPdfDocumentIntelligenceProvider.Supports(Fluent.Pdf.Intelligence.PdfIntelligenceTask,System.Boolean)</code>

Checks exact task/image prerequisites offline.

### <code>M:Fluent.Pdf.Intelligence.IPdfEmbeddingProvider.Prepare(Fluent.Pdf.Intelligence.PdfEmbeddingRequest,Fluent.Pdf.PdfOperationContext)</code>

Prepares one request offline with conservative complete-input token/cost reservation.

### <code>M:Fluent.Pdf.Intelligence.IPdfIntelligenceTransport.OpenResponse(System.Uri,System.IO.Stream,System.Int64,Fluent.Pdf.PdfOperationContext)</code>

Completes upload to the exact endpoint and returns an SDK-owned response body. Request is borrowed read-only and expires at return; response reads must honor the context's cancellation/deadline. Enforce configured origin/IP/TLS/authentication/redirect policy without logging bodies or credentials.

### <code>M:Fluent.Pdf.Intelligence.IPdfVectorStore.Authorize(Fluent.Pdf.Intelligence.PdfRetrievalScope,System.String,System.Boolean,Fluent.Pdf.PdfOperationContext)</code>

Authorizes read/write before any embedding dispatch. Only an explicitly configured administrator may authorize absent-index creation.

### <code>M:Fluent.Pdf.Intelligence.IPdfVectorStore.Remove(Fluent.Pdf.Intelligence.PdfRetrievalScope,System.String,System.Int64,Fluent.Pdf.PdfOperationContext)</code>

Reauthorizes and removes exactly one generation atomically. A removed scope cannot be searched or recreated by ordinary writers/readers.

### <code>M:Fluent.Pdf.Intelligence.IPdfVectorStore.Replace(Fluent.Pdf.Intelligence.PdfVectorIndex,System.String,System.Int64,Fluent.Pdf.PdfOperationContext)</code>

Reauthorizes and atomically replaces one exact scope if its generation matches. Existing permission mutation must reject. No partial batch visibility.

### <code>M:Fluent.Pdf.Intelligence.IPdfVectorStore.Search(Fluent.Pdf.Intelligence.PdfRetrievalScope,System.String,System.Int64,Fluent.Pdf.Intelligence.PdfEmbeddingModel,Fluent.Pdf.Intelligence.PdfEmbeddingVector,System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Reauthorizes, verifies generation/model/dimensions, and returns bounded cosine-ranked matches from this exact scope only.

### <code>M:Fluent.Pdf.Intelligence.PdfChatDocumentModel.#ctor(Fluent.Pdf.Intelligence.PdfIntelligenceModel,Fluent.Pdf.Intelligence.IPdfIntelligenceTransport,System.Boolean,System.Int32)</code>

Captures an explicit fixed-endpoint local/remote model and caller transport. Image support defaults off. The byte-token ceiling1–16 must be qualified for the exact backend/tokenizer and reserves the entire wire request conservatively.

### <code>M:Fluent.Pdf.Intelligence.PdfChatDocumentModel.Prepare(Fluent.Pdf.Intelligence.PdfIntelligenceJob,Fluent.Pdf.PdfOperationContext)</code>

Builds one deterministic request offline, separates trusted instructions from quoted source data, disables tools/streaming/storage, and reserves configured token/cost ceilings before execution.

### <code>M:Fluent.Pdf.Intelligence.PdfChatDocumentModel.Supports(Fluent.Pdf.Intelligence.PdfIntelligenceTask,System.Boolean)</code>

Reports the six named tasks, requiring explicit backend image support for image inputs.

### <code>M:Fluent.Pdf.Intelligence.PdfDocumentIntelligence.Analyze(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{Fluent.Pdf.Intelligence.PdfIntelligenceSelection},Fluent.Pdf.Intelligence.PdfIntelligenceTask,Fluent.Pdf.Intelligence.IPdfDocumentIntelligenceProvider,System.String,System.String,Fluent.Pdf.Intelligence.PdfIntelligenceSchema,Fluent.Pdf.Intelligence.PdfIntelligenceOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfRenderOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Analyzes explicitly selected mapped text or authorized image regions. Text excludes hidden layers/nonpainting modes but does not establish visible ink or remove concealed content. No provider or remote origin is selected by default.

### <code>M:Fluent.Pdf.Intelligence.PdfEmbeddingCall.Execute(Fluent.Pdf.PdfOperationContext)</code>

Executes once under the supplied active shared context. No implicit follow-up, retry or authority expansion is permitted.

### <code>M:Fluent.Pdf.Intelligence.PdfEmbeddingModel.#ctor(Fluent.Pdf.Intelligence.PdfIntelligenceModel,System.Int32)</code>

Captures a pinned model and output dimension. Distinct versions or dimensions cannot share an index.

### <code>M:Fluent.Pdf.Intelligence.PdfEmbeddingResponse.#ctor(System.String,System.String,System.Collections.Generic.IEnumerable{Fluent.Pdf.Intelligence.PdfEmbeddingVector},System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Captures a bounded immutable response; SDK dispatch independently verifies binding/count/dimensions/usage.

### <code>M:Fluent.Pdf.Intelligence.PdfEmbeddingVector.#ctor(System.Collections.Generic.IEnumerable{System.Double},Fluent.Pdf.PdfOperationContext)</code>

Copies 1..4096 finite values. Zero vectors reject because cosine similarity is undefined.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceCall.Execute(Fluent.Pdf.PdfOperationContext)</code>

Executes only this prepared fixed-destination call. Honor shared cancellation/deadline/budgets; no tools, follow-up URLs, document actions or implicit retries.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceCitation.#ctor(System.String,System.String,System.String)</code>

Captures a request-local source ID, JSON pointer into returned data and literal quote. Image citations require an empty quote.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceField.#ctor(System.String,Fluent.Pdf.Intelligence.PdfIntelligenceSchema,System.Boolean)</code>

Captures an ASCII identifier of1–64 characters and a required/optional member schema.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceModel.#ctor(System.String,System.String,Fluent.Pdf.Intelligence.PdfIntelligenceLocation,System.Uri,System.Int32,System.String,System.Decimal,System.Decimal,System.Decimal)</code>

Captures nonsecret ASCII identities and finite configured rates per million reported tokens. Remote endpoints require HTTPS; local endpoints require a literal loopback address. Credentials/query/fragment are forbidden in the endpoint.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceOptions.#ctor(System.Uri,System.Boolean,System.Int32,System.Int32,System.Int32,System.Decimal,System.String,System.Int32,System.Int32)</code>

Captures finite limits. A remote model requires the exact HTTPS origin here. Cost defaults to zero and uses only explicitly configured rates; it is not an account-wide billing limit. Images require separate explicit authority.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceResponse.#ctor(System.String,System.String,System.Collections.Generic.IEnumerable{Fluent.Pdf.Intelligence.PdfIntelligenceCitation},Fluent.Pdf.Intelligence.PdfIntelligenceUsage)</code>

Captures bounded response data and at most1024 citation proposals. It is not itself an accepted analysis result.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Array(Fluent.Pdf.Intelligence.PdfIntelligenceSchema,System.Int32,System.Int32)</code>

Creates a bounded array with at most1024 items.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Boolean</code>

Creates a boolean schema.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Integer(System.Int64,System.Int64)</code>

Creates an exact signed64-bit integer schema.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Null</code>

Creates a null-only schema.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Nullable(Fluent.Pdf.Intelligence.PdfIntelligenceSchema)</code>

Allows null or one explicit nested value schema.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Number(System.Nullable{System.Decimal},System.Nullable{System.Decimal})</code>

Creates an exact decimal schema with optional finite bounds.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Object(System.Collections.Generic.IEnumerable{Fluent.Pdf.Intelligence.PdfIntelligenceField})</code>

Creates a closed object of at most128 distinct members.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.String(System.Int32,System.Int32,System.Collections.Generic.IEnumerable{System.String})</code>

Creates a bounded Unicode scalar string and optional closed enumeration of at most128 values.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceSelection.#ctor(System.Int32,System.Nullable{Fluent.Pdf.PdfRect})</code>

Captures an explicit page and positive region; omitted region means its entire displayed CropBox.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceSource.OpenImage(Fluent.Pdf.PdfOperationContext)</code>

Opens a borrowed PNG lease under the active provider context. Providers must dispose it before returning.

### <code>M:Fluent.Pdf.Intelligence.PdfIntelligenceUsage.#ctor(System.Int32,System.Int32,System.Decimal,System.String)</code>

Captures finite reported counts and configured-rate cost; backend billing accuracy requires separate qualification.

### <code>M:Fluent.Pdf.Intelligence.PdfJsonEmbeddingProvider.#ctor(Fluent.Pdf.Intelligence.PdfEmbeddingModel,Fluent.Pdf.Intelligence.IPdfIntelligenceTransport,System.Int32)</code>

Captures an exact endpoint/model and explicit caller transport. The 1..16 byte-token ceiling must be qualified for that backend.

### <code>M:Fluent.Pdf.Intelligence.PdfJsonEmbeddingProvider.Prepare(Fluent.Pdf.Intelligence.PdfEmbeddingRequest,Fluent.Pdf.PdfOperationContext)</code>

Serializes one offline bounded model/input/dimensions request and reserves its entire wire length conservatively.

### <code>M:Fluent.Pdf.Intelligence.PdfMemoryVectorStore.#ctor(System.String,System.Int32,System.Int32,System.Int64)</code>

Creates a private store with explicit administrator, finite document/record ceilings and estimated retained-byte ceiling. Separate stores may be used per tenant.

### <code>M:Fluent.Pdf.Intelligence.PdfMemoryVectorStore.Authorize(Fluent.Pdf.Intelligence.PdfRetrievalScope,System.String,System.Boolean,Fluent.Pdf.PdfOperationContext)</code>

Checks stored ACL under a private lock. Absent scopes are disclosed only to the administrator for creation.

### <code>M:Fluent.Pdf.Intelligence.PdfMemoryVectorStore.Remove(Fluent.Pdf.Intelligence.PdfRetrievalScope,System.String,System.Int64,Fluent.Pdf.PdfOperationContext)</code>

Atomically revokes an exact index after writer/administrator and generation checks. Subsequent access fails before embedding.

### <code>M:Fluent.Pdf.Intelligence.PdfMemoryVectorStore.Replace(Fluent.Pdf.Intelligence.PdfVectorIndex,System.String,System.Int64,Fluent.Pdf.PdfOperationContext)</code>

Atomically commits a complete index after ACL, exact-revision, capacity and generation checks. Cancellation/failure before commit preserves the old index.

### <code>M:Fluent.Pdf.Intelligence.PdfMemoryVectorStore.Search(Fluent.Pdf.Intelligence.PdfRetrievalScope,System.String,System.Int64,Fluent.Pdf.Intelligence.PdfEmbeddingModel,Fluent.Pdf.Intelligence.PdfEmbeddingVector,System.Int32,Fluent.Pdf.PdfOperationContext)</code>

Scores only authorized exact-revision records using scaled compensated cosine sums and deterministic chunk-order ties. A concurrent replacement rejects the stale search.

### <code>M:Fluent.Pdf.Intelligence.PdfRetrievalPermissions.#ctor(System.String,System.String,System.Collections.Generic.IEnumerable{System.String})</code>

Captures explicit policy/version evidence, one writer and 1..128 readers. Host code must authenticate principals and establish source extraction authority before capture.

### <code>M:Fluent.Pdf.Intelligence.PdfRetrievalScope.#ctor(System.String,System.String,System.String)</code>

Captures three separately compared immutable scope components.

### <code>M:Fluent.Pdf.Intelligence.PdfSemanticChunkOptions.#ctor(System.Int32,System.Int32,System.Int32,System.Boolean,Fluent.Pdf.PdfTableExtractionOptions)</code>

Captures finite chunk/text bounds and optional explicit geometric table inference. Invisible text is excluded unless explicitly requested.

### <code>M:Fluent.Pdf.Intelligence.PdfSemanticChunks.Capture(Fluent.Pdf.PdfDocumentSnapshot,System.Collections.Generic.IEnumerable{System.Int32},Fluent.Pdf.Intelligence.PdfSemanticChunkOptions,Fluent.Pdf.PdfProviderRegistry,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Captures only explicit ascending pages. Preserves every captured cluster, prefers line/whitespace boundaries, and never crosses an inferred table boundary. Does not establish visible ink or redact concealed text.

### <code>M:Fluent.Pdf.Intelligence.PdfVectorIndexInfo.#ctor(System.Int64,Fluent.Pdf.Intelligence.PdfEmbeddingModel,Fluent.Pdf.Intelligence.PdfRetrievalPermissions)</code>

Captures authoritative stored state; version zero denotes owner-authorized creation only.

### <code>M:Fluent.Pdf.Intelligence.PdfVectorMatch.#ctor(Fluent.Pdf.Intelligence.PdfVectorIndex,System.Int32,System.Double)</code>

Captures adapter output; SDK independently checks the requested scope and score bounds.

### <code>M:Fluent.Pdf.Intelligence.PdfVectorRetrieval.Index(Fluent.Pdf.Intelligence.PdfSemanticChunkResult,Fluent.Pdf.Intelligence.PdfRetrievalScope,Fluent.Pdf.Intelligence.PdfRetrievalPermissions,System.String,Fluent.Pdf.Intelligence.IPdfEmbeddingProvider,Fluent.Pdf.Intelligence.IPdfVectorStore,Fluent.Pdf.Intelligence.PdfIntelligenceOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Authorizes before embedding, validates exact source/model/batch metadata, then atomically commits a complete index. No shared embedding cache or automatic retry.

### <code>M:Fluent.Pdf.Intelligence.PdfVectorRetrieval.Remove(Fluent.Pdf.Intelligence.PdfRetrievalScope,System.String,Fluent.Pdf.Intelligence.IPdfVectorStore,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Revokes an exact authorized index generation. Readers and ordinary writers cannot recreate an absent scope.

### <code>M:Fluent.Pdf.Intelligence.PdfVectorRetrieval.Search(Fluent.Pdf.Intelligence.PdfRetrievalScope,System.String,System.String,Fluent.Pdf.Intelligence.IPdfEmbeddingProvider,Fluent.Pdf.Intelligence.IPdfVectorStore,System.Int32,Fluent.Pdf.Intelligence.PdfIntelligenceOptions,Fluent.Pdf.PdfOperationPolicy,System.Threading.CancellationToken)</code>

Authorizes the exact scope before model preparation or dispatch, rejects a different model/dimension, and reauthorizes before returning ranked matches.

### <code>P:Fluent.Pdf.Intelligence.IPdfDocumentIntelligenceProvider.Model</code>

Gets immutable model identity/location/rates captured once per job.

### <code>P:Fluent.Pdf.Intelligence.IPdfEmbeddingProvider.Model</code>

Gets immutable pinned identity and dimensionality without network access.

### <code>P:Fluent.Pdf.Intelligence.PdfChatDocumentModel.Model</code>

Gets exact immutable model/endpoint/pricing configuration.

### <code>P:Fluent.Pdf.Intelligence.PdfChatDocumentModel.SupportsImages</code>

Gets explicit backend PNG input capability.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingCall.ConfiguredCostCeiling</code>

Gets reserved configured-rate cost ceiling.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingCall.InputTokenCeiling</code>

Gets reserved conservative token ceiling.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingCall.Model</code>

Gets exact immutable provider model.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingCall.RequestSha256</code>

Gets input/model/scope binding.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingModel.Dimensions</code>

Gets exact output vector length.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingModel.Model</code>

Gets immutable exact provider/model/endpoint/pricing configuration.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingRequest.RequestSha256</code>

Gets the exact model/input/scope request hash.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingRequest.Texts</code>

Gets bounded raw text inputs in required response order.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingResponse.ExactModelVersion</code>

Gets reported exact model version.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingResponse.InputTokens</code>

Gets bounded reported input usage, not verified backend billing.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingResponse.RequestSha256</code>

Gets exact request binding.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingResponse.Vectors</code>

Gets ordered immutable vectors.

### <code>P:Fluent.Pdf.Intelligence.PdfEmbeddingVector.Values</code>

Gets immutable finite original values; the store normalizes only during cosine scoring.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceCall.InputTokenCeiling</code>

Gets conservative prepared input token bound.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceCall.MaximumOutputTokens</code>

Gets the explicit completion token bound sent to the backend.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceCall.Model</code>

Gets the immutable captured provider model.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceCall.RequestSha256</code>

Gets the exact prepared request binding.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceCall.ReservedConfiguredCost</code>

Gets configured-rate cost reserved before execution.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceCitation.OutputPath</code>

Gets JSON pointer into the accepted structured data.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceCitation.Quote</code>

Gets literal source quote or empty for an image frame.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceCitation.SourceId</code>

Gets the request-local source ID.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.CaptureRegion</code>

Gets the exact displayed physical-point capture region, not inferred quote ink geometry.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.OutputPath</code>

Gets cited JSON pointer into accepted output.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.PageIndex</code>

Gets the zero-based source PDF page.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.Quote</code>

Gets an exact uniquely matched quote or empty for an image.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.SourceId</code>

Gets validated request-local source identity.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.SourceSha256</code>

Gets exact captured source SHA256.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceEvidence.TextOffset</code>

Gets quote UTF16 offset in captured text, or null for an image.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceField.Name</code>

Gets the exact case-sensitive member name.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceField.Required</code>

Gets whether the member is required.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceField.Schema</code>

Gets the immutable member schema.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceJob.Instruction</code>

Gets caller-authored trusted task instructions/question.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceJob.Options</code>

Gets immutable per-job authority/ceilings.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceJob.RequestSha256</code>

Gets the exact selected-source/task/schema/request binding.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceJob.Schema</code>

Gets exact structured data schema.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceJob.Sources</code>

Gets only explicitly selected source data.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceJob.TargetLanguage</code>

Gets explicit translation language or empty.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceJob.Task</code>

Gets the explicit requested task.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceModel.Currency</code>

Gets configured cost currency.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceModel.Endpoint</code>

Gets the explicit fixed endpoint or null for in-process execution.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceModel.ExactModelVersion</code>

Gets the exact requested and returned model version.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceModel.InputPricePerMillion</code>

Gets configured input cost per million tokens.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceModel.Location</code>

Gets the declared execution boundary.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceModel.MaximumContextTokens</code>

Gets the configured maximum input/output token context.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceModel.OutputPricePerMillion</code>

Gets configured output cost per million tokens.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceModel.ProviderId</code>

Gets a nonsecret pinned provider/configuration identity.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceModel.RequestCharge</code>

Gets configured fixed request charge.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceOptions.AllowImages</code>

Gets explicit selected-region image processing authority.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceOptions.AllowedRemoteOrigin</code>

Gets explicit remote origin authority or null.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceOptions.CostCurrency</code>

Gets exact configured cost currency; no conversion occurs.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceOptions.MaximumCitations</code>

Gets maximum accepted source citations.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceOptions.MaximumConfiguredCost</code>

Gets per-job configured-rate reservation ceiling.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceOptions.MaximumInputTokens</code>

Gets the provider's conservative input token reservation ceiling.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceOptions.MaximumOutputCharacters</code>

Gets cumulative accepted structured text character ceiling.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceOptions.MaximumOutputTokens</code>

Gets requested and reported completion token ceiling.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceOptions.MaximumSourceCharacters</code>

Gets cumulative selected text character ceiling.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceResponse.Citations</code>

Gets unaccepted source proposals.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceResponse.DataJson</code>

Gets unaccepted structured JSON.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceResponse.RequestSha256</code>

Gets provider-observed request binding.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceResponse.Usage</code>

Gets reported usage.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceResult.DataJson</code>

Gets canonical accepted structured JSON data, never automatically executed/rendered as markup.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceResult.Evidence</code>

Gets resolved source evidence.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceResult.Model</code>

Gets the exact caller-selected model configuration.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceResult.OperationReport</code>

Gets complete diagnostics and shared resource accounting.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceResult.RequestSha256</code>

Gets exact request/source/schema binding.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceResult.Usage</code>

Gets reported bounded tokens and configured-rate cost.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.AllowedStrings</code>

Gets an optional closed ordinal string enumeration.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Fields</code>

Gets explicit object members.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.ItemSchema</code>

Gets array item/nullable value schema or null.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.Kind</code>

Gets the explicit JSON kind.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.MaximumLength</code>

Gets maximum string-scalar/array length.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.MaximumNumber</code>

Gets optional exact upper numeric bound.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.MinimumLength</code>

Gets minimum string-scalar/array length.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSchema.MinimumNumber</code>

Gets optional exact lower numeric bound.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSelection.PageIndex</code>

Gets the zero-based source page.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSelection.Region</code>

Gets explicit displayed capture region or null for the full crop.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSource.Id</code>

Gets request-local source identity.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSource.IsImage</code>

Gets whether the source contains explicitly authorized PNG pixels.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSource.PageIndex</code>

Gets exact zero-based source page.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSource.Region</code>

Gets the exact displayed physical-point capture frame, not an inferred quote ink box.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSource.Sha256</code>

Gets exact captured text/image SHA256.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceSource.Text</code>

Gets explicitly selected source-order text or empty for an image.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceUsage.ConfiguredCost</code>

Gets reported tokens priced using the explicit configuration.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceUsage.Currency</code>

Gets configured cost currency.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceUsage.InputTokens</code>

Gets reported input tokens.

### <code>P:Fluent.Pdf.Intelligence.PdfIntelligenceUsage.OutputTokens</code>

Gets reported completion tokens.

### <code>P:Fluent.Pdf.Intelligence.PdfJsonEmbeddingProvider.Model</code>

Gets the pinned model/dimension boundary.

### <code>P:Fluent.Pdf.Intelligence.PdfRetrievalPermissions.PolicyVersion</code>

Gets host policy/version evidence, carried with every stored index.

### <code>P:Fluent.Pdf.Intelligence.PdfRetrievalPermissions.Readers</code>

Gets the immutable authorized readers.

### <code>P:Fluent.Pdf.Intelligence.PdfRetrievalPermissions.Writer</code>

Gets the explicit writer principal.

### <code>P:Fluent.Pdf.Intelligence.PdfRetrievalScope.DocumentId</code>

Gets trusted host document identity.

### <code>P:Fluent.Pdf.Intelligence.PdfRetrievalScope.DocumentSha256</code>

Gets the exact complete source PDF SHA256.

### <code>P:Fluent.Pdf.Intelligence.PdfRetrievalScope.TenantId</code>

Gets trusted host tenant identity.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunk.Characters</code>

Gets original mapping/operation/displayed/native-geometry records, including inferred-geometry flags.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunk.Index</code>

Gets stable source-order chunk ordinal within this exact capture.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunk.PageIndex</code>

Gets zero-based original page index.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunk.Sha256</code>

Gets exact text UTF8 SHA256.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunk.TableIndex</code>

Gets page-local inferred table ordinal or null. Table splitting preserves this identity.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunk.Text</code>

Gets exact mapped text in operator execution order.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunk.TextOffset</code>

Gets UTF16 offset in that page's captured raw text.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunk.Utf8Bytes</code>

Gets exact UTF8 length under the utf8-bytes/v1 profile.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunkOptions.IncludeInvisibleText</code>

Gets explicit nonpainting-text inclusion.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunkOptions.MaximumCharacters</code>

Gets the cumulative captured UTF16 ceiling.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunkOptions.MaximumChunks</code>

Gets the total finite chunk ceiling.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunkOptions.MaximumUtf8Bytes</code>

Gets the hard UTF8 byte ceiling, without an unqualified model token-count claim.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunkOptions.Tables</code>

Gets optional geometric table inference, not author-confirmed semantics.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunkResult.CaptureSha256</code>

Gets capture hash including revision, options, selected pages, boundaries, text and inference provenance.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunkResult.Chunks</code>

Gets all primary chunks in selected page/source order.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunkResult.DocumentSha256</code>

Gets complete source PDF SHA256, distinct from text/capture identity.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunkResult.OperationReport</code>

Gets shared parse/extraction/chunk resource accounting.

### <code>P:Fluent.Pdf.Intelligence.PdfSemanticChunkResult.PageIndices</code>

Gets explicitly captured page indices, including empty pages.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorIndex.Capture</code>

Gets original chunk/page/layout/mapping provenance.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorIndex.Embeddings</code>

Gets ordered immutable vectors and exact scoped request binding.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorIndex.Model</code>

Gets exact model/version/dimension identity.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorIndex.Permissions</code>

Gets explicit application permission provenance.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorIndex.Scope</code>

Gets exact tenant/document/revision identity.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorIndexInfo.Model</code>

Gets stored pinned model or null only for authorized creation.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorIndexInfo.Permissions</code>

Gets authoritative existing permissions or null only for authorized creation.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorIndexInfo.Version</code>

Gets monotonic store generation, used to prevent lost concurrent writes.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorIndexResult.Index</code>

Gets exact committed source/model/permission provenance.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorIndexResult.OperationReport</code>

Gets shared embedding/commit diagnostics and accounting.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorIndexResult.Version</code>

Gets committed monotonic store generation.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorMatch.Chunk</code>

Gets exact chunk text, page, offsets, mappings and inferred table identity.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorMatch.Index</code>

Gets the authoritative matched index, scope, model and ACL.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorMatch.Score</code>

Gets cosine score in [-1,1], not calibrated relevance.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorSearchResult.Matches</code>

Gets bounded ranked authorized matches.

### <code>P:Fluent.Pdf.Intelligence.PdfVectorSearchResult.OperationReport</code>

Gets shared authorization/query-embedding/search accounting.

### <code>T:Fluent.Pdf.Intelligence.IPdfDocumentIntelligenceProvider</code>

Explicit caller-owned local/remote model integration. Property/support/preparation methods must not perform I/O.

### <code>T:Fluent.Pdf.Intelligence.IPdfEmbeddingProvider</code>

Explicit optional local/remote embedding adapter; implementations must honor shared budgets, cancellation and network authority.

### <code>T:Fluent.Pdf.Intelligence.IPdfIntelligenceTransport</code>

Explicit caller-owned fixed-endpoint chat transport. No default HTTP client, credential lookup, proxy, redirect or model download is supplied.

### <code>T:Fluent.Pdf.Intelligence.IPdfVectorStore</code>

Original optional vector-store boundary. Implementations must authorize before returning metadata, enforce authoritative ACLs at every operation and commit atomically with generation checks. Remote store authority must be separately explicit in the host.

### <code>T:Fluent.Pdf.Intelligence.PdfChatDocumentModel</code>

Original closed chat-completions JSON-schema bridge with explicit model, transport and conservative byte-token ceiling. No tool execution, automatic follow-up or retry occurs.

### <code>T:Fluent.Pdf.Intelligence.PdfDocumentIntelligence</code>

Explicit selection, privacy authority, bounded provider execution and structured source validation under one root context.

### <code>T:Fluent.Pdf.Intelligence.PdfEmbeddingCall</code>

Prepared exact embedding dispatch, with no implicit retry or model selection.

### <code>T:Fluent.Pdf.Intelligence.PdfEmbeddingModel</code>

Exact embedding identity, dimensions and caller-configured execution/pricing boundary.

### <code>T:Fluent.Pdf.Intelligence.PdfEmbeddingRequest</code>

Immutable exact input batch and request binding. Preparation must remain offline.

### <code>T:Fluent.Pdf.Intelligence.PdfEmbeddingResponse</code>

One exact ordered model response and reported input usage.

### <code>T:Fluent.Pdf.Intelligence.PdfEmbeddingVector</code>

Finite nonzero vector, privately copied under explicit resource accounting.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceCall</code>

One prepared bounded call. Preparation must be offline; execution receives the same mandatory context.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceCitation</code>

A bounded model-proposed citation. The SDK resolves identity, exact unique quote and capture frame before exposing provenance.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceEvidence</code>

SDK-resolved provenance. A unique text quote match or exact captured image frame does not establish semantic answer correctness.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceField</code>

One explicit object member and its immutable schema.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceJob</code>

Immutable validated request and selected sources; supplied source text is untrusted data.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceLocation</code>

Declared model execution boundary.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceModel</code>

Immutable exact model, endpoint, context ceiling and caller-configured pricing. Pricing is configuration, not verified market/billing data.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceOptions</code>

Immutable per-operation data/cost/token authority. Remote origin and image processing are disabled by default.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceResponse</code>

Provider result requiring SDK schema, quota, binding and citation validation before public acceptance.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceResult</code>

Detached schema/quota/binding/provenance-validated result. Model factual, linguistic and OCR quality require separate review.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceSchema</code>

An immutable finite JSON schema tree. Unknown object members reject; string lengths count Unicode scalars.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceSelection</code>

One explicit zero-based page and optional displayed top-left physical-point region.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceSource</code>

One immutable explicitly captured excerpt/image with SDK-owned page/region provenance.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceTask</code>

Explicit model tasks; source data is never interpreted as an SDK command.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceUsage</code>

Detached reported token usage and cost calculated from explicit configured rates.

### <code>T:Fluent.Pdf.Intelligence.PdfIntelligenceValueKind</code>

Closed JSON schema kinds. Exact decimal numbers and signed64-bit integers are used; external references and executable validators are absent.

### <code>T:Fluent.Pdf.Intelligence.PdfJsonEmbeddingProvider</code>

Original fixed-endpoint float-array JSON embedding bridge. No implicit transport, credential lookup, model assets or retries.

### <code>T:Fluent.Pdf.Intelligence.PdfMemoryVectorStore</code>

Bounded original managed cosine store. No global cache, model/network calls, serialization or implicit scope expansion. Host authenticates each principal.

### <code>T:Fluent.Pdf.Intelligence.PdfRetrievalPermissions</code>

Application-authorized immutable permission provenance. This is not authentication or inferred PDF permission authority.

### <code>T:Fluent.Pdf.Intelligence.PdfRetrievalScope</code>

Explicit tenant/document/exact PDF revision. Identity is supplied by the trusted host, never by document/model output.

### <code>T:Fluent.Pdf.Intelligence.PdfSemanticChunk</code>

Exact contiguous raw mapped source text, with no invented separators or overlapping primary text. Geometry describes advance cells, not visible ink.

### <code>T:Fluent.Pdf.Intelligence.PdfSemanticChunkOptions</code>

Finite source-order chunk profile. UTF8 bytes are a portable token ceiling only for separately qualified byte-bounded tokenizers.

### <code>T:Fluent.Pdf.Intelligence.PdfSemanticChunkResult</code>

Detached immutable chunks bound to the complete input revision. Concatenation per captured page exactly reconstructs captured raw text.

### <code>T:Fluent.Pdf.Intelligence.PdfSemanticChunks</code>

Original bounded layout-aware raw-text chunking. Logical reading order, ActualText substitution and author-confirmed semantics require separate profiles.

### <code>T:Fluent.Pdf.Intelligence.PdfVectorIndex</code>

Complete immutable revision-bound embedding index prepared before an atomic store replacement.

### <code>T:Fluent.Pdf.Intelligence.PdfVectorIndexInfo</code>

Authorized index state used for compare-and-swap writes and consistent query model selection.

### <code>T:Fluent.Pdf.Intelligence.PdfVectorIndexResult</code>

Complete index commit evidence.

### <code>T:Fluent.Pdf.Intelligence.PdfVectorMatch</code>

One match with exact source and permission provenance; similarity is not a factual confidence measure.

### <code>T:Fluent.Pdf.Intelligence.PdfVectorRetrieval</code>

Scoped retrieval orchestration. Capture is a separate explicit operation; indexing and search each share one model/store budget and reauthorize at commit/read.

### <code>T:Fluent.Pdf.Intelligence.PdfVectorSearchResult</code>

Authorized exact-scope search evidence.

