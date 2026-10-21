using Microsoft.UI.Xaml;

namespace PdfManager.App;

public partial class App : Application
{
    /// <summary>Held while the app runs, so the uninstaller can ask to close it first (installer\PDF-Manager.iss).</summary>
    private static readonly Mutex Running = new(false, "PdfManager.Running");

    public App()
    {
        InitializeComponent();
    }

    public static MainWindow MainWindow { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
