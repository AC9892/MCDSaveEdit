using System;
using MCDSaveEdit.Save.Models.Profiles;

namespace MCDSaveEdit.Logic
{
    public static class ItemEditing
    {
        public static void ReplaceArmorProperty(Item item, int index, string newArmorPropertyId)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.Armorproperties == null) throw new InvalidOperationException("The item has no armor properties.");
            if (index < 0 || index >= item.Armorproperties.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (string.IsNullOrWhiteSpace(newArmorPropertyId)) throw new ArgumentException("An armor property identifier is required.", nameof(newArmorPropertyId));
            item.Armorproperties[index].Id = newArmorPropertyId;
        }
    }
}
