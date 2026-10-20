# Explicit ICC color operations and production-rendering contracts

FP-041, queue issue #58, has the following source profile. Full requirement scope and production qualification remain mandatory; no issue is marked complete by this source delivery.

`PdfColorRendering.RenderPage` requires an explicit `IPdfColorManagedPageRenderer`; ordinary device-color preview providers are never substituted. Immutable choices capture a complete RGB ICC output profile, ICC intent, overprint simulation and spot rejection/alternate policy. The provider must honor embedded profiles/output intents and explicit print/screen/visibility policy or reject unsupported behavior. Alternate spot conversion requires a loss diagnostic. Exact BGRA extent, alpha, pixels, cancellation and stream lifetimes use the existing shared rendering boundary. No complete color-managed page engine is bundled or qualified by this contract.

`PdfMatrixIccTransform.Transform` is an original usable media-relative RGB matrix/TRC color converter with D50 XYZ PCS. It reads explicit RGB XYZ/TRC tags, validates extents, finite invertible primaries and monotone bounded curve/parametric functions, maps source curves through XYZ and numerically inverts destination curves. Out-of-gamut colors reject instead of silently clipping. LUT-based profiles and unsupported spaces reject. This color operation does not imply that an ordinary renderer has honored embedded document profiles.

Process CMYK/separations, general LUT/perceptual/absolute gamut mapping, black-point compensation, complete overprint/spot/transparency implementations and independent production color-chart/page qualification remain mandatory.

Source validation is recorded in [the queue evidence ledger](evidence/issues-51-60-source.json). All four core target frameworks expose the same API; actual-runtime/final-package limitations remain explicit.
