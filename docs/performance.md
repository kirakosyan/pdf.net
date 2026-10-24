# Performance measurement and resource envelopes

`eng/Performance` is a source consumer for .NET 8 and .NET 10. It measures the existing managed eager reader, first-page model availability and atomic full rewrite using original small, 1,000-page, four-page image-bearing and input-limit fixtures. The exact corpus manifest is pinned in `eng/performance-corpus.json`; regeneration must reproduce its hashes. The image fixture is not an OCR benchmark, and the 1,000-page synthetic fixture does not establish performance on complex real documents.

Each run records the observed CPU model, physical memory, logical processors, OS, architecture, runtime, SDK/consumer hashes, source commit/dirty state, corpus hash and measurement-code hashes. Every case has twenty or more independent fresh-process samples and an equal number of samples in one process after three warmups. Cold means a fresh process; the operating-system file cache remains uncontrolled. Measured operation latency excludes process startup and corpus hash verification; separate process records retain end-to-end wall time and exit status.

The runner retains all raw samples and computes nearest-rank p50/p95/p99, operation throughput, process high-water working set, first-page model timing and output bytes. Memory includes runtime, consumer and warmup allocations. SDK expanded-byte counters measure charged work rather than process RSS. First-page model timing includes the eager open and is not a measurement of rendered pixels, selective range transfer, browser layout, conversion or OCR. These distinct production workloads require their own retained measurements and reviewed budgets.

## Reproduce source calibration

Build the solution with the committed lock files. Use fresh output directories; neither the corpus generator nor the consumer replaces an existing directory.

```powershell
dotnet eng/Performance/bin/Release/net10.0/Performance.dll create artifacts/performance-corpus
python eng/performance.py run --corpus artifacts/performance-corpus/corpus.json --assembly eng/Performance/bin/Release/net10.0/Performance.dll --hardware artifacts/hardware.json --output artifacts/performance-net10 --samples 20
```

The explicit hardware JSON contains `cpuModel`, positive `physicalMemoryBytes`, positive `logicalProcessors` and `provenance`. Record actual system observations, retain the file, and use the same reviewed host configuration for comparisons. The runner checks processor-count agreement. A supplied profile alone does not prove host provenance, power policy, thermal state, affinity, background load or virtualization; production review must establish those conditions.

All measured operations retain mandatory SDK budgets: a 64 MiB input ceiling, 256 MiB cumulative expanded work, 64 MiB output ceiling and the standard finite object/pixel/depth/worker/deadline limits. The adversarial fixture deliberately reduces input to one byte and requires `Limit.InputBytes` with no output. Output uses owned files and atomic SDK saves. The eager reader retains bounded input/model state; it does not promise constant-memory streaming or automatic disk spilling. For selective source access, use the explicit revision-bound [progressive APIs](progressive-access.md). Jobs exceeding their configured budgets fail rather than silently raising a ceiling or spilling into an undeclared location.

## Reviewed regression gates

`python eng/performance.py check --report <performance.json> --baseline <reviewed-baseline.json>` checks exact hardware/runtime/corpus/workload/measurement-contract identity, case/state/sample/metric inventories and explicit review metadata. It reopens hash-bound raw observations, verifies retained output bytes, and recomputes every aggregate. Each baseline case carries the measured `metrics` and an explicit `limits` entry for every metric. Latency, memory and output values must meet the absolute ceiling and cannot increase more than 10%; throughput must meet its absolute floor and cannot decrease more than 10%. Changed hardware, changed measurement semantics, missing samples, nonfinite values, altered reports or omitted cases fail. Calibration does not create or approve a baseline. A consequential accepted change requires a separately reviewed baseline and explanation.

The production gate now requires a `performance` suite in every existing release lane. Each suite additionally binds `baselinePath`/`baselineSha256`. The checker requires the current commit, lane and source/package stage, a clean production report and reviewed managed, range, rendering, HTML, conversion and OCR coverage. The original source calibration deliberately cannot satisfy that gate. No new lane replaces an existing minimum-runtime or package-consumer requirement.

Actual local observations and their limits are retained in [FP-088 source evidence](evidence/FP-088-source.json). Reviewed hardware/corpus envelopes, full provider/native memory accounting, actual minimum-runtime consumers, all production lanes and eight-hour endurance remain open. No SDK package is created or published by these tools.
