#r "../../src/Fluent.Pdf/bin/Release/net10.0/Fluent.Pdf.dll"
#r "../../src/Fluent.Pdf.Formats/bin/Release/net10.0/Fluent.Pdf.Formats.dll"
#r "../../src/Fluent.Pdf.Fonts/bin/Release/net10.0/Fluent.Pdf.Fonts.dll"

open System
open System.IO
open Fluent.Pdf
open Fluent.Pdf.Formats
open Fluent.Pdf.Fonts

// Original source consumer; no package or minimum-runtime qualification implied.
let run () =
    let outputDirectory = fsi.CommandLineArgs |> Array.skip 1 |> Array.exactlyOne |> Path.GetFullPath
    Directory.CreateDirectory(outputDirectory) |> ignore
    let fontPath = Path.Combine(__SOURCE_DIRECTORY__, "../../tests/Fluent.Pdf.Contract.Tests/Fixtures/text/latin.ttf")
    use fontInput = File.OpenRead(fontPath)
    let font = PdfFont.Read(fontInput, PdfProviderRegistry(ManagedFontProvider.Instance))
    let tasks = PdfTasks(PdfProviderRegistry(PdfSimpleTextShaper()))
    let style = PdfFlowTextStyle(PdfFontFallback(font), 12.)
    let blocks : PdfFlowBlock array = [| PdfFlowParagraph("office office", style) |]
    let geometry = PdfPageGeometry(PdfPageBoxes(PdfRect(0., 0., 120., 100.)))
    let layout = PdfFlowOptions(geometry, PdfMargins.Uniform(5.))
    use output = new MemoryStream()
    let report = tasks.Generate(blocks, layout, output)
    if not report.Succeeded || not output.CanWrite then failwith "Facade generation failed"
    output.Position <- 0L
    use document = Pdf.Open(output)
    let text = tasks.Extract(document.CreateSnapshot(), 0).Text
    if text <> "office office" then failwith "Facade text mismatch"
    File.WriteAllBytes(Path.Combine(outputDirectory, "fsharp.pdf"), output.ToArray())
    printfn "FSHARP_TASK_FACADE_PASS;net10;source-only"
run ()
