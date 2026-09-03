using MCDSaveEdit.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MCDSaveEdit.UI
{
    public partial class BackupBrowserWindow : Window
    {
        private readonly string _targetPath;
        private readonly string _backupDirectory;

        public BackupBrowserWindow(string targetPath)
        {
            _targetPath = targetPath;
            _backupDirectory = Path.Combine(Path.GetDirectoryName(targetPath)!, BackupService.BackupDirectoryName);
            InitializeComponent();
            refresh();
        }

        public string? SelectedBackupPath { get; private set; }

        private void refresh()
        {
            var backups = BackupService.GetBackups(_targetPath);
            backupsListView.ItemsSource = backups;
            summaryTextBlock.Text = backups.Count == 0
                ? $"No backups were found for {Path.GetFileName(_targetPath)}."
                : $"Select a backup for {Path.GetFileName(_targetPath)}. The current save will be backed up before restoration.";
            openFolderButton.IsEnabled = Directory.Exists(_backupDirectory);
            restoreButton.IsEnabled = false;
        }

        private void backupsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            restoreButton.IsEnabled = backupsListView.SelectedItem is BackupEntry;
        }

        private void backupsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (backupsListView.SelectedItem is BackupEntry) selectBackup();
        }

        private void restoreButton_Click(object sender, RoutedEventArgs e)
        {
            selectBackup();
        }

        private void selectBackup()
        {
            if (!(backupsListView.SelectedItem is BackupEntry selected)) return;
            SelectedBackupPath = selected.Path;
            DialogResult = true;
        }

        private void refreshButton_Click(object sender, RoutedEventArgs e)
        {
            refresh();
        }

        private void openFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_backupDirectory))
                Process.Start("explorer.exe", $"\"{_backupDirectory}\"");
        }
    }
}
