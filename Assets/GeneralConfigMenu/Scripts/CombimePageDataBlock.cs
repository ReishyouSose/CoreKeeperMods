using CoreLib.Data.Configuration;
using System;
using System.Collections.Generic;

namespace Assets.GeneralConfigMenu.Scripts.DataStruct
{
    public class CombimePageDataBlock : ScriptableDataBlock
    {
        public List<EntriesWithCategoty> Categories;
    }

    [Serializable]
    public struct EntriesWithCategoty
    {
        public string CategoryName;
        public List<AdditionalKeyWithEntry> Entries;
    }

    [Serializable]
    public struct AdditionalKeyWithEntry
    {
        public string EntryName;
        public bool DefaultDisable;
        public bool NeedOverrideLevel;
        public ConfigAccessLevel OverrideLevel;
        public bool RequireReload;
        public List<string> Additional;
    }
}
