using Assets.GeneralConfigMenu.Scripts.DataStruct;
using CoreLib.Data.Configuration;
using System;
using System.Collections.Generic;
using System.IO;

namespace Assets.GeneralConfigMenu.Scripts
{
    public class NewCombinePage
    {
        private const string Value = nameof(Value);
        internal readonly static Dictionary<string, CombindConfigPage> PageList = new();
        internal Dictionary<ConfigEntry<bool>, ConfigData> Configs;
        internal ConfigFile File;
        public static void Init()
        {
            if (!ScriptableData.TryGetDataBlocks<CombimePageDataBlock>(out var dataBlocks))
                return;
            foreach (var combine in dataBlocks)
            {
                var mod = ScriptableDataEditorUtility.GetDataBlockHeader(combine);
                var path = Path.Combine(mod, combine.name);
            }
        }
        public void Register()
        {
            File = new ConfigFile(FilePath + ".cfg", true);
            Configs = new();
            Init(File);
            PageList.Add(FilePath, this);
        }
        public void Add(ConfigEntry<bool> entry, out ConfigData data)
        {
            if (!Configs.TryGetValue(entry, out data))
                data = Configs[entry] = new(entry);
        }
        public bool TryAddValue<T>(ConfigEntry<bool> entry, T defaultV, AcceptableValueBase accept = null, string key = null)
        {
            key ??= Value;
            if (Configs.TryGetValue(entry, out ConfigData data))
            {
                ConfigDefinition def = data.Switch.Definition;
                ConfigScope scope = data.Switch.Scope;
                data.SetValue(key, File.Bind(new(def.Section, def.Key + key),
                    defaultV, new(string.Empty, accept), scope));
                return true;
            }
            return false;
        }
        public bool TryGetValue<T>(ConfigEntry<bool> entry, out ConfigEntry<T> value, string key = null)
        {
            key ??= Value;
            value = null;
            if (!Configs.TryGetValue(entry, out var data))
                return false;
            if (!data.Enable)
                return false;
            return data.TryGetValue(key, out value);
        }

        public bool TryGetValues(ConfigEntry<bool> entry, out Dictionary<string, ConfigEntryBase> values)
        {
            values = null;
            if (!Configs.TryGetValue(entry, out var data))
                return false;
            if (!data.Enable)
                return false;
            values = data.Values;
            return true;
        }

        public static bool TryGetConfigFile(string pathWithoutExtension, out ConfigFile file)
        {
            file = null;
            if (PageList.TryGetValue(pathWithoutExtension, out var page))
            {
                file = page.File;
                return true;
            }
            return false;
        }
    }
}
