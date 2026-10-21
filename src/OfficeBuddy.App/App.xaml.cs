using Microsoft.UI.Xaml;

namespace OfficeBuddy.App;

public partial class App : Application
{
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
