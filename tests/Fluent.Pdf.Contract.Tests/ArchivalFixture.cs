using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Fluent.Pdf.Compliance;

namespace Fluent.Pdf.Contract.Tests;
// Explicit test-only external host. It exercises the real independent engine, not SDK OS-isolation qualification.
internal static class ArchivalFixture
{
    // Original LittleCMS-generated sRGB ICC v2.1 fixture; profile output may be used freely.
    private const string IccBytes = "AAAiXGxjbXMCEAAAbW50clJHQiBYWVogB+oACgASAA8AEAAhYWNzcEFQUEwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPbWAAEAAAAA0y1sY21zAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAALZGVzYwAAAQgAAACEY3BydAAAAYwAAAAhd3RwdAAAAbAAAAAUY2hhZAAAAcQAAAAsclhZWgAAAfAAAAAUYlhZWgAAAgQAAAAUZ1hZWgAAAhgAAAAUclRSQwAAAiwAACAMZ1RSQwAAAiwAACAMYlRSQwAAAiwAACAMY2hybQAAIjgAAAAkZGVzYwAAAAAAAAAOc1JHQiBidWlsdC1pbgAAAAAAAAAADgBzAFIARwBCACAAYgB1AGkAbAB0AC0AaQBuAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAdGV4dAAAAABObyBjb3B5cmlnaHQsIHVzZSBmcmVlbHkAAAAAWFlaIAAAAAAAAPbWAAEAAAAA0y1zZjMyAAAAAAABDEIAAAXe///zJQAAB5MAAP2Q///7of///aIAAAPcAADAblhZWiAAAAAAAABvoAAAOPUAAAOQWFlaIAAAAAAAACSfAAAPhAAAtsNYWVogAAAAAAAAYpcAALeHAAAY2WN1cnYAAAAAAAAQAAAAAAEAAgAEAAUABgAHAAkACgALAAwADgAPABAAEQATABQAFQAWABgAGQAaABsAHAAeAB8AIAAhACMAJAAlACYAKAApACoAKwAtAC4ALwAwADIAMwA0ADUANwA4ADkAOgA7AD0APgA/AEAAQgBDAEQARQBHAEgASQBKAEwATQBOAE8AUQBSAFMAVABVAFcAWABZAFoAXABdAF4AXwBhAGIAYwBkAGYAZwBoAGkAawBsAG0AbgBvAHEAcgBzAHQAdgB3AHgAeQB7AHwAfQB+AIAAgQCCAIMAhQCGAIcAiACJAIsAjACNAI4AkACRAJIAkwCVAJYAlwCYAJoAmwCcAJ0AnwCgAKEAogCkAKUApgCnAKgAqgCrAKwArQCvALAAsQCyALQAtQC2ALcAuQC6ALsAvAC+AL8AwADBAMIAxADFAMYAxwDJAMoAywDMAM4AzwDQANEA0wDUANUA1wDYANkA2gDcAN0A3gDgAOEA4gDkAOUA5gDoAOkA6gDsAO0A7wDwAPEA8wD0APYA9wD4APoA+wD9AP4A/wEBAQIBBAEFAQcBCAEKAQsBDQEOAQ8BEQESARQBFQEXARgBGgEbAR0BHwEgASIBIwElASYBKAEpASsBLQEuATABMQEzATQBNgE4ATkBOwE8AT4BQAFBAUMBRQFGAUgBSgFLAU0BTwFQAVIBVAFVAVcBWQFaAVwBXgFgAWEBYwFlAWcBaAFqAWwBbgFvAXEBcwF1AXYBeAF6AXwBfgF/AYEBgwGFAYcBiQGKAYwBjgGQAZIBlAGWAZcBmQGbAZ0BnwGhAaMBpQGnAakBqwGsAa4BsAGyAbQBtgG4AboBvAG+AcABwgHEAcYByAHKAcwBzgHQAdIB1AHWAdgB2gHcAd4B4QHjAeUB5wHpAesB7QHvAfEB8wH1AfgB+gH8Af4CAAICAgQCBwIJAgsCDQIPAhICFAIWAhgCGgIdAh8CIQIjAiUCKAIqAiwCLgIxAjMCNQI4AjoCPAI+AkECQwJFAkgCSgJMAk8CUQJTAlYCWAJaAl0CXwJhAmQCZgJpAmsCbQJwAnICdQJ3AnkCfAJ+AoECgwKGAogCiwKNApACkgKVApcCmgKcAp8CoQKkAqYCqQKrAq4CsAKzArUCuAK7Ar0CwALCAsUCyALKAs0CzwLSAtUC1wLaAt0C3wLiAuQC5wLqAuwC7wLyAvUC9wL6Av0C/wMCAwUDCAMKAw0DEAMTAxUDGAMbAx4DIAMjAyYDKQMsAy4DMQM0AzcDOgM9Az8DQgNFA0gDSwNOA1EDVANWA1kDXANfA2IDZQNoA2sDbgNxA3QDdwN6A30DgAOCA4UDiAOLA44DkQOUA5gDmwOeA6EDpAOnA6oDrQOwA7MDtgO5A7wDvwPCA8UDyQPMA88D0gPVA9gD2wPfA+ID5QPoA+sD7gPyA/UD+AP7A/4EAgQFBAgECwQPBBIEFQQYBBwEHwQiBCUEKQQsBC8EMwQ2BDkEPQRABEMERwRKBE0EUQRUBFcEWwReBGIEZQRoBGwEbwRzBHYEeQR9BIAEhASHBIsEjgSSBJUEmQScBKAEowSnBKoErgSxBLUEuAS8BL8EwwTGBMoEzgTRBNUE2ATcBOAE4wTnBOoE7gTyBPUE+QT9BQAFBAUIBQsFDwUTBRYFGgUeBSIFJQUpBS0FMQU0BTgFPAVABUMFRwVLBU8FUgVWBVoFXgViBWYFaQVtBXEFdQV5BX0FgQWEBYgFjAWQBZQFmAWcBaAFpAWoBawFrwWzBbcFuwW/BcMFxwXLBc8F0wXXBdsF3wXjBecF6wXvBfQF+AX8BgAGBAYIBgwGEAYUBhgGHAYhBiUGKQYtBjEGNQY5Bj4GQgZGBkoGTgZTBlcGWwZfBmMGaAZsBnAGdAZ5Bn0GgQaFBooGjgaSBpcGmwafBqQGqAasBrEGtQa5Br4GwgbGBssGzwbUBtgG3AbhBuUG6gbuBvIG9wb7BwAHBAcJBw0HEgcWBxsHHwckBygHLQcxBzYHOgc/B0MHSAdNB1EHVgdaB18HYwdoB20HcQd2B3sHfweEB4kHjQeSB5cHmwegB6UHqQeuB7MHtwe8B8EHxgfKB88H1AfZB90H4gfnB+wH8Qf1B/oH/wgECAkIDQgSCBcIHAghCCYIKwgvCDQIOQg+CEMISAhNCFIIVwhcCGEIZghrCHAIdQh6CH8IhAiJCI4IkwiYCJ0IoginCKwIsQi2CLsIwAjFCMoIzwjUCNkI3wjkCOkI7gjzCPgI/QkDCQgJDQkSCRcJHQkiCScJLAkxCTcJPAlBCUYJTAlRCVYJWwlhCWYJawlxCXYJewmBCYYJiwmRCZYJmwmhCaYJqwmxCbYJvAnBCcYJzAnRCdcJ3AniCecJ7QnyCfgJ/QoCCggKDQoTChkKHgokCikKLwo0CjoKPwpFCkoKUApWClsKYQpmCmwKcgp3Cn0KgwqICo4KlAqZCp8KpQqqCrAKtgq8CsEKxwrNCtMK2AreCuQK6grvCvUK+wsBCwcLDAsSCxgLHgskCyoLLws1CzsLQQtHC00LUwtZC18LZAtqC3ALdgt8C4ILiAuOC5QLmgugC6YLrAuyC7gLvgvEC8oL0AvWC9wL4gvpC+8L9Qv7DAEMBwwNDBMMGQwgDCYMLAwyDDgMPgxFDEsMUQxXDF0MZAxqDHAMdgx9DIMMiQyPDJYMnAyiDKgMrwy1DLsMwgzIDM4M1QzbDOEM6AzuDPUM+w0BDQgNDg0VDRsNIQ0oDS4NNQ07DUINSA1PDVUNXA1iDWkNbw12DXwNgw2JDZANlg2dDaQNqg2xDbcNvg3FDcsN0g3ZDd8N5g3sDfMN+g4BDgcODg4V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TpZOqU67Ts1O307yTwRPFk8pTztPTk9gT3JPhU+XT6pPvE/OT+FP81AGUBhQK1A9UFBQYlB1UIdQmlCtUL9Q0lDkUPdRCVEcUS9RQVFUUWdReVGMUZ9RsVHEUddR6VH8Ug9SIlI0UkdSWlJtUoBSklKlUrhSy1LeUvFTBFMWUylTPFNPU2JTdVOIU5tTrlPBU9RT51P6VA1UIFQzVEZUWVRsVH9UklSlVLhUy1TeVPJVBVUYVStVPlVRVWVVeFWLVZ5VsVXFVdhV61X+VhJWJVY4VktWX1ZyVoVWmVasVr9W01bmVvpXDVcgVzRXR1dbV25XgleVV6lXvFfQV+NX91gKWB5YMVhFWFhYbFiAWJNYp1i6WM5Y4lj1WQlZHVkwWURZWFlrWX9Zk1mnWbpZzlniWfZaCVodWjFaRVpZWmxagFqUWqhavFrQWuRa+FsLWx9bM1tHW1tbb1uDW5dbq1u/W9Nb51v7XA9cI1w3XEtcYFx0XIhcnFywXMRc2FzsXQFdFV0pXT1dUV1lXXpdjl2iXbZdy13fXfNeCF4cXjBeRF5ZXm1egl6WXqpev17TXude/F8QXyVfOV9OX2Jfd1+LX6BftF/JX91f8mAGYBtgL2BEYFhgbWCCYJZgq2C/YNRg6WD9YRJhJ2E7YVBhZWF6YY5ho2G4Yc1h4WH2YgtiIGI1YkliXmJzYohinWKyYsdi22LwYwVjGmMvY0RjWWNuY4NjmGOtY8Jj12PsZAFkFmQrZEBkVWRqZH9klWSqZL9k1GTpZP5lE2UpZT5lU2VoZX1lk2WoZb1l0mXoZf1mEmYnZj1mUmZnZn1mkmanZr1m0mboZv1nEmcoZz1nU2doZ35nk2epZ75n1GfpZ/9oFGgqaD9oVWhqaIBolmiraMFo1mjsaQJpF2ktaUNpWGluaYRpmWmvacVp22nwagZqHGoyakhqXWpzaolqn2q1aspq4Gr2awxrIms4a05rZGt6a5Brpmu8a9Jr6Gv+bBRsKmxAbFZsbGyCbJhsrmzEbNps8G0GbRxtM21JbV9tdW2LbaFtuG3ObeRt+m4RbiduPW5TbmpugG6Wbq1uw27ZbvBvBm8cbzNvSW9gb3ZvjG+jb7lv0G/mb/1wE3AqcEBwV3BtcIRwmnCxcMdw3nD0cQtxInE4cU9xZnF8cZNxqnHAcddx7nIEchtyMnJIcl9ydnKNcqRyunLRcuhy/3MWcyxzQ3Nac3FziHOfc7ZzzXPkc/p0EXQodD90VnRtdIR0m3SydMl04HT3dQ51JnU9dVR1a3WCdZl1sHXHdd519nYNdiR2O3ZSdmp2gXaYdq92x3bedvV3DHckdzt3Undqd4F3mHewd8d33nf2eA14JXg8eFR4a3iCeJp4sXjJeOB4+HkPeSd5PnlWeW55hXmdebR5zHnjeft6E3oqekJ6Wnpxeol6oXq4etB66HsAexd7L3tHe197dnuOe6Z7vnvWe+58BXwdfDV8TXxlfH18lXytfMV83Hz0fQx9JH08fVR9bH2EfZx9tH3NfeV9/X4Vfi1+RX5dfnV+jX6lfr5+1n7ufwZ/Hn83f09/Z39/f5d/sH/If+B/+YARgCmAQYBagHKAioCjgLuA1IDsgQSBHYE1gU6BZoF/gZeBsIHIgeGB+YISgiqCQ4JbgnSCjIKlgr6C1oLvgweDIIM5g1GDaoODg5uDtIPNg+WD/oQXhDCESIRhhHqEk4SshMSE3YT2hQ+FKIVBhVqFcoWLhaSFvYXWhe+GCIYhhjqGU4ZshoWGnoa3htCG6YcChxuHNIdNh2eHgIeZh7KHy4fkh/2IF4gwiEmIYoh7iJWIrojHiOCI+okTiSyJRolfiXiJkYmricSJ3on3ihCKKopDil2KdoqPiqmKworcivWLD4soi0KLW4t1i46LqIvCi9uL9YwOjCiMQoxbjHWMj4yojMKM3Iz1jQ+NKY1CjVyNdo2QjamNw43djfeOEY4rjkSOXo54jpKOrI7GjuCO+o8Tjy2PR49hj3uPlY+vj8mP44/9kBeQMZBLkGWQf5CakLSQzpDokQKRHJE2kVCRa5GFkZ+RuZHTke6SCJIikjySV5JxkouSppLAktqS9JMPkymTRJNek3iTk5Otk8iT4pP8lBeUMZRMlGaUgZSblLaU0JTrlQWVIJU7lVWVcJWKlaWVwJXalfWWD5YqlkWWX5Z6lpWWsJbKluWXAJcblzWXUJdrl4aXoZe7l9aX8ZgMmCeYQphdmHeYkpitmMiY45j+mRmZNJlPmWqZhZmgmbuZ1pnxmgyaJ5pCml6aeZqUmq+ayprlmwCbHJs3m1KbbZuIm6Sbv5vam/WcEZwsnEecY5x+nJmctZzQnOudB50inT2dWZ10nZCdq53GneKd/Z4ZnjSeUJ5rnoeeop6+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";
    internal static PdfColorSpace Icc()
    {
        using var input = new MemoryStream(Convert.FromBase64String(IccBytes));
        return PdfColorSpace.Icc(input);
    }

