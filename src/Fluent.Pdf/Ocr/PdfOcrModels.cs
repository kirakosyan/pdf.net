using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit immutable metadata for one locally provisioned printed-text language/script model. Declarations do not qualify accuracy or licensing.</summary>
public sealed class PdfOcrLanguageModel
{
    /// <summary>Captures a portable lowercase native alias, exact version/content digest/length, license/source IDs and declared scripts/languages. A script model may use an explicit flat alias rather than a directory path.</summary>
    public PdfOcrLanguageModel(string identifier, string version, string sha256, long byteLength, string license, string sourceId, string[] scripts, string[] languages)
    {
        if (!Token(identifier))
            throw new ArgumentException("An explicit bounded lowercase model alias is required.", nameof(identifier));
        if (sha256 == null || sha256.Length != 64 || sha256.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')))
            throw new ArgumentException("An exact lowercase model SHA256 is required.", nameof(sha256));
        if (byteLength < 1 || byteLength > 268435456)
            throw new ArgumentOutOfRangeException(nameof(byteLength));
        if (scripts == null || scripts.Length < 1 || scripts.Length > 64 || scripts.Any(s => s == null || s.Length != 4 || s.Any(c => !(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z'))) || scripts.Distinct(StringComparer.Ordinal).Count() != scripts.Length)
            throw new ArgumentException("Explicit unique ISO15924 script declarations are required.", nameof(scripts));
        if (languages == null || languages.Length > 64 || languages.Any(s => s == null || s.Length < 2 || s.Length > 128 || s.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-'))) || languages.Distinct(StringComparer.Ordinal).Count() != languages.Length)
            throw new ArgumentException("A bounded unique language-tag declaration is required; script-only models may use an empty list.", nameof(languages));
        PdfOcrRules.Text(version ?? throw new ArgumentNullException(nameof(version)));
        PdfOcrRules.Text(license ?? throw new ArgumentNullException(nameof(license)));
        PdfOcrRules.Text(sourceId ?? throw new ArgumentNullException(nameof(sourceId)));
        Identifier = identifier;
        Version = Guard.Text(version, nameof(version), 256);
        Sha256 = sha256;
        ByteLength = byteLength;
        License = Guard.Text(license, nameof(license), 256);
        SourceId = Guard.Text(sourceId, nameof(sourceId), 1024);
        Scripts = Array.AsReadOnly((string[])scripts.Clone());
        Languages = Array.AsReadOnly((string[])languages.Clone());
    }

    /// <summary>Gets the exact native alias, without a path or inferred language selection.</summary>
    public string Identifier { get; }
    /// <summary>Gets the explicit model release/version.</summary>
    public string Version { get; }
    /// <summary>Gets the exact lowercase trained-data SHA256.</summary>
    public string Sha256 { get; }
    /// <summary>Gets the exact trained-data byte length.</summary>
    public long ByteLength { get; }
    /// <summary>Gets the explicitly declared license identity; legal qualification remains separate.</summary>
    public string License { get; }
    /// <summary>Gets a retained provenance/source identity; no URI or resource is fetched.</summary>
    public string SourceId { get; }
    /// <summary>Gets declared supported scripts, without an accuracy guarantee.</summary>
    public IReadOnlyList<string> Scripts { get; }
    /// <summary>Gets declared language tags; empty denotes an explicitly script-only declaration.</summary>
    public IReadOnlyList<string> Languages { get; }
    /// <summary>Gets the exact flat staged model filename.</summary>
    public string FileName => Identifier + ".traineddata";

    internal static bool Token(string? value) => value != null && value.Length >= 1 && value.Length <= 64 && value[0] >= 'a' && value[0] <= 'z' && value.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_');
}

/// <summary>An immutable caller-selected catalog of at most32 explicit local models. It never searches directories or downloads a model.</summary>
public sealed class PdfOcrModelCatalog
{
    /// <summary>Captures unique immutable model declarations. Every selected model must later match the native deployment's exact data asset.</summary>
    public PdfOcrModelCatalog(params PdfOcrLanguageModel[] models)
    {
        if (models == null || models.Length < 1 || models.Length > 32 || models.Any(m => m == null) || models.Select(m => m.Identifier).Distinct(StringComparer.Ordinal).Count() != models.Length)
            throw new ArgumentException("A finite nonempty catalog with unique model aliases is required.", nameof(models));
        Models = Array.AsReadOnly((PdfOcrLanguageModel[])models.Clone());
    }

    /// <summary>Gets immutable explicit model metadata.</summary>
    public IReadOnlyList<PdfOcrLanguageModel> Models { get; }

    /// <summary>Selects exact plus-separated aliases in caller order and returns a content/version-bound selection. Missing/duplicate aliases reject rather than falling back.</summary>
    public PdfOcrModelSelection Select(string language, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (language == null)
            throw new ArgumentNullException(nameof(language));
        PdfOcrModelSelection? result = null;
        var report = PdfFont.Run("SelectOcrModels", "managed.ocr-models", policy, cancellationToken, c => result = SelectCore(language, c), PdfCapability.RecognizeText);
        result!.OperationReport = report;
        return result;
    }

    internal PdfOcrModelSelection SelectCore(string language, PdfOperationContext c)
    {
        c.Checkpoint();
        if (language.Length > 256)
            throw Error("Selection", "The explicit model selection exceeds its finite extent.");
        c.Consume(PdfResourceKind.ExpandedBytes, language.Length * 8L + 1024);
        var names = language.Split('+');
        if (names.Length < 1 || names.Length > 32 || names.Any(n => !PdfOcrLanguageModel.Token(n)) || names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw Error("Selection", "Model selection requires unique explicit lowercase aliases in caller order.");
        c.Consume(PdfResourceKind.Objects, names.Length + 1L);
        c.Consume(PdfResourceKind.ExpandedBytes, names.Length * 64L + 256);
        var selected = new PdfOcrLanguageModel[names.Length];
        var builder = new StringBuilder();
        for (int i = 0; i < names.Length; i++)
        {
            c.Checkpoint();
            var model = Models.FirstOrDefault(m => m.Identifier == names[i]) ?? throw Error("Unavailable", "A requested model is absent from the explicit catalog; no download or fallback is performed.");
            selected[i] = model;
            long textLength = model.Identifier.Length + model.Version.Length + model.Sha256.Length + model.ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture).Length + 4L;
            c.Consume(PdfResourceKind.ExpandedBytes, textLength * 8 + 256);
            string length = model.ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
            builder.Append(model.Identifier.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(model.Identifier).Append(model.Version.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(model.Version).Append(model.Sha256.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(model.Sha256).Append(length.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(length);
        }

        c.Consume(PdfResourceKind.ExpandedBytes, builder.Length * 8L + 4096);
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        using var hash = SHA256.Create();
        string identity = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        return new(language, identity, selected);
        PdfException Error(string code, string message) => PdfWriterCore.Error(c, "Ocr.Model" + code, message);
    }
}

/// <summary>An immutable exact model/version/content selection, independent of accuracy or publication qualification.</summary>
public sealed class PdfOcrModelSelection
{
    internal PdfOcrModelSelection(string language, string identity, PdfOcrLanguageModel[] models)
    {
        Language = language;
        Identity = identity;
        Models = Array.AsReadOnly(models);
    }

    /// <summary>Gets exact ordered plus-separated native aliases.</summary>
    public string Language { get; }
    /// <summary>Gets the lowercase digest binding ordered aliases, versions, content hashes and byte lengths.</summary>
    public string Identity { get; }
    /// <summary>Gets exact model metadata in requested order.</summary>
    public IReadOnlyList<PdfOcrLanguageModel> Models { get; }
    /// <summary>Gets root operation diagnostics after public selection; null for internal same-context selection.</summary>
    public PdfOperationReport? OperationReport { get; internal set; }
}
