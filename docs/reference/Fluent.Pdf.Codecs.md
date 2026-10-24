# Fluent.Pdf.Codecs source reference

Version `1.0.0`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).

## Public declarations

The baseline preserves exact nullability, parameter names, defaults and constant values.

```text
Fluent.Pdf.Codecs.IsolatedImageCodecProvider
Fluent.Pdf.Codecs.IsolatedImageCodecProvider.Decode(Fluent.Pdf.PdfImageCodec codec, Fluent.Pdf.PdfImageInfo! info, Fluent.Pdf.PdfValue! imageDictionary, Fluent.Pdf.PdfValue! decodeParameters, System.IO.Stream! encodedInput, System.IO.Stream! sampleOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Codecs.IsolatedImageCodecProvider.Descriptor.get -> Fluent.Pdf.PdfProviderDescriptor!
Fluent.Pdf.Codecs.IsolatedImageCodecProvider.Encode(Fluent.Pdf.PdfImageCodec codec, Fluent.Pdf.PdfImageInfo! info, Fluent.Pdf.PdfValue! parameters, System.IO.Stream! sampleInput, System.IO.Stream! encodedOutput, Fluent.Pdf.PdfOperationContext! context) -> void
Fluent.Pdf.Codecs.IsolatedImageCodecProvider.Inspect(Fluent.Pdf.PdfImageCodec codec, System.IO.Stream! encodedInput, Fluent.Pdf.PdfValue! imageDictionary, Fluent.Pdf.PdfOperationContext! context) -> Fluent.Pdf.PdfImageInfo!
Fluent.Pdf.Codecs.IsolatedImageCodecProvider.IsolatedImageCodecProvider(Fluent.Pdf.Workers.NativeWorkerOptions! worker) -> void
Fluent.Pdf.Codecs.IsolatedImageCodecProvider.Supports(Fluent.Pdf.PdfImageCodec codec, bool encoding) -> bool
const Fluent.Pdf.Codecs.IsolatedImageCodecProvider.BridgeAbi = "fluent-pdf-codecs-1" -> string!
const Fluent.Pdf.Codecs.IsolatedImageCodecProvider.BridgeVersion = "1.0.0" -> string!
```

## Member documentation

### <code>F:Fluent.Pdf.Codecs.IsolatedImageCodecProvider.BridgeAbi</code>

The exact original codec bridge ABI required by this broker.

### <code>F:Fluent.Pdf.Codecs.IsolatedImageCodecProvider.BridgeVersion</code>

The pinned bridge implementation version.

### <code>M:Fluent.Pdf.Codecs.IsolatedImageCodecProvider.#ctor(Fluent.Pdf.Workers.NativeWorkerOptions)</code>

Captures a caller-supplied, hash-pinned offline bridge deployment with thread support explicitly enabled.

### <code>M:Fluent.Pdf.Codecs.IsolatedImageCodecProvider.Decode(Fluent.Pdf.PdfImageCodec,Fluent.Pdf.PdfImageInfo,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfValue,System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Decodes through a process boundary and accepts only a complete, geometry-matched result.

### <code>M:Fluent.Pdf.Codecs.IsolatedImageCodecProvider.Encode(Fluent.Pdf.PdfImageCodec,Fluent.Pdf.PdfImageInfo,Fluent.Pdf.PdfValue,System.IO.Stream,System.IO.Stream,Fluent.Pdf.PdfOperationContext)</code>

Encodes through a process boundary. DCT quality is explicit; JPX and JBIG2 encoding is lossless.

### <code>M:Fluent.Pdf.Codecs.IsolatedImageCodecProvider.Inspect(Fluent.Pdf.PdfImageCodec,System.IO.Stream,Fluent.Pdf.PdfValue,Fluent.Pdf.PdfOperationContext)</code>

Inspects original bounded headers without invoking the pixel decoder.

### <code>M:Fluent.Pdf.Codecs.IsolatedImageCodecProvider.Supports(Fluent.Pdf.PdfImageCodec,System.Boolean)</code>

Reports the three codec algorithms, including lossless JBIG2 encoding using generic MMR regions.

### <code>P:Fluent.Pdf.Codecs.IsolatedImageCodecProvider.Descriptor</code>

Gets the immutable codec capability declaration; production qualification is tracked separately.

### <code>T:Fluent.Pdf.Codecs.IsolatedImageCodecProvider</code>

Offline DCT, JPX and JBIG2 codecs executed in a fresh pinned native worker.

