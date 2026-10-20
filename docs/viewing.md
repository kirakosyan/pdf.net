# Headless viewing sessions and exact tiles

FP-042, queue issue #60, has the following source profile. Full requirement scope and production qualification remain mandatory; no issue is marked complete by this source delivery.

`PdfViewSession` captures one immutable document snapshot and caller-owned renderer. Each async render has a shared-context operation policy and linked cancellation token. A finite outstanding-request ceiling rejects overload before queuing another operation. `CancelPending` cancels only current requests; later requests receive fresh tokens. Cancellation callbacks run outside the cache lock. Dispose cancels current work and drops cache references without disposing caller providers.

The byte-capped LRU keeps immutable raster samples only; overlarge rasters are returned without caching. Eviction/clear/disposal do not invalidate samples already returned to callers. In-flight operations and caller-retained rasters have separate ownership and per-operation budgets. Cache keys include page, complete render options and exact integer tile windows.

`PlanTiles` uses the renderer's full rotated CropBox/UserUnit grid and carries exact integer windows into requests. Floating-point region snapping cannot create tile gaps/overlaps. `RenderThumbnailAsync` fits a full-page grid to explicit pixel ceilings. `DisplayToUser`, `SelectText` and `HitTest` use displayed physical points and preserve extracted character/operation provenance. Selection uses advance-cell bounds, not glyph-ink contours, and does not trigger OCR. Existing accessibility inspection is separately available to applications; no GUI dependency enters core.

True progressive/incremental input loading, general accessibility/view integration, asynchronous renderer supervision and production scroll/zoom/selection/performance/concurrency qualification remain mandatory. The ordinary syntax reader still eagerly captures input.

Source validation is recorded in [the queue evidence ledger](evidence/issues-51-60-source.json). All four core target frameworks expose the same API; actual-runtime/final-package limitations remain explicit.
