# Text and structured input

FP-050 has a managed **source-validation profile**, not production qualification. Full Markdown, structured-document fidelity, language/resource coverage and every production gate remain mandatory. No candidate or public package may be created from these source checks.

`PdfInputConversion.Generate` returns a caller-owned document. `Write` and `WriteFile` parse, bind, paginate, assemble link geometry and serialize within one `PdfOperationContext`. Source streams start at the current position and stay open. Stream output can contain a prefix on failure. File output runs all hooks and serialization privately before atomic replacement.

Supply `PdfInputConversionOptions` with generation options (including an explicit culture name), ordinal named styles and optional captured drawings. `body` is required. `heading`, `code`, `strong` and `emphasis` are required when used. Styles select actual fonts, size, color and shaping choices; the converter does not search machine fonts. Supplying the same font for several semantic styles deliberately retains that font. The converter adds no resource resolver, browser, network access or file lookup.

The defaults add explicit ceilings of 4 MiB source bytes and 100,000 syntax/model nodes to the operation's existing byte/object/depth/time/page/output limits. UTF-8 without a BOM and BOM-selected UTF-8/16/32 are decoded strictly. Malformed Unicode fails. Paragraphs/runs retain their separate one-million UTF-16-unit limits and paragraphs retain the 4096-run ceiling. Input, expansion and node allocations are charged before capture. Scalar numbers must be exactly representable as decimal; overflow, rounding and underflow reject.

## Source forms

- Text uses blank lines as paragraph boundaries; consecutive blank separators coalesce. Ordinary line breaks remain explicit flow breaks. Extraction exposes source text and separately inferred geometric lines; it does not insert paragraph separators into source text.
- Markdown supports ATX headings with literal titles, ordinary paragraphs/soft breaks and two-space hard breaks, flat ordered/bullet lists, rectangular leading/trailing-pipe tables with an unaligned hyphen separator row, single-level emphasis/strong/inline code, literal fenced code and literal-label inline URI links. Code retains spaces and line boundaries. Tables/list items use the actual flow table/list models, including explicit counters and header rows. Source inline roles are marked as Code/Strong/Em. This is not a PDF/UA claim or a complete accessible structure tree.
- Unsupported Markdown nesting, alignment syntax, raw HTML, images, extensions, reference links, heading inline constructs and fence language metadata produce stable loss diagnostics. Strict mode fails; permissive mode records each omission. Complete Markdown profiles remain required; this bounded profile does not redefine the requirement.
- JSON and XML use the version-one schemas below. Unknown fields/attributes/elements/block types are losses, strict-mode failures and permissive-mode warnings. Malformed syntax, duplicate JSON members, invalid versions, ambiguous text/runs, unsafe links and absent styles/resources always fail.

Links become actual PDF Link annotations with passive URI actions. HTTP/HTTPS links cannot contain credentials; mailto is also accepted. International hosts use IDN ASCII names and URI paths/query text are escaped to ASCII. The SDK never activates these links. Nested list/table Form transforms and pagination are resolved through a same-context private text/geometry inspection; a linked clipped run rejects rather than guessing visible bounds. Private source markers are implementation metadata, not authenticated provenance. Complete bidi/clipping/annotation/accessibility and viewer profiles remain mandatory.

## JSON version one

```json
{
  "version": 1,
  "data": { "amount": 1234.5, "name": "AB", "selected": true },
  "blocks": [
    { "type": "heading", "text": "AB", "level": 1 },
    { "type": "paragraph", "text": "{{amount:N2}}", "style": "body" },
    { "type": "paragraph", "runs": [
      { "text": "{{name}}", "style": "body", "uri": "https://example.test/" },
      { "text": "BA", "style": "code", "role": "Code" }
    ] },
    { "type": "list", "items": ["AB", "BA"], "ordered": true, "start": 1 },
    { "type": "table", "headers": 1, "rows": [["AB", "BA"], ["BA", "AB"]] },
    { "type": "code", "text": "AB  BA" },
    { "type": "drawing", "resource": "original-logo" },
    { "type": "pageBreak" }
  ]
}
```

`data` is optional and accepts only strings, exactly representable decimal numbers, booleans and null. No dynamic object binding or expression evaluation occurs. `{{name}}` and `{{name:format}}` substitute captured scalars using the explicit generation culture. Missing names and malformed placeholders fail. The existing bounded scalar formatter rejects unsafe precision before formatting allocates. Binding inserts text, never markup. Text and runs are mutually exclusive; runs must be nonempty. Inline roles are closed to Code, Strong and Em. Drawing names refer only to `PdfInputDrawing` entries supplied in options; URL/path-shaped names are never opened.

## XML version one

```xml
<document version="1">
  <data>
    <value name="amount" type="decimal">1234.5</value>
    <value name="name" type="text">AB</value>
  </data>
  <blocks>
    <heading level="1">AB</heading>
    <paragraph style="body">{{amount:N2}}</paragraph>
    <paragraph>
      <run style="body" uri="https://example.test/">{{name}}</run>
      <run style="code" role="Code">BA</run>
    </paragraph>
    <list ordered="true" start="1"><item>AB</item><item>BA</item></list>
    <table headers="1"><row><cell>AB</cell><cell>BA</cell></row></table>
    <code>AB  BA</code>
    <drawing resource="original-logo"/>
    <pageBreak/>
  </blocks>
</document>
```

The unqualified schema has optional `data` before exactly one `blocks` element. Data names are unique; types are `text`, `decimal`, `boolean` (true/false) and `null` (empty). Decimal lexical values follow the exact closed JSON numeric grammar. Blocks share the JSON meanings; numeric attributes are bounded nonnegative integers. Paragraphs contain literal text or only run children with formatting whitespace. Lists contain literal items; tables contain rows of literal cells. No mixed-content inference occurs.

DTDs, external entities, resolvers, namespaces/schema fetching, XInclude and processing instructions are prohibited. The XML reader has explicit document/entity character limits and every captured node uses the shared operation budget. Comments and processing instructions are outside this profile and reject. Resources are captured in options, never resolved from XML.

## Applicability and evidence

The managed contracts target net472, netstandard2.0, net8.0 and net10.0 without a new dependency. Providers retain their explicit applicability. Executable C# and VB consumers use `ConvertInput` and the same public stream API. Exact final-package consumers and actual minimum runtimes remain release requirements.

See [source evidence](evidence/FP-050-source.json), [standards observations](evidence/FP-050-standards.json), [independent original outputs](evidence/FP-050-independent.json), and [production gates](production-quality.md). Source validation cannot close full Markdown/schema/culture/resource fidelity, security/fuzz/concurrency/endurance, minimum-runtime, RID or package qualification.
