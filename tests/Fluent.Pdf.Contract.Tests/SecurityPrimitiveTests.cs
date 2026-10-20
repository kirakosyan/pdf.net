using System.Security.Cryptography;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class SecurityPrimitiveTests
{
    private static byte[] Hex(string value) => Enumerable.Range(0, value.Length / 2).Select(i => Convert.ToByte(value.Substring(i * 2, 2), 16)).ToArray();
    [Fact]
    public void PlatformAesCbcMatchesThePublishedNistSp80038aVectorInBothDirections()
    {
        var key = Hex("2b7e151628aed2a6abf7158809cf4f3c");
        var iv = Hex("000102030405060708090a0b0c0d0e0f");
        var plain = Hex("6bc1bee22e409f96e93d7e117393172aae2d8a571e03ac9c9eb76fac45af8e51");
        var cipher = Hex("7649abac8119b246cee98e9b12e9197d5086cb9b507219ee95db113a917678b2");
        PolicyTests.Run(context =>
        {
            Assert.Equal(cipher, PdfSecurityCryptography.Aes(key, iv, plain, true, CipherMode.CBC, PaddingMode.None, context));
            Assert.Equal(plain, PdfSecurityCryptography.Aes(key, iv, cipher, false, CipherMode.CBC, PaddingMode.None, context));
        });
    }

    [Theory]
    [InlineData("75736572", "0001020304050607", "", "731758c09c8b0160a34721d18bdd24220abada0070aa3f05b8103fd5b8d05f17")]
    [InlineData("", "08090a0b0c0d0e0f", "", "280e9748351df9469e9a36529a54e059b8169fa20939811cdc4f6550af339b36")]
    [InlineData("cf80cf81cebfceb2cebfcebbceae", "2a2a2a2a2a2a2a2a", "", "8045ebd363c5e693bcbc405f7775a7e0edc5de667aec294a8cb29b824775cb5a")]
    [InlineData("6f776e6572", "0001020304050607", "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f202122232425262728292a2b2c2d2e2f", "f77bbaa82ba46eca0e3997ad8c91b9fa4a3f83750e13a950f901abef1f69b683")]
    public void Revision6HashMatchesIndependentUserOwnerEmptyAndPreparedUnicodeAnswers(string password, string salt, string user, string expected)
    {
        PolicyTests.Run(context => Assert.Equal(Hex(expected), PdfSecurityCryptography.Revision6Hash(Hex(password), Hex(salt), Hex(user), context)));
    }

    [Fact]
    public void ExplicitLegacyPrimitiveMatchesRfc6229AndZeroizesItsOwnedState()
    {
        PolicyTests.Run(context => Assert.Equal(Hex("b2396305f03dc027ccc3524a0a1118a8"), PdfSecurityCryptography.Arcfour(Hex("0102030405"), new byte[16], context)));
        var secret = new byte[]
        {
            1,
            2,
            3
        };
        PdfSecurityCryptography.Clear(secret);
        Assert.Equal(new byte[3], secret);
    }

    [Fact]
    public void CryptoBudgetAndMalformedBlocksFailBeforeReturningData()
    {
        Assert.Equal("Security.CipherExtent", Assert.Throws<PdfException>(() => PolicyTests.Run(context => PdfSecurityCryptography.Aes(new byte[16], new byte[16], new byte[15], false, CipherMode.CBC, PaddingMode.None, context))).Diagnostic.Code);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PolicyTests.Run(context => PdfSecurityCryptography.Revision6Hash(new byte[127], new byte[8], Array.Empty<byte>(), context), new(maximumExpandedBytes: 16384))).Diagnostic.Code);
    }
}
