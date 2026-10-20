# Signed revisions and conservative edit preflight

FP-102, queue issue #54, has the following source profile. Full requirement scope and production qualification remain mandatory; no issue is marked complete by this source delivery.

`PdfSignatures.AuditRevisions` walks every linked revision, newest-first in its returned inventory. It records each cross-reference declaration, whether an older revision declared that object number, and cryptographic observations for signatures introduced/redeclared there. Historical signatures remain inspectable when a later revision frees or replaces their active objects. Free declarations retain the incremented generation rather than impersonating the prior live identity.

Signature inspection binds the exact original Contents token and complete signed prefix to a linked source revision. Covered revision length and total input length distinguish signed bytes from later bytes. Structural additions/redefinitions/frees are not inferred semantic classifications or trust/permission decisions.

`PreflightEdit` checks the existing conservative DocMDP/FieldMDP policy barrier without catching and hiding a latched operation failure. Generic protected-document edits reject unless explicit invalidation is authorized. An ordinary full rewrite of a signed document rejects without invalidation authority. Allowed incremental edits preserve the prefix but explicitly report that later edits have not been validated. Signing preflight checks protection/order/field-lock policy; final field identity, blankness and cryptographic self-verification remain mandatory at signing time.

General permitted form/annotation edit classes, malicious-update semantic classification, structure/appearance reconciliation and independent signature-preservation qualification remain mandatory. An appearance, unchanged prefix or successful preflight alone never proves signature preservation.

Source validation is recorded in [the queue evidence ledger](evidence/issues-51-60-source.json). All four core target frameworks expose the same API; actual-runtime/final-package limitations remain explicit.
