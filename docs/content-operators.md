# Content operators and version selection

FP-007 is implemented in source in the managed core on the four target frameworks. Production qualification remains pending; no rendering capability is implied.

`PdfContentOperation` exposes the standard content operators as `PdfOperator` values with immutable direct operands. Construction validates counts, operand kinds, finite numbers, dash patterns, integer ranges and text-adjustment arrays. `PdfContentProgram.Create` validates a complete page/form or Type 3 glyph program under an operation policy. Graphics, text, marked-content and compatibility scopes must balance. Text positioning/showing requires a text object; path/object painting and special graphics-state operators require it to be closed. Glyph metrics must occur exactly once at the start of a Type 3 glyph program. Named fonts, shadings, graphics states and other resources remain symbolic references; content validation does not interpret their semantics or establish visual correctness.

```csharp
using Fluent.Pdf;
using PdfFacade = Fluent.Pdf.Pdf;

using var document = PdfFacade.Create().AddPage();
document.Pages[0].Content = PdfContentProgram.Create(new[]
{
    new PdfContentOperation(PdfOperator.SaveState),
    new PdfContentOperation(PdfOperator.FillRgb, PdfValue.Integer(1), PdfValue.Integer(0), PdfValue.Integer(0)),
    new PdfContentOperation(PdfOperator.Rectangle, PdfValue.Integer(20), PdfValue.Integer(20), PdfValue.Integer(100), PdfValue.Integer(50)),
    new PdfContentOperation(PdfOperator.Fill),
    new PdfContentOperation(PdfOperator.RestoreState)
});
document.SaveFile("rectangle.pdf");
```

`Parse` reads borrowed decoded bytes and snapshots all data eagerly. `Write` emits canonical decoded content and leaves its output open; an output failure can leave partial bytes. `Edit` returns a separately validated immutable program. Parsed strings preserve their encoded bytes; text decoding and font interpretation belong to the font/text requirements. Streams and indirect references are forbidden inside operands. Unknown operators reject by default, including within compatibility sections. `Preserve` requires a caller-declared `unknownOperatorVersion`; authored extension operators similarly require an explicit minimum version. Preserving a token does not implement its execution semantics.

Inline images use exact raw row geometry or the outer filter's deterministic terminator. Supported boundary readers cover Flate (including checksum), LZW, ASCIIHex, ASCII85, RunLength, CCITT fax and JPEG markers. The parser never guesses an image boundary by finding `EI` in binary pixels. Inline dictionaries and encoded bytes remain immutable. Geometry/pixel limits are checked before compressed boundary decoding, and expanded byte limits cover decoder work. Raw images require a direct color space with a known component count; an unresolved named resource fails with `Content.InlineColorSpaceRequired`. Unsupported boundary filters fail explicitly. Full image interpretation, named color-resource resolution and codec profile qualification are separate image/graphics work. `PdfContentOperation.InlineImage` snapshots a borrowed encoded stream and rejects extra bytes after its exact boundary.

`PdfPage.ReadContent` decodes original `Contents` streams under the document policy, combines content-stream arrays in order with separating newlines, and validates the resulting complete program. Resource providers are explicit. It executes no document actions. Assign `Content` to replace a page's stream; an empty program clears it, while null retains original content. Snapshots retain immutable content after subsequent page edits or disposal. Full and incremental saves integrate replacement streams and their version requirements without mutating the original source graph.

The writer combines source/header/catalog versions, typed content floors, `PdfWriteOptions.MinimumVersion`, cross-reference representation and recognized structural features. The current feature facts include transparency (1.4), object/xref streams, optional content, JPX/Crypt filters (1.5), UserUnit/OpenType (1.6), developer extensions (1.7), and PDF 2.0 black-point compensation/structure namespaces. Indirect feature declarations are resolved with operation bounds. An exact `Version` below the resulting floor fails before caller output. Incremental upgrades declare the new version in the catalog while preserving the original header bytes. Opaque custom low-level objects/content require an explicit minimum version from their author; the writer cannot infer arbitrary vendor semantics.

`PdfExtensionRequirement` declares a developer prefix, supported base version and positive extension level. Declarations merge into catalog `Extensions`, preserve unknown keys and reject lower existing version/level requirements. Source extensions preserve by default, with valid base/level declarations. `PdfUnknownExtensionPolicy.Reject` requires the caller to declare every encountered prefix. Declaring an extension is format metadata, not an implementation of that extension. PDF 2.0 feature declarations likewise do not claim complete PDF 2.0 conformance.

Source tests cover all standard operator spellings, grammar/state failures, unknown-token policy, binary inline boundaries, independent JPEG/fax inputs, content arrays, snapshots, incremental prefix preservation, version floors, extension merging, cancellation, budgets and output-byte fault injection. The full production matrix, final package consumers, standards/fidelity corpus, long fuzzing and endurance remain required. NuGet candidate creation and publication remain blocked.
