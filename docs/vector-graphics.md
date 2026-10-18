# Vector graphics, color and transparency

FP-010 has an original managed source implementation. It writes standard vector/resource data and preserves explicit color and compositing choices. It introduces no rendering, GUI, native color-management or platform dependency in the core. The same public contracts target net472, netstandard2.0, net8.0 and net10.0. Complete production qualification and NuGet candidate/publication remain blocked.

`PdfPoint`, `PdfRect` and `PdfMatrix` use finite PDF Cartesian user coordinates. Rectangles have nonnegative extents; forms and pattern cells require positive bounds. Matrix multiplication documents its order, and inversion rejects singular/nonfinite results. `PdfPath` captures move/line/cubic/close/rectangle segments, requires an actual current point and supports immutable range edits. These are vector operators rather than sampled geometry.

`PdfDrawing.Create` captures a self-contained program and explicit `PdfResourceSet`, validates named resources, checks current fill/stroke color-channel and pattern state across q/Q, and adds a private graphics-state scope. It never reads installed fonts or executes actions. Raw `PdfContentProgram` remains the lower-level inspection/preservation boundary for editing original page content; a new private drawing requires every named resource in its scope. Unknown/malformed color-space profiles and mismatched color operands fail explicitly.

`PdfGraphicsResource` captures immutable authored resource graphs. Nested streams become indirect objects at save time, and reused resource/font instances are shared across forms and pages. Detached authored definitions cannot contain foreign object references. Advanced definitions require an explicit version floor; typed factories add their known feature requirements. `PdfPage.SetResource` replaces one named resource within its category. Font resources delegate to the existing font binding. `RemoveResource` removes the replacement and retains an original binding when present. Snapshots retain immutable bindings after subsequent edits/disposal; saves merge replacements with original inherited resources.

```csharp
var path = PdfPath.Create(new[]
{
    PdfPathSegment.Move(20, 20),
    PdfPathSegment.Curve(40, 100, 120, 100, 160, 20),
    PdfPathSegment.Close()
});
var operations = new List<PdfContentOperation>
{
    new(PdfOperator.FillRgb, PdfValues.Real(.2), PdfValues.Real(.4), PdfValues.Real(.6))
};
operations.AddRange(path.Operations);
operations.Add(new(PdfOperator.FillStroke));
var drawing = PdfDrawing.Create(operations);
var form = PdfGraphicsObjects.Form(drawing, new PdfRect(0, 0, 200, 120));
document.Pages[0].SetResource("Art", form);
document.Pages[0].Content = PdfContentProgram.Create(new[]
{
    new PdfContentOperation(PdfOperator.SaveState),
    new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Art")),
    new PdfContentOperation(PdfOperator.RestoreState)
});
```

`PdfColorSpace` supplies device Gray/RGB/CMYK, calibrated Gray/RGB, Lab, ICCBased, Separation, DeviceN and colored/uncolored Pattern spaces. `PdfColor` captures matching finite channels and explicit ranges. Tint transforms retain exact function data; DeviceN checks channel counts, distinct colorant names, reserved All and repeated None semantics. DeviceN above eight channels requires PDF 1.6. No color conversion or machine profile selection occurs. Advanced NChannel attributes and other color profiles can be authored through explicit raw resource definitions with the caller's version declaration; complete profile qualification remains required.

`Icc` eagerly snapshots borrowed bytes and validates header/version/channel/class/PCS/D50 fields and bounded tag extents, alignment, uniqueness, permitted exact sharing and a complete input-transform declaration. It preserves the original profile bytes and a compatible explicit alternate. Unsupported profiles fail without SDK substitution, including under permissive processing. Header/table checks are not a full ICC transform validator. The SDK neither runs an ICC engine here nor claims every profile's visual behavior. Independent LittleCMS checks validate the small original/generated fixtures.

ICC version floors follow conservative normative version mapping: header 2.0/2.1 requires PDF 1.3, 2.2/2.3 requires 1.4, 2.4 requires 1.5; 4.0 requires 1.5, 4.1 requires 1.6 and 4.2 requires 1.7. Later minor versions use PDF 2.0, whose normative writing guidance permits later ICC profiles while requiring reader support through 4.3. Version requirements propagate through alternate spaces, patterns, shadings and blending groups. These facts derive from ISO 32000-1 Table 67 and ISO 32000-2 clause 8.6.5.5 with approved errata; the source IDs/digests are retained in qualification research rather than importing specification text or implementation bodies.

`PdfFunction` authors exponential, sampled and piecewise interpolation/tint data. Sample planes must exactly cover their declared dimensional grid and are copied after size/overflow and budget checks. Arbitrary document functions are not executed. `PdfShading` authors functional, axial, radial, free-form triangle, lattice triangle, Coons and tensor-product shadings. Mesh streams retain exact packed bytes and decode pairs. Original framing checks bound vertex/patch continuity and reject truncation; the SDK performs no coordinate quantization or rasterization. All shadings negotiate at least PDF 1.3.

`PdfPattern.Tiling` retains explicit bounds, steps, tiling policy, matrix and private resource scope. Uncolored cells reject color selection and non-stencil image painting, including nested forms. Standard outline text is supported; authored Type 3 use in an uncolored cell requires an explicitly validated recursive glyph-resource profile and fails here. Opaque nested resource scopes fail rather than guessing. `PdfPattern.Shading` captures a shading and optional exact compositing state. Colored/uncolored paint operands must match their Pattern color space.

`PdfGraphicsState` retains standard separable/nonseparable blend modes, independent fill/stroke alpha, alpha-as-shape, text knockout, overprint flags/mode and a soft mask. Unknown typed blend names fail strictly; explicit permissive processing records `Graphics.UnsupportedBlendMode` and substitution by Normal. Advanced resource definitions retain unknown names only with an unsupported-content diagnostic under permissive processing. No unsupported blend is silently accepted as qualified behavior.

`PdfTransparencyGroup` emits an explicit Form XObject group with blending space, isolation and knockout flags. `PdfSoftMask` derives alpha or luminosity from that vector group, with an explicit compatible luminosity backdrop and optional unary transfer function. The writer emits actual group/mask resources without flattening them to images. Forms preserve their private resources and bounds/matrix; layout and page coordinate conversion use the canvas requirement.

Source tests cover immutable paths, matrix arithmetic, explicit color channels, ICC preservation/errors, interpolation grids, mesh packets/truncation, blend losses, pattern restrictions, soft masks/groups, named-resource scope, version floors, snapshot/source merging, resource sharing, cancellation/budgets and independent jobs. Independently authored Python reference PDFs and Poppler compare device/calibrated/ICC/spot/DeviceN colors, gradients/meshes, patterns, blend modes, masks/groups, curves and clipping. Full color/profile/compositing corpus, standards/fidelity, actual minimum runtimes, final packages and long security/fuzz/endurance gates remain mandatory.
