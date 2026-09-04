using MCDSaveEdit.Data;
using MCDSaveEdit.Logic;
using MCDSaveEdit.Save.Models.Enums;
using MCDSaveEdit.Save.Models.Profiles;
using MCDSaveEdit.Services;
using MCDSaveEdit.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
#nullable enable

namespace MCDSaveEdit.UI
{
    /// <summary>
    /// Interaction logic for ItemListScreen.xaml
    /// </summary>
    public partial class ItemListScreen : UserControl
    {
        public bool modernPresentation {
            get { return itemGrid.modernPresentation; }
            set { itemGrid.modernPresentation = value; }
        }
        private static readonly BitmapImage? _allItemsButtonImageSource = ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_all_default");
        private static readonly BitmapImage? _meleeItemsButtonImageSource = ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_melee_default");
        private static readonly BitmapImage? _rangedItemsButtonImageSource = ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_ranged_default");
        private static readonly BitmapImage? _armorItemsButtonImageSource = ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_armour_default");
        private static readonly BitmapImage? _artifactItemsButtonImageSource = ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_consume_default");
        private static readonly BitmapImage? _enchantedItemsButtonImageSource = ImageResolver.instance.imageSource("/Dungeons/Content/UI/Materials/Inventory2/Filter/filter_enchant_default");

        public static void preload() { }

        private IListItems? _model;
        public IListItems? model
        {
            get { return _model; }
            set
            {
                _model = value;
                setupCommands();
                //updateUI(); //Not Needed
            }
        }

        public ItemListScreen()
        {
            InitializeComponent();
            translateStaticStrings();
            if (AppModel.gameContentLoaded)
            {
                useGameContentImages();
            }
            meleeItemsButton.IsEnabled = AppModel.gameContentLoaded;
            rangedItemsButton.IsEnabled = AppModel.gameContentLoaded;
            armorItemsButton.IsEnabled = AppModel.gameContentLoaded;
            artifactItemsButton.IsEnabled = AppModel.gameContentLoaded;
        }

        private void useGameContentImages()
        {
            tryLoadAndSetImage(allItemsButton, _allItemsButtonImageSource);
            tryLoadAndSetImage(meleeItemsButton, _meleeItemsButtonImageSource);
            tryLoadAndSetImage(rangedItemsButton, _rangedItemsButtonImageSource);
            tryLoadAndSetImage(armorItemsButton, _armorItemsButtonImageSource);
            tryLoadAndSetImage(artifactItemsButton, _artifactItemsButtonImageSource);
            tryLoadAndSetImage(enchantedItemsButton, _enchantedItemsButtonImageSource);
        }

        private void tryLoadAndSetImage(Button button, ImageSource? imageSource)
        {
            if (imageSource != null)
            {
                var image = new Image();
                image.Source = imageSource;
                button.Content = image;
            }
        }

        private void translateStaticStrings()
        {
            allItemsButton.Content = R.getString("ItemTag_All") ?? R.ALL_ITEMS_FILTER;
            meleeItemsButton.Content = R.getString("ItemTag_Melee") ?? R.MELEE_ITEMS_FILTER;
            rangedItemsButton.Content = R.getString("ItemTag_Ranged") ?? R.RANGED_ITEMS_FILTER;
            armorItemsButton.Content = R.getString("ItemTag_Armor") ?? R.ARMOR_ITEMS_FILTER;
            artifactItemsButton.Content = R.getString("ItemTag_Items") ?? R.ARTIFACT_ITEMS_FILTER;
            enchantedItemsButton.Content = R.getString("ItemTag_Enchanted") ?? R.ENCHANTED_ITEMS_FILTER;
        }

        private void setupCommands()
        {
            if (_model == null) { return; }
            var model = _model!;

            model.filteredItemList.subscribe(updateGridItemsUIReloadingAll);
        }

        public void updateUI()
        {
            updateGridItemsUI(model?.filteredItemList.value);
        }

        private void updateGridItemsUIReloadingAll(IEnumerable<Item>? items)
        {
            updateGridItemsUI(items, true);
        }

        private void updateGridItemsUI(IEnumerable<Item>? items, bool forceReloadAll = false)
        {
            if (_model?.profile.value == null || items == null)
            {
                itemGrid.clear();
                return;
            }

            //Optimization. Only reload the grid if necessary or forced
            if(forceReloadAll == false && itemGrid.renderedItemCount == items.Count())
            {
                return;
            }

            var currentFilter = _model?.filter.value;
            bool showAddButton = currentFilter != null && currentFilter != ItemFilterEnum.Enchanted && currentFilter != ItemFilterEnum.All;
            itemGrid.render(items!, _model!.selectItem, showAddButton, () => addNewItemButton_Click(_model?.filter.value));
        }

        private void addNewItemButton_Click(ItemFilterEnum? currentFilter)
        {
            if (currentFilter == null || currentFilter == ItemFilterEnum.Enchanted || currentFilter == ItemFilterEnum.All) { return; }
            EventLogger.logEvent("addNewItemButton_Click", new Dictionary<string, object>() { { "currentFilter", currentFilter.ToString() } });
            var item = Constants.createDefaultItemForFilter(currentFilter!.Value);
            model?.addItemToList(item);
            model?.selectItem(item);
        }

        private void setItemFilter(ItemFilterEnum filter)
        {
            if(_model?.profile.value == null) { return; }
            _model!.filter.setValue = filter;
        }

        private void allItemsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_model?.profile.value == null) { return; }
            EventLogger.logEvent("allItemsButton_Click");
            setItemFilter(ItemFilterEnum.All);
        }
        private void allMeleeItemsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_model?.profile.value == null) { return; }
            EventLogger.logEvent("allMeleeItemsButton_Click");
            setItemFilter(ItemFilterEnum.MeleeWeapons);
        }
        private void allRangedItemsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_model?.profile.value == null) { return; }
            EventLogger.logEvent("allRangedItemsButton_Click");
            setItemFilter(ItemFilterEnum.RangedWeapons);
        }
        private void allArmorItemsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_model?.profile.value == null) { return; }
            EventLogger.logEvent("allArmorItemsButton_Click");
            setItemFilter(ItemFilterEnum.Armor);
        }
        private void allArtifactItemsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_model?.profile.value == null) { return; }
            EventLogger.logEvent("allArtifactItemsButton_Click");
            setItemFilter(ItemFilterEnum.Artifacts);
        }
        private void allEnchantedItemsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_model?.profile.value == null) { return; }
            EventLogger.logEvent("allEnchantedItemsButton_Click");
            setItemFilter(ItemFilterEnum.Enchanted);
        }

    }
}
