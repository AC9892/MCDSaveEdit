using System.Windows;
#nullable enable

namespace MCDSaveEdit.UI
{
    public partial class ModernDialog : Window
    {
        private MessageBoxResult _primaryResult = MessageBoxResult.OK;
        private MessageBoxResult _secondaryResult = MessageBoxResult.None;
        private MessageBoxResult _tertiaryResult = MessageBoxResult.None;
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        private ModernDialog(string title, string message, string? details, MessageBoxButton buttons, MessageBoxImage image)
        {
            InitializeComponent();
            Title = title;
            titleTextBlock.Text = title;
            messageTextBlock.Text = message;
            iconTextBlock.Text = image == MessageBoxImage.Error ? "×" : image == MessageBoxImage.Question ? "?" : image == MessageBoxImage.Information ? "i" : "!";
            iconTextBlock.Foreground = (System.Windows.Media.Brush)FindResource(image == MessageBoxImage.Error ? "AppDangerBrush" : image == MessageBoxImage.Warning ? "AppWarningBrush" : "AppAccentBrush");
            if (!string.IsNullOrWhiteSpace(details)) { detailsTextBox.Text = details; detailsExpander.Visibility = Visibility.Visible; }
            ConfigureButtons(buttons);
        }

        public static MessageBoxResult Show(Window? owner, string title, string message, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None, string? details = null, string? primaryText = null, string? secondaryText = null)
        {
            var dialog = new ModernDialog(title, message, details, buttons, image);
            if (owner != null) dialog.Owner = owner;
            if (!string.IsNullOrWhiteSpace(primaryText)) dialog.primaryButton.Content = primaryText;
            if (!string.IsNullOrWhiteSpace(secondaryText)) dialog.secondaryButton.Content = secondaryText;
            dialog.ShowDialog();
            return dialog.Result;
        }

        private void ConfigureButtons(MessageBoxButton buttons)
        {
            if (buttons == MessageBoxButton.YesNoCancel) { tertiaryButton.Content = "Cancel"; tertiaryButton.Visibility = Visibility.Visible; _tertiaryResult = MessageBoxResult.Cancel; secondaryButton.Content = "Discard"; secondaryButton.Visibility = Visibility.Visible; _secondaryResult = MessageBoxResult.No; primaryButton.Content = "Save"; _primaryResult = MessageBoxResult.Yes; }
            else if (buttons == MessageBoxButton.YesNo) { secondaryButton.Content = "No"; secondaryButton.Visibility = Visibility.Visible; _secondaryResult = MessageBoxResult.No; primaryButton.Content = "Yes"; _primaryResult = MessageBoxResult.Yes; }
            else if (buttons == MessageBoxButton.OKCancel) { secondaryButton.Content = "Cancel"; secondaryButton.Visibility = Visibility.Visible; _secondaryResult = MessageBoxResult.Cancel; primaryButton.Content = "Continue"; _primaryResult = MessageBoxResult.OK; }
            else { primaryButton.Content = "OK"; _primaryResult = MessageBoxResult.OK; }
        }
        private void primaryButton_Click(object sender, RoutedEventArgs e) { Result = _primaryResult; Close(); }
        private void secondaryButton_Click(object sender, RoutedEventArgs e) { Result = _secondaryResult; Close(); }
        private void tertiaryButton_Click(object sender, RoutedEventArgs e) { Result = _tertiaryResult; Close(); }
    }
}
