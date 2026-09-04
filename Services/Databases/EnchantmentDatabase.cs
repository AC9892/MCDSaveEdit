using MCDSaveEdit.Data;
using System;
using System.Collections.Generic;

namespace MCDSaveEdit.Services
{
    public enum EnchantmentFilter
    {
        All,
        Melee,
        Ranged,
        Armor,
        ExclusiveAndUnused
    }

    public static class EnchantmentDatabase
    {
        public static HashSet<string> allEnchantments = new HashSet<string>();

        // Internal IDs are taken from the game's assets and grouped according to the
        // Minecraft Dungeons enchantment tables. Unknown future IDs remain available in All.
        private static readonly HashSet<string> melee = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AnimaConduitMelee", "Backstabber", "DamageSynergy", "BusyBee", "Chains", "Committed",
            "CriticalHit", "DynamoMelee", "Echo", "EnigmaResonatorMelee", "Exploding", "FireAspect",
            "Freezing", "GravityMelee", "GuardingStrike", "BaneOfIllagers", "Leeching", "Looting",
            "PainCycle", "PoisonedMelee", "ProspectorMelee", "RadianceMelee", "Rampaging",
            "PotionThirstMelee", "Sharpness", "Shockwave", "Smiting", "SoulSiphon", "Stunning",
            "Swirling", "Thundering", "Unchanting", "VoidTouchedMelee", "Weakening"
        };

        private static readonly HashSet<string> ranged = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Accelerating", "AnimaConduitRanged", "ArtifactCharge", "BonusShot", "BurstBowstring",
            "ChainReaction", "CommittedRanged", "CooldownShot", "CriticalHitRanged", "DippingPoison",
            "DynamoRanged", "EnigmaResonatorRanged", "ExplodingRanged", "FireAspectRanged", "FuseShot",
            "GravityRanged", "Growing", "Infinity", "LevitationShot", "LootingRanged", "MultiShot",
            "MultiCharge", "Piercing", "PoisonedRanged", "Power", "ProspectorRanged", "Punch",
            "RadianceRanged", "RapidFire", "PotionThirstRanged", "Ricochet", "RollCharge", "ShockWeb",
            "SmitingRanged", "SoulSiphonRanged", "Supercharge", "TempoTheft", "UnchantingRanged",
            "VoidTouchedRanged", "WeakeningRanged", "WildRage"
        };

        private static readonly HashSet<string> armor = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Acrobat", "BagOfSouls", "BeastBoss", "BeastBurst", "BeastSurge", "Burning", "Chilling",
            "Celerity", "Cowardice", "DeathBarter", "Deflecting", "Electrified", "Explorer", "FinalShout",
            "FireFocus", "Firetrail", "FoodReserves", "Frenzied", "GravityPulse", "HealthSynergy",
            "ResurrectionSurge", "LightningFocus", "LuckOfTheSea", "EmeraldDivination", "MultiDodge",
            "PoisonFocus", "PotionFortification", "ProspectorArmor", "Protection", "Reckless", "Recycler",
            "Flee", "ShadowFlash", "ShadowSurge", "Snowing", "SoulFocus", "SpiritSpeed", "SpeedSynergy",
            "SurpriseGift", "Swiftfooted", "Thorns", "TumbleBee"
        };

        private static readonly HashSet<string> exclusiveAndUnused = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Constants.DEFAULT_ENCHANTMENT_ID,
            "EmeraldShield", "EnvironmentalProtection", "JunglePoisonMelee", "JunglePoisonRanged",
            "ReliableRicochet", "Rushdown", "ShadowShot", "ShadowBarb", "SharedPain", "ThriveUnderPressure",
            "Altruistic", "Barrier", "BowsBoon", "BowBoon", "Knockback", "Regeneration", "Shielding",
            "Invisible", "DecalTrail", "Huge", "MobResurrectionAura", "Withering", "DoubleDamage",
            "FastAttack", "Quick"
        };

        public static bool matchesFilter(string enchantmentId, EnchantmentFilter filter)
        {
            switch (filter)
            {
                case EnchantmentFilter.Melee: return melee.Contains(enchantmentId);
                case EnchantmentFilter.Ranged: return ranged.Contains(enchantmentId);
                case EnchantmentFilter.Armor: return armor.Contains(enchantmentId);
                case EnchantmentFilter.ExclusiveAndUnused: return exclusiveAndUnused.Contains(enchantmentId);
                default: return true;
            }
        }
    }
}
