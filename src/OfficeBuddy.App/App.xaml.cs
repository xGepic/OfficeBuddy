using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OfficeBuddy.App.Services;

namespace OfficeBuddy.App;

public partial class App : Application
{
    /// <summary>The control font sizes the text-size setting scales, at normal size, as App.xaml sets them.</summary>
    private static readonly Dictionary<string, double> NormalFontSizes = new()
    {
        ["ControlContentThemeFontSize"] = 16,
        ["ContentControlFontSize"] = 16,
        ["InfoBarTitleFontSize"] = 16,
        ["InfoBarMessageFontSize"] = 16,
    };

    /// <summary>
    /// The text styles the text-size setting scales, with their size at normal size. Their own font
    /// size keys can't be changed once the app runs, so each is replaced by one based on it.
    /// </summary>
    private static readonly Dictionary<string, double> NormalTextStyleSizes = new()
    {
        ["CaptionTextBlockStyle"] = 14,
        ["BodyTextBlockStyle"] = 16,
        ["BodyStrongTextBlockStyle"] = 16,
        ["SubtitleTextBlockStyle"] = 20,
        ["TitleTextBlockStyle"] = 28,
        ["TitleLargeTextBlockStyle"] = 40,
    };

    public App()
    {
        InitializeComponent();
    }

    public static MainWindow MainWindow { get; private set; } = null!;

    /// <summary>The text size the app was started with; a change in the settings applies on the next start.</summary>
    internal static TextSize TextSize { get; private set; }

    /// <summary>How much larger than normal the app shows its text, e.g. 1.25.</summary>
    public static double TextScale => TextSize switch
    {
        TextSize.Large => 1.25,
        TextSize.Larger => 1.5,
        _ => 1,
    };

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        TextSize = AppSettings.TextSize;
        if (TextSize != TextSize.Normal)
        {
            ScaleText();
        }

        // Files dragged onto the exe or its shortcut, or sent to it from File Explorer, come as arguments.
        var files = Environment.GetCommandLineArgs().Skip(1).Where(File.Exists).ToList();
        MainWindow = new MainWindow(files);
        MainWindow.Activate();
    }

    /// <summary>Makes the text larger by <see cref="TextScale"/>, before any screen is built.</summary>
    private void ScaleText()
    {
        foreach (var (key, size) in NormalFontSizes)
        {
            Resources[key] = Math.Round(size * TextScale);
        }

        foreach (var (key, size) in NormalTextStyleSizes)
        {
            var style = new Style(typeof(TextBlock)) { BasedOn = (Style)Resources[key] };
            style.Setters.Add(new Setter(TextBlock.FontSizeProperty, Math.Round(size * TextScale)));
            Resources[key] = style;
        }
    }
}
