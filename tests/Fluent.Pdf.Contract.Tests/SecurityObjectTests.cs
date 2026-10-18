using System.IO;
using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class SecurityObjectTests
{
    private static byte[] Hex(string value) => Enumerable.Range(0, value.Length / 2).Select(i => Convert.ToByte(value.Substring(i * 2, 2), 16)).ToArray();
    private static PdfValue D(params (string, PdfValue)[] values) => NavigationTests.D(values);
    private static PdfValue N(string value) => PdfValue.Name(value);
    private static PdfValue Stream(byte[] bytes, PdfValue dictionary, PdfOperationContext context)
    {
        using var input = new MemoryStream(bytes);
        var store = PdfByteStore.Read(input, context);
        return PdfValue.StreamValue(dictionary, store, 0, store.Length);
    }

    private static byte[] Raw(PdfValue value)
    {
        using var input = value.OpenRawStream();
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    public static IEnumerable<object[]> IndependentObjects() => SecurityObjectKnownAnswers.Values();
    [Theory]
    [MemberData(nameof(IndependentObjects))]
    public void StringAndEmbeddedStreamKeysMatchFiveIndependentEncryptionVariants(int index, int number, int generation, string cipher, string plain, int streamNumber, int streamGeneration, string streamCipher, string streamPlain)
    {
        var row = SecurityKnownAnswers.PasswordHandlers().ElementAt(index);
        int revision = (int)row[0];
        var entries = new Dictionary<string, PdfValue>
        {
            {
                "Filter",
                N("Standard")
            },
            {
                "R",
                PdfValue.Integer(revision)
            },
            {
                "V",
                PdfValue.Integer((int)row[1])
            },
            {
                "Length",
                PdfValue.Integer((int)row[2])
            },
            {
                "P",
                PdfValue.Integer((long)row[3])
            },
            {
                "O",
                PdfValue.String(Hex((string)row[6]))
            },
            {
                "U",
                PdfValue.String(Hex((string)row[7]))
            }
        };
        if (revision >= 5)
        {
            entries["OE"] = PdfValue.String(Hex((string)row[8]));
            entries["UE"] = PdfValue.String(Hex((string)row[9]));
            entries["Perms"] = PdfValue.String(Hex((string)row[10]));
        }

        if (revision >= 4)
        {
            entries["StmF"] = entries["StrF"] = N("StdCF");
            entries["CF"] = D(("StdCF", D(("CFM", N(revision == 4 ? "AESV2" : "AESV3")), ("Length", PdfValue.Integer(revision == 4 ? 16 : 32)))));
        }

        PolicyTests.Run(context =>
        {
            using var security = PdfStandardSecurity.Authenticate(PdfValue.Dictionary(entries), Hex((string)row[4]), Encoding.ASCII.GetBytes("user"), true, context);
            var crypt = new PdfObjectCrypt(security, new(9999), true, context);
            var decoded = crypt.Transform(PdfValue.String(Hex(cipher)), new(number, generation), false, value => value);
            Assert.Equal(Hex(plain), decoded.StringBytes);
            var encodedStream = Stream(Hex(streamCipher), D(("Type", N("EmbeddedFile"))), context);
            var decodedStream = crypt.Transform(encodedStream, new(streamNumber, streamGeneration), false, value => value);
            Assert.Equal(Hex(streamPlain), Raw(decodedStream));
        });
    }

    [Fact]
    public void IdentityOverridesSurviveEncryptionAndAreRemovedFromDecryptedOutput()
    {
        using var user = new PdfPassword(Encoding.ASCII.GetBytes("user"));
        using var owner = new PdfPassword(Encoding.ASCII.GetBytes("owner"));
        PolicyTests.Run(context =>
        {
            using var security = PdfStandardSecurity.Create(new(user, owner), new byte[16], context);
            var crypt = new PdfObjectCrypt(security, new(99), false, context);
            var data = Encoding.ASCII.GetBytes("explicit plaintext stream");
            var source = Stream(data, D(("Filter", N("Crypt")), ("DecodeParms", D(("Name", N("Identity"))))), context);
            var encrypted = crypt.Transform(source, new(10), true, value => value);
            Assert.Equal(data, Raw(encrypted));
            Assert.Equal("Crypt", encrypted.Entries["Filter"].NameValue);
            var decrypted = crypt.Transform(encrypted, new(10), false, value => value);
            Assert.Equal(data, Raw(decrypted));
            Assert.False(decrypted.Entries.ContainsKey("Filter"));
            Assert.False(decrypted.Entries.ContainsKey("DecodeParms"));
        });
    }

    [Fact]
    public void PlaintextMetadataAndSignatureContentsRemainExactWhileOtherStringsAreEncrypted()
    {
        using var user = new PdfPassword(Encoding.ASCII.GetBytes("user"));
        using var owner = new PdfPassword(Encoding.ASCII.GetBytes("owner"));
        PolicyTests.Run(context =>
        {
            using var security = PdfStandardSecurity.Create(new(user, owner, encryptMetadata: false), new byte[16], context);
            var crypt = new PdfObjectCrypt(security, new(99), false, context);
            var data = Encoding.ASCII.GetBytes("original XML metadata");
            var source = Stream(data, D(("Type", N("Metadata"))), context);
            Assert.Equal(data, Raw(crypt.Transform(source, new(10), true, value => value)));
            var signature = D(("Type", N("Sig")), ("Contents", PdfValue.String(data)), ("Reason", PdfValue.String(data)));
            var encrypted = crypt.Transform(signature, new(11), true, value => value);
            Assert.Equal(data, encrypted.Entries["Contents"].StringBytes);
            Assert.NotEqual(data, encrypted.Entries["Reason"].StringBytes);
            var decrypted = crypt.Transform(encrypted, new(11), false, value => value);
            Assert.Equal(data, decrypted.Entries["Contents"].StringBytes);
            Assert.Equal(data, decrypted.Entries["Reason"].StringBytes);
            Assert.Same(signature, crypt.Transform(signature, new(99), true, value => value));
        });
    }

    [Fact]
    public void InvalidCryptFilterOrderFailsBeforeStreamDecryption()
    {
        using var user = new PdfPassword(Encoding.ASCII.GetBytes("user"));
        using var owner = new PdfPassword(Encoding.ASCII.GetBytes("owner"));
        Assert.Equal("Security.CryptFilter", Assert.Throws<PdfException>(() => PolicyTests.Run(context =>
        {
            using var security = PdfStandardSecurity.Create(new(user, owner), new byte[16], context);
            var crypt = new PdfObjectCrypt(security, new(99), false, context);
            var source = Stream(new byte[] { 1 }, D(("Filter", PdfValue.Array(N("FlateDecode"), N("Crypt")))), context);
            crypt.Transform(source, new(10), false, value => value);
        })).Diagnostic.Code);
    }
}
