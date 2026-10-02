using Assets.CoreEnhance.Scripts.Systems.Misc;
using Assets.GeneralConfigMenu.Scripts;
using CoreLib.Data.Configuration;
using System;
using System.Collections.Generic;

namespace Assets.CoreEnhance.Scripts.Cores
{
    public class EnhanceConfig : CombindConfigPage
    {
        private static EnhanceConfig ins;
        private Dictionary<EnhanceCategory, ConfigEntry<bool>> configs;

        public override string FilePath => "CoreEnhance/Config";

        public override void Init(ConfigFile file)
        {
            ins = this;
            configs = new();
            string section = string.Empty;
            foreach (var category in Enum.GetValues(typeof(EnhanceCategory)))
            {
                EnhanceCategory index = (EnhanceCategory)category;
                string function = category.ToString();
                if (function.StartsWith('_'))
                {
                    section = function[1..];
                    continue;
                }
                ConfigDefinition def = new(section, function);
                ConfigScope scope = null;
                bool _default = true;
                switch (index)
                {
                    case EnhanceCategory.Boulder:
                    case EnhanceCategory.Plant:
                    case EnhanceCategory.Crafting:
                    case EnhanceCategory.FishingNetNoCritter:
                    case EnhanceCategory.FishingNetCanGetItem:
                    case EnhanceCategory.ModifySledgeRange:
                        _default = false;
                        break;
                    case EnhanceCategory.RollSkill:
                        _default = false;
                        scope = new(ConfigAccessLevel.Admin, false);
                        break;
                    case EnhanceCategory.Level:
                        _default = false;
                        scope = new(ConfigAccessLevel.Admin, false);
                        break;
                    case EnhanceCategory.GoldenPlantToSeed:
                        _default = false;
                        scope = new(ConfigAccessLevel.Server, true);
                        break;
                    case EnhanceCategory.ExtraInventory:
                        _default = false;
                        scope = new(ConfigAccessLevel.Admin, true);
                        break;
                    case EnhanceCategory.ProjectileTimerMultiple:
                        scope = new(ConfigAccessLevel.Admin, true);
                        break;
                }
                var entry = file.Bind(def, _default, scope: scope ?? new());
                Add(entry, out _);
                configs.Add(index, entry);
            }
            AddValue();
        }

        private void AddValue()
        {
            TryAddValue(EnhanceCategory.Arena, 1000, new AcceptableValueRange<int>(100, 9999));
            TryAddValue(EnhanceCategory.BossScan, true);
            TryAddValue(EnhanceCategory.Merchant, 0, new AcceptableValueRange<int>(0, 3500));
            TryAddValue(EnhanceCategory.Crafting, true, null, "Animals");
            TryAddValue(EnhanceCategory.Crafting, true, null, "FishingNet");
            TryAddValue(EnhanceCategory.FishingNetCanGetItem, 0.4f, new AcceptableValueRange<float>(0, 0.5f));
            TryAddValue(EnhanceCategory.GoldenPlantToSeed, 8, new AcceptableValueRange<int>(0, 8));
            TryAddValue(EnhanceCategory.Level, 10, new AcceptableValueRange<int>(1, 100));
            TryAddValue(EnhanceCategory.ChainMining, true, null, "Adsorption");
            TryAddValue(EnhanceCategory.ChainMining, true, null, "NeedPlayer");
            TryAddValue(EnhanceCategory.ChainMining, true, null, "GiveExp");
            TryAddValue(EnhanceCategory.ChainMining, ChainTarget.OreAndWood, null, "Target");
            TryAddValue(EnhanceCategory.ModifySledgeRange, 2.5f, new AcceptableValueRange<float>(1.4f, 5f));
            TryAddValue(EnhanceCategory.ExtraInventory, 2, new AcceptableValueRange<int>(1, 10));
            TryAddValue(EnhanceCategory.ProjectileTimerMultiple, 2f, new AcceptableValueRange<float>(1f, 10f));
        }

        /// <returns>查询失败或条目未启用均返回false</returns>
        public static bool IsEnable(EnhanceCategory category)
        {
            if (!ins.configs.TryGetValue(category, out var entry))
                return false;
            return entry.Value;
        }
        private bool TryAddValue<T>(EnhanceCategory category, T defaultV, AcceptableValueBase accept = null, string key = null)
        {
            if (!ins.configs.TryGetValue(category, out var entry))
                return false;
            return TryAddValue(entry, defaultV, accept, key);
        }

        /// <returns>查询失败或条目未启用均返回false</returns>
        public static bool TryGetValue<T>(EnhanceCategory category, out ConfigEntry<T> value, string key = null)
        {
            value = null;
            if (!ins.configs.TryGetValue(category, out var entry))
                return false;
            return ins.TryGetValue(entry, out value, key);
        }

        /// <returns>查询失败或条目未启用均返回false</returns>
        public static bool TryGetValues(EnhanceCategory category, out Dictionary<string, ConfigEntryBase> values)
        {
            values = null;
            if (!ins.configs.TryGetValue(category, out var entry))
                return false;
            return ins.TryGetValues(entry, out values);
        }
    }
}
