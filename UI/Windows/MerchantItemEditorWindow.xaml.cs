using MCDSaveEdit.Data;
using MCDSaveEdit.Logic;
using MCDSaveEdit.Save.Models.Profiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
#nullable enable

namespace MCDSaveEdit.UI
{
    public partial class MerchantItemEditorWindow : Window
    {
        private readonly bool _restrictInvestedEnchantments;
        private readonly Item _workingItem;
        public Item EditedItem => _workingItem;

        public MerchantItemEditorWindow(Item source, string contextTitle, bool restrictInvestedEnchantments)
        {
            _restrictInvestedEnchantments = restrictInvestedEnchantments;
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            _workingItem = JsonSerializer.Deserialize<Item>(JsonSerializer.Serialize(source, options), options)
                ?? throw new InvalidOperationException("The item could not be copied for editing.");
            InitializeComponent();
            titleText.Text = contextTitle;
            restrictionText.Text = restrictInvestedEnchantments
                ? "Power, rarity, type, gilding, armor properties, and enchantment choices can be changed. Ordinary enchantment choices must remain uninvested (tier 0); gilded enchantments may have a tier."
                : "Power, rarity, type, gilding, armor properties, and enchantments can all be changed.";
            validationText.Text = restrictInvestedEnchantments
                ? "Merchant stock cannot contain invested ordinary enchantments."
                : "Changes are applied only when you press Apply Item.";

            itemScreen.configureEmbeddedEditor();
            itemScreen.item = _workingItem;
            itemScreen.saveChanges = new RelayCommand<Item>(_ => itemScreen.updateUI());
            itemScreen.selectEnchantment = new RelayCommand<Enchantment>(selectEnchantment);
            itemScreen.addEnchantmentSlot = new RelayCommand<object>(_ => addEnchantmentSlot());
            enchantmentScreen.saveChanges = new RelayCommand<Enchantment>(_ => itemScreen.updateUI());
            enchantmentScreen.close = new RelayCommand<Enchantment>(_ => closeEnchantment());
        }

        private void selectEnchantment(Enchantment enchantment)
        {
            enchantmentScreen.enchantment = enchantment;
            enchantmentScreen.isGilded = ReferenceEquals(enchantment, _workingItem.NetheriteEnchant);
            enchantmentEmptyPanel.Visibility = Visibility.Collapsed;
            enchantmentScreen.Visibility = Visibility.Visible;
        }

        private void closeEnchantment()
        {
            enchantmentScreen.Visibility = Visibility.Collapsed;
            enchantmentEmptyPanel.Visibility = Visibility.Visible;
            itemScreen.updateUI();
        }

        private void addEnchantmentSlot()
        {
            var enchantments = _workingItem.Enchantments?.ToList() ?? new List<Enchantment>();
            if (enchantments.Count >= Constants.MAXIMUM_ENCHANTMENT_OPTIONS_PER_ITEM) return;
            enchantments.Add(new Enchantment { Id = Constants.DEFAULT_ENCHANTMENT_ID, Level = 0 });
            enchantments.Add(new Enchantment { Id = Constants.DEFAULT_ENCHANTMENT_ID, Level = 0 });
            enchantments.Add(new Enchantment { Id = Constants.DEFAULT_ENCHANTMENT_ID, Level = 0 });
            _workingItem.Enchantments = enchantments.Take(Constants.MAXIMUM_ENCHANTMENT_OPTIONS_PER_ITEM).ToArray();
            itemScreen.updateUI();
        }

        private void applyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_restrictInvestedEnchantments && MerchantItemEditing.HasInvestedOrdinaryEnchantments(_workingItem))
            {
                ModernDialog.Show(this, "Merchant enchantment tiers are invalid",
                    "Ordinary merchant-stock enchantments must remain at tier 0.",
                    MessageBoxButton.OK, MessageBoxImage.Warning,
                    "The enchantment choices may be changed, and a gilded enchantment may have a tier because it does not consume the buyer's enchantment points.");
                return;
            }
            DialogResult = true;
            Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
