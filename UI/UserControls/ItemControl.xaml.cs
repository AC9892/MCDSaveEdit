using MCDSaveEdit.Data;
using MCDSaveEdit.Logic;
using MCDSaveEdit.Save.Models.Enums;
using MCDSaveEdit.Save.Models.Profiles;
using MCDSaveEdit.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
#nullable enable

namespace MCDSaveEdit.UI
{
    /// <summary>
    /// Interaction logic for ItemControl.xaml
    /// </summary>
    public partial class ItemControl : UserControl
    {
        private static readonly BitmapImage? enchantmentPointsImageSource = ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Inventory2/Enchantment/enchant_counter");
        private static readonly BitmapImage? gildedImageSource = ImageResolver.instance.imageSource("/Dungeons/Content/Content_DLC4/UI/Materials/Inventory/Inventory_slot_gilded_plate");
        private static readonly BitmapImage? markedNewImageSource = ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/HotBar2/Icons/inventoryslot_newitem");

        public static void preload()
        {
            ImageResolver.instance.imageSourceForRarity(Rarity.Common);
            ImageResolver.instance.imageSourceForRarity(Rarity.Rare);
            ImageResolver.instance.imageSourceForRarity(Rarity.Unique);
        }

        public ItemControl()
        {
            InitializeComponent();
            if (AppModel.gameContentLoaded)
            {
                useGameContentImages();
            }

            //Clear out design/testing values
            updateUI();
        }

        private void useGameContentImages()
        {
            enchantmentPointsImage.Source = enchantmentPointsImageSource;
            gildedImage.Source = gildedImageSource;
            markedNewImage.Source = markedNewImageSource;
        }

        private Item? _item;
        public Item? item
        {
            get { return _item; }
            set { _item = value; updateUI(); }
        }

        public void clearAll()
        {
            image.Source = null;
            backImage.Source = null;
            gildedImage.Visibility = Visibility.Hidden;
            markedNewImage.Visibility = Visibility.Hidden;
            enchantmentGlintOverlay.Visibility = Visibility.Hidden;
            inventoryIndexLabel.Content = null;
            powerLabel.Content = null;
            powerLabelBackground.Visibility = Visibility.Collapsed;
            titleLabel.Content = null;
            titleLabel.Visibility = Visibility.Hidden;
            enchantmentPointsBadge.Visibility = Visibility.Hidden;
        }

        public void updateUI()
        {
            if(_item == null)
            {
                clearAll();
                return;
            }

            powerLabel.Content = _item.level();
            powerLabelBackground.Visibility = Visibility.Visible;
            titleLabel.Content = R.itemName(_item.Type);
            image.Source = ImageResolver.instance.imageSourceForItem(_item);
            if (image.Source == null || !AppModel.gameContentLoaded)
            {
                titleLabel.Visibility = Visibility.Visible;
            }
            else
            {
                titleLabel.Visibility = Visibility.Hidden;
            }

            backImage.Source = ImageResolver.instance.imageSourceForRarity(_item.Rarity);

            if(_item.NetheriteEnchant != null)
            {
                gildedImage.Visibility = Visibility.Visible;
            }
            else
            {
                gildedImage.Visibility = Visibility.Hidden;
            }

            markedNewImage.Visibility = _item.MarkedNew == true ? Visibility.Visible : Visibility.Hidden;

            if (Config.instance.showInventoryIndexOrEquipmentSlot)
            {
                inventoryIndexLabel.Content = _item.InventoryIndex?.ToString() ?? _item.EquipmentSlot;
            }

            var enchantmentPoints = _item.enchantmentPoints();
            var activeEnchantment = _item.Enchantments?.FirstOrDefault(enchantment =>
                enchantment != null &&
                enchantment.Level > 0 &&
                enchantment.Id != Constants.DEFAULT_ENCHANTMENT_ID);
            var isEnchanted = activeEnchantment != null;
            enchantmentGlintOverlay.Visibility = isEnchanted ? Visibility.Visible : Visibility.Hidden;

            if (isEnchanted && enchantmentPoints > 0)
            {
                enchantmentPointsBadge.Visibility = Visibility.Visible;
                enchantmentPointsLabel.Content = enchantmentPoints;
                enchantmentPointsBadge.ToolTip = $"{enchantmentPoints} enchantment points invested";
                enchantmentPointsImage.Visibility = enchantmentPointsImage.Source != null
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                enchantmentPointsBadge.Visibility = Visibility.Hidden;
            }
        }
    }
}
