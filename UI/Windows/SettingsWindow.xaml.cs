using MCDSaveEdit.Logic.Validation;
using MCDSaveEdit.Properties;
using MCDSaveEdit.Services;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Input;

namespace MCDSaveEdit.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly string? _backupDirectory;
        private readonly string _originalTheme = Settings.Default.Theme;
        private bool _saved;
        private bool _embedded;
        public Action? SettingsSaved { get; set; }
        public Action? SettingsReset { get; set; }

        public SettingsWindow(string? currentSavePath = null)
        {
            if (!string.IsNullOrWhiteSpace(currentSavePath))
                _backupDirectory = Path.Combine(Path.GetDirectoryName(currentSavePath)!, MCDSaveEdit.Services.BackupService.BackupDirectoryName);
            InitializeComponent();
            validationStrictnessComboBox.ItemsSource = Enum.GetValues(typeof(SaveValidationStrictness));
            themeComboBox.ItemsSource = new[] { "System", "Light", "Dark" };
            confirmOverwriteCheckBox.IsChecked = Settings.Default.ConfirmBeforeOverwrite;
            automaticBackupsCheckBox.IsChecked = Settings.Default.AutomaticBackupsEnabled;
            backupRetentionTextBox.Text = Settings.Default.BackupRetentionCount.ToString();
            saveValidationCheckBox.IsChecked = Settings.Default.SaveValidationEnabled;
            advancedEditingCheckBox.IsChecked = Settings.Default.AdvancedEditingEnabled;
            diagnosticsLoggingCheckBox.IsChecked = Settings.Default.DiagnosticsLoggingEnabled;
            startupDiagnosticsCheckBox.IsChecked = Settings.Default.ShowStartupDiagnostics;
            liveMonitorHotkeyTextBox.Text = Settings.Default.LiveMonitorHotkey;
            themeComboBox.SelectedItem = new[] { "System", "Light", "Dark" }.Contains(Settings.Default.Theme)
                ? Settings.Default.Theme : "System";
            gamePakPathTextBox.Text = Settings.Default.GamePakDirectory;
            refreshGameFileStatus();

            if (Enum.TryParse(Settings.Default.SaveValidationStrictness, out SaveValidationStrictness strictness))
                validationStrictnessComboBox.SelectedItem = strictness;
            else
                validationStrictnessComboBox.SelectedItem = SaveValidationStrictness.Standard;
            openBackupFolderButton.IsEnabled = _backupDirectory != null && Directory.Exists(_backupDirectory);
        }

        public UIElement DetachContentForEmbedding()
        {
            _embedded = true;
            ShowInTaskbar = false;
            var embeddedContent = (UIElement)Content;
            Content = null;
            return embeddedContent;
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(backupRetentionTextBox.Text, out var retention) || retention < 1 || retention > 1000)
            {
                validationMessage.Text = "Backup retention must be from 1 to 1000.";
                backupRetentionTextBox.Focus();
                return;
            }

            if (!LiveMonitorHotkey.TryParse(liveMonitorHotkeyTextBox.Text, out _, out _, out string normalizedHotkey))
            {
                validationMessage.Text = "Choose a hotkey with a modifier (for example Ctrl+Shift+M), or an F-key.";
                liveMonitorHotkeyTextBox.Focus();
                return;
            }

            Settings.Default.ConfirmBeforeOverwrite = confirmOverwriteCheckBox.IsChecked == true;
            Settings.Default.AutomaticBackupsEnabled = automaticBackupsCheckBox.IsChecked == true;
            Settings.Default.BackupRetentionCount = retention;
            Settings.Default.SaveValidationEnabled = saveValidationCheckBox.IsChecked == true;
            var strictness = validationStrictnessComboBox.SelectedItem is SaveValidationStrictness selectedStrictness
                ? selectedStrictness
                : SaveValidationStrictness.Standard;
            Settings.Default.SaveValidationStrictness = strictness.ToString();
            string? resolvedPakPath = GameInstallationDetector.TryResolvePakDirectory(gamePakPathTextBox.Text);
            if (!string.IsNullOrWhiteSpace(gamePakPathTextBox.Text) && resolvedPakPath == null)
            {
                validationMessage.Text = "The selected game directory does not contain readable Minecraft Dungeons pak files.";
                return;
            }
            Settings.Default.GamePakDirectory = resolvedPakPath ?? string.Empty;
            Settings.Default.Theme = themeComboBox.SelectedItem as string ?? "System";
            Settings.Default.AdvancedEditingEnabled = advancedEditingCheckBox.IsChecked == true;
            Settings.Default.DiagnosticsLoggingEnabled = diagnosticsLoggingCheckBox.IsChecked == true;
            Settings.Default.ShowStartupDiagnostics = startupDiagnosticsCheckBox.IsChecked == true;
            Settings.Default.LiveMonitorHotkey = normalizedHotkey;
            Settings.Default.Save();
            ThemeManager.Apply(Settings.Default.Theme);
            EventLogger.Reconfigure();
            _saved = true;
            if (_embedded)
                SettingsSaved?.Invoke();
            else
                DialogResult = true;
        }

        private void liveMonitorHotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Back || key == Key.Delete)
            {
                liveMonitorHotkeyTextBox.Clear();
                return;
            }
            string captured = LiveMonitorHotkey.FromKeyEvent(e);
            if (!string.IsNullOrEmpty(captured)) liveMonitorHotkeyTextBox.Text = captured;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Apply(_originalTheme);
            if (_embedded)
                SettingsReset?.Invoke();
            else
                DialogResult = false;
        }

        private void settingNavigation_Click(object sender, RoutedEventArgs e)
        {
            if (settingsTabControl == null) return;
            if (sender is RadioButton button && int.TryParse(button.CommandParameter?.ToString(), out int index))
                settingsTabControl.SelectedIndex = index;
        }

        private void themeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitialized && themeComboBox.SelectedItem is string theme)
                ThemeManager.Apply(theme);
        }

        private void settingsWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_saved) ThemeManager.Apply(_originalTheme);
        }

        private void gamePakPathTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (IsInitialized) refreshGameFileStatus();
        }

        private void autoDetectGameFilesButton_Click(object sender, RoutedEventArgs e)
        {
            GameInstallationInfo? installation = GameInstallationDetector.DetectFirstUsable(gamePakPathTextBox.Text);
            if (installation == null)
            {
                gameFileStatusTextBlock.Text = "No installation was found in the known bounded locations.";
                return;
            }
            gamePakPathTextBox.Text = installation.PakDirectory;
            refreshGameFileStatus();
        }

        private void browseGameFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                ShowHiddenItems = true,
                InitialDirectory = Directory.Exists(gamePakPathTextBox.Text)
                    ? gamePakPathTextBox.Text
                    : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            };
            if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return;
            gamePakPathTextBox.Text = GameInstallationDetector.TryResolvePakDirectory(dialog.FileName) ?? dialog.FileName;
            refreshGameFileStatus();
        }

        private void rescanGameFilesButton_Click(object sender, RoutedEventArgs e)
        {
            refreshGameFileStatus();
        }

        private void resetGameFilesButton_Click(object sender, RoutedEventArgs e)
        {
            gamePakPathTextBox.Text = string.Empty;
            refreshGameFileStatus();
        }

        private void openGameFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string? path = GameInstallationDetector.TryResolvePakDirectory(gamePakPathTextBox.Text);
            if (path != null) Process.Start("explorer.exe", $"\"{path}\"");
        }

        private void clearImageCacheButton_Click(object sender, RoutedEventArgs e)
        {
            int previousCount = ImageResolver.instance.cachedImageCount;
            ImageResolver.instance.clearImageCache();
            gameFileStatusTextBlock.Text = $"Cleared {previousCount} cached image(s). Images will be loaded again when needed.";
        }

        private void refreshGameFileStatus()
        {
            string? resolved = GameInstallationDetector.TryResolvePakDirectory(gamePakPathTextBox.Text);
            if (resolved == null)
            {
                gameInstallationTypeTextBlock.Text = string.IsNullOrWhiteSpace(gamePakPathTextBox.Text) ? "Not configured" : "Unknown";
                gameRootTextBlock.Text = "—";
                gameFileStatusTextBlock.Text = string.IsNullOrWhiteSpace(gamePakPathTextBox.Text)
                    ? "Choose Auto Detect or Browse. Save editing remains available without game assets."
                    : "No primary Minecraft Dungeons pak was found at this location.";
                openGameFolderButton.IsEnabled = false;
                return;
            }

            GameInstallationInfo? installation = GameInstallationDetector.DetectAll(resolved)
                .FirstOrDefault(candidate => candidate.IsUsable && string.Equals(candidate.PakDirectory, resolved, StringComparison.OrdinalIgnoreCase));
            gameInstallationTypeTextBlock.Text = installation?.Type.ToString() ?? GameInstallationType.Manual.ToString();
            gameRootTextBlock.Text = GameInstallationDetector.GetGameRoot(resolved);
            gameFileStatusTextBlock.Text = "Pak directory is readable. The path will be used on the next game-content reload.";
            openGameFolderButton.IsEnabled = true;
        }

        private void openBackupFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_backupDirectory != null && Directory.Exists(_backupDirectory))
                Process.Start("explorer.exe", $"\"{_backupDirectory}\"");
        }
    }
}
