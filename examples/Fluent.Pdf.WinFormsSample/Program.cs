using System.Windows.Forms;
using Fluent.Pdf.Applications;

namespace Fluent.Pdf.ViewSamples;
internal static class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        try
        {
#if !NETFRAMEWORK
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
#endif
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using var session = ViewerSampleSession.Open(arguments);
            using var window = new Form
            {
                Text = "PDF application integration",
                Width = 900,
                Height = 700
            };
            var control = new PdfPageControl(session.Viewer);
            window.Controls.Add(control);
            int result = 0;
            string stage = "initial-render";
            window.Shown += async (_, _) =>
            {
                try
                {
                    await control.RefreshPageAsync();
                    if (session.IsFixture)
                    {
                        stage = "initial-page";
                        if (control.LastDisplayedPage != 0 || control.CapturedText != "Original automation Ω")
                            throw new InvalidOperationException("WinForms initial page failed.");
                        stage = "accessible-name-and-role";
                        if (control.AccessibilityObject.Name != "PDF page viewer" || control.AccessibilityObject.Role != AccessibleRole.Document)
                            throw new InvalidOperationException("WinForms accessible view boundary failed.");
                        stage = "next-page";
                        await control.CommandAsync(PdfApplicationViewCommand.NextPage);
                        stage = "keyboard-preprocess";
                        if (control.LastDisplayedPage != 1 || control.CapturedText != "Second automation page" || !control.DispatchKey(Keys.Home))
                            throw new InvalidOperationException("WinForms navigation boundary failed.");
                        await control.PendingCommand;
                        stage = "keyboard-page";
                        if (control.LastDisplayedPage != 0)
                            throw new InvalidOperationException("WinForms routed keyboard page failed.");
                        Console.WriteLine("WINFORMS_VIEW_PASS;" + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + ";" + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture + ";fixture-renderer;keyboard-and-MSAA");
                    }
                }
                catch (Exception error)
                {
                    result = 1;
                    Console.Error.WriteLine("WINFORMS_VIEW_FAILED;" + stage + ";" + error.GetType().Name + ";" + control.LastFailure);
                }
                finally
                {
                    if (session.IsFixture)
                        window.Close();
                }
            };
            Application.Run(window);
            return result;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error is Fluent.Pdf.PdfException pdf ? pdf.Diagnostic.Code : "ApplicationSample.ConfigurationFailed");
            return 2;
        }
    }
}
