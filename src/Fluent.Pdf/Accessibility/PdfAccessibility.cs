using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit logical structure authoring, passive inspection and identity-preserving semantic remediation.</summary>
public static partial class PdfAccessibility
{
    /// <summary>Inspects actual page/Form MCIDs, logical order, annotation/parent associations and incomplete automated blockers. Human review and complete independent validation remain separate requirements.</summary>
    public static PdfAccessibilityInspection Inspect(PdfDocumentSnapshot source, PdfAccessibilityProfile profile = PdfAccessibilityProfile.Ua1, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        PdfStructureRules.Profile(profile);
        providers ??= PdfProviderRegistry.Default;
        PdfStructureReader? reader = null;
        PdfStructureElementInfo[] elements = Array.Empty<PdfStructureElementInfo>();
        PdfStructureContent[] content = Array.Empty<PdfStructureContent>();
        PdfAccessibilityFinding[] findings = Array.Empty<PdfAccessibilityFinding>();
        var report = PdfFont.Run("InspectAccessibility", "managed.accessibility", policy, cancellationToken, c =>
        {
            using var graph = new PdfGraphEdit(source, new(), c);
            reader = new(graph, profile, providers);
            reader.Tick((reader.Elements.Count + reader.Content.Count + reader.Findings.Count) * 16L + 256);
            elements = reader.Elements.ToArray();
            content = reader.Content.ToArray();
            findings = reader.Findings.ToArray();
        }, PdfCapability.ReadDocument);
        return new(reader!.Graph.Snapshot, profile, reader.Language, elements, content, findings, report);
    }

    /// <summary>Authors a complete explicit structure tree over actual existing marked content/annotations; every MCID must have exactly one owner. No PDF/UA identification or conformance claim is fabricated.</summary>
    public static PdfDocument Author(PdfDocumentSnapshot source, PdfStructurePlan plan, PdfProviderRegistry? providers = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        providers ??= PdfProviderRegistry.Default;
        return PdfGraphEdit.Run("AuthorAccessibility", source, options, policy, cancellationToken, g => new PdfStructureWriter(g, plan, providers).Write(), plan.Profile == PdfAccessibilityProfile.Ua2 ? "2.0" : "1.7");
    }

    /// <summary>Changes author-supplied role/language/descriptions and optionally reorders existing children by an exact permutation, preserving identities and all content/parent associations. Null fields retain source values.</summary>
    public static PdfDocument Remediate(PdfAccessibilityInspection source, PdfObjectId elementId, string? role = null, string? language = null, string? alternativeText = null, string? actualText = null, int[]? childOrder = null, PdfProviderRegistry? providers = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (role != null)
            PdfStructureRules.Role(role);
        PdfStructureRules.Language(language);
        PdfStructureRules.Text(alternativeText, 1000000, nameof(alternativeText));
        PdfStructureRules.Text(actualText, 1000000, nameof(actualText));
        if (childOrder != null && childOrder.Length > 100000)
            throw new ArgumentException("A bounded complete child permutation is required.", nameof(childOrder));
        providers ??= PdfProviderRegistry.Default;
        var order = childOrder == null ? null : (int[])childOrder.Clone();
        return PdfGraphEdit.Run("RemediateAccessibility", source.Source, options, policy, cancellationToken,
            g => RemediateCore(g, source.Profile, elementId, role, language, alternativeText, actualText, order, providers),
            source.Profile == PdfAccessibilityProfile.Ua2 ? "2.0" : "1.7");
    }

    internal static void RemediateCore(PdfGraphEdit g, PdfAccessibilityProfile profile, PdfObjectId elementId, string? role, string? language, string? alternativeText, string? actualText, int[]? order, PdfProviderRegistry providers)
    {
        var reader = new PdfStructureReader(g, profile, providers);
        var element = reader.Elements.FirstOrDefault(e => e.ObjectId == elementId) ?? throw reader.Error("ElementIdentity", "The selected identity is not an element in this source tree.");
        var values = g.Copy(element.Dictionary);
        string nextRole = reader.EffectiveRole(role ?? element.Role);
        if ((nextRole == "Figure" || nextRole == "Formula" || nextRole == "Form") && string.IsNullOrWhiteSpace(alternativeText ?? element.AlternativeText) && string.IsNullOrWhiteSpace(actualText ?? element.ActualText))
            throw reader.Error("AlternativeText", "This role needs meaningful explicit alternative or replacement text.");
        if (role != null && role != element.Role && (new[]
        {
            "Table",
            "TR",
            "TH",
            "TD",
            "THead",
            "TBody",
            "TFoot"
        }.Contains(role) || new[]
        {
            "Table",
            "TR",
            "TH",
            "TD",
            "THead",
            "TBody",
            "TFoot"
        }.Contains(reader.EffectiveRole(element.Role)) || element.Children.Any(child => reader.Name(reader.Resolve(child), "Type") == "OBJR")))
            throw reader.Error("RoleAssociations", "Changing table or annotation semantic ownership requires a complete explicit structure remediation plan.");
        if (role != null && role != element.Role && values.ContainsKey("A"))
            throw reader.Error("RoleAttributes", "Changing a role with attached attributes requires an explicit complete attribute remediation profile.");
        if (role != null)
            values["S"] = PdfValue.Name(role);
        void Set(string key, string? text)
        {
            if (text != null)
                values[key] = PdfTextStrings.Encode(text, g.Context);
        }

        Set("Lang", language);
        Set("Alt", alternativeText);
        Set("ActualText", actualText);
        if (order != null)
        {
            reader.Tick(order.Length * 32L + 128);
            if (order.Length != element.Children.Count || order.Any(i => i < 0 || i >= order.Length) || order.Distinct().Count() != order.Length)
                throw reader.Error("ChildOrder", "Reading-order edits require an exact permutation of all existing children.");
            values["K"] = PdfValue.OwnedArray(order.Select(i => element.Children[i]).ToList());
        }

        var revision = reader.Get(element.Dictionary, "R");
        int number = revision.Kind == PdfValueKind.Null ? 0 : reader.Integer(revision);
        if (number == 1000000)
            throw reader.Error("RevisionLimit", "The structure revision inventory is exhausted.");
        values["R"] = PdfValue.Integer(number + 1);
        g.Edit.Set(elementId, PdfValue.OwnedDictionary(values));
        _ = new PdfStructureReader(g, profile, providers);
    }
}
