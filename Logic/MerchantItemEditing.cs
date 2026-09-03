using MCDSaveEdit.Data;
using MCDSaveEdit.Save.Models.Profiles;
using System.Linq;

namespace MCDSaveEdit.Logic
{
    public static class MerchantItemEditing
    {
        public static bool HasInvestedOrdinaryEnchantments(Item item)
        {
            return item.Enchantments?.Any(enchantment =>
                enchantment != null
                && enchantment.Id != Constants.DEFAULT_ENCHANTMENT_ID
                && enchantment.Level > 0) ?? false;
        }
    }
}
