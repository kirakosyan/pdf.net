namespace Fluent.Pdf;

internal static class Guard
{
    internal static string Text(string value, string parameter, int maximumLength = 256)
    {
        if (value is null) throw new ArgumentNullException(parameter);
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
            throw new ArgumentException("A nonempty value within the documented length limit is required.", parameter);
        return value;
    }

    internal static void Size(PdfPageSize size, string parameter)
    {
        if (!size.IsValid) throw new ArgumentException("A page size must have finite positive dimensions.", parameter);
    }

    internal static void Capability(PdfCapability capability)
    {
        if (!Enum.IsDefined(typeof(PdfCapability), capability))
            throw new ArgumentOutOfRangeException(nameof(capability));
    }
}
