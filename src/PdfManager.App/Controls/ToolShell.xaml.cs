using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PdfManager.App.Services;
using PdfManager.Core;

namespace PdfManager.App.Controls;

/// <summary>
/// The frame every Tool screen shares: back button, title, undo/redo and Öffnen buttons, a drop
/// zone for files from File Explorer, a message bar, the Tool's own body, and a footer for its main
/// action. It asks "Änderungen verwerfen?" before the Tool is left with unsaved changes.
/// </summary>
public sealed partial class ToolShell : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ToolShell), new PropertyMetadata(""));

    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(nameof(Body), typeof(object), typeof(ToolShell), new PropertyMetadata(null));

    public static readonly DependencyProperty FooterProperty =
        DependencyProperty.Register(nameof(Footer), typeof(object), typeof(ToolShell), new PropertyMetadata(null));

    private static readonly TimeSpan BusyOverlayDelay = TimeSpan.FromMilliseconds(300);
    private readonly DispatcherQueueTimer _busyOverlayTimer;
    private IToolSession? _session;
    private bool _isConfirmingDiscard;
    private bool _discardConfirmedForClose;

    public ToolShell()
    {
        InitializeComponent();
        // handledEventsToo: list views inside the body handle drag events for their own reordering.
        Root.AddHandler(DragOverEvent, new DragEventHandler(Root_DragOver), handledEventsToo: true);
        Root.AddHandler(DropEvent, new DragEventHandler(Root_Drop), handledEventsToo: true);

        _busyOverlayTimer = DispatcherQueue.CreateTimer();
        _busyOverlayTimer.Interval = BusyOverlayDelay;
        _busyOverlayTimer.IsRepeating = false;
        _busyOverlayTimer.Tick += (_, _) => BusyOverlay.Opacity = 1;

        Loaded += (_, _) =>
        {
            // Loaded can come more than once; subscribe only once.
            App.MainWindow.AppWindow.Closing -= AppWindow_Closing;
            App.MainWindow.AppWindow.Closing += AppWindow_Closing;
        };
        Unloaded += (_, _) => App.MainWindow.AppWindow.Closing -= AppWindow_Closing;
    }

    public bool IsBusy { get; private set; }

    public event EventHandler? BackRequested;

    /// <summary>Raised with the paths of files picked with Öffnen or dropped onto the screen.</summary>
    public event EventHandler<IReadOnlyList<string>>? FilesOpened;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>What Öffnen offers and which files count as unsupported: PDF Documents unless the Tool says otherwise.</summary>
    internal OpenableFiles Opens { get; set; } = OpenableFiles.Documents;

    /// <summary>The Tool's session, which the undo/redo buttons and the discard prompt work on.</summary>
    public IToolSession? Session
    {
        get => _session;
        set
        {
            _session?.Changed -= Session_Changed;

            _session = value;
            _session?.Changed += Session_Changed;
            UpdateUndoRedoButtons();
        }
    }

    /// <summary>
    /// Blocks the screen while Documents load or save, with <paramref name="message"/> shown if it
    /// takes a moment. Dispose the result when the work is done.
    /// </summary>
    public IDisposable ShowBusy(string message)
    {
        IsBusy = true;
        BusyText.Text = message;
        BackButton.IsEnabled = false;
        OpenButton.IsEnabled = false;
        UndoButton.IsEnabled = false;
        RedoButton.IsEnabled = false;
        BusyOverlay.Visibility = Visibility.Visible;
        _busyOverlayTimer.Start();
        return new BusyScope(this);
    }

    /// <summary>Replaces the text shown while busy, e.g. to show progress.</summary>
    public void UpdateBusyMessage(string message) => BusyText.Text = message;

    private void HideBusy()
    {
        _busyOverlayTimer.Stop();
        BusyOverlay.Opacity = 0;
        BusyOverlay.Visibility = Visibility.Collapsed;
        BackButton.IsEnabled = true;
        OpenButton.IsEnabled = true;
        IsBusy = false;
        UpdateUndoRedoButtons();
    }

    private sealed class BusyScope(ToolShell shell) : IDisposable
    {
        public void Dispose() => shell.HideBusy();
    }

    public void ShowSuccess(string message) => ShowMessage(InfoBarSeverity.Success, message);

    public void ShowError(string message) => ShowMessage(InfoBarSeverity.Error, message);

    public void ShowInfo(string message) => ShowMessage(InfoBarSeverity.Informational, message);

    /// <summary>
    /// Saves the Tool's result to one file: asks where with a save dialog for
    /// <paramref name="fileType"/> (a PDF Document unless the Tool says otherwise) that opens in
    /// <paramref name="folder"/> with <paramref name="fileName"/> filled in, then runs
    /// <paramref name="save"/> while the screen is busy and reports how it went. Never throws.
    /// </summary>
    internal Task SaveToFileAsync(string? folder, string fileName, Func<string, Task> save, FileFilter? fileType = null) =>
        SaveThroughDialogAsync(
            () => FileDialogs.SaveFile(App.MainWindow.Handle, Strings.Get("SaveDialogTitle"), fileType ?? FileFilter.Pdf, folder, fileName),
            // The save dialog itself asks before overwriting.
            _ => Task.FromResult(true),
            async path =>
            {
                await save(path);
                ShowSuccess(Strings.Format("SavedAs", Path.GetFileName(path), Path.GetDirectoryName(path)!));
            },
            path => Strings.Format("SaveFailed", Path.GetFileName(path)));

    /// <summary>
    /// Saves several files at once: asks once for a folder and base name with a save dialog for
    /// <paramref name="fileType"/> that opens in <paramref name="folder"/> with
    /// <paramref name="baseName"/> filled in, asks before
    /// replacing files of the names <paramref name="fileNamesFor"/> gives for that base name, then
    /// runs <paramref name="save"/> (folder, base name, returning the paths written) while the
    /// screen is busy, and offers to open the folder. Never throws.
    /// </summary>
    internal Task SaveToFolderAsync(
        string? folder, string baseName, FileFilter fileType, Func<string, IReadOnlyList<string>> fileNamesFor,
        Func<string, string, Task<IReadOnlyList<string>>> save) =>
        SaveThroughDialogAsync(
            () => FileDialogs.SaveFile(App.MainWindow.Handle, Strings.Get("SaveSeveralDialogTitle"), fileType,
                folder, baseName, askBeforeOverwriting: false),
            path => ConfirmReplacingAsync(Path.GetDirectoryName(path)!, fileNamesFor(Path.GetFileNameWithoutExtension(path))),
            async path =>
            {
                var written = await save(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path));
                ShowSavedFiles(written);
            },
            path => Strings.Format("SaveSeveralFailed", Path.GetDirectoryName(path)!));

    /// <summary>Asks before files that are already in the folder get replaced; true if none are or the user agrees.</summary>
    private async Task<bool> ConfirmReplacingAsync(string folder, IReadOnlyList<string> fileNames)
    {
        var existing = fileNames.Where(name => File.Exists(Path.Combine(folder, name))).ToList();
        return existing.Count == 0 || await AskAsync(
            Strings.Get("ReplaceFilesTitle"),
            existing.Count == 1
                ? Strings.Format("ReplaceFilesOne", existing[0])
                : Strings.Format("ReplaceFilesMany", existing[0], existing.Count - 1),
            Strings.Get("ReplaceFilesConfirm"));
    }

    private async Task SaveThroughDialogAsync(
        Func<string?> choosePath, Func<string, Task<bool>> confirm, Func<string, Task> saveTo, Func<string, string> failureMessage)
    {
        string? path = null;
        try
        {
            path = choosePath();
            if (path is null || !await confirm(path))
            {
                return;
            }

            using var busy = ShowBusy(Strings.Get("BusySaving"));
            await saveTo(path);
        }
        catch (Exception ex) when (path is not null && ex is IOException or UnauthorizedAccessException)
        {
            // In use by another program, read-only, or a folder without write access.
            ShowError(failureMessage(path));
        }
        catch (COMException)
        {
            ShowError(Strings.Get("DialogFailed"));
        }
        catch (Exception)
        {
            ShowError(Strings.Get("SaveFailedUnexpected"));
        }
    }

    /// <summary>Confirms several saved files, with a button that shows them in File Explorer.</summary>
    private void ShowSavedFiles(IReadOnlyList<string> written)
    {
        ShowSuccess(Strings.Format("SavedSeveral", written.Count,
            Path.GetFileName(written[0]), Path.GetFileName(written[^1]), Path.GetDirectoryName(written[0])!));
        var openFolder = new Button { Content = Strings.Get("OpenFolder") };
        openFolder.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{written[0]}\"") { UseShellExecute = true });
            }
            catch (Exception)
            {
                // Explorer would not start; the message already says where the files are.
            }
        };
        MessageBar.ActionButton = openFolder;
    }

    /// <summary>
    /// Explains which files could not be opened and why, one sentence per reason. Shows nothing if
    /// every file opened.
    /// </summary>
    public void ShowLoadFailures(IReadOnlyList<LoadFailure> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        var sentences = failures
            .GroupBy(failure => failure.Reason)
            .Select(group => DescribeLoadFailures(group.Key, [.. group]));
        ShowError(string.Join("\n", sentences));
    }

    private string DescribeLoadFailures(LoadFailureReason reason, IReadOnlyList<LoadFailure> failures)
    {
        var (one, many) = reason switch
        {
            LoadFailureReason.Protected => ("LoadFailedProtectedOne", "LoadFailedProtectedMany"),
            LoadFailureReason.Damaged => ("LoadFailedDamagedOne", "LoadFailedDamagedMany"),
            LoadFailureReason.Unsupported => (Opens.UnsupportedOneKey, Opens.UnsupportedManyKey),
            _ => throw new ArgumentOutOfRangeException(nameof(reason)),
        };
        var names = string.Join(", ", failures.Select(failure => $"„{failure.FileName}“"));
        return Strings.Format(failures.Count == 1 ? one : many, names);
    }

    private void ShowMessage(InfoBarSeverity severity, string message)
    {
        MessageBar.ActionButton = null;
        MessageBar.Severity = severity;
        MessageBar.Message = message;
        MessageBar.IsOpen = true;
    }

    private void Session_Changed(object? sender, EventArgs e) => UpdateUndoRedoButtons();

    private void UpdateUndoRedoButtons()
    {
        // Not while the discard dialog is open: Ctrl+Z must not change what it asks about.
        var canAct = !IsBusy && !_isConfirmingDiscard;
        UndoButton.IsEnabled = canAct && _session is { CanUndo: true };
        RedoButton.IsEnabled = canAct && _session is { CanRedo: true };
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => _session?.Undo();

    private void Redo_Click(object sender, RoutedEventArgs e) => _session?.Redo();

    private async void Back_Click(object sender, RoutedEventArgs e)
    {
        if (await ConfirmDiscardAsync())
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (IsBusy)
        {
            // Wait for opening or saving to finish; closing now could leave a half-written file.
            args.Cancel = true;
            return;
        }

        if (_discardConfirmedForClose || _session is not { HasUnsavedChanges: true })
        {
            return;
        }

        // The event cannot wait for the dialog, so stop this close and close again once confirmed.
        args.Cancel = true;
        if (await ConfirmDiscardAsync())
        {
            _discardConfirmedForClose = true;
            App.MainWindow.Close();
        }
    }

    /// <summary>
    /// Whether the current work may be dropped (by leaving the Tool or opening another Document):
    /// nothing is unsaved, or the user agrees to discard it.
    /// </summary>
    public async Task<bool> ConfirmDiscardAsync()
    {
        if (_session is not { HasUnsavedChanges: true })
        {
            return true;
        }

        if (_isConfirmingDiscard)
        {
            // Already asking, e.g. the window's close button was clicked twice.
            return false;
        }

        _isConfirmingDiscard = true;
        UpdateUndoRedoButtons();
        try
        {
            return await AskAsync(Strings.Get("DiscardTitle"), Strings.Get("DiscardMessage"), Strings.Get("DiscardConfirm"));
        }
        finally
        {
            _isConfirmingDiscard = false;
            UpdateUndoRedoButtons();
        }
    }

    /// <summary>Asks a yes/no question; "Abbrechen" is the safe default.</summary>
    private async Task<bool> AskAsync(string title, string message, string confirm)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = confirm,
            CloseButtonText = Strings.Get("Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// For Tools that work on one Document (or one image): opens the first of <paramref name="paths"/>
    /// with <paramref name="open"/> after asking to discard unsaved changes, while the screen is busy,
    /// and explains what went wrong. Says so if more than one file was given.
    /// </summary>
    /// <returns>Whether the file was opened.</returns>
    public async Task<bool> OpenOneAsync(IReadOnlyList<string> paths, Func<string, Task<LoadFailure?>> open)
    {
        // Opening another Document replaces the current one and its changes.
        if (paths.Count == 0 || !await ConfirmDiscardAsync())
        {
            return false;
        }

        LoadFailure? failure;
        using (ShowBusy(Strings.Get(Opens.BusyOpeningOneKey)))
        {
            failure = await open(paths[0]);
        }

        if (failure is not null)
        {
            ShowLoadFailures([failure]);
            return false;
        }

        if (paths.Count > 1)
        {
            ShowInfo(Strings.Format(Opens.OneOnlyKey, Path.GetFileName(paths[0])));
        }
        return true;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> paths;
        try
        {
            paths = FileDialogs.OpenFiles(App.MainWindow.Handle, Strings.Get(Opens.DialogTitleKey), Opens.Filter);
        }
        catch (COMException)
        {
            ShowError(Strings.Get("DialogFailed"));
            return;
        }

        if (paths.Count > 0)
        {
            FilesOpened?.Invoke(this, paths);
        }
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (!IsBusy)
        {
            DroppedFiles.AcceptIfOffered(e, Strings.Get("DropToOpen"));
        }
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        var paths = await DroppedFiles.GetPathsAsync(e);
        if (paths.Count > 0)
        {
            FilesOpened?.Invoke(this, paths);
        }
    }
}
