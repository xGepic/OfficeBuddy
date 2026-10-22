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
        // Files dragged onto the exe or its shortcut, or sent to it from File Explorer, come as arguments.
        var files = Environment.GetCommandLineArgs().Skip(1).Where(File.Exists).ToList();
        MainWindow = new MainWindow(files);
        MainWindow.Activate();
    }
}
