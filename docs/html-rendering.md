# Optional Chromium HTML conversion

FP-044 has a managed source contract and an optional original `Fluent.Pdf.Html` DevTools adapter. `PdfHtml.Render` stages browser PDF bytes privately, imports them through the ordinary managed reader under the same operation context, and returns the normal editable `PdfDocument`. Metadata edits, extraction and full/incremental/atomic saves keep their existing contracts. The managed core links no browser, Node.js, GUI or native package.

## Sources and resources

`PdfHtmlSource.FromHtml` captures well-formed logical Unicode, with a reserved virtual HTTPS document address by default. `Read` accepts UTF-8 HTML from the current position of a borrowed file/document stream; a UTF-8 BOM is allowed. `FromResource` selects an explicitly addressed main HTML resource. `PdfHtmlResource.Read` captures immutable resource bytes and an explicit Content-Type from a borrowed stream. Resource URLs are matching keys and relative-resource bases; these APIs fetch nothing.

The source profile bounds markup to 4 Mi UTF-16 code units, file markup to 8 MiB UTF-8, each captured resource to 16 MiB, and the inventory to 128 uniquely addressed resources/64 MiB. Main addresses must be absolute HTTP(S), without user information or fragments. Captured bytes survive input disposal. Preparation and rendering are separate explicit operations with their own supplied policies.

The adapter intercepts GET requests and serves supplied markup/resources first. An explicit per-job resource session can then apply [resource/authentication policy](html-resources.md); the default remains offline. Strict mode rejects missing subresources; permissive mode records `Html.ResourceMissing` and fails the request. Missing main content rejects. Native data/blob resources require the qualified host's memory/process boundary. The browser's own tab favicon may receive an empty response; explicit image requests still follow policy.

## Engine and job boundary

Register `ChromiumHtmlRenderer` with a `ChromiumConnectionFactory`, an exact four-part browser version and exact source revision. The adapter checks the actual `Browser.getVersion` product/revision and DevTools 1.3 protocol **before sending document input**. `Descriptor`, `ExpectedVersion` and `ExpectedRevision` expose the pins. An exact pin is not a blanket support claim for every Chromium build.

Each conversion requires a fresh caller-provisioned supervised process/connection, then creates a new off-the-record browser context and target. The SDK disables downloads/cache and bypasses service workers, configures a fixed locale/timezone, and explicitly selects whether source JavaScript executes. JavaScript is disabled by default. Browser output streams in bounded 64 KiB chunks; malformed JSON, duplicate keys, unexpected command/session identities, unsupported child targets, script errors, dialogs and authentication fail with stable `Html.*` diagnostics. Failed web fonts produce a reported substitution; strict mode rejects it. Context/target cleanup is intentional, while unexpected crashes remain errors.

The supplied host is a trusted provider boundary. It must pin/provision the complete engine/assets/fonts, expose only permitted read-only files, prevent unauthorized network access, enforce memory/process/time bounds, and terminate/join all owned work within five seconds after cancellation/deadline. It must bound and charge transport allocations before creating them. The adapter does not install, discover or download a host. A complete deployable SDK host and its OS/RID/runtime security qualification remain mandatory work; an arbitrary callback is not proof of those guarantees.

```csharp
var source = PdfHtmlSource.FromHtml(html, resources: explicitResources);
var renderer = new Fluent.Pdf.Html.ChromiumHtmlRenderer(
    qualifiedPrivateHost, exactBrowserVersion, exactBrowserRevision);
using var document = PdfHtml.Render(source, new PdfProviderRegistry(renderer),
    new PdfHtmlOptions(enableJavaScript: false), explicitPolicy);
document.SaveFile(destination);
```

FP-046 adds [finite readiness and lifecycle controls](html-readiness.md): explicit selectors/promises, isolated synchronous font/image checks and bounded observed network quietness under a shared phase deadline. Locale, timezone and print/screen media are explicit. Child-frame/worker/replacement-navigation profiles remain mandatory. FP-047 owns complete print-media/accessibility controls. Current paper size remains a browser print request; imported boxes retain engine precision (the observed A4 candidate rounds to approximately 595.91998 × 841.91998 points).

## Source evidence and applicability

Core contracts, optional adapter and C#/VB consumers target net472, netstandard2.0, net8.0 and net10.0. Source unit tests exercise protocol/ownership/failure/resource boundaries; a protocol peer does not qualify Chromium layout. Complete actual browser behavior on all six modern RIDs, actual minimum Framework bitness lanes, fallback and final-package consumers remains required.

`eng/HtmlBrowserProbe` is an explicit Linux/.NET 10 trusted-original-fixture observer. Its manifest pins browser/Node/font SHA-256, version/revision, temporary parent and output directory. It never downloads assets or disables Chromium's sandbox. It runs a fixed original grid/flex/SVG/canvas/web-font/dynamic-text fixture with JavaScript enabled and disabled, imports each PDF, edits metadata and extracts text through the SDK. The development host uses private profiles and bounded framed I/O but **does not qualify the SDK OS network/memory/security boundary**. Owned profiles and observations remain retained for source evidence.

```text
dotnet run --project eng/HtmlBrowserProbe -c Release -- \
  --trusted-original-fixtures /absolute/path/to/fixture-manifest.json
```

The manifest has `browserExecutable`, `nodeExecutable`, `fontFile`, corresponding `*Sha256` properties, `browserVersion`, `browserRevision`, `temporaryParent` and `outputDirectory`. No source path is inferred. The checked source fixture uses the repository's original font, not machine-selected font goldens. Actual independent reports cover exact Unicode, declared grid/flex positions, SVG dimensions, canvas/script behavior and retention after SDK editing. Complete visual/semantic/accessibility, font policy, engine/dependency review, browser security/fuzz/concurrency/endurance and production host/package qualification remain mandatory. Packing/publication stay blocked.
