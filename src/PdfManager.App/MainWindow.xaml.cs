using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using PdfManager.App.Pages;
using Windows.Graphics;
using WinRT.Interop;

namespace PdfManager.App;

public sealed partial class MainWindow : Window
{
    // Below this work area (in effective pixels, e.g. a 1920×1080 laptop at 150 %) the app opens maximized,
    // so the start screen's tiles fit without scrolling.
    private const double SmallScreenWidth = 1500;
    private const double SmallScreenHeight = 860;

    public MainWindow()
    {
        InitializeComponent();
        Title = Strings.Get("AppTitle");
        AppTitleText.Text = Title;
        Handle = WindowNative.GetWindowHandle(this);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var scale = MonitorScale();
        if (workArea.Width / scale < SmallScreenWidth || workArea.Height / scale < SmallScreenHeight)
        {
            (AppWindow.Presenter as OverlappedPresenter)?.Maximize();
        }
        else
        {
            CenterOnScreen(workArea, widthShare: 0.8, heightShare: 0.85);
        }

        RootFrame.Navigate(typeof(StartPage));
    }

    /// <summary>The native window handle, needed as owner for file dialogs.</summary>
    public nint Handle { get; }

    /// <summary>Sizes the window relative to the screen's work area, which already accounts for display scaling.</summary>
    private void CenterOnScreen(RectInt32 workArea, double widthShare, double heightShare)
    {
        var width = (int)(workArea.Width * widthShare);
        var height = (int)(workArea.Height * heightShare);
        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width,
            height));
    }

    /// <summary>The display scaling of the window's monitor, e.g. 1.5 for 150 %.</summary>
    private double MonitorScale()
    {
        // Asked of the monitor, not the window: the window is not shown yet and would report 96 DPI.
        var monitor = MonitorFromWindow(Handle, MonitorDefaultToPrimary);
        return GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpi, out _) == 0 ? dpi / 96.0 : 1.0;
    }

    private const uint MonitorDefaultToPrimary = 1;
    private const int MdtEffectiveDpi = 0;

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);
}
