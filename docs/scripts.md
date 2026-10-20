# Passive scripts and opt-in bounded calculations

FP-039, queue issue #57, has the following source profile. Full requirement scope and production qualification remain mandatory; no issue is marked complete by this source delivery.

`PdfScripts.Inspect` reports active JavaScript definitions and known trigger attachments with physical object/path provenance. Strings and bounded script streams are decoded without execution. `SetDocumentScript` explicitly adds, replaces or removes one named script while preserving other name trees. Removing an attachment is not secure sanitization: orphaned definitions and earlier revisions can remain inspectable. Existing action/form APIs continue to author/remove other action slots.

`EvaluateCalculation` is disabled unless enableExecution is explicitly true. Its original closed interpreter accepts only `event.value =` followed by finite decimal literals, unary/binary arithmetic, parentheses and exact `this.getField("name").value` reads from caller-supplied decimal inputs. One optional trailing semicolon is allowed. No loops, functions, arbitrary properties, dynamic calls, reflection or file/network/process capabilities exist. Unknown syntax/host APIs/triggers reject. Decimal overflow/division-by-zero and missing inputs have stable diagnostics. Every atom is object/depth charged and every parsing/evaluation loop checkpoints; source is capped at65536 UTF16 units. Applying returned values/appearances is a separate explicit form operation.

```csharp
var result = PdfScripts.EvaluateCalculation(
    "event.value = this.getField(\"price\").value * 2;",
    values, enableExecution: true);
```

General JavaScript, event triggers, validation semantics and isolated general-engine/sandbox corpus qualification remain mandatory. This finite subset does not claim compatibility with arbitrary document scripts.

Source validation is recorded in [the queue evidence ledger](evidence/issues-51-60-source.json). All four core target frameworks expose the same API; actual-runtime/final-package limitations remain explicit.
