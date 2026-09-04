using MCDSaveEdit.Properties;
using MCDSaveEdit.ViewModels;
using System.Windows;

namespace MCDSaveEdit.UI
{
    public partial class SplashWindow : Window
    {
        public SplashWindow(LoadingViewModel model)
        {
            InitializeComponent();
            DataContext = model;
            if (Settings.Default.ShowStartupDiagnostics)
            {
                detailsPanel.Visibility = Visibility.Visible;
                detailsButton.Content = "Hide Details";
            }
        }

        private void detailsButton_Click(object sender, RoutedEventArgs e)
        {
            bool show = detailsPanel.Visibility != Visibility.Visible;
            detailsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            detailsButton.Content = show ? "Hide Details" : "Show Details";
        }

        private void copyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(textbox.Text ?? string.Empty);
        }
    }
}
