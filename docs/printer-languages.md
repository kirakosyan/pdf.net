# Printer-language source profiles

`PdfStaticEps` implements an original closed EPS drawing-data profile in managed code. It interprets only finite literal drawing/state records; no general PostScript VM, procedure evaluation, filesystem/process primitive, font/resource discovery or native interpreter executes. `WritePage`/`WritePageFile` export opaque device Gray/RGB vectors and nested vector Forms through the same bounded source geometry profile as SVG, with `Printer.StaticEps.*` diagnostics. Curves, affine transforms, line styles and compound clipping remain vectors. Text, images, annotations, layers, unsupported color/effects and export hairlines reject before caller writes. No raster fallback occurs.

| Managed static EPS data | Supported table |
| --- | --- |
| Envelope | DSC `%!PS-Adobe-3.0 EPSF-3.0`, initial integer `BoundingBox`, optional contained `HiResBoundingBox`, `EndComments` within64 KiB; declared language levels1/2 only |
| Path | `newpath`, `moveto`, `rmoveto`, `lineto`, `rlineto`, `curveto`, `rcurveto`, `closepath` |
| Paint/clip | `stroke`, `fill`, `eofill`, `clip`, `eoclip` |
| State/transform | balanced `gsave`/`grestore`, `translate`, nonsingular `scale`/`concat`, `rotate`; state scopes/transforms require a cleared current path |
| Color/style | `setgray`, `setrgbcolor`, managed import `setcmykcolor`, `setlinewidth`, `setlinecap`, `setlinejoin`, `setmiterlimit`, `setdash`, literal boolean `setstrokeadjust` |
| Page | one optional final `showpage`; trailing drawing/multiple pages reject |
| Numeric bounds | finite literals of magnitude at most1e9, with nonzero magnitude at least1e-30; operand stack1024, flat arrays64; positive boxes at most14400 points per axis |

Deferred/duplicate boxes, unsupported versions, unfinished paths/states, arbitrary arithmetic/procedures/dictionaries, text/fonts/images, unknown operators and DSC include/embedded/binary directives reject. PostScript default stroke adjustment is retained as an explicit PDF `SA` flag; both true and false states are written. EPS export explicitly disables stroke adjustment to preserve PDF vector intent. The shared vector export bridge accepts a neutral `SA=false` resource without claiming general extended-state support. Lexing uses PostScript ASCII whitespace and handles CR, LF and CRLF comment/header boundaries; Unicode whitespace is not normalized into executable syntax. Comments carry no executable semantics. There is no permissive execution fallback for an unknown managed command.

`PdfPrinterLanguages.Import` is a separate optional host boundary. The caller must provide an `IPdfIsolatedPrinterInterpreter` that explicitly reports the exact requested profile:

| Requested profile | Bundled full interpreter |
| --- | --- |
| PostScript level2 / level3 | None; explicit qualified isolated host required |
| EPS level2 / level3 | None; explicit qualified isolated host required for general EPS; the managed drawing-data API is separate |
| PCL5 / PCL5e / PCL5c / PCL XL | None; explicit qualified isolated host required, without version substitution |

Missing/unsupported hosts fail before input capture. The wrapper captures at most8 MiB input, supplies expiring context-bound leases, stages at most64 MiB PDF and imports it under the same context/page ceiling before returning any model. Caller streams/hosts remain caller-owned; generic host failures are redacted and latched limits/cancellation/strict losses propagate unchanged. A real host must acquire its actual worker scopes and deny arbitrary host files/processes, shell/network access and ambient engine/font/procset discovery in a fresh OS-isolated job. Implementing the interface or passing its labeled boundary fixtures does not prove isolation or interpreter functionality. No full interpreter is silently adopted or downloaded; full version-specific command/font/vector corpora and OS/engine qualification remain mandatory.

The managed profile targets all four frameworks without platform GUI types. Source tests exercise curves/clips/dashes, shifted boxes/relative paths, rejected commands/versions, ownership, limits/cancellation, exact host availability, expired leases, failure redaction and strict/permissive diagnostics. Ghostscript10.05.1 independently renders only our controlled original EPS files; MuPDF checks exact managed round-trip pixels. The same independent Ghostscript renderer must produce identical native EPS/PDF pixels for vectors, shifted boxes and both thin-line stroke-adjustment cases; separate EPS-to-PDF rewrite metrics remain recorded. This is development validation, without an SDK native engine adoption or sandbox qualification. FP-058 remains Planned with all full PS/EPS/PCL and production/package scope mandatory.
