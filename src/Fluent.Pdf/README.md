# Fluent.Pdf package boundary

This project implements the managed document/page model, immutable options, diagnostics and explicit provider contracts on net472, netstandard2.0, net8.0 and net10.0. See the [API contract](../../docs/api-contract.md) for behavior and executable examples.

No PDF reader/writer, renderer or optional engine is bundled. Open/save requires an explicitly registered provider and fails with PdfProviderUnavailableException otherwise. Test fixture bytes are not PDF output.

Contract tests and API checks validate this implemented layer. Pack remains blocked by the production gate while readiness is false. Complete the dependency-ordered requirements and qualify the exact package before stable publication.
