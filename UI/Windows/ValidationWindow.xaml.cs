using MCDSaveEdit.Logic.Validation;
using System.Windows;

namespace MCDSaveEdit.UI
{
    public partial class ValidationWindow : Window
    {
        private readonly SaveValidationReport _report;

        public ValidationWindow(SaveValidationReport report, bool preSave = true)
        {
            _report = report;
            InitializeComponent();
            issuesListView.ItemsSource = report.Issues;
            summaryTextBlock.Text = report.HasErrors
                ? "Errors likely to produce an unusable save were found. Review every issue before continuing."
                : report.HasWarnings ? "Warnings were found. Review them before saving." : "Validation information:";
            saveAnywayButton.Content = report.HasErrors ? "Save Anyway (Unsafe)" : "Save Anyway";
            if (!preSave)
            {
                saveAnywayButton.Visibility = Visibility.Collapsed;
                closeButton.Content = "Close";
            }
        }

        private void copyReportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_report.ToCopyableText());
            }
            catch (System.Exception exception)
            {
                MessageBox.Show("The validation report could not be copied to the clipboard.\n\n" + exception.Message,
                    "Copy Validation Report", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void saveAnywayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_report.HasErrors)
            {
                var confirmation = MessageBox.Show(
                    "Validation found serious errors that may corrupt the save. Do you explicitly want to save anyway?",
                    "Confirm Unsafe Save",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);
                if (confirmation != MessageBoxResult.Yes) return;
            }

            DialogResult = true;
        }
    }
}
