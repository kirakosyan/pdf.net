# Page canvas and geometry

The managed core supplies `PdfPageGeometry`, `PdfPageBoxes` and `PdfCanvas` on all four library targets. Geometry records native PDF coordinates for MediaBox, CropBox, BleedBox, TrimBox and ArtBox, clockwise viewing rotation and physical points per native `UserUnit`. Explicitly constructed secondary boxes must lie within MediaBox. Opening a source uses the standard effective intersection and reports `Geometry.EffectivePageBox`; unchanged source syntax remains preserved. Resizing proportionally maps all boxes while retaining the origin and scale.

`UserToDisplay` maps the selected native box into rotated, top-left physical point coordinates. `DisplayToUser` is its inverse. Canvas options choose the box, top-left/bottom-left origin, point/inch/millimeter/centimeter/pica/pixel units, explicit pixel DPI and margins in the chosen units. Default canvases clip to their content frame. Nonfinite inputs and degenerate content frames fail before output.

```csharp
var geometry = new PdfPageGeometry(
    new PdfPageBoxes(new PdfRect(-20, 10, 240, 160)),
    PdfPageRotation.Clockwise90);
var path = PdfPath.Create(new[] {
    PdfPathSegment.Rectangle(new PdfRect(0, 0, 30, 15)) });
var canvas = PdfCanvas.Create(geometry, c => {
    using (c.Save()) {
        c.Translate(10, 10);
        c.Rotate(30);
        c.DrawPath(path);
    }
}, new PdfCanvasOptions(PdfLengthUnit.Millimeter,
    margins: PdfMargins.Uniform(5)));
using var document = Pdf.Create().AddPage(geometry);
canvas.ApplyTo(document.Pages[0]);
document.SaveFile("canvas.pdf");
```

The callback is synchronous and confined to its creating thread. Save scopes close in reverse order before return. A retained builder fails after return. Cancellation, elapsed time and cumulative resource limits share one context across path measurement, resource creation and form composition. Catching a latched failure cannot produce a successful result. Completed canvases can be shared by independent jobs.

`DrawPath` supports explicit fill/stroke paints and styles. `DrawDrawing` and `DrawForm` fit an upright vector source frame, including the form's own matrix. `DrawImage` fits an explicitly supplied Image XObject using its declared pixel aspect; decoding and image construction belong to FP-017. Contain, cover and stretch have explicit anchors; cover captures an additional clip. `DrawText` places already shaped immutable text at a baseline in canvas units, with font size in physical typographic points. It shares the canvas context with font embedding. Glyph ink remains conservatively bounded by the font metrics. Logical `ActualText` raises the PDF version floor to 1.4.

Placements retain insertion index, z-order, source frame, transformed quad, axis-aligned bounds, conservative visible bounds and native user bounds. Hit tests inspect the transformed frame, effective CropBox and every captured path clip. They do not test glyph ink or image alpha. Path measurement computes cubic extrema; path fill hit tests use monotone curve intersections with nonzero/even-odd winding and an explicit boundary tolerance. Hairline stroke bounds use the configured DPI conservatively; no fixed device-independent ink envelope is claimed.

Applying an overlay or underlay validates source graphics-state balance, creates a private form/resource scope and commits staged page changes after the final checkpoint. The original encoded content stream bytes remain intact between a separate `q`/`Q` pair. Underlays paint newest first; overlays paint in application order. Replacement explicitly replaces page content. The canvas must match the page geometry at application time; later page edits do not mutate a completed canvas or snapshot. Content assignment clears pending layers while retaining resource bindings. Large canvases batch private resources at 4096 bindings per form; a page can hold at most 4096 explicit bindings.

The C#/VB `WriteCanvasPdf` consumers exercise this source contract. Independent development PDFs cover all four rotations, nonzero origins, two user scales, every standard box, transformed paths, image orientation and text placement. Source checks and raster comparisons do not qualify complete geometry, minimum runtimes, final packages or the production fidelity/security/endurance gates. NuGet packing and publication remain blocked.
