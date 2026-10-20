# Passive XFA packets and explicit static conversion

FP-038, queue issue #56, has the following source profile. Full requirement scope and production qualification remain mandatory; no issue is marked complete by this source delivery.

`PdfXfa.Inspect` captures ordered name/indirect-stream XFA packets, original stream identities and bounded decoded XML. Scalar datasets inventory uses unique dot-separated local-element paths below datasets/data. Repeated, dotted and namespace-ambiguous scalar identities reject rather than inventing SOM indices. Single-XDP stream layout is outside this profile. No script, external entity, resource or layout instruction executes.

`Fill` updates existing unique scalar paths and rewrites only the datasets stream in a staged graph transaction. Other packets retain their encoded streams/identities. XML DTDs/external resolution are disabled, packet text is bounded, and preflight charges nodes/attributes/depth before XML tree construction. Caller snapshots remain unchanged.

`ConvertToAcroForm` requires exact coverage of static template/data identities and explicit caller-supplied text-field definitions, pages, widget appearances and editing fonts. It preserves qualified field names and Unicode values. Only unnamespaced/XFA2.8/XFA3.3 template/subform/field/ui/textEdit/value/text constructs and explicit position layout are supported; unknown namespace extensions reject. Attributed or mixed scalar/container datasets need a separate binding profile; dynamic layout, scripts, binding attributes, unknown constructs, repeated data and existing hybrid fields reject. It removes the XFA attachment and creates standard AcroForm text fields under the same context. Capture original packets with Inspect when they must be retained separately. Supplied appearances must represent the intended values; automatic XFA layout/appearance reconstruction is not claimed.

Full static/dynamic XFA layout, indexed SOM/binding/template constructs, flattened conversion and licensed independent visual/data corpus qualification remain mandatory.

Source validation is recorded in [the queue evidence ledger](evidence/issues-51-60-source.json). All four core target frameworks expose the same API; actual-runtime/final-package limitations remain explicit.
