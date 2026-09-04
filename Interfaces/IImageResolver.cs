using MCDSaveEdit.Save.Models.Enums;
using MCDSaveEdit.Save.Models.Profiles;
using System.Windows.Media.Imaging;
#nullable enable

namespace MCDSaveEdit.Interfaces
{
    public interface IImageResolver
    {
        string? path { get; }
        int cachedImageCount { get; }
        string? lastImageError { get; }
        void clearImageCache();
        BitmapImage? imageSourceForItem(Item item);
        BitmapImage? imageSourceForItem(string itemType);
        BitmapImage? imageSourceForRarity(Rarity rarity);
        BitmapImage? imageSourceForEnchantment(Enchantment enchantment);
        BitmapImage? imageSourceForEnchantment(string enchantmentType);
        BitmapImage? imageSource(string path);
    }
}
