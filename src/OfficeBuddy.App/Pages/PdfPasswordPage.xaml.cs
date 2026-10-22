using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.Core.Files;
using OfficeBuddy.Core.Tools;

namespace OfficeBuddy.App.Pages;

public sealed partial class PdfPasswordPage : Page
{
    private readonly PdfPasswordSession _session = new();
    // While the boxes and the choice are filled from the session, their changes are not the user's.
    private bool _isRefreshing;

    public PdfPasswordPage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("PdfPasswordTitle");
        Shell.Session = _session;
        _session.Changed += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
    }

    /// <summary>Whether the Document will get a new password, rather than lose the one it had.</summary>
    private bool SetsNewPassword => !_session.WasProtected || ProtectedChoice.SelectedIndex == 1;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ToolLaunch launch)
        {
            await OpenAsync(launch.FilePaths);
        }
    }

    private void Shell_BackRequested(object? sender, EventArgs e) => Frame.GoBack();

    private async void Shell_FilesOpened(object? sender, IReadOnlyList<string> paths) => await OpenAsync(paths);

    /// <summary>
    /// Opens the first file, asking for its password as long as it needs one and the user doesn't
    /// cancel. Like the other Tools, it asks first whether unsaved changes may be dropped.
    /// </summary>
    private async Task OpenAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0 || !await Shell.ConfirmDiscardAsync())
        {
            return;
        }

        var path = paths[0];
        string? password = null;
        while (true)
        {
            LoadFailure? failure;
            using (Shell.ShowBusy(Strings.Get("BusyOpening")))
            {
                failure = await _session.OpenAsync(path, password);
            }

            if (failure is not { Reason: LoadFailureReason.Protected })
            {
                if (failure is not null)
                {
                    Shell.ShowLoadFailures([failure]);
                }
                else
                {
                    ResetChoice();
                    if (paths.Count > 1)
                    {
                        Shell.ShowInfo(Strings.Format("OneDocumentOnly", Path.GetFileName(path)));
                    }
                }
                return;
            }

            password = await AskPasswordAsync(Path.GetFileName(path), wasWrong: password is not null);
            if (password is null)
            {
                return;
            }
        }
    }

    /// <summary>Asks for the password to open the file; null if the user cancels.</summary>
    private async Task<string?> AskPasswordAsync(string fileName, bool wasWrong)
    {
        var box = new PasswordBox { Header = Strings.Get("PasswordDialogBox"), Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = Strings.Format("PasswordDialogMessage", fileName), TextWrapping = TextWrapping.Wrap });
        if (wasWrong)
        {
            content.Children.Add(new TextBlock
            {
                Text = Strings.Get("PasswordDialogWrong"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            });
        }
        content.Children.Add(box);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Strings.Get("PasswordDialogTitle"),
            Content = content,
            PrimaryButtonText = Strings.Get("PasswordDialogOpen"),
            CloseButtonText = Strings.Get("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.Opened += (_, _) => box.Focus(FocusState.Programmatic);
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Password : null;
    }

    /// <summary>A Protected Document starts out to lose its password; any other to get one.</summary>
    private void ResetChoice()
    {
        _isRefreshing = true;
        ProtectedChoice.SelectedIndex = 0;
        NewPasswordBox.Password = "";
        RepeatPasswordBox.Password = "";
        _isRefreshing = false;
        Refresh();
    }

    private void ProtectedChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isRefreshing)
        {
            PassOnPassword();
        }
    }

    private void Password_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isRefreshing)
        {
            PassOnPassword();
        }
    }

    /// <summary>The session gets a new password only once both boxes say the same.</summary>
    private void PassOnPassword()
    {
        var matches = NewPasswordBox.Password.Length > 0 && NewPasswordBox.Password == RepeatPasswordBox.Password;
        _session.SetPassword(SetsNewPassword && matches ? NewPasswordBox.Password : null);
        Refresh();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        await Shell.SaveToFileAsync(_session.SuggestedFolder, _session.SuggestedFileName!, _session.SaveAsync);
        Refresh();
    }

    private void Refresh()
    {
        var document = _session.Document;
        EmptyState.Visibility = document is null ? Visibility.Visible : Visibility.Collapsed;
        DocumentArea.Visibility = document is null ? Visibility.Collapsed : Visibility.Visible;
        if (document is null)
        {
            SaveButton.IsEnabled = false;
            return;
        }

        NameText.Text = document.FileName;
        PagesText.Text = Strings.PageCount(document.PageCount);
        StatusText.Text = Strings.Get(_session.WasProtected ? "PdfPasswordWasProtected" : "PdfPasswordNotProtected");
        LockIcon.Glyph = _session.WasProtected ? "" : "";
        ProtectedChoice.Visibility = _session.WasProtected ? Visibility.Visible : Visibility.Collapsed;

        // After an undo, the boxes show the password the session has again, or none.
        var boxesAgree = NewPasswordBox.Password.Length > 0 && NewPasswordBox.Password == RepeatPasswordBox.Password;
        if (_session.NewPassword is { } password && password != NewPasswordBox.Password)
        {
            ShowPassword(password, newPasswordChosen: true);
        }
        else if (_session.NewPassword is null && SetsNewPassword && boxesAgree)
        {
            ShowPassword("", newPasswordChosen: ProtectedChoice.SelectedIndex == 1);
        }

        NewPasswordPanel.Visibility = SetsNewPassword ? Visibility.Visible : Visibility.Collapsed;
        var mismatch = RepeatPasswordBox.Password.Length > 0 && NewPasswordBox.Password != RepeatPasswordBox.Password;
        MismatchText.Text = Strings.Get("PdfPasswordMismatch");
        MismatchText.Visibility = SetsNewPassword && mismatch ? Visibility.Visible : Visibility.Collapsed;

        SaveText.Text = Strings.Get(SetsNewPassword ? "PdfPasswordSaveWith" : "PdfPasswordSaveWithout");
        AutomationProperties.SetName(SaveButton, SaveText.Text);
        // Choosing a new password but not having it twice the same would otherwise save without one.
        SaveButton.IsEnabled = _session.CanSave && (!SetsNewPassword || _session.NewPassword is not null);
    }

    private void ShowPassword(string password, bool newPasswordChosen)
    {
        _isRefreshing = true;
        ProtectedChoice.SelectedIndex = newPasswordChosen ? 1 : 0;
        NewPasswordBox.Password = password;
        RepeatPasswordBox.Password = password;
        _isRefreshing = false;
    }
}
