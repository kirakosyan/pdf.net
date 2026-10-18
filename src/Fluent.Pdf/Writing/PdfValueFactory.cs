using System.Globalization;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Bounded stream authoring and exact finite numeric conversion for immutable syntax values.</summary>
public static class PdfValues
{
    /// <summary>Snapshots borrowed encoded bytes under a fresh operation budget. The caller retains the input stream.</summary>
    public static PdfValue Stream(PdfValue dictionary, Stream encodedInput, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (dictionary == null)
            throw new ArgumentNullException(nameof(dictionary));
        if (dictionary.Kind != PdfValueKind.Dictionary)
            throw new ArgumentException("A direct dictionary is required.", nameof(dictionary));
        if (encodedInput == null)
            throw new ArgumentNullException(nameof(encodedInput));
        if (!encodedInput.CanRead)
            throw new ArgumentException("A readable stream is required.", nameof(encodedInput));
        var context = new PdfOperationContext("CreateStream", "managed.writer", PdfCapability.WriteDocument, policy ?? PdfOperationPolicy.Default, cancellationToken);
        bool success = false;
        try
        {
            using var lease = new BorrowedStream(encodedInput, true, context);
            var bytes = PdfByteStore.Read(lease, context);
            context.Consume(PdfResourceKind.Objects, 1);
            context.EnsureComplete();
            success = true;
            return PdfValue.StreamValue(dictionary, bytes, 0, bytes.Length);
        }
        catch
        {
            context.Checkpoint();
            throw;
        }
        finally
        {
            context.Finish(success);
            context.DisposeSources();
        }
    }

    /// <summary>Converts a finite Double to a round-trippable PDF decimal token without exponent notation.</summary>
    public static PdfValue Real(double value)
    {
        if (double.IsInfinity(value) || double.IsNaN(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        string text = value.ToString("G17", CultureInfo.InvariantCulture);
        int e = text.IndexOfAny(new[] { 'e', 'E' });
        if (e < 0)
            return PdfValue.Number(text);
        int exponent = int.Parse(text.Substring(e + 1), CultureInfo.InvariantCulture);
        string mantissa = text.Substring(0, e), sign = "";
        if (mantissa[0] == '-')
        {
            sign = "-";
            mantissa = mantissa.Substring(1);
        }

        int point = mantissa.IndexOf('.');
        if (point < 0)
            point = mantissa.Length;
        else
            mantissa = mantissa.Remove(point, 1);
        point += exponent;
        return PdfValue.Number(point <= 0 ? sign + "0." + new string ('0', -point) + mantissa : point >= mantissa.Length ? sign + mantissa + new string ('0', point - mantissa.Length) : sign + mantissa.Insert(point, "."));
    }
}
