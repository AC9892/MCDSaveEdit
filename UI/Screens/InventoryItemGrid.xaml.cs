using MCDSaveEdit.Data;
using MCDSaveEdit.Logic;
using MCDSaveEdit.Save.Models.Profiles;
using MCDSaveEdit.Services;
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
    /// Isolated renderer for the original three-column Inventory/Storage item grid.
    /// It intentionally owns no profile, filtering, transfer, or save-editing logic.
    /// </summary>
    public partial class InventoryItemGrid : UserControl
    {
        private const int ITEMS_PER_ROW = 3;
        private const double INVENTORY_ITEM_SIDE_LENGTH = 100;

        public int renderedItemCount { get; private set; }
        private bool _modernPresentation;
        public bool modernPresentation {
            get { return _modernPresentation; }
            set {
                _modernPresentation = value;
                itemsGrid.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
                modernItemsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public InventoryItemGrid()
        {
            InitializeComponent();
        }

        public void clear()
        {
            renderedItemCount = 0;
            inventoryCountLabel.Content = string.Empty;
            itemsGrid.RowDefinitions.Clear();
            itemsGrid.Children.Clear();
            modernItemsPanel.Children.Clear();
        }

        public void render(IEnumerable<Item> sourceItems, Action<Item> selectItem, bool showAddButton, Action addItem)
        {
            var items = sourceItems.ToList();
            itemsGrid.RowDefinitions.Clear();
            itemsGrid.Children.Clear();
            modernItemsPanel.Children.Clear();

            int itemCount = 0;
            foreach (var item in items)
            {
                if (modernPresentation)
                {
                    modernItemsPanel.Children.Add(createModernItemButton(item, selectItem));
                    itemCount++;
                    continue;
                }
                var itemControl = new ItemControl {
                    item = item,
                    Height = INVENTORY_ITEM_SIDE_LENGTH,
                    Width = INVENTORY_ITEM_SIDE_LENGTH,
                };
                var itemButton = new Button {
                    Background = null,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Height = INVENTORY_ITEM_SIDE_LENGTH,
                    Width = INVENTORY_ITEM_SIDE_LENGTH,
                    Margin = new Thickness(0),
                    Content = itemControl,
                    Command = new RelayCommand<Item>(selectItem),
                    CommandParameter = item,
                };
                addButtonToGrid(itemButton, itemCount++);
            }

            if (showAddButton)
            {
                if (modernPresentation)
                {
                    var modernAddButton = new Button {
                        Width = 92, Height = 108, Margin = new Thickness(3), Content = "+ Add item",
                        Command = new RelayCommand<object>(_ => addItem()),
                    };
                    modernAddButton.SetResourceReference(FrameworkElement.StyleProperty, "ItemSlotButton");
                    modernItemsPanel.Children.Add(modernAddButton);
                }
                else
                {
                var newItemButton = new Button {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Height = INVENTORY_ITEM_SIDE_LENGTH,
                    Width = INVENTORY_ITEM_SIDE_LENGTH,
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    Content = "+",
                    Command = new RelayCommand<object>(_ => addItem()),
                };
                addButtonToGrid(newItemButton, itemCount);
                }
            }

            renderedItemCount = itemCount;
            string? countText = R.getString("inventory_count");
            inventoryCountLabel.Content = countText != null
                ? countText.Replace("{current}", itemCount.ToString()).Replace("{max}", Constants.MAXIMUM_INVENTORY_ITEM_COUNT.ToString())
                : R.formatITEMS_COUNT_LABEL(itemCount, Constants.MAXIMUM_INVENTORY_ITEM_COUNT);
        }

        private Button createModernItemButton(Item item, Action<Item> selectItem)
        {
            var itemControl = new ItemControl { item = item, Height = 68, Width = 68, HorizontalAlignment = HorizontalAlignment.Center };
            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.Children.Add(itemControl);
            var name = new TextBlock {
                Text = R.itemName(item.Type), FontSize = 10, FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)FindResource("AppForegroundBrush"),
            };
            Grid.SetRow(name, 1);
            content.Children.Add(name);
            var button = new Button {
                Width = 92, Height = 108, Margin = new Thickness(3), Padding = new Thickness(5),
                Content = content, Command = new RelayCommand<Item>(selectItem), CommandParameter = item,
                ToolTip = $"{R.itemName(item.Type)} — Power {item.level()} — {item.Rarity}",
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "ItemSlotButton");
            return button;
        }

        private void addButtonToGrid(Button button, int index)
        {
            if (index % ITEMS_PER_ROW == 0)
                itemsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(INVENTORY_ITEM_SIDE_LENGTH) });
            itemsGrid.Children.Add(button);
            Grid.SetRow(button, index / ITEMS_PER_ROW);
            Grid.SetColumn(button, index % ITEMS_PER_ROW);
        }
    }
}
