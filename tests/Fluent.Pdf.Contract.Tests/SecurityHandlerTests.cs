using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class SecurityHandlerTests
{
    private static byte[] Hex(string value) => Enumerable.Range(0, value.Length / 2).Select(i => Convert.ToByte(value.Substring(i * 2, 2), 16)).ToArray();
    public static IEnumerable<object[]> IndependentAnswers() => SecurityKnownAnswers.PasswordHandlers();
    [Theory]
    [MemberData(nameof(IndependentAnswers))]
    public void OwnerAndUserAuthenticationMatchIndependentFileKeysAndWrongCredentialsFail(int revision, int version, int length, long permissions, string identity, string key, string o, string u, string oe, string ue, string perms)
    {
        var entries = new Dictionary<string, PdfValue>
        {
            {
                "Filter",
                PdfValue.Name("Standard")
            },
            {
                "R",
                PdfValue.Integer(revision)
            },
            {
                "V",
                PdfValue.Integer(version)
            },
            {
                "Length",
                PdfValue.Integer(length)
            },
            {
                "P",
                PdfValue.Integer(permissions)
            },
            {
                "O",
                PdfValue.String(Hex(o))
            },
            {
                "U",
                PdfValue.String(Hex(u))
            }
        };
        if (revision >= 5)
        {
            entries["OE"] = PdfValue.String(Hex(oe));
            entries["UE"] = PdfValue.String(Hex(ue));
            entries["Perms"] = PdfValue.String(Hex(perms));
        }

        var dictionary = PdfValue.Dictionary(entries);
        foreach (var role in new[]
        {
            PdfAuthenticationRole.Owner,
            PdfAuthenticationRole.User
        }

        )
        {
            PolicyTests.Run(context =>
            {
                using var authenticated = PdfStandardSecurity.Authenticate(dictionary, Hex(identity), Encoding.ASCII.GetBytes(role == PdfAuthenticationRole.Owner ? "owner" : "user"), true, context);
                Assert.Equal(role, authenticated.Role);
                Assert.Equal(Hex(key), authenticated.Key);
                Assert.Equal(unchecked((uint)permissions), authenticated.PermissionBits);
            });
        }

        Assert.Equal("Security.AuthenticationFailed", Assert.Throws<PdfException>(() => PolicyTests.Run(context => PdfStandardSecurity.Authenticate(dictionary, Hex(identity), Encoding.ASCII.GetBytes("wrong"), true, context))).Diagnostic.Code);
        if (revision <= 3 || revision == 5)
            Assert.Equal("Security.LegacyReadBlocked", Assert.Throws<PdfException>(() => PolicyTests.Run(context => PdfStandardSecurity.Authenticate(dictionary, Hex(identity), Encoding.ASCII.GetBytes("owner"), false, context))).Diagnostic.Code);
    }

    [Theory]
    [InlineData(PdfPasswordCipher.Aes256, true)]
    [InlineData(PdfPasswordCipher.Aes256, false)]
    [InlineData(PdfPasswordCipher.Aes128, true)]
    [InlineData(PdfPasswordCipher.Aes128, false)]
    public void ExplicitAuthoringOptionsRoundTripRolesAndMetadataPermissions(PdfPasswordCipher cipher, bool metadata)
    {
        using var user = new PdfPassword(Encoding.ASCII.GetBytes("user"));
        using var owner = new PdfPassword(Encoding.ASCII.GetBytes("owner"));
        var options = new PdfPasswordProtection(user, owner, PdfDocumentPermissions.Print | PdfDocumentPermissions.FillForms, cipher, metadata);
        PolicyTests.Run(context =>
        {
            using var created = PdfStandardSecurity.Create(options, new byte[16], context);
            Assert.Equal(cipher == PdfPasswordCipher.Aes256 ? 6 : 4, created.Revision);
            using var read = PdfStandardSecurity.Authenticate(created.Dictionary, new byte[16], Encoding.ASCII.GetBytes("user"), false, context);
            Assert.Equal(PdfAuthenticationRole.User, read.Role);
            Assert.Equal(created.Key, read.Key);
            Assert.Equal(metadata, read.EncryptMetadata);
        });
    }

    [Fact]
    public void RevisionSixRejectsPermissionTamperingAfterSuccessfulPasswordAuthentication()
    {
        using var user = new PdfPassword(Encoding.ASCII.GetBytes("user"));
        using var owner = new PdfPassword(Encoding.ASCII.GetBytes("owner"));
        Assert.Equal("Security.PermissionsIntegrity", Assert.Throws<PdfException>(() => PolicyTests.Run(context =>
        {
            using var security = PdfStandardSecurity.Create(new(user, owner, PdfDocumentPermissions.Print), new byte[16], context);
            var entries = security.Dictionary.Entries.ToDictionary(pair => pair.Key, pair => pair.Value);
            entries["P"] = PdfValue.Integer(-4);
            using var rejected = PdfStandardSecurity.Authenticate(PdfValue.Dictionary(entries), new byte[16], Encoding.ASCII.GetBytes("user"), false, context);
        })).Diagnostic.Code);
    }
}
