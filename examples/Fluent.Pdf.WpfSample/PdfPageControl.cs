using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Fluent.Pdf.Applications;

namespace Fluent.Pdf.ViewSamples;
// Reusable sample control. Host owns the controller; this control owns image/subscription/request state.
internal sealed class PdfPageControl : UserControl, IDisposable
{
    private readonly PdfApplicationViewer _viewer;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    private readonly Image _image = new()
    {
        Stretch = Stretch.Uniform,
        Focusable = false
    };
    private readonly TextBox _text = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Height = 100,
        IsTabStop = true
    };
    private readonly TextBlock _status = new();
    internal PdfPageControl(PdfApplicationViewer viewer)
    {
        _viewer = viewer;
        Focusable = true;
        IsTabStop = true;
        AutomationProperties.SetName(this, "PDF page viewer");
        AutomationProperties.SetHelpText(this, "Page Up/Down, Home/End, Control plus/minus, Control zero and Escape. Captured text is available separately.");
        AutomationProperties.SetName(_image, "Rendered PDF page");
        AutomationProperties.SetName(_text, "Captured page text in source order");
        AutomationProperties.SetName(_status, "PDF view status");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        var layout = new DockPanel();
        var commands = new WrapPanel();
        DockPanel.SetDock(commands, Dock.Top);
        DockPanel.SetDock(_text, Dock.Bottom);
        DockPanel.SetDock(_status, Dock.Bottom);
        Add("Previous page", PdfApplicationViewCommand.PreviousPage);
        Add("Next page", PdfApplicationViewCommand.NextPage);
        Add("Zoom in", PdfApplicationViewCommand.ZoomIn);
        Add("Zoom out", PdfApplicationViewCommand.ZoomOut);
        Add("Actual size", PdfApplicationViewCommand.ActualSize);
        Add("Cancel rendering", PdfApplicationViewCommand.Cancel);
        void Add(string name, PdfApplicationViewCommand command)
        {
            var button = new Button
            {
                Content = name,
                Margin = new Thickness(2),
                IsTabStop = true
            };
            AutomationProperties.SetName(button, name);
            button.Click += async (_, _) => await CommandAsync(command);
            commands.Children.Add(button);
        }

        layout.Children.Add(commands);
        layout.Children.Add(_text);
        layout.Children.Add(_status);
        layout.Children.Add(_image);
        Content = layout;
        _viewer.StateChanged += Changed;
        KeyDown += (_, args) =>
        {
            if (!_text.IsKeyboardFocusWithin && RouteKey(args.Key, Keyboard.Modifiers))
                args.Handled = true;
        };
    }

    internal string CapturedText => _text.Text;
    internal int LastDisplayedPage { get; private set; } = -1;
    internal string LastFailure { get; private set; } = "";
    internal Task PendingCommand { get; private set; } = Task.CompletedTask;

    private void Changed(object? sender, PdfApplicationViewState state)
    {
        if (_disposed || Dispatcher.HasShutdownStarted)
            return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_disposed && _viewer.State.Generation == state.Generation)
                _status.Text = state.IsBusy ? "Rendering page " + (state.PageIndex + 1) : state.DiagnosticCode.Length == 0 ? "Page " + (state.PageIndex + 1) + " of " + state.PageCount : state.DiagnosticCode;
        }));
    }

    internal async Task RefreshPageAsync()
    {
        try
        {
            var frame = await _viewer.RenderAsync(_lifetime.Token);
            if (_disposed || frame.State.Generation != _viewer.State.Generation)
                return;
            using var png = new MemoryStream();
            frame.WritePng(png);
            png.Position = 0;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = png;
            bitmap.EndInit();
            bitmap.Freeze();
            _image.Source = bitmap;
            _text.Text = frame.CapturedText;
            LastDisplayedPage = frame.State.PageIndex;
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)when (_disposed || _lifetime.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            LastFailure = error is Fluent.Pdf.PdfException pdfError ? pdfError.Diagnostic.Code : error.GetType().Name;
            if (!_disposed)
                _status.Text = error is Fluent.Pdf.PdfException pdf ? pdf.Diagnostic.Code : "ApplicationView.Failed";
        }
    }

    internal async Task CommandAsync(PdfApplicationViewCommand command)
    {
        _viewer.Execute(command);
        if (command != PdfApplicationViewCommand.Cancel)
            await RefreshPageAsync();
    }

    internal bool RouteKey(Key key, ModifierKeys modifiers)
    {
        PdfApplicationViewCommand? command = (key, modifiers) switch
        {
            (Key.PageDown, ModifierKeys.None) => PdfApplicationViewCommand.NextPage,
            (Key.PageUp, ModifierKeys.None) => PdfApplicationViewCommand.PreviousPage,
            (Key.Home, ModifierKeys.None) => PdfApplicationViewCommand.FirstPage,
            (Key.End, ModifierKeys.None) => PdfApplicationViewCommand.LastPage,
            (Key.OemPlus, ModifierKeys.Control) => PdfApplicationViewCommand.ZoomIn,
            (Key.OemMinus, ModifierKeys.Control) => PdfApplicationViewCommand.ZoomOut,
            (Key.D0, ModifierKeys.Control) => PdfApplicationViewCommand.ActualSize,
            (Key.Escape, ModifierKeys.None) => PdfApplicationViewCommand.Cancel,
            _ => null
        };
        if (!command.HasValue)
            return false;
        PendingCommand = CommandAsync(command.Value);
        return true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PagePeer(this);
    private sealed class PagePeer : FrameworkElementAutomationPeer
    {
        internal PagePeer(PdfPageControl owner) : base(owner)
        {
        }

        protected override string GetClassNameCore() => "PdfPageControl";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _lifetime.Cancel();
        _viewer.StateChanged -= Changed;
        _image.Source = null;
        _lifetime.Dispose();
    }
}
