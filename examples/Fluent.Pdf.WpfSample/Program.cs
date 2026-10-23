using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;

namespace Fluent.Pdf.ViewSamples;
internal static class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        try
        {
            using var session = ViewerSampleSession.Open(arguments);
            var application = new Application();
            using var control = new PdfPageControl(session.Viewer);
            var window = new Window
            {
                Title = "PDF application integration",
                Width = 900,
                Height = 700,
                Content = control
            };
            int result = 0;
            string stage = "initial-render";
            window.Loaded += async (_, _) =>
            {
                try
                {
                    await control.RefreshPageAsync();
                    if (session.IsFixture)
                    {
                        stage = "initial-page-and-peer";
                        var peer = UIElementAutomationPeer.CreatePeerForElement(control);
                        if (control.LastDisplayedPage != 0 || control.CapturedText != "Original automation Ω" || peer == null || peer.GetName() != "PDF page viewer" || peer.GetAutomationControlType() != AutomationControlType.Document)
                            throw new InvalidOperationException("WPF accessible view boundary failed.");
                        control.Focus();
                        stage = "keyboard-route";
                        var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(control), 0, Key.PageDown)
                        {
                            RoutedEvent = Keyboard.KeyDownEvent
                        };
                        control.RaiseEvent(key);
                        if (!key.Handled)
                            throw new InvalidOperationException("WPF key route missing.");
                        await control.PendingCommand;
                        stage = "keyboard-page";
                        if (control.LastDisplayedPage != 1 || control.CapturedText != "Second automation page")
                            throw new InvalidOperationException("WPF routed page boundary failed.");
                        Console.WriteLine("WPF_VIEW_PASS;" + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + ";" + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture + ";fixture-renderer;keyboard-and-UIA");
                    }
                }
                catch (Exception error)
                {
                    result = 1;
                    Console.Error.WriteLine("WPF_VIEW_FAILED;" + stage + ";" + error.GetType().Name + ";" + control.LastFailure);
                }
                finally
                {
                    if (session.IsFixture)
                        window.Close();
                }
            };
            application.Run(window);
            return result;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error is Fluent.Pdf.PdfException pdf ? pdf.Diagnostic.Code : "ApplicationSample.ConfigurationFailed");
            return 2;
        }
    }
}
