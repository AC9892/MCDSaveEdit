using MCDSaveEdit.Data;
using MCDSaveEdit.Save.Models.Enums;
using MCDSaveEdit.Save.Models.Profiles;
using MCDSaveEdit.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
#nullable enable

namespace MCDSaveEdit.UI
{
    /// <summary>
    /// Interaction logic for EnchantmentSetControl.xaml
    /// </summary>
    public partial class EnchantmentSetControl : UserControl
    {
        public EnchantmentSetControl()
        {
            InitializeComponent();
            if (AppModel.gameContentLoaded)
            {
                useGameContentImages();
            }

            updateUI();
        }

        private void useGameContentImages()
        {
            backgroundImage.Source = ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/StatusEffect/Enchantment/EnchantmentsBackground");
            topEnchantmentSymbolImage.Source = ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Mobs/enchant_common_icon");
        }

        private Enchantment[]? _enchantments;
        public IEnumerable<Enchantment>? enchantments
        {
            get { return _enchantments; }
            set { _enchantments = value?.ToArray(); updateUI(); }
        }

        public void clearAll()
        {
            enchantment1Image.Source = null;
            enchantment1Button.CommandParameter = null;
            enchantment2Image.Source = null;
            enchantment2Button.CommandParameter = null;
            enchantment3Image.Source = null;
            enchantment3Button.CommandParameter = null;
            setChoiceButtonsVisibility(Visibility.Collapsed);
            upgradedEnchantmentButton.Visibility = Visibility.Visible;
            upgradedEnchantmentImage.Source = ImageResolver.instance.imageSourceForEnchantment(Constants.DEFAULT_ENCHANTMENT_ID);
            upgradedEnchantmentButton.CommandParameter = null;
        }
        public void updateUI()
        {
            if(_enchantments == null || _enchantments.Length == 0)
            {
                clearAll();
                return;
            }

            var upgradedEnchantment = _enchantments.FirstOrDefault(x => x.Level > 0);
            if(upgradedEnchantment != null)
            {
                enchantment1Image.Source = null;
                enchantment1Button.CommandParameter = null;
                enchantment2Image.Source = null;
                enchantment2Button.CommandParameter = null;
                enchantment3Image.Source = null;
                enchantment3Button.CommandParameter = null;
                setChoiceButtonsVisibility(Visibility.Collapsed);
                upgradedEnchantmentButton.Visibility = Visibility.Visible;
                upgradedEnchantmentImage.Source = ImageResolver.instance.imageSourceForEnchantment(upgradedEnchantment);
                upgradedEnchantmentButton.CommandParameter = upgradedEnchantment;
            }
            else
            {
                var enchantment1 = _enchantments.ElementAtOrDefault(0);
                var enchantment2 = _enchantments.ElementAtOrDefault(1);
                var enchantment3 = _enchantments.ElementAtOrDefault(2);
                enchantment1Image.Source = enchantment1 == null ? null : ImageResolver.instance.imageSourceForEnchantment(enchantment1);
                enchantment1Button.CommandParameter = enchantment1;
                enchantment2Image.Source = enchantment2 == null ? null : ImageResolver.instance.imageSourceForEnchantment(enchantment2);
                enchantment2Button.CommandParameter = enchantment2;
                enchantment3Image.Source = enchantment3 == null ? null : ImageResolver.instance.imageSourceForEnchantment(enchantment3);
                enchantment3Button.CommandParameter = enchantment3;
                setChoiceButtonsVisibility(Visibility.Visible);
                upgradedEnchantmentButton.Visibility = Visibility.Collapsed;
                upgradedEnchantmentImage.Source = null;
                upgradedEnchantmentButton.CommandParameter = null;
            }
        }

        private void setChoiceButtonsVisibility(Visibility visibility)
        {
            enchantment1Button.Visibility = visibility;
            enchantment2Button.Visibility = visibility;
            enchantment3Button.Visibility = visibility;
        }

        private ICommand? _command;
        public ICommand? command
        {
            get { return _command; }
            set { _command = value; updateCommand(); }
        }

        public void updateCommand()
        {
            enchantment1Button.Command = _command;
            enchantment2Button.Command = _command;
            enchantment3Button.Command = _command;
            upgradedEnchantmentButton.Command = _command;
        }
    }
}
