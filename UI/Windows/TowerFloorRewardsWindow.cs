using MCDSaveEdit.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
#nullable enable

namespace MCDSaveEdit.UI
{
    public sealed class TowerFloorRewardsWindow : Window
    {
        private sealed class FloorEditor
        {
            public TowerFloorRewardConfiguration Source { get; }
            public List<ComboBox> RewardSelectors { get; } = new List<ComboBox>();

            public FloorEditor(TowerFloorRewardConfiguration source) { Source = source; }
        }

        private sealed class RewardTokenOption
        {
            public string Value { get; set; } = string.Empty;
            public string Display { get; set; } = string.Empty;

            public override string ToString() => Display;
        }

        private readonly List<FloorEditor> _editors = new List<FloorEditor>();
        private readonly List<RewardTokenOption> _rewardTokens;
        public IReadOnlyList<TowerFloorRewardConfiguration> EditedFloors { get; private set; }
            = Array.Empty<TowerFloorRewardConfiguration>();

        public TowerFloorRewardsWindow(IReadOnlyList<TowerFloorRewardConfiguration> floors)
        {
            _rewardTokens = new[] { "any", "melee", "ranged", "armor", "artefact" }
                .Concat(floors.SelectMany(floor => floor.Rewards))
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(token => token == "any" ? 0 : 1)
                .ThenBy(token => token, StringComparer.OrdinalIgnoreCase)
                .Select(token => new RewardTokenOption
                {
                    Value = token,
                    Display = string.Equals(token, "artefact", StringComparison.OrdinalIgnoreCase)
                        ? "Artifact"
                        : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(token)
                })
                .ToList();
            Title = "Tower Reward Configuration";
            Width = 1200;
            Height = 760;
            MinWidth = 920;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = (Brush)FindResource("AppBackgroundBrush");
            Foreground = (Brush)FindResource("AppForegroundBrush");

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var intro = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            intro.Children.Add(new TextBlock { Text = "Tower reward configuration", FontSize = 24, FontWeight = FontWeights.SemiBold });
            intro.Children.Add(new TextBlock
            {
                Text = $"Edit the five reward-category slots for floors 1–{floors.Count}. Floor 0 is the entrance and is deliberately preserved.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = (Brush)FindResource("AppSecondaryForegroundBrush")
            });
            intro.Children.Add(new TextBlock
            {
                Text = "Choose a reward category from each dropdown. These are categories, not exact generated item IDs.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0),
                Foreground = (Brush)FindResource("AppWarningBrush")
            });
            root.Children.Add(intro);

            var table = new StackPanel();
            table.Children.Add(createHeaderRow());
            foreach (TowerFloorRewardConfiguration floor in floors.OrderBy(floor => floor.FloorIndex))
            {
                var editor = new FloorEditor(floor);
                _editors.Add(editor);
                table.Children.Add(createFloorRow(editor));
            }
            var scroll = new ScrollViewer
            {
                Content = table,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.Children.Add(new TextBlock
            {
                Text = "Saving requires Minecraft Dungeons to be closed and creates a timestamped backup.",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("AppSecondaryForegroundBrush")
            });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var cancel = new Button { Content = "Cancel", Margin = new Thickness(0, 0, 8, 0) };
            cancel.Click += (_, __) => { DialogResult = false; Close(); };
            var save = new Button { Content = $"Apply {floors.Count}-Floor Configuration", Style = (Style)FindResource("PrimaryButton") };
            save.Click += save_Click;
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
            Grid.SetColumn(buttons, 1);
            footer.Children.Add(buttons);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            Content = root;
        }

        private static Grid createColumns()
        {
            var grid = new Grid { MinWidth = 1120 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(205) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
            for (int index = 0; index < 5; index++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
            return grid;
        }

        private Grid createHeaderRow()
        {
            Grid row = createColumns();
            row.Margin = new Thickness(0, 0, 0, 5);
            string[] labels = { "FLOOR", "TILE", "TYPE", "REWARD 1", "REWARD 2", "REWARD 3", "REWARD 4", "REWARD 5" };
            for (int index = 0; index < labels.Length; index++)
            {
                var label = new TextBlock
                {
                    Text = labels[index],
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(5, 0, 5, 0),
                    Foreground = (Brush)FindResource("AppSecondaryForegroundBrush")
                };
                Grid.SetColumn(label, index);
                row.Children.Add(label);
            }
            return row;
        }

        private Grid createFloorRow(FloorEditor editor)
        {
            Grid row = createColumns();
            row.Margin = new Thickness(0, 0, 0, 5);
            row.Children.Add(new TextBlock
            {
                Text = editor.Source.FloorIndex.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.SemiBold
            });
            addReadOnlyText(row, 1, editor.Source.Tile);
            addReadOnlyText(row, 2, string.IsNullOrWhiteSpace(editor.Source.FloorType) ? "—" : editor.Source.FloorType);
            for (int rewardIndex = 0; rewardIndex < 5; rewardIndex++)
            {
                string token = rewardIndex < editor.Source.Rewards.Count ? editor.Source.Rewards[rewardIndex] : "any";
                var selector = new ComboBox
                {
                    ItemsSource = _rewardTokens,
                    DisplayMemberPath = nameof(RewardTokenOption.Display),
                    SelectedValuePath = nameof(RewardTokenOption.Value),
                    SelectedValue = _rewardTokens.FirstOrDefault(value => string.Equals(value.Value, token, StringComparison.OrdinalIgnoreCase))?.Value ?? "any",
                    Margin = new Thickness(3, 0, 3, 0),
                    MinHeight = 30,
                    ToolTip = "Choose the reward category for this slot"
                };
                editor.RewardSelectors.Add(selector);
                Grid.SetColumn(selector, rewardIndex + 3);
                row.Children.Add(selector);
            }
            return row;
        }

        private void addReadOnlyText(Grid row, int column, string text)
        {
            var label = new TextBlock
            {
                Text = text,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 0),
                ToolTip = text,
                Foreground = (Brush)FindResource("AppSecondaryForegroundBrush")
            };
            Grid.SetColumn(label, column);
            row.Children.Add(label);
        }

        private void save_Click(object sender, RoutedEventArgs e)
        {
            if (_editors.Any(editor => editor.RewardSelectors.Any(selector => string.IsNullOrWhiteSpace(selector.SelectedValue as string))))
            {
                ModernDialog.Show(this, "Missing reward category",
                    "Every floor must have five non-empty reward category tokens.",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            EditedFloors = _editors.Select(editor => new TowerFloorRewardConfiguration
            {
                FloorIndex = editor.Source.FloorIndex,
                Tile = editor.Source.Tile,
                FloorType = editor.Source.FloorType,
                Rewards = editor.RewardSelectors.Select(selector => ((string)selector.SelectedValue).Trim()).ToList()
            }).ToList();
            DialogResult = true;
            Close();
        }
    }
}
