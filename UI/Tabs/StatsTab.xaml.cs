using MCDSaveEdit.Data;
using MCDSaveEdit.Logic;
using MCDSaveEdit.Services;
using MCDSaveEdit.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
#nullable enable

namespace MCDSaveEdit.UI
{
    /// <summary>
    /// Interaction logic for StatsTab.xaml
    /// </summary>
    public partial class StatsTab : UserControl
    {
        private ProfileViewModel? _model;
        public ProfileViewModel? model {
            get { return _model; }
            set { _model = value; }
        }

        public StatsTab()
        {
            InitializeComponent();
            mobSortComboBox.ItemsSource = new[] { "Mob name", "Kills: high to low", "Kills: low to high" };
            mobSortComboBox.SelectedIndex = 0;
            translateStaticStrings();

            //Clear out design/testing values
            updateUI();
        }

        public void updateUI()
        {
            fillStatsStack();
            fillMobKillsStack();
        }

        private void translateStaticStrings()
        {
            statsLabel.Text = R.PROGRESS_STAT_COUNTERS;
            mobKillsLabel.Text = R.MOB_KILLS;
        }

        private void fillStatsStack()
        {
            statsStack.Children.Clear();
            if (_model?.profile.value?.ProgressStatCounters == null) { return; }

            foreach (var pair in _model!.profile.value!.ProgressStatCounters.OrderBy(pair => pair.Key))
            {
                var field = createStatField(pair.Key, pair.Value);
                statsStack.Children.Add(field);
            }
        }

        private void fillMobKillsStack()
        {
            mobKillsStack.Children.Clear();
            if (_model?.profile.value?.MobKills == null) { return; }

            IEnumerable<KeyValuePair<string, long>> pairs = _model!.profile.value!.MobKills;
            string search = mobSearchTextBox?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(search))
                pairs = pairs.Where(pair => friendlyStatName(pair.Key).IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 || pair.Key.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            switch (mobSortComboBox?.SelectedIndex ?? 0)
            {
                case 1: pairs = pairs.OrderByDescending(pair => pair.Value).ThenBy(pair => friendlyStatName(pair.Key)); break;
                case 2: pairs = pairs.OrderBy(pair => pair.Value).ThenBy(pair => friendlyStatName(pair.Key)); break;
                default: pairs = pairs.OrderBy(pair => friendlyStatName(pair.Key)); break;
            }
            foreach (var pair in pairs)
            {
                var field = createStatField(pair.Key, pair.Value, true);
                mobKillsStack.Children.Add(field);
            }
        }

        private Panel createStatField(string fieldName, long fieldValue, bool useFriendlyName = false)
        {
            var label = new TextBlock() {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0),
                Margin = new Thickness(5),
                FontSize = 14,
                Text = useFriendlyName ? friendlyStatName(fieldName) : fieldName,
                ToolTip = useFriendlyName ? fieldName : null,
            };

            var textbox = new TextBox() {
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 5, 0, 5),
                Width = 70,
                FontSize = 16,
                Text = fieldValue.ToString(),
                Tag = fieldName,
            };
            textbox.TextChanged += statTextbox_TextChanged;

            var stepper = new Stepper() {
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 0, 5),
                Width = 20,
                Tag = textbox,
            };
            stepper.UpButtonClick += statStepper_UpButtonClick;
            stepper.DownButtonClick += statStepper_DownButtonClick;

            var dockPanel = new DockPanel() { Height = 40, Margin = new Thickness(5, 0, 5, 0) };

            dockPanel.Children.Add(label);
            DockPanel.SetDock(label, Dock.Left);
            dockPanel.Children.Add(stepper);
            DockPanel.SetDock(stepper, Dock.Right);
            dockPanel.Children.Add(textbox);
            DockPanel.SetDock(textbox, Dock.Right);

            return dockPanel;
        }

        private static string friendlyStatName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            var spaced = System.Text.RegularExpressions.Regex.Replace(value.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ");
            spaced = System.Text.RegularExpressions.Regex.Replace(spaced, "(?<=[A-Za-z])(?=[0-9])", " ");
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(spaced.ToLowerInvariant());
        }

        private void mobSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => fillMobKillsStack();
        private void mobSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitialized) fillMobKillsStack();
        }

        #region User Input Methods

        private void statStepper_UpButtonClick(object sender, RoutedEventArgs e)
        {
            if (_model?.profile.value == null) { return; }
            var stepper = sender as Stepper;
            if (stepper == null) { return; }
            var textBox = stepper.Tag as TextBox;
            if (textBox == null) { return; }
            if (long.TryParse(textBox.Text, out long currentValue))
            {
                textBox.Text = Math.Min(currentValue + 1, long.MaxValue).ToString();
            }
        }

        private void statStepper_DownButtonClick(object sender, RoutedEventArgs e)
        {
            if (_model?.profile.value == null) { return; }
            var stepper = sender as Stepper;
            if (stepper == null) { return; }
            var textBox = stepper.Tag as TextBox;
            if (textBox == null) { return; }
            if (long.TryParse(textBox.Text, out long currentValue))
            {
                textBox.Text = Math.Max(currentValue - 1, 0).ToString();
            }
        }

        private void statTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_model?.profile.value == null) { return; }
            var statTextBox = sender as TextBox;
            if (statTextBox == null) { return; }
            var fieldName = statTextBox.Tag as string;
            if (fieldName == null) { return; }

            if (long.TryParse(statTextBox.Text, out long newValue))
            {
                EventLogger.logEvent("statTextbox_TextChanged");
                statTextBox.ClearValue(Border.BorderBrushProperty);
                if (_model!.profile.value.ProgressStatCounters.ContainsKey(fieldName))
                {
                    _model!.profile.value.ProgressStatCounters[fieldName] = newValue;
                }
                else
                {
                    _model!.profile.value.MobKills[fieldName] = newValue;
                }
                _model.notifyChanged();
            }
            else
            {
                statTextBox.SetResourceReference(Border.BorderBrushProperty, "AppDangerBrush");
            }
        }

        #endregion
    }
}