    internal static string Root()
    {
        var path = new DirectoryInfo(AppContext.BaseDirectory);
        while (path != null && !File.Exists(Path.Combine(path.FullName, "Fluent.Pdf.slnx")))
            path = path.Parent;
        return path?.FullName ?? throw new InvalidOperationException("Source fixture root was not found.");
    }

    internal static PdfProviderRegistry Providers() => new(new VeraPdfReportValidator(Run));
    private static void Run(PdfArchivalProfile profile, Stream input, Stream report, PdfOperationContext context)
    {
        string jar = Path.Combine(Root(), "artifacts", "archival-fixtures", "greenfield-apps-1.28.2.jar");
        if (!File.Exists(jar))
            throw new InvalidOperationException("Provision the pinned independent validator with eng/build-archival-fixture.ps1 before source tests.");
        string directory = Path.Combine(Root(), "artifacts", "contracts", "archival", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "original-source-candidate.pdf");
        using (var output = File.Create(file))
            Copy(input, output, context, long.MaxValue);
        // Source staging is an explicit test-host decision. The optional SDK report bridge creates no files or processes.
        var start = new ProcessStartInfo("java", "-Djava.awt.headless=true -Xms16m -Xmx128m -XX:ActiveProcessorCount=2 -cp " + Quote(jar) + " org.verapdf.apps.GreenfieldCliWrapper --format xml --flavour " + PdfArchival.ProfileIdentifier(profile) + " " + Quote(file))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var worker = context.EnterScope(PdfResourceKind.Workers);
        using var process = new Process
        {
            StartInfo = start
        };
        context.Checkpoint();
        process.Start();
        var clock = Stopwatch.StartNew();
        var stdout = Task.Run(() =>
        {
            using var retained = File.Create(Path.Combine(directory, "validation.xml"));
            Copy(process.StandardOutput.BaseStream, report, context, 16777216, retained);
        });
        var stderr = Task.Run(() =>
        {
            using var retained = File.Create(Path.Combine(directory, "stderr.txt"));
            Copy(process.StandardError.BaseStream, retained, context, 65536);
        });
        try
        {
            while (!process.WaitForExit(25))
            {
                context.Checkpoint();
                if (clock.Elapsed > TimeSpan.FromSeconds(30))
                    throw new TimeoutException("Independent source validator exceeded its test deadline.");
            }

            while (!Task.WaitAll(new[] { stdout, stderr }, 25))
            {
                context.Checkpoint();
                if (clock.Elapsed > TimeSpan.FromSeconds(30))
                    throw new TimeoutException("Independent source validator output exceeded its test deadline.");
            }

            context.Checkpoint();
        // The CLI uses nonzero exit for a completed noncompliant result; exact report completeness decides the result.
        }
        finally
        {
            if (!process.HasExited)
                process.Kill();
            process.WaitForExit(5000);
            try
            {
                Task.WaitAll(new[] { stdout, stderr }, 5000);
            }
            catch (AggregateException)
            {
                context.Checkpoint();
                throw;
            }
        }
    }

    private static void Copy(Stream source, Stream output, PdfOperationContext context, long maximum, Stream? retained = null)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 4096);
        var part = new byte[4096];
        long total = 0;
        int count;
        while ((count = source.Read(part, 0, part.Length)) > 0)
        {
            context.Checkpoint();
            if (count > maximum - total)
                throw new InvalidOperationException("Independent fixture host output exceeded its declared bound.");
            context.Consume(PdfResourceKind.ExpandedBytes, count);
            total += count;
            output.Write(part, 0, count);
            retained?.Write(part, 0, count);
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
