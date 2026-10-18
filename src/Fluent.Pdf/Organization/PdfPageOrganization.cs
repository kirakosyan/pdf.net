using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Bounded managed page import, extraction and composition; source sessions and streams remain caller-owned.</summary>
public static partial class PdfPageOrganization
{
    /// <summary>Imports ordered selections into one independent document with remapped object identities.</summary>
    public static PdfDocument Merge(IEnumerable<PdfPageInput> inputs, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (inputs == null)
            throw new ArgumentNullException(nameof(inputs));
        options ??= new();
        PdfDocument? result = null;
        try
        {
            var report = PdfFont.Run("OrganizePages", "managed.organization", policy, cancellationToken, context =>
            {
                using var engine = new PdfPageImportEngine(options, context);
                using var enumerator = inputs.GetEnumerator();
                while (true)
                {
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.Objects, 1);
                    if (!enumerator.MoveNext())
                        break;
                    context.Consume(PdfResourceKind.ExpandedBytes, 256);
                    engine.Add(enumerator.Current ?? throw new ArgumentException("Inputs cannot contain null.", nameof(inputs)));
                }

                result = engine.Finish();
            }, PdfCapability.WriteDocument);
            result!.SetCompositionReport(report);
            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }

    /// <summary>Extracts or reorders selected pages as an independent document.</summary>
    public static PdfDocument Extract(PdfDocumentSnapshot source, PdfPageSelection selection, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Merge(new[] { new PdfPageInput(source, selection ?? throw new ArgumentNullException(nameof(selection))) }, options, policy, cancellationToken);
    internal static PdfDocument Normalize(PdfDocumentSnapshot snapshot, PdfOrganizationOptions options, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, snapshot.Pages.Count * 128L + 256);
        var writes = new PdfWriteOptions(allowSignatureInvalidation: options.AllowSignatureInvalidation);
        var source = new PdfDocumentSnapshot(snapshot.Options, snapshot.Pages.ToArray(), snapshot.SourceDocument, snapshot.OriginalOrder, writes, snapshot.PageNumbering);
        using var buffer = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
        using (var lease = new BorrowedStream(buffer, false, context))
            ManagedPdfProvider.Instance.Write(source, lease, context);
        return Read(buffer.Finish(), snapshot.Pages.Count, context);
    }

    internal static PdfDocument Read(PdfByteStore bytes, int maximumPages, PdfOperationContext context)
    {
        var result = Pdf.Create(new(maximumPages: maximumPages, operationPolicy: context.Policy));
        try
        {
            using var input = bytes.Open(0, bytes.Length);
            using var lease = new BorrowedStream(input, true, context);
            ManagedPdfProvider.Instance.Read(lease, result, context);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }
}
