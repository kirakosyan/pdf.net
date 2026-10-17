# Verified GitHub implementation backlog

Created and round-trip verified 112 unassigned requirement issues: 111 open SDK production tasks and the completed Reader placeholder. The separate [roadmap issue #113](https://github.com/kirakosyan/pdf.net/issues/113) gives the production milestones and assignment sequence.

Every SDK task has prerequisite issue links, feature-specific scope/acceptance/validation, non-goals, area labels and the 1.0.0 Production SDK milestone. The first stable release requires the complete SDK scope.

[Machine-readable mapping](../specs/github-issues.json) · [Dependency order](implementation-plan.md) · [Production gates](production-quality.md)

| Order | Requirement | GitHub issue | State |
| --- | --- | --- | --- |
| 1 | [FP-001](requirements/FP-001.md) | [#1: [FP-001] Public API and provider contracts](https://github.com/kirakosyan/pdf.net/issues/1) | OPEN |
| 2 | [FP-002](requirements/FP-002.md) | [#2: [FP-002] Operation policies, diagnostics and resource budgets](https://github.com/kirakosyan/pdf.net/issues/2) | OPEN |
| 3 | [FP-085](requirements/FP-085.md) | [#3: [FP-085] Native provider deployment and isolation](https://github.com/kirakosyan/pdf.net/issues/3) | OPEN |
| 4 | [FP-003](requirements/FP-003.md) | [#4: [FP-003] PDF syntax and cross-reference reader](https://github.com/kirakosyan/pdf.net/issues/4) | OPEN |
| 5 | [FP-004](requirements/FP-004.md) | [#5: [FP-004] Stream filters, object streams and codecs](https://github.com/kirakosyan/pdf.net/issues/5) | OPEN |
| 6 | [FP-005](requirements/FP-005.md) | [#6: [FP-005] PDF writer and incremental transactions](https://github.com/kirakosyan/pdf.net/issues/6) | OPEN |
| 7 | [FP-007](requirements/FP-007.md) | [#7: [FP-007] Content operators and PDF version negotiation](https://github.com/kirakosyan/pdf.net/issues/7) | OPEN |
| 8 | [FP-008](requirements/FP-008.md) | [#8: [FP-008] Fonts, embedding, subsetting and fallback](https://github.com/kirakosyan/pdf.net/issues/8) | OPEN |
| 9 | [FP-009](requirements/FP-009.md) | [#9: [FP-009] Unicode, bidirectional text and shaping](https://github.com/kirakosyan/pdf.net/issues/9) | OPEN |
| 10 | [FP-010](requirements/FP-010.md) | [#10: [FP-010] Vector graphics, color and transparency](https://github.com/kirakosyan/pdf.net/issues/10) | OPEN |
| 11 | [FP-011](requirements/FP-011.md) | [#11: [FP-011] Page canvas and geometry](https://github.com/kirakosyan/pdf.net/issues/11) | OPEN |
| 12 | [FP-012](requirements/FP-012.md) | [#12: [FP-012] Flow layout and pagination](https://github.com/kirakosyan/pdf.net/issues/12) | OPEN |
| 13 | [FP-013](requirements/FP-013.md) | [#13: [FP-013] Tables and data binding](https://github.com/kirakosyan/pdf.net/issues/13) | OPEN |
| 14 | [FP-014](requirements/FP-014.md) | [#14: [FP-014] Lists, columns and document composition](https://github.com/kirakosyan/pdf.net/issues/14) | OPEN |
| 15 | [FP-015](requirements/FP-015.md) | [#15: [FP-015] Headers, footers, page numbers and contents](https://github.com/kirakosyan/pdf.net/issues/15) | OPEN |
| 16 | [FP-016](requirements/FP-016.md) | [#16: [FP-016] Page organization and imposition](https://github.com/kirakosyan/pdf.net/issues/16) | OPEN |
| 17 | [FP-017](requirements/FP-017.md) | [#17: [FP-017] Images and reusable page graphics](https://github.com/kirakosyan/pdf.net/issues/17) | OPEN |
| 18 | [FP-019](requirements/FP-019.md) | [#18: [FP-019] Annotation model and appearance streams](https://github.com/kirakosyan/pdf.net/issues/18) | OPEN |
| 19 | [FP-020](requirements/FP-020.md) | [#19: [FP-020] Links, destinations, actions and navigation](https://github.com/kirakosyan/pdf.net/issues/19) | OPEN |
| 20 | [FP-021](requirements/FP-021.md) | [#20: [FP-021] Outlines, page labels and viewer preferences](https://github.com/kirakosyan/pdf.net/issues/20) | OPEN |
| 21 | [FP-022](requirements/FP-022.md) | [#21: [FP-022] Metadata and document identity](https://github.com/kirakosyan/pdf.net/issues/21) | OPEN |
| 22 | [FP-023](requirements/FP-023.md) | [#22: [FP-023] Attachments, portfolios and associated files](https://github.com/kirakosyan/pdf.net/issues/22) | OPEN |
| 23 | [FP-024](requirements/FP-024.md) | [#23: [FP-024] Optional-content layers](https://github.com/kirakosyan/pdf.net/issues/23) | OPEN |
| 24 | [FP-025](requirements/FP-025.md) | [#24: [FP-025] Text extraction with layout geometry](https://github.com/kirakosyan/pdf.net/issues/24) | OPEN |
| 25 | [FP-036](requirements/FP-036.md) | [#25: [FP-036] AcroForm creation and editing](https://github.com/kirakosyan/pdf.net/issues/25) | OPEN |
| 26 | [FP-037](requirements/FP-037.md) | [#26: [FP-037] Form filling, data exchange and flattening](https://github.com/kirakosyan/pdf.net/issues/26) | OPEN |
| 27 | [FP-040](requirements/FP-040.md) | [#27: [FP-040] PDF page rendering](https://github.com/kirakosyan/pdf.net/issues/27) | OPEN |
| 28 | [FP-031](requirements/FP-031.md) | [#28: [FP-031] Password and certificate encryption](https://github.com/kirakosyan/pdf.net/issues/28) | OPEN |
| 29 | [FP-066](requirements/FP-066.md) | [#29: [FP-066] PDF/A archival profiles](https://github.com/kirakosyan/pdf.net/issues/29) | OPEN |
| 30 | [FP-006](requirements/FP-006.md) | [#30: [FP-006] Repair, recovery and structural validation](https://github.com/kirakosyan/pdf.net/issues/30) | OPEN |
| 31 | [FP-098](requirements/FP-098.md) | [#31: [FP-098] PDF 2.0 and extension preservation](https://github.com/kirakosyan/pdf.net/issues/31) | OPEN |
| 32 | [FP-044](requirements/FP-044.md) | [#32: [FP-044] HTML, CSS and JavaScript to PDF](https://github.com/kirakosyan/pdf.net/issues/32) | OPEN |
| 33 | [FP-045](requirements/FP-045.md) | [#33: [FP-045] Resource loading, authentication and network policy](https://github.com/kirakosyan/pdf.net/issues/33) | OPEN |
| 34 | [FP-046](requirements/FP-046.md) | [#34: [FP-046] Dynamic-page readiness and browser lifecycle](https://github.com/kirakosyan/pdf.net/issues/34) | OPEN |
| 35 | [FP-047](requirements/FP-047.md) | [#35: [FP-047] Print CSS, HTML pagination and accessibility](https://github.com/kirakosyan/pdf.net/issues/35) | OPEN |
| 36 | [FP-018](requirements/FP-018.md) | [#36: [FP-018] Watermarks, stamps and artifacts](https://github.com/kirakosyan/pdf.net/issues/36) | OPEN |
| 37 | [FP-028](requirements/FP-028.md) | [#37: [FP-028] Graphics, image, font and artifact extraction](https://github.com/kirakosyan/pdf.net/issues/37) | OPEN |
| 38 | [FP-026](requirements/FP-026.md) | [#38: [FP-026] Search, regex and text replacement](https://github.com/kirakosyan/pdf.net/issues/38) | OPEN |
| 39 | [FP-027](requirements/FP-027.md) | [#39: [FP-027] Table and structured-data extraction](https://github.com/kirakosyan/pdf.net/issues/39) | OPEN |
| 40 | [FP-099](requirements/FP-099.md) | [#40: [FP-099] Document event hooks and data-source generation](https://github.com/kirakosyan/pdf.net/issues/40) | OPEN |
| 41 | [FP-050](requirements/FP-050.md) | [#41: [FP-050] Text, Markdown and structured input](https://github.com/kirakosyan/pdf.net/issues/41) | OPEN |
| 42 | [FP-053](requirements/FP-053.md) | [#42: [FP-053] Word document conversion adapter](https://github.com/kirakosyan/pdf.net/issues/42) | OPEN |
| 43 | [FP-112](requirements/FP-112.md) | [#43: [FP-112] Document templates and mail merge](https://github.com/kirakosyan/pdf.net/issues/43) | OPEN |
| 44 | [FP-067](requirements/FP-067.md) | [#44: [FP-067] Tagged PDF and PDF/UA accessibility](https://github.com/kirakosyan/pdf.net/issues/44) | OPEN |
| 45 | [FP-074](requirements/FP-074.md) | [#45: [FP-074] Local OCR provider and wordnet reuse](https://github.com/kirakosyan/pdf.net/issues/45) | OPEN |
| 46 | [FP-075](requirements/FP-075.md) | [#46: [FP-075] Multilingual OCR and language models](https://github.com/kirakosyan/pdf.net/issues/46) | OPEN |
| 47 | [FP-076](requirements/FP-076.md) | [#47: [FP-076] Searchable PDF and structured OCR output](https://github.com/kirakosyan/pdf.net/issues/47) | OPEN |
| 48 | [FP-077](requirements/FP-077.md) | [#48: [FP-077] Scan preprocessing and reading order](https://github.com/kirakosyan/pdf.net/issues/48) | OPEN |
| 49 | [FP-030](requirements/FP-030.md) | [#49: [FP-030] True redaction and comprehensive sanitization](https://github.com/kirakosyan/pdf.net/issues/49) | OPEN |
| 50 | [FP-032](requirements/FP-032.md) | [#50: [FP-032] Digital signatures and certification](https://github.com/kirakosyan/pdf.net/issues/50) | OPEN |
| 51 | [FP-033](requirements/FP-033.md) | [#51: [FP-033] Signature verification and trust decisions](https://github.com/kirakosyan/pdf.net/issues/51) | OPEN |
| 52 | [FP-034](requirements/FP-034.md) | [#52: [FP-034] HSM and external signing providers](https://github.com/kirakosyan/pdf.net/issues/52) | OPEN |
| 53 | [FP-035](requirements/FP-035.md) | [#53: [FP-035] PAdES, timestamps and long-term validation](https://github.com/kirakosyan/pdf.net/issues/53) | OPEN |
| 54 | [FP-102](requirements/FP-102.md) | [#54: [FP-102] Signature-preserving editing and revision audit](https://github.com/kirakosyan/pdf.net/issues/54) | OPEN |
| 55 | [FP-029](requirements/FP-029.md) | [#55: [FP-029] Document comparison and change reports](https://github.com/kirakosyan/pdf.net/issues/55) | OPEN |
| 56 | [FP-038](requirements/FP-038.md) | [#56: [FP-038] XFA forms and conversion](https://github.com/kirakosyan/pdf.net/issues/56) | OPEN |
| 57 | [FP-039](requirements/FP-039.md) | [#57: [FP-039] Document JavaScript and sandboxed calculations](https://github.com/kirakosyan/pdf.net/issues/57) | OPEN |
| 58 | [FP-041](requirements/FP-041.md) | [#58: [FP-041] Color-managed production rendering](https://github.com/kirakosyan/pdf.net/issues/58) | OPEN |
| 59 | [FP-064](requirements/FP-064.md) | [#59: [FP-064] PDF raster export and image containers](https://github.com/kirakosyan/pdf.net/issues/59) | OPEN |
| 60 | [FP-042](requirements/FP-042.md) | [#60: [FP-042] Headless viewing primitives](https://github.com/kirakosyan/pdf.net/issues/60) | OPEN |
| 61 | [FP-043](requirements/FP-043.md) | [#61: [FP-043] Cross-platform printing](https://github.com/kirakosyan/pdf.net/issues/61) | OPEN |
| 62 | [FP-048](requirements/FP-048.md) | [#62: [FP-048] Web framework and XAML adapters](https://github.com/kirakosyan/pdf.net/issues/62) | OPEN |
| 63 | [FP-049](requirements/FP-049.md) | [#63: [FP-049] Isolated rendering service and container deployment](https://github.com/kirakosyan/pdf.net/issues/63) | OPEN |
| 64 | [FP-051](requirements/FP-051.md) | [#64: [FP-051] Raster documents to PDF](https://github.com/kirakosyan/pdf.net/issues/64) | OPEN |
| 65 | [FP-052](requirements/FP-052.md) | [#65: [FP-052] SVG import and vector export](https://github.com/kirakosyan/pdf.net/issues/65) | OPEN |
| 66 | [FP-054](requirements/FP-054.md) | [#66: [FP-054] Spreadsheet and tabular conversion](https://github.com/kirakosyan/pdf.net/issues/66) | OPEN |
| 67 | [FP-055](requirements/FP-055.md) | [#67: [FP-055] Presentation conversion](https://github.com/kirakosyan/pdf.net/issues/67) | OPEN |
| 68 | [FP-056](requirements/FP-056.md) | [#68: [FP-056] PDF to HTML and semantic web output](https://github.com/kirakosyan/pdf.net/issues/68) | OPEN |
| 69 | [FP-057](requirements/FP-057.md) | [#69: [FP-057] EPUB and electronic-book conversion](https://github.com/kirakosyan/pdf.net/issues/69) | OPEN |
| 70 | [FP-058](requirements/FP-058.md) | [#70: [FP-058] PostScript, EPS and printer-language import](https://github.com/kirakosyan/pdf.net/issues/70) | OPEN |
| 71 | [FP-059](requirements/FP-059.md) | [#71: [FP-059] XPS and OpenXPS conversion](https://github.com/kirakosyan/pdf.net/issues/71) | OPEN |
| 72 | [FP-060](requirements/FP-060.md) | [#72: [FP-060] TeX and LaTeX conversion](https://github.com/kirakosyan/pdf.net/issues/72) | OPEN |
| 73 | [FP-061](requirements/FP-061.md) | [#73: [FP-061] CGM and platform metafile conversion](https://github.com/kirakosyan/pdf.net/issues/73) | OPEN |
| 74 | [FP-062](requirements/FP-062.md) | [#74: [FP-062] XSL-FO and XML transformation](https://github.com/kirakosyan/pdf.net/issues/74) | OPEN |
| 75 | [FP-063](requirements/FP-063.md) | [#75: [FP-063] OFD fixed-layout conversion](https://github.com/kirakosyan/pdf.net/issues/75) | OPEN |
| 76 | [FP-065](requirements/FP-065.md) | [#76: [FP-065] Conversion orchestration and format discovery](https://github.com/kirakosyan/pdf.net/issues/76) | OPEN |
| 77 | [FP-068](requirements/FP-068.md) | [#77: [FP-068] PDF/X prepress profiles](https://github.com/kirakosyan/pdf.net/issues/77) | OPEN |
| 78 | [FP-069](requirements/FP-069.md) | [#78: [FP-069] PDF/E engineering and advanced content](https://github.com/kirakosyan/pdf.net/issues/78) | OPEN |
| 79 | [FP-070](requirements/FP-070.md) | [#79: [FP-070] Electronic invoices and associated XML](https://github.com/kirakosyan/pdf.net/issues/79) | OPEN |
| 80 | [FP-071](requirements/FP-071.md) | [#80: [FP-071] Preflight, remediation and audit reports](https://github.com/kirakosyan/pdf.net/issues/80) | OPEN |
| 81 | [FP-072](requirements/FP-072.md) | [#81: [FP-072] Optimization, compression and deduplication](https://github.com/kirakosyan/pdf.net/issues/81) | OPEN |
| 82 | [FP-073](requirements/FP-073.md) | [#82: [FP-073] Linearization and progressive access](https://github.com/kirakosyan/pdf.net/issues/82) | OPEN |
| 83 | [FP-078](requirements/FP-078.md) | [#83: [FP-078] Barcode and QR document workflows](https://github.com/kirakosyan/pdf.net/issues/83) | OPEN |
| 84 | [FP-079](requirements/FP-079.md) | [#84: [FP-079] Opt-in document intelligence providers](https://github.com/kirakosyan/pdf.net/issues/84) | OPEN |
| 85 | [FP-080](requirements/FP-080.md) | [#85: [FP-080] Semantic chunking and retrieval adapters](https://github.com/kirakosyan/pdf.net/issues/85) | OPEN |
| 86 | [FP-081](requirements/FP-081.md) | [#86: [FP-081] Windows COM interoperability](https://github.com/kirakosyan/pdf.net/issues/86) | OPEN |
| 87 | [FP-082](requirements/FP-082.md) | [#87: [FP-082] Application integrations and viewer controls](https://github.com/kirakosyan/pdf.net/issues/87) | OPEN |
| 88 | [FP-083](requirements/FP-083.md) | [#88: [FP-083] Command-line and batch automation](https://github.com/kirakosyan/pdf.net/issues/88) | OPEN |
| 89 | [FP-084](requirements/FP-084.md) | [#89: [FP-084] Developer documentation and executable examples](https://github.com/kirakosyan/pdf.net/issues/89) | OPEN |
| 90 | [FP-086](requirements/FP-086.md) | [#90: [FP-086] Runtime and architecture qualification](https://github.com/kirakosyan/pdf.net/issues/90) | OPEN |
| 91 | [FP-087](requirements/FP-087.md) | [#91: [FP-087] Async processing, concurrency and cancellation](https://github.com/kirakosyan/pdf.net/issues/91) | OPEN |
| 92 | [FP-088](requirements/FP-088.md) | [#92: [FP-088] Performance, streaming and resource envelopes](https://github.com/kirakosyan/pdf.net/issues/92) | OPEN |
| 93 | [FP-089](requirements/FP-089.md) | [#93: [FP-089] Security, fuzzing and malformed-content robustness](https://github.com/kirakosyan/pdf.net/issues/93) | OPEN |
| 94 | [FP-090](requirements/FP-090.md) | [#94: [FP-090] Independent fidelity and interoperability corpus](https://github.com/kirakosyan/pdf.net/issues/94) | OPEN |
| 95 | [FP-091](requirements/FP-091.md) | [#95: [FP-091] Deterministic output, caching and tenancy](https://github.com/kirakosyan/pdf.net/issues/95) | OPEN |
| 96 | [FP-092](requirements/FP-092.md) | [#96: [FP-092] Observability and support diagnostics](https://github.com/kirakosyan/pdf.net/issues/96) | OPEN |
| 97 | [FP-093](requirements/FP-093.md) | [#97: [FP-093] Reproducible NuGet packages and API compatibility](https://github.com/kirakosyan/pdf.net/issues/97) | OPEN |
| 98 | [FP-095](requirements/FP-095.md) | [#98: [FP-095] Dependency licensing and supply-chain inventory](https://github.com/kirakosyan/pdf.net/issues/98) | OPEN |
| 99 | [FP-096](requirements/FP-096.md) | [#99: [FP-096] PDF Reader placeholder boundary](https://github.com/kirakosyan/pdf.net/issues/99) | CLOSED |
| 100 | [FP-097](requirements/FP-097.md) | [#100: [FP-097] Production document workflow acceptance](https://github.com/kirakosyan/pdf.net/issues/100) | OPEN |
| 101 | [FP-100](requirements/FP-100.md) | [#101: [FP-100] Document collections, packages and interchange](https://github.com/kirakosyan/pdf.net/issues/101) | OPEN |
| 102 | [FP-101](requirements/FP-101.md) | [#102: [FP-101] Accessibility remediation and semantic authoring](https://github.com/kirakosyan/pdf.net/issues/102) | OPEN |
| 103 | [FP-103](requirements/FP-103.md) | [#103: [FP-103] Image processing and document cleanup](https://github.com/kirakosyan/pdf.net/issues/103) | OPEN |
| 104 | [FP-104](requirements/FP-104.md) | [#104: [FP-104] Business data extraction and export schemas](https://github.com/kirakosyan/pdf.net/issues/104) | OPEN |
| 105 | [FP-105](requirements/FP-105.md) | [#105: [FP-105] Cross-platform native distribution packages](https://github.com/kirakosyan/pdf.net/issues/105) | OPEN |
| 106 | [FP-106](requirements/FP-106.md) | [#106: [FP-106] Compatibility policy and long-term maintenance](https://github.com/kirakosyan/pdf.net/issues/106) | OPEN |
| 107 | [FP-107](requirements/FP-107.md) | [#107: [FP-107] DjVu and CDR document import](https://github.com/kirakosyan/pdf.net/issues/107) | OPEN |
| 108 | [FP-108](requirements/FP-108.md) | [#108: [FP-108] PDF to Markdown and text interchange](https://github.com/kirakosyan/pdf.net/issues/108) | OPEN |
| 109 | [FP-109](requirements/FP-109.md) | [#109: [FP-109] Task-oriented facades and plugin composition](https://github.com/kirakosyan/pdf.net/issues/109) | OPEN |
| 110 | [FP-110](requirements/FP-110.md) | [#110: [FP-110] HTML element and block extraction](https://github.com/kirakosyan/pdf.net/issues/110) | OPEN |
| 111 | [FP-111](requirements/FP-111.md) | [#111: [FP-111] Hyphenation and advanced text-fit controls](https://github.com/kirakosyan/pdf.net/issues/111) | OPEN |
| 112 | [FP-094](requirements/FP-094.md) | [#112: [FP-094] Production release and Trusted Publishing](https://github.com/kirakosyan/pdf.net/issues/112) | OPEN |

The first assignments are #1 (public contracts), #2 (operation/resource policies), #3 (native deployment contracts), then the linked dependency order. The first adoption checkpoint is documented in the roadmap and implementation plan. Reader product implementation is deferred.
