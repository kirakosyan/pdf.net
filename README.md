# Fluent.Pdf

Fluent.Pdf is an MIT-licensed managed PDF library for .NET. Version 1.0.0 releases the currently implemented core profiles: PDF creation, inspection and editing with explicit operation budgets, cancellation and provider contracts.

**Release scope:** the single Fluent.Pdf package and symbols. This is the owner-selected implemented-core release, not completion or qualification of the full SDK roadmap. Read the [1.0.0 scope and limitations](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/releases/1.0.0.md) before deployment. Unfinished roadmap issues remain open.

## Install

`dotnet add package Fluent.Pdf --version 1.0.0`

The package includes net472, netstandard2.0, net8.0 and net10.0 assemblies, XML API documentation, portable symbols, SourceLink, MIT licensing and dependency notices. Modern consumer checks run on Windows, Linux and macOS x64/ARM64; Framework checks use Windows x86/x64 with actual 4.7.2 and current Framework.

## Implemented profiles

The managed core provides document/page models, bounded PDF syntax and filters, page reading and transactional full/incremental writing, content/version handling, drawing and geometry, managed layout profiles, page organization, metadata, annotations, passive navigation, attachments, optional-content layers, forms and text/resource inspection/editing. Supported constructs and strict unsupported-content diagnostics are defined in the feature documentation; a public API does not imply complete profile coverage.

The core performs no automatic engine, font or model downloads. Caller streams remain caller-owned; mutations of one document must be serialized. [Operation policies](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/operation-policies.md) define resource limits, cancellation and atomic file output.

## Optional functionality and limits

This release does not ship companion assemblies or native/browser/OCR engines, models, optional codecs/fonts/shapers, office/other-format adapters, signing providers or GUI/COM/printing integrations. Provider-dependent APIs require an explicitly supplied implementation. Complete standards, accessibility, fidelity, security/fuzz/endurance and broad document-interoperability qualification remain unfinished. No blanket PDF/A, PDF/UA, PDF/X, PDF/E, PAdES, OCR accuracy or complete conversion claim is made.

See the [release notes](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/releases/1.0.0.md), [API and examples](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/api-contract.md), [PDF reading/writing](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/pdf-writing.md), [developer guide](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/developer-guide.md) and [platform contract](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/platforms.md). The package-only release consumer source is [CoreConsumer](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/eng/CoreConsumer/Program.cs).

## Repository and roadmap

The [feature catalogue](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/feature-catalogue.md), [product specification](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/product-spec.md), [architecture](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/architecture.md), [implementation order](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/implementation-plan.md) and [GitHub backlog](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/github-backlog.md) describe the broader planned SDK. They include optional projects and bounded source profiles outside the published core. The separate Reader remains deferred.

Full-roadmap qualification remains governed by [production quality](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/production-quality.md) and specs/release-readiness.json. The explicit version-specific core policy is specs/core-release.json; it does not mark broader requirements qualified. [Release automation](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/releasing.md) retains exact artifact hashes, runtime consumer reports, independent generated-PDF checks and scoped cleanup evidence.

## Build and license

Use the .NET SDK selected by global.json and PowerShell 7. Follow [AGENTS.md](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/AGENTS.md) and [testing](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/docs/testing.md) for source/build/API checks. Default packing still enforces full-roadmap qualification; the core release path requires its explicit version-specific authorization and verified artifacts.

[MIT](https://github.com/kirakosyan/pdf.net/blob/v1.0.0/LICENSE). Optional dependencies retain their own licenses; the package includes the notice inventory.
