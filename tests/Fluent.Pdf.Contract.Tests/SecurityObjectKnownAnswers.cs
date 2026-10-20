namespace Fluent.Pdf.Contract.Tests;
// Original document ciphertext and plaintext from the independent fixture report; all credentials and keys are public test data.
internal static class SecurityObjectKnownAnswers
{
    internal static IEnumerable<object[]> Values()
    {
        yield return new object[]
        {
            0,
            1,
            0,
            "3e06076261fdab36a0a8c0f0dffa403fe5dfc6ada5d1e6fd303362438ca300cd6cd5a55f283e8dd6712f",
            "feff004f0072006900670069006e0061006c00200055006e00690063006f0064006500202013002003c0",
            5,
            0,
            "1c4bbd95e12342554a4387f032876cab2a5ec75895735db6112cd8c4302856d776",
            "6f726967696e616c207365637265742066697874757265207061796c6f616400ff"
        };
        yield return new object[]
        {
            1,
            1,
            0,
            "6bf62f3655e38a4215fa3a3d52bfa73d8dff2b0a6ba825b119a70df5dd3e531e2099c879077145cea998",
            "feff004f0072006900670069006e0061006c00200055006e00690063006f0064006500202013002003c0",
            5,
            0,
            "863d2e434f506dfb82497efde74597ae002fe96ead9318214fc028bd5804894f4d",
            "6f726967696e616c207365637265742066697874757265207061796c6f616400ff"
        };
        yield return new object[]
        {
            2,
            1,
            0,
            "04d413d157395c8aa66109efc3d3964ef5c1e28fc1a90791d26828ad1d1e74318fc4ca2463f13adf6851f2479cfb5608e599adf434290f96dc76814dbf9c1686",
            "feff004f0072006900670069006e0061006c00200055006e00690063006f0064006500202013002003c0",
            5,
            0,
            "4da6feec7d7509b391164e3338684336aaf08c5b7b7ffc90d03169fb12782251b600bef7149b8e17a56527e34c2744fd64abdca51ab49251bf6289f9814f4903",
            "6f726967696e616c207365637265742066697874757265207061796c6f616400ff"
        };
        yield return new object[]
        {
            3,
            1,
            0,
            "64df7ede840926ba7fe06db78832aaf1da64de75fae69a22f36faf9fdcd20f2cddabc1139d673f84275c06e24546885363e8edd9e0fe1c5df106c7887e2652db",
            "feff004f0072006900670069006e0061006c00200055006e00690063006f0064006500202013002003c0",
            5,
            0,
            "84bae70abe6b1cf70a01b14e5119bfbfced55ddd77d0d4c58a7bb2a501aaa00bdc4de447eae1d699b61ca1732b44d9f09b035f7f345aef8397855d589feeeaab",
            "6f726967696e616c207365637265742066697874757265207061796c6f616400ff"
        };
        yield return new object[]
        {
            4,
            1,
            0,
            "9bad0b50242340b92f495fee3b9648f9981b4476636a453cf4119e16593e4f2ed7a7bcfa6100365a6d274c10ca9dfac9e149c8792362f8d15572ac787f3addac",
            "feff004f0072006900670069006e0061006c00200055006e00690063006f0064006500202013002003c0",
            5,
            0,
            "17d84fba71221ee74e23b8d4f8b82451973abc4aedb95df406cb93213055e46fc8fac2abec74aebbe8b1deeb06f38079aad8f3a70c85dd600f49f95341a6568b",
            "6f726967696e616c207365637265742066697874757265207061796c6f616400ff"
        };
    }
}
