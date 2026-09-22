using Assets.GeneralConfigMenu.Scripts.DataStruct;
using CoreLib.Data.Configuration;
using System.Collections.Generic;

namespace Assets.GeneralConfigMenu.Scripts
{
    public abstract class CombindConfigPage
    {
        private const string Value = nameof(Value);
        internal readonly static Dictionary<string, CombindConfigPage> PageList = new();
        internal Dictionary<ConfigEntry<bool>, ConfigData> Configs;
        internal ConfigFile File;

        /// <summary>Don't with extension</summary>
        public abstract string FilePath { get; }
        public abstract void Init(ConfigFile file);
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
