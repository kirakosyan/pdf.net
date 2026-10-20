namespace Fluent.Pdf.Contract.Tests;
// Original test-document parameters independently computed by pypdf 6.10.0. These are public test keys, never production credentials.
internal static class SecurityKnownAnswers
{
    internal static IEnumerable<object[]> PasswordHandlers()
    {
        yield return new object[]
        {
            2,
            1,
            40,
            4294967236L,
            "6337346531633135643365633736393561313537393231316130663565393436",
            "bcaad72d05",
            "94e8094419662a774442fb072e3d9f19e9d130ec09a4d0061e78fe920f7ab62f",
            "efb1b1a3f37f0f9dbf1360a4c2037505adb128410dfb1027c31db615433f60af",
            "",
            "",
            ""
        };
        yield return new object[]
        {
            3,
            2,
            128,
            4294964164L,
            "6337346531633135643365633736393561313537393231316130663565393436",
            "6392d72783aebd7df4e165f7508bf10d",
            "0ba3835f88f90388e74e54584125ce142be0de24c6b0d37746e075b891756671",
            "7c644c60ffe502fd39184dac8a7c1b4028bf4e5e4e758a4164004e56fffa0108",
            "",
            "",
            ""
        };
        yield return new object[]
        {
            4,
            4,
            128,
            4294964164L,
            "6337346531633135643365633736393561313537393231316130663565393436",
            "6392d72783aebd7df4e165f7508bf10d",
            "0ba3835f88f90388e74e54584125ce142be0de24c6b0d37746e075b891756671",
            "7c644c60ffe502fd39184dac8a7c1b4028bf4e5e4e758a4164004e56fffa0108",
            "",
            "",
            ""
        };
        yield return new object[]
        {
            5,
            5,
            256,
            4294964164L,
            "6337346531633135643365633736393561313537393231316130663565393436",
            "0bf9f2f372fb931cec71174386d153c2bf0bb4ce934da10b7487f58115591cff",
            "6e4fcfec2d7518bfd3b5b962010ccac259479b463cdf568c9f8117d88e9c7f4e125a2175fc6b7e9a8c32275c4c05c09f",
            "0f44dcac70ef8d78ae53e46bc5319996c8b045f7ce5358e14b81c7c881efd8041f9ca487596986abdc06f0330a9cb0f7",
            "daeaffe2942791befdf2f5d7a539ad92f36b4398fa56391c9d23eda59534895f",
            "fe9ce343b30559310b7303d8cbc9e8bc7fced8917c317e081d3db0656f5e89f4",
            "a4e02e21cb550cb2216fb6b9e1fd32af"
        };
        yield return new object[]
        {
            6,
            5,
            256,
            4294964164L,
            "6337346531633135643365633736393561313537393231316130663565393436",
            "3dbfc066bdf8c180cbedb2d201bede95aa92d6866fe0cd0e8753e89eb6e50eaf",
            "51c282aa2a325498e15a7d5d41325b3dd521c24b3b91461798b1c35a53a88d6b9e6aa78339c26b76eca4775626d5ee24",
            "ae6a1fc2fb58e15dc4c751bc02759c45380c2c9a8ffd64920513163abf4c39498a01468a6821bf650e5367a0a120000a",
            "0b1c46078db3f824d6ba9f9cb93c22f1cf57552b8fff2d6ff8400456a4d1173f",
            "1f61e22d7a50fdacc608697c95579cd1b6f1066a53e21d2028c71aa04758bc8c",
            "0d083329e8296996d91d5e3f05956463"
        };
    }
}
