using MCDSaveEdit.Data;
using MCDSaveEdit.Services;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
#nullable enable

namespace MCDSaveEdit.UI
{
    /// <summary>
    /// Interaction logic for GameFilesWindow.xaml
    /// </summary>
    public partial class GameFilesWindow : Window
    {
        public enum GameFilesWindowResult
        {
            exit,
            useSelectedPath,
            noContent,
        }

        public string? selectedPath { get { return pathTextBox.Text; } private set { pathTextBox.Text = value; } }
        public GameFilesWindowResult result { get; private set; }
        public Action? onClose;

        public GameFilesWindow(string? defaultPath, bool allowNoContent)
        {
            InitializeComponent();
            setConstantStrings(defaultPath, allowNoContent);
            refreshSearchLocations(defaultPath);
        }

        private void setConstantStrings(string? defaultPath, bool allowNoContent)
        {
            Title = R.GAME_FILES_WINDOW_TITLE;
            messageTextBlock.Text = "MCDSaveEdit can still open saves, but item images and some game-data features may be unavailable.";
            gameFilesGroupBox.Text = "Locate game files";
            pathLabel.Text = "Choose the Minecraft Dungeons folder or its Content\\Paks directory.";
            pathTextBox.Text = defaultPath ?? string.Empty;
            exitButton.Content = R.EXIT;
            okButton.Content = R.OK;
            if(allowNoContent)
            {
                noButton.Content = R.GAME_FILES_WINDOW_NO_CONTENT_BUTTON;
            }
            else
            {
                noButton.Content = R.CANCEL;
            }
        }

        private void exitButton_Click(object sender, RoutedEventArgs e)
        {
            EventLogger.logEvent("exitButton_Click");
            result = GameFilesWindowResult.exit;
            this.Close();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            bool isValidPath = isValidSelectedPath();
            EventLogger.logEvent("okButton_Click", new Dictionary<string, object> { { "isValidPath", isValidPath } });
            if (isValidPath)
            {
                result = GameFilesWindowResult.useSelectedPath;
                this.Close();
                return;
            }

            statusTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("AppDangerBrush");
            statusTextBlock.Text = "That folder does not contain readable Minecraft Dungeons PAK files.";
        }

        private bool isValidSelectedPath()
        {
            var testPath = selectedPath?.Trim();
            if (string.IsNullOrWhiteSpace(testPath))
            {
                return false;
            }

            string? resolved = GameInstallationDetector.TryResolvePakDirectory(testPath);
            if (resolved == null) return false;
            selectedPath = resolved;
            statusTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("AppSuccessBrush");
            statusTextBlock.Text = $"Ready: {resolved}";
            return true;
        }

        private void noButton_Click(object sender, RoutedEventArgs e)
        {
            EventLogger.logEvent("noButton_Click");
            result = GameFilesWindowResult.noContent;
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            onClose?.Invoke();
        }

        private void pathBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            EventLogger.logEvent("pathBrowseButton_Click");
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.EnsurePathExists = true;
            dialog.ShowHiddenItems = true;
            var appDataFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            dialog.InitialDirectory = appDataFolderPath;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                selectedPath = dialog.FileName;
                isValidSelectedPath();
            }            
        }

        private void autoDetectButton_Click(object sender, RoutedEventArgs e)
        {
            GameInstallationInfo? installation = GameInstallationDetector.DetectFirstUsable(selectedPath);
            if (installation == null)
            {
                statusTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("AppWarningBrush");
                statusTextBlock.Text = "No readable Minecraft Dungeons pak directory was found in known locations.";
                refreshSearchLocations(selectedPath);
                return;
            }

            selectedPath = installation.PakDirectory;
            statusTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("AppSuccessBrush");
            statusTextBlock.Text = $"Detected {installation.Type}: {installation.PakDirectory}";
            refreshSearchLocations(selectedPath);
        }

        private void refreshSearchLocations(string? configuredPath)
        {
            searchLocationsTextBox.Text = string.Join(Environment.NewLine,
                GameInstallationDetector.DetectAll(configuredPath)
                    .Select(candidate => $"{candidate.Type}: {candidate.PakDirectory} — {candidate.Status}"));
        }
    }
}
