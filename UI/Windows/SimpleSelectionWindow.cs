using MCDSaveEdit.Data;
using MCDSaveEdit.Save.Models.Enums;
using MCDSaveEdit.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
#nullable enable

namespace MCDSaveEdit.UI
{
    /// <summary>
    /// A selection window for armor properties, enchantments, and items
    /// </summary>
    public partial class SimpleSelectionWindow : Window, ISelectionWindow
    {
        private ListBox _listBox = new ListBox();
        private readonly TextBox _searchBox = new TextBox();
        private readonly StackPanel _filterPanel = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly List<ListBoxItem> _allItems = new List<ListBoxItem>();
        private readonly Dictionary<EnchantmentFilter, Button> _enchantmentFilterButtons = new Dictionary<EnchantmentFilter, Button>();
        private EnchantmentFilter _activeEnchantmentFilter = EnchantmentFilter.All;
        private bool _showingEnchantments;
        private bool _isProcessing = true;

        public Action<string?>? onSelection { get; set; }

        public string? selectedItem {
            get {
                if(_listBox.SelectedItem is ListBoxItem listItem)
                {
                    return listItem.Tag as string;
                }
                return _listBox.SelectedItem as string;
            }
        }

        public SimpleSelectionWindow()
        {
            ResizeMode = ResizeMode.CanResize;
            Height = 650;
            Width = 380;
            MinWidth = 320;
            MinHeight = 450;
            Padding = new Thickness(14);
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ScrollViewer.SetCanContentScroll(_listBox, false);
            ScrollViewer.SetVerticalScrollBarVisibility(_listBox, ScrollBarVisibility.Visible);
            _listBox.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            _listBox.SelectionChanged += listBox_SelectionChanged;
            _searchBox.Margin = new Thickness(0, 0, 0, 8);
            _searchBox.ToolTip = "Search available choices";
            AutomationProperties.SetName(_searchBox, "Search available choices");
            _searchBox.TextChanged += searchBox_TextChanged;
            _filterPanel.Margin = new Thickness(0, 0, 0, 8);
            var tools = new StackPanel();
            tools.Children.Add(_searchBox);
            tools.Children.Add(_filterPanel);
            var root = new DockPanel();
            DockPanel.SetDock(tools, Dock.Top);
            root.Children.Add(tools);
            root.Children.Add(_listBox);
            Content = root;
        }

