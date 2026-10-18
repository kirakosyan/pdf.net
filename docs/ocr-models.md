# Explicit multilingual OCR models and accuracy

FP-075 has a source profile. Complete multilingual/script corpora, native-engine/RID qualification and final packages remain mandatory. NuGet packing and publication stay blocked.

Supply immutable PdfOcrLanguageModel declarations containing a flat lowercase native alias, exact version, SHA-256, byte length, license/source identity and declared scripts/language tags. PdfOcrModelCatalog captures1–32 unique models. Select plus-separated aliases explicitly, in caller order; missing or duplicate aliases reject. A script model can use a flat alias with declared scripts and an empty language list. No directory scan, download, system-model discovery, language detection or fallback occurs. Metadata declarations do not prove coverage or legal qualification.

Pass the catalog explicitly to IsolatedTesseractOcr. Each declaration must match an exact data asset in the caller's native deployment. Private staging verifies the actual bytes; missing assets preserve Native.AssetMissing and the filename. Native initialization must report exactly the requested loaded-language set, even when the engine returns successful initialization after loading only part of a mixed request. Ocr.ModelLanguageBinding rejects partial or extra models. The original two-argument provider constructor retains its English-only profile; its results report that complete model metadata was unavailable.

PdfOcrResult.Models preserves selected immutable declarations. ModelIdentity binds ordered aliases, versions, content digests and byte lengths with length-prefixed invariant encoding. MeanConfidence and word confidence remain engine estimates, separate from observed accuracy. Core remains independent of the optional native adapter.

PdfOcrAccuracy.Evaluate measures finite labeled samples against prior, explicit per-language thresholds. It groups by exact language and model identity, keeping model versions separate. CER compares exact Unicode scalars; WER compares ordinal words delimited by the fixed Unicode15.1 White_Space list. There is no hidden normalization, case folding, trimming or confidence filtering. Insertions count, including on blank references; a group with no reference characters or words cannot pass. Measured ratios may exceed1. Digests bind ordered case IDs, languages, model identities and exact reference/recognized strings. The root context bounds input, allocation, enumeration and comparison work and preserves cancellation. Passing a threshold is an observation, not model or SDK qualification.

The retained original three-case subset uses explicitly provisioned tessdata_fast4.1.0 English and German models from neutral source S75-MODELS-01, commit65727574dfcd264acbb0c3e07860e4e9e9b22185, Apache-2.0:

| Alias | Declared script/language | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| eng | Latn/en | 4113088 | 7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2 |
| deu | Latn/de | 1525436 | 19d219bbb6672c869d20a9636c6816a81eb9a71796cb93ebe0cb1530e2cdb22d |

The actual Tesseract5.5.0 bridge observation passed the clean English and German cases separately. Mixed eng+deu lost an umlaut: CER1/30 and WER1/4 exceeded the prior1%/3% limits despite mean confidence94. That failed check, its original unchanged checker, labels, models and outputs remain retained. The measurement collector reports the failure without altering acceptance. This is a direct C ABI tool observation, not isolated SDK-host or independent-recognizer qualification; the local kernel lacks Landlock. No model or engine binary is distributed in Git or SDK packages.

C# and VB consumers select models and measure a labeled corpus on all SDK TFMs. Independent source jobs remain isolated; no model cache, credentials or temporary directory is global. Exact final-package consumers await the mandatory production gates. See [source evidence](evidence/FP-075-source.json), [model observations](evidence/FP-075-independent.json), [local OCR](local-ocr.md) and [production gates](production-quality.md).
