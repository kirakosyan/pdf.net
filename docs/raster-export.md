# Bounded lossless raster containers

FP-064, queue issue #59, has the following source profile. Full requirement scope and production qualification remain mandatory; no issue is marked complete by this source delivery.

`PdfRasterExport.Write` and `WriteFile` render selected pages/regions in exact caller order using one context and one raster frame at a time. Complete encoded output is privately staged within cumulative expanded/output budgets; file replacement is atomic and caller streams remain open. Staging is bounded in-memory storage, not an unbounded disk spool.

Implemented original containers are eight-bit RGBA PNG, opaque24-bit BMP, and uncompressed classic multipage RGBA TIFF. PNG/TIFF preserve straight unassociated alpha; BMP requires an explicit opaque matte and rejects ICC output. All include effective physical resolution after integer page-grid rounding. PNG/TIFF embed the exact optional RGB output profile, requiring the color-managed rendering service. PNG compresses scanlines through the existing bounded zlib encoder. TIFF writes known-size frame directories sequentially without backpatching or retaining all decoded frames. Classic32-bit offsets and at most65535 frames are enforced. PNG/BMP require one selected page; TIFF permits ordered repeated pages.

Explicit render options cover DPI, region, alpha/matte, RGB/gray, annotations and print/layer usage. No quantization or lossy conversion is hidden. JPEG/GIF/WebP, bitonal/fax and additional TIFF compression/precision profiles remain mandatory separate work; requesting an undefined container rejects. Independent Pillow and LibTiff decoding cover the source fixtures, not complete image/container/security qualification.

Source validation is recorded in [the queue evidence ledger](evidence/issues-51-60-source.json). All four core target frameworks expose the same API; actual-runtime/final-package limitations remain explicit.