        private void listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isProcessing || !IsLoaded) return;
            EventLogger.logEvent("listBox_SelectionChanged", new Dictionary<string, object>() { { "item", selectedItem ?? "null" } });
            _listBox.SelectionChanged -= listBox_SelectionChanged;
            onSelection?.Invoke(selectedItem);
            this.Close();
        }

        public void loadArmorProperties(string? selectedArmorProperty = null)
        {
            Title = R.SELECT_ARMOR_PROPERTY;
            _isProcessing = true;
            _showingEnchantments = false;
            _filterPanel.Children.Clear();
            clearItems();

            foreach (var armorProperty in ItemDatabase.armorProperties.OrderBy(str => str))
            {
                var itemView = new BaseSelectionWindow.ArmorPropertyView { titleContent = R.armorProperty(armorProperty) };
                if (Config.instance.showIDsInSelectionWindow)
                {
                    itemView.subtitleContent = armorProperty;
                }

                var listItem = new ListBoxItem { Content = itemView, Tag = armorProperty };
                addItem(listItem);

                if (selectedArmorProperty == armorProperty)
                {
                    _listBox.SelectedItem = listItem;
                }
            }

            _isProcessing = false;
        }

        public void loadEnchantments(string? selectedEnchantment = null)
        {
            Title = R.getString("UIHints_SelectEnchantmentTitle") ?? R.SELECT_ENCHANTMENT;
            _isProcessing = true;
            _showingEnchantments = true;
            _activeEnchantmentFilter = EnchantmentFilter.All;
            clearItems();

            foreach (var enchantment in EnchantmentDatabase.allEnchantments.Concat(new[] { Constants.DEFAULT_ENCHANTMENT_ID })
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(R.enchantmentName))
            {
                var imageSource = ImageResolver.instance.imageSourceForEnchantment(enchantment);
                if (imageSource == null)
                {
                    continue;
                }

                var itemView = new BaseSelectionWindow.EnchantmentView { imageSource = imageSource, titleContent = R.enchantmentName(enchantment) };
                itemView.powerful = Constants.powerful.Contains(enchantment);
                if(Config.instance.showIDsInSelectionWindow)
                {
                    itemView.subtitleContent = enchantment;
                }

                var listItem = new ListBoxItem { Content = itemView, Tag = enchantment };
                addItem(listItem);

                if (selectedEnchantment == enchantment)
                {
                    _listBox.SelectedItem = listItem;
                }
            }

            buildEnchantmentFilterTabs();
            applyVisibleFilters();
            _listBox.SelectedItem = _allItems.FirstOrDefault(item =>
                string.Equals(item.Tag as string, selectedEnchantment, StringComparison.OrdinalIgnoreCase));
            _isProcessing = false;
        }

        public void loadFilteredItems(ItemFilterEnum filter, string? selectedItem = null)
        {
            Title = getTitleForFilter(filter);
            _showingEnchantments = false;
            _filterPanel.Children.Clear();
            buildItemList(filter, selectedItem: selectedItem);

            _isProcessing = false;
        }

        private string getTitleForFilter(ItemFilterEnum filter)
        {
            switch(filter)
            {
                case ItemFilterEnum.Armor: return R.getString("merchant_slot_select_item") ?? R.SELECT_ARMOR;
                case ItemFilterEnum.Artifacts: return R.getString("merchant_slot_select_item") ?? R.SELECT_ARTIFACT;
                case ItemFilterEnum.MeleeWeapons: return R.getString("merchant_slot_select_item") ?? R.SELECT_MELEE_WEAPON;
                case ItemFilterEnum.RangedWeapons: return R.getString("merchant_slot_select_item") ?? R.SELECT_RANGED_WEAPON;
                default: return R.getString("merchant_slot_select_item") ?? R.SELECT_ITEM;
            }
        }

        public void loadItems(string? selectedItem = null)
        {
            Title = R.getString("merchant_slot_select_item") ?? R.SELECT_ITEM;
            _showingEnchantments = false;

            var anyButton = createFilterButton(ItemFilterEnum.All);
            var meleeButton = createFilterButton(ItemFilterEnum.MeleeWeapons);
            var rangedButton = createFilterButton(ItemFilterEnum.RangedWeapons);
            var armorButton = createFilterButton(ItemFilterEnum.Armor);
            var artifactButton = createFilterButton(ItemFilterEnum.Artifacts);

            _filterPanel.Children.Clear();
            _filterPanel.Children.Add(anyButton);
            _filterPanel.Children.Add(meleeButton);
            _filterPanel.Children.Add(rangedButton);
            _filterPanel.Children.Add(armorButton);
            _filterPanel.Children.Add(artifactButton);

            buildItemList(selectedItem: selectedItem);
        }

        private Button createFilterButton(ItemFilterEnum filter)
        {
            var button = new Button { Margin = new Thickness(0, 0, 6, 0), Width = 38, Height = 38, Padding = new Thickness(6), ToolTip = getTitleForFilter(filter) };
            AutomationProperties.SetName(button, getTitleForFilter(filter));
            button.Content = new Image { Source = imageSourceForFilter(filter), Stretch = Stretch.Uniform };
            button.Command = new RelayCommand<object>(filterItems);
            button.CommandParameter = filter;
            return button;
        }

        private ImageSource? imageSourceForFilter(ItemFilterEnum filter)
        {
            switch (filter)
            {
                case ItemFilterEnum.All:
                    return ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_all_default");
                case ItemFilterEnum.MeleeWeapons:
                    return ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_melee_default");
                case ItemFilterEnum.RangedWeapons:
                    return ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_ranged_default");
                case ItemFilterEnum.Armor:
                    return ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_armour_default");
                case ItemFilterEnum.Artifacts:
                    return ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_consume_default");
            }
            throw new ArgumentException($"No MysteryBox string for {filter}", "filter");
        }

        private void filterItems(object filter)
        {
            if(filter is ItemFilterEnum filterEnum)
            {
                buildItemList(filterEnum);
            }
        }

        private void buildItemList(ItemFilterEnum filter = ItemFilterEnum.All, string? selectedItem = null)
        {
            _isProcessing = true;
            clearItems();

            foreach (var item in itemsForFilter(filter).OrderBy(str => str))
            {
                var imageSource = ImageResolver.instance.imageSourceForItem(item);
                var itemView = new BaseSelectionWindow.ItemView { imageSource = imageSource, titleContent = R.itemName(item) };
                if(Config.instance.showIDsInSelectionWindow)
                {
                    itemView.subtitleContent = item;
                }

                var listItem = new ListBoxItem { Content = itemView, Tag = item };
                if (item.ToLowerInvariant().Contains("unique"))
                {
                    var backgroundImage = ImageResolver.instance.imageSourceForRarity(Rarity.Unique);
                    var brush = new ImageBrush(backgroundImage);
                    listItem.Background = brush;
                }
                addItem(listItem);

                if (selectedItem == item)
                {
                    _listBox.SelectedItem = listItem;
                }
            }
            _isProcessing = false;
        }

        private void clearItems()
        {
            _listBox.Items.Clear();
            _allItems.Clear();
            _searchBox.Text = string.Empty;
        }

        private void addItem(ListBoxItem item)
        {
            _allItems.Add(item);
            _listBox.Items.Add(item);
        }

        private void searchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            applyVisibleFilters();
        }

        private void buildEnchantmentFilterTabs()
        {
            _filterPanel.Children.Clear();
            _enchantmentFilterButtons.Clear();
            addEnchantmentFilterTab(EnchantmentFilter.All, "All enchantments", "/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_all_default");
            addEnchantmentFilterTab(EnchantmentFilter.Melee, "Melee enchantments", "/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_melee_default");
            addEnchantmentFilterTab(EnchantmentFilter.Ranged, "Ranged enchantments", "/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_ranged_default");
            addEnchantmentFilterTab(EnchantmentFilter.Armor, "Armor enchantments", "/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_armour_default");
            addEnchantmentFilterTab(EnchantmentFilter.ExclusiveAndUnused, "Exclusive & unused enchantments", "/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_consume_default");
            updateEnchantmentFilterTabs();
        }

        private void addEnchantmentFilterTab(EnchantmentFilter filter, string tooltip, string imagePath)
        {
            var button = new Button
            {
                Width = 44,
                Height = 40,
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(7),
                ToolTip = tooltip,
                Tag = filter,
                Content = new Image { Source = ImageResolver.instance.imageSource(imagePath), Stretch = Stretch.Uniform }
            };
            AutomationProperties.SetName(button, tooltip);
            button.Click += (_, __) =>
            {
                _activeEnchantmentFilter = filter;
                updateEnchantmentFilterTabs();
                applyVisibleFilters();
            };
            _enchantmentFilterButtons[filter] = button;
            _filterPanel.Children.Add(button);
        }

        private void updateEnchantmentFilterTabs()
        {
            foreach (var entry in _enchantmentFilterButtons)
            {
                entry.Value.BorderThickness = entry.Key == _activeEnchantmentFilter ? new Thickness(2) : new Thickness(1);
                entry.Value.BorderBrush = entry.Key == _activeEnchantmentFilter
                    ? (Brush)FindResource("AppAccentBrush")
                    : (Brush)FindResource("AppBorderBrush");
            }
        }

        private void applyVisibleFilters()
        {
            string search = _searchBox.Text?.Trim() ?? string.Empty;
            bool wasProcessing = _isProcessing;
            _isProcessing = true;
            _listBox.Items.Clear();
            foreach (var item in _allItems.Where(item =>
                (!_showingEnchantments || EnchantmentDatabase.matchesFilter(item.Tag?.ToString() ?? string.Empty, _activeEnchantmentFilter))
                && ((item.Tag?.ToString() ?? string.Empty).IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    ((item.Content as BaseSelectionWindow.ItemView)?.titleContent?.ToString() ?? string.Empty).IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0)))
                _listBox.Items.Add(item);
            _isProcessing = wasProcessing;
        }

        private IEnumerable<string> itemsForFilter(ItemFilterEnum filter)
        {
            switch(filter)
            {
                case ItemFilterEnum.Artifacts: return ItemDatabase.artifacts;
                case ItemFilterEnum.Armor: return ItemDatabase.armor;
                case ItemFilterEnum.MeleeWeapons: return ItemDatabase.meleeWeapons;
                case ItemFilterEnum.RangedWeapons: return ItemDatabase.rangedWeapons;
                case ItemFilterEnum.All: return ItemDatabase.all;
                //case ItemFilterEnum.Enchanted: return new string[0];
            }
            throw new ArgumentException($"No item database for {filter}", "filter");
        }
    }
}
