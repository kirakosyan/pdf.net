using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Fluent.Pdf.Applications;

namespace Fluent.Pdf.ViewSamples;
// Reusable sample control; borrows its controller, owns only images/subscriptions/request lifetime.
internal sealed class PdfPageControl : UserControl
{
    private readonly PdfApplicationViewer _viewer;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly PictureBox _image = new()
    {
        Dock = DockStyle.Fill,
        SizeMode = PictureBoxSizeMode.Zoom,
        AccessibleName = "Rendered PDF page",
        TabStop = false
    };
    private readonly TextBox _text = new()
    {
        Dock = DockStyle.Bottom,
        Height = 100,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        AccessibleName = "Captured page text in source order",
        TabStop = true
    };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 28,
        AccessibleName = "PDF view status"
    };
    internal PdfPageControl(PdfApplicationViewer viewer)
    {
        _viewer = viewer;
        AccessibleName = "PDF page viewer";
        AccessibleRole = AccessibleRole.Document;
        TabStop = true;
        Dock = DockStyle.Fill;
        var commands = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            AccessibleName = "PDF page navigation",
            TabStop = false
        };
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
                Text = name,
                AccessibleName = name,
                AutoSize = true,
                TabStop = true
            };
            button.Click += async (_, _) => await CommandAsync(command);
            commands.Controls.Add(button);
        }

        Controls.Add(_image);
        Controls.Add(_text);
        Controls.Add(_status);
        Controls.Add(commands);
        _viewer.StateChanged += Changed;
    }

    internal string CapturedText => _text.Text;
    internal int LastDisplayedPage { get; private set; } = -1;
    internal string LastFailure { get; private set; } = "";

    private void Changed(object? sender, PdfApplicationViewState state)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        try
        {
            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed && _viewer.State.Generation == state.Generation)
                    _status.Text = state.IsBusy ? "Rendering page " + (state.PageIndex + 1) : state.DiagnosticCode.Length == 0 ? "Page " + (state.PageIndex + 1) + " of " + state.PageCount : state.DiagnosticCode;
            }));
        }
        catch (InvalidOperationException)when (IsDisposed || Disposing || !IsHandleCreated)
        {
        }
    }

    internal async Task RefreshPageAsync()
    {
        try
        {
            var frame = await _viewer.RenderAsync(_lifetime.Token);
            if (IsDisposed || frame.State.Generation != _viewer.State.Generation)
                return;
            using var png = new MemoryStream();
            frame.WritePng(png);
            png.Position = 0;
            using var decoded = Image.FromStream(png);
            var bitmap = new Bitmap(decoded);
            var old = _image.Image;
            _image.Image = bitmap;
            old?.Dispose();
            _text.Text = frame.CapturedText;
            LastDisplayedPage = frame.State.PageIndex;
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)when (IsDisposed || _lifetime.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            LastFailure = error is Fluent.Pdf.PdfException pdfError ? pdfError.Diagnostic.Code : error.GetType().Name;
            if (!IsDisposed)
                _status.Text = error is Fluent.Pdf.PdfException pdf ? pdf.Diagnostic.Code : "ApplicationView.Failed";
        }
    }

    internal async Task CommandAsync(PdfApplicationViewCommand command)
    {
        _viewer.Execute(command);
        if (command != PdfApplicationViewCommand.Cancel)
            await RefreshPageAsync();
    }

    internal Task PendingCommand { get; private set; } = Task.CompletedTask;

    internal bool RouteKey(Keys keys)
    {
        PdfApplicationViewCommand? command = keys switch
        {
            Keys.PageDown => PdfApplicationViewCommand.NextPage,
            Keys.PageUp => PdfApplicationViewCommand.PreviousPage,
            Keys.Home => PdfApplicationViewCommand.FirstPage,
            Keys.End => PdfApplicationViewCommand.LastPage,
            Keys.Control | Keys.Oemplus => PdfApplicationViewCommand.ZoomIn,
            Keys.Control | Keys.OemMinus => PdfApplicationViewCommand.ZoomOut,
            Keys.Control | Keys.D0 => PdfApplicationViewCommand.ActualSize,
            Keys.Escape => PdfApplicationViewCommand.Cancel,
            _ => null
        };
        if (!command.HasValue)
            return false;
        PendingCommand = CommandAsync(command.Value);
        return true;
    }

    internal bool DispatchKey(Keys key)
    {
        var message = Message.Create(Handle, 0x0100, new IntPtr((int)key), IntPtr.Zero);
        return PreProcessMessage(ref message);
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData) => _text.Focused && (keyData == Keys.Home || keyData == Keys.End || keyData == Keys.PageUp || keyData == Keys.PageDown) ? base.ProcessCmdKey(ref message, keyData) : RouteKey(keyData) || base.ProcessCmdKey(ref message, keyData);
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lifetime.Cancel();
            _viewer.StateChanged -= Changed;
            var image = _image.Image;
            _image.Image = null;
            image?.Dispose();
            _lifetime.Dispose();
        }

        base.Dispose(disposing);
    }
}
