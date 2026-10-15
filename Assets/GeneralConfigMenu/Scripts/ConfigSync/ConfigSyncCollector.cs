using System.Collections.Generic;
using Unity.Entities;

namespace Assets.GeneralConfigMenu.Scripts.ConfigSync
{
    public class ConfigSyncCollector
    {
        public int SyncID;
        public int ConfigCount;
        public Entity Player;
        public readonly Dictionary<int, ConfigEntryCollector> Changes = new();
        public readonly List<ConfigEntryRpc> Entries = new();
        public readonly List<ConfigChunkRpc> Chunks = new();
        private int collectedCount;
        public bool ShouldApply;
        public bool Completed { get; private set; }

        public ConfigSyncCollector()
        {
            Completed = true;
        }
        public ConfigSyncCollector(ConfigSyncRpc sync)
        {
            SyncID = sync.SyncID;
            ConfigCount = sync.ConfigCount;
            Player = sync.Player;
            ShouldApply = true;
            if (ConfigCount == 0)
                Completed = true;
        }

        public void CollectEntry(ConfigEntryRpc entry)
        {
            Changes.Add(entry.ConfigID, new(entry));
        }
        public bool CollectChunk(ConfigChunkRpc chunk)
        {
            if (Changes.TryGetValue(chunk.ConfigID, out var entry))
            {
                if (entry.CollectChunk(chunk) && ++collectedCount == ConfigCount)
                    Completed = true;
                return true;
            }
            return false;
        }
    }
    public class ConfigEntryCollector
    {
        public int ChunkCount;
        public byte[] Value;
        private int collectedCount;
        public ConfigEntryCollector() { }
        public ConfigEntryCollector(ConfigEntryRpc entry)
        {
            ChunkCount = entry.ChunkCount;
            Value = new byte[entry.ActualLength];
        }

        public bool CollectChunk(ConfigChunkRpc chunk)
        {
            var offset = chunk.Offset;
            var value = Value;
            int length = value.Length;
            for (int i = 0; i < 16; i++)
            {
                int index = i + offset;
                if (index >= length)
                    break;
                value[index] = chunk.GetByte(i);
            }
            return ++collectedCount == ChunkCount;
        }
    }
}