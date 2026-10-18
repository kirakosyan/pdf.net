using System.Collections.ObjectModel;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable validated content program. Encoded text bytes and named resources remain uninterpreted.</summary>
public sealed class PdfContentProgram
{
    private PdfContentProgram(List<PdfContentOperation> operations, PdfContentKind kind, string version)
    {
        Operations = new ReadOnlyCollection<PdfContentOperation>(operations);
        Kind = kind;
        MinimumVersion = version;
    }

    /// <summary>Gets the operators in execution order.</summary>
    public IReadOnlyList<PdfContentOperation> Operations { get; }
    /// <summary>Gets the program's page/form or Type 3 glyph context.</summary>
    public PdfContentKind Kind { get; }
    /// <summary>Gets the aggregate standard and explicitly declared extension version floor.</summary>
    public string MinimumVersion { get; }
    /// <summary>Gets the successful construction or parsing report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;

    /// <summary>Copies and validates authored operators under a fresh operation budget.</summary>
    public static PdfContentProgram Create(IEnumerable<PdfContentOperation> operations, PdfContentKind kind = PdfContentKind.Page, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (operations == null)
            throw new ArgumentNullException(nameof(operations));
        CheckKind(kind);
        PdfContentProgram? result = null;
        Run("CreateContent", policy, cancellationToken, context => result = CreateCore(operations, kind, context), report =>
        {
            if (result != null)
                result.OperationReport = report;
        });
        return result!;
    }

    /// <summary>Parses borrowed decoded bytes. Preserving unknown operators requires an explicit version floor.</summary>
    public static PdfContentProgram Parse(Stream decodedInput, PdfContentKind kind = PdfContentKind.Page, PdfUnknownOperatorPolicy unknownOperators = PdfUnknownOperatorPolicy.Reject, string? unknownOperatorVersion = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (decodedInput == null)
            throw new ArgumentNullException(nameof(decodedInput));
        if (!decodedInput.CanRead)
            throw new ArgumentException("A readable stream is required.", nameof(decodedInput));
        CheckKind(kind);
        if (!Enum.IsDefined(typeof(PdfUnknownOperatorPolicy), unknownOperators))
            throw new ArgumentOutOfRangeException(nameof(unknownOperators));
        if (unknownOperators == PdfUnknownOperatorPolicy.Preserve && unknownOperatorVersion == null)
            throw new ArgumentException("Preservation requires a declared extension version floor.", nameof(unknownOperatorVersion));
        if (unknownOperatorVersion != null)
            PdfVersionNumber.Parse(unknownOperatorVersion);
        PdfContentProgram? result = null;
        Run("ParseContent", policy, cancellationToken, context =>
        {
            using var input = new BorrowedStream(decodedInput, true, context);
            result = ParseCore(PdfByteStore.Read(input, context), kind, unknownOperators, unknownOperatorVersion, context);
        }, report =>
        {
            if (result != null)
                result.OperationReport = report;
        });
        return result!;
    }

    /// <summary>Writes canonical decoded content to a borrowed stream after validating the complete program.</summary>
    public PdfOperationReport Write(Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (!output.CanWrite)
            throw new ArgumentException("A writable stream is required.", nameof(output));
        return Run("WriteContent", policy, cancellationToken, context =>
        {
            ValidateCore(Operations, Kind, context);
            using var lease = new BorrowedStream(output, false, context);
            WriteCore(lease, context);
        });
    }

    /// <summary>Returns a separately validated program with an operator replaced, inserted or removed.</summary>
    public PdfContentProgram Edit(int index, int removeCount, IEnumerable<PdfContentOperation> replacement, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (index < 0 || removeCount < 0 || index > Operations.Count - removeCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (replacement == null)
            throw new ArgumentNullException(nameof(replacement));
        return Create(Edited(), Kind, policy, cancellationToken);
        IEnumerable<PdfContentOperation> Edited()
        {
            for (int i = 0; i < index; i++)
                yield return Operations[i];
            foreach (var operation in replacement)
                yield return operation;
            for (int i = index + removeCount; i < Operations.Count; i++)
                yield return Operations[i];
        }
    }

    internal static PdfContentProgram CreateCore(IEnumerable<PdfContentOperation> operations, PdfContentKind kind, PdfOperationContext context, bool inlinePixelsPreflighted = false)
    {
        var items = new List<PdfContentOperation>();
        int version = 12;
        foreach (var operation in operations)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 32);
            if (operation == null)
                throw new ArgumentException("An operator cannot be CLR null.", nameof(operations));
            items.Add(operation);
            version = Math.Max(version, PdfVersionNumber.Parse(operation.MinimumVersion));
        }

        ValidateCore(items, kind, context, !inlinePixelsPreflighted);
        return new PdfContentProgram(items, kind, PdfVersionNumber.Text(version));
    }

    internal static PdfContentProgram ParseCore(PdfByteStore bytes, PdfContentKind kind, PdfUnknownOperatorPolicy unknown, string? version, PdfOperationContext context, Func<PdfValue, int>? inlineComponents = null)
    {
        var cursor = new PdfSyntaxCursor(bytes, context);
        var operations = new List<PdfContentOperation>();
        var operands = new List<PdfValue>();
        while (true)
        {
            cursor.Skip();
            if (cursor.Peek() < 0)
                break;
            context.Checkpoint();
            int next = cursor.Peek();
            if (next == '/' || next == '(' || next == '<' || next == '[')
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 16);
                operands.Add(cursor.Value());
                continue;
            }

            string token = cursor.Token();
            if (token == "ID" || token == "EI")
                throw PdfContentState.Error(context, "Content.UnexpectedInlineMarker", "An inline-image delimiter occurs outside an inline image.");
            if (token == "true" || token == "false" || token == "null" || next == '+' || next == '-' || next == '.' || next >= '0' && next <= '9')
            {
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 64);
                try
                {
                    operands.Add(token == "null" ? PdfValue.Null : token == "true" || token == "false" ? PdfValue.Boolean(token == "true") : PdfValue.Number(token));
                }
                catch (ArgumentException)
                {
                    throw PdfContentState.Error(context, "Content.Operand", "A numeric operand is malformed.");
                }

                continue;
            }

            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, operands.Count * 16L + 128);
            PdfContentOperation operation;
            try
            {
                if (token == "BI")
                {
                    if (operands.Count != 0)
                        throw PdfContentState.Error(context, "Content.InlineOperands", "BI cannot have preceding operands.");
                    operation = PdfInlineImage.Read(cursor, inlineComponents);
                }
                else if (PdfOperatorGrammar.TryGet(token, out var code))
                    operation = new PdfContentOperation(code, operands.ToArray());
                else if (unknown == PdfUnknownOperatorPolicy.Preserve)
                    operation = PdfContentOperation.Extension(token, version!, operands.ToArray());
                else
                    throw PdfContentState.Error(context, "Content.UnknownOperator", "An unknown content operator requires an explicit preservation policy and version floor.");
            }
            catch (ArgumentException)
            {
                throw PdfContentState.Error(context, "Content.OperatorGrammar", "An operator has invalid operands or an invalid extension token.");
            }

            operations.Add(operation);
            operands.Clear();
        }

        if (operands.Count != 0)
            throw PdfContentState.Error(context, "Content.MissingOperator", "The program ends with operands without an operator.");
        return CreateCore(operations, kind, context, true);
    }

    internal static void ValidateCore(IReadOnlyList<PdfContentOperation> operations, PdfContentKind kind, PdfOperationContext context, bool chargeInlinePixels = true)
    {
        foreach (var operation in operations)
        {
            foreach (var operand in operation.Operands)
                Inspect(operand, context);
            if (operation.Operation == PdfOperator.InlineImage)
                PdfInlineImage.Validate(operation, context, chargeInlinePixels);
        }

        PdfContentState.Validate(operations, kind, context);
    }

    internal static void Inspect(PdfValue value, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        using var scope = context.EnterScope(PdfResourceKind.Depth);
        if (value.Kind == PdfValueKind.Reference || value.Kind == PdfValueKind.Stream)
            throw PdfContentState.Error(context, "Content.IndirectOperand", "Content operands must be direct values without streams or references.");
        if (value.Kind == PdfValueKind.Array)
            foreach (var item in value.Items)
                Inspect(item, context);
        if (value.Kind == PdfValueKind.Dictionary)
            foreach (var item in value.Entries.Values)
                Inspect(item, context);
    }

    internal void WriteCore(Stream output, PdfOperationContext context)
    {
        var sink = new PdfWriterCore.Sink(output, context);
        foreach (var operation in Operations)
        {
            context.Checkpoint();
            if (operation.Operation == PdfOperator.InlineImage)
            {
                PdfInlineImage.Write(operation, sink, context);
                continue;
            }

            foreach (var value in operation.Operands)
            {
                sink.Value(value, false);
                sink.Text(" ");
            }

            sink.Text(operation.Token);
            sink.Text("\n");
        }

        sink.Flush();
    }

    internal PdfValue ToStream(PdfOperationContext context)
    {
        ValidateCore(Operations, Kind, context);
        using var output = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
        WriteCore(output, context);
        var data = output.Finish();
        return PdfValue.StreamValue(PdfValue.Dictionary(new Dictionary<string, PdfValue>()), data, 0, data.Length);
    }

    internal static PdfOperationReport Run(string name, PdfOperationPolicy? policy, CancellationToken token, Action<PdfOperationContext> action, Action<PdfOperationReport>? finished = null)
    {
        var context = new PdfOperationContext(name, "managed.content", PdfCapability.WriteDocument, policy ?? PdfOperationPolicy.Default, token);
        bool succeeded = false;
        PdfOperationReport report;
        try
        {
            action(context);
            context.EnsureComplete();
            succeeded = true;
        }
        catch
        {
            context.Checkpoint();
            throw;
        }
        finally
        {
            report = context.Finish(succeeded);
            finished?.Invoke(report);
            context.DisposeSources();
        }

        return report;
    }

    private static void CheckKind(PdfContentKind kind)
    {
        if (!Enum.IsDefined(typeof(PdfContentKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
    }
}
