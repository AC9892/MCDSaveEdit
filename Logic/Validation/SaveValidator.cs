using MCDSaveEdit.Data;
using MCDSaveEdit.Save.Models.Profiles;
using MCDSaveEdit.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MCDSaveEdit.Logic.Validation
{
    public static class SaveValidator
    {
        public static SaveValidationReport Validate(ProfileSaveFile? profile,
            SaveValidationStrictness strictness = SaveValidationStrictness.Standard)
        {
            var issues = new List<SaveValidationIssue>();
            if (profile == null)
            {
                issues.Add(Error("profile", "No save profile is loaded."));
                return new SaveValidationReport(issues);
            }

            if (profile.Version <= 0)
                issues.Add(Warning("profile.version", "The save version is missing or unsupported."));
            if (profile.ExtensionData?.Count > 0)
                issues.Add(Info("profile", $"{profile.ExtensionData.Count} unrecognized root field(s) will be preserved."));
            if (strictness == SaveValidationStrictness.Strict && ItemDatabase.all.Count == 0)
                issues.Add(Info("validation.gameData", "Game data is not loaded, so strict item, passive, and enchantment ID checks were skipped."));
            if (profile.Items == null)
                issues.Add(Error("profile.items", "The required inventory array is missing."));
            if (profile.Currency == null)
                issues.Add(Error("profile.currency", "The currency array is missing."));

            ValidateCurrencies(profile.Currency, issues);
            ValidateItems(profile.Items, "profile.items", strictness, issues);
            ValidateItems(profile.StorageChestItems, "profile.storageChestItems", strictness, issues);

            if (strictness != SaveValidationStrictness.Basic && string.IsNullOrWhiteSpace(profile.UniqueSaveId))
                issues.Add(Warning("profile.uniqueSaveId", "The save has no unique save identifier."));

            return new SaveValidationReport(issues);
        }

        private static void ValidateCurrencies(Currency[]? currencies, ICollection<SaveValidationIssue> issues)
        {
            if (currencies == null) return;
            var knownTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < currencies.Length; index++)
            {
                var currency = currencies[index];
                var path = $"profile.currency[{index}]";
                if (currency == null)
                {
                    issues.Add(Error(path, "Currency entry is null."));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(currency.Type))
                    issues.Add(Error(path + ".type", "Currency type is required."));
                else if (!knownTypes.Add(currency.Type))
                    issues.Add(Warning(path + ".type", $"Currency type '{currency.Type}' appears more than once."));
            }
        }

        private static void ValidateItems(Item[]? items, string basePath, SaveValidationStrictness strictness,
            ICollection<SaveValidationIssue> issues)
        {
            if (items == null) return;
            var inventoryIndexes = new HashSet<long>();
            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                var path = $"{basePath}[{index}]";
                if (item == null)
                {
                    issues.Add(Error(path, "Item entry is null."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.Type))
                    issues.Add(Error(path + ".type", "Item type is required."));
                else if (strictness == SaveValidationStrictness.Strict && ItemDatabase.all.Count > 0 && !ItemDatabase.all.Contains(item.Type))
                    issues.Add(Warning(path + ".type", $"Item ID '{item.Type}' is not present in the loaded game data."));

                if (double.IsNaN(item.Power) || double.IsInfinity(item.Power))
                    issues.Add(Error(path + ".power", "Power must be a finite number."));
                else if (item.Power < Constants.MINIMUM_ITEM_LEVEL)
                    issues.Add(Error(path + ".power", "Power cannot be negative."));
                else if (item.Power > Constants.MAXIMUM_ITEM_LEVEL)
                    issues.Add(Warning(path + ".power", $"Power exceeds the supported editor range of {Constants.MAXIMUM_ITEM_LEVEL:N0}."));

                if (item.InventoryIndex.HasValue && !inventoryIndexes.Add(item.InventoryIndex.Value))
                    issues.Add(Warning(path + ".inventoryIndex", $"Inventory index {item.InventoryIndex.Value} is duplicated."));

                ValidateEnchantments(item.Enchantments, path + ".enchantments", strictness, issues);
                if (item.NetheriteEnchant != null)
                    ValidateEnchantment(item.NetheriteEnchant, path + ".netheriteEnchant", strictness, issues);

                if (item.Armorproperties == null) continue;
                for (var passiveIndex = 0; passiveIndex < item.Armorproperties.Length; passiveIndex++)
                {
                    var passive = item.Armorproperties[passiveIndex];
                    var passivePath = $"{path}.armorproperties[{passiveIndex}]";
                    if (passive == null || string.IsNullOrWhiteSpace(passive.Id))
                        issues.Add(Error(passivePath, "Armor passive is null or has no ID."));
                    else if (strictness == SaveValidationStrictness.Strict && ItemDatabase.armorProperties.Count > 0 && !ItemDatabase.armorProperties.Contains(passive.Id))
                        issues.Add(Warning(passivePath + ".id", $"Passive ID '{passive.Id}' is not present in the loaded game data."));
                }
            }
        }

        private static void ValidateEnchantments(Enchantment[]? enchantments, string basePath,
            SaveValidationStrictness strictness, ICollection<SaveValidationIssue> issues)
        {
            if (enchantments == null) return;
            for (var index = 0; index < enchantments.Length; index++)
            {
                var enchantment = enchantments[index];
                var path = $"{basePath}[{index}]";
                if (enchantment == null)
                    issues.Add(Error(path, "Enchantment entry is null."));
                else
                    ValidateEnchantment(enchantment, path, strictness, issues);
            }
        }

        private static void ValidateEnchantment(Enchantment enchantment, string path,
            SaveValidationStrictness strictness, ICollection<SaveValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(enchantment.Id))
                issues.Add(Error(path + ".id", "Enchantment ID is required."));
            else if (strictness == SaveValidationStrictness.Strict && EnchantmentDatabase.allEnchantments.Count > 0 &&
                !EnchantmentDatabase.allEnchantments.Contains(enchantment.Id) && enchantment.Id != Constants.DEFAULT_ENCHANTMENT_ID)
                issues.Add(Warning(path + ".id", $"Enchantment ID '{enchantment.Id}' is not present in the loaded game data."));

            if (enchantment.Level < Constants.MINIMUM_ENCHANTMENT_TIER || enchantment.Level > Constants.MAXIMUM_ENCHANTMENT_TIER)
                issues.Add(Warning(path + ".level", $"Enchantment tier is outside {Constants.MINIMUM_ENCHANTMENT_TIER}–{Constants.MAXIMUM_ENCHANTMENT_TIER}."));
            if (enchantment.InvestedPoints < 0)
                issues.Add(Error(path + ".investedPoints", "Invested points cannot be negative."));
        }

        private static SaveValidationIssue Warning(string path, string message) =>
            new SaveValidationIssue(SaveValidationSeverity.Warning, path, message);

        private static SaveValidationIssue Info(string path, string message) =>
            new SaveValidationIssue(SaveValidationSeverity.Info, path, message);

        private static SaveValidationIssue Error(string path, string message) =>
            new SaveValidationIssue(SaveValidationSeverity.Error, path, message);
    }
}
