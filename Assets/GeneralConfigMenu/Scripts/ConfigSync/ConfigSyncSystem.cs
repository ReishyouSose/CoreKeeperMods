using CoreLib.Data.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Assets.GeneralConfigMenu.Scripts.ConfigSync
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class ConfigSyncSystem : PugSimulationSystemBase
    {
        private int sendTimes;
        private List<(Entity, List<(int, string)>)> send;
        private List<ConfigEntryBase> configs;
        private NativeQueue<Entity> syncAll;
        private ComponentLookup<ConfigSyncRpc> syncLookup;
        private ComponentLookup<ConfigEntryRpc> entryLookup;
        private ComponentLookup<ConfigChunkRpc> chunkLookup;
        private ComponentLookup<ReceiveRpcCommandRequest> receiveLookup;
        private ComponentLookup<PlayerGhost> playerLookup;
        private ComponentLookup<ConnectionAdminLevelCD> adminLookup;
        private EntityArchetype syncArchetype;
        private EntityArchetype entryArchetype;
        private EntityArchetype chunkArchetype;
        private EntityQuery query;
        private Dictionary<(int syncID, Entity connect), ConfigSyncCollector> collectors;
        private Dictionary<(int, Entity), List<ConfigEntryRpc>> pendingEntries;
        private Dictionary<(int, Entity), List<ConfigChunkRpc>> pendingChunks;

        protected override void OnCreate()
        {
            send = new();
            configs = OrderedSyncEntries();
            foreach (var entry in configs)
                entry.ConfigFile.SaveOnConfigSet = false;
            if (isServer)
            {
                playerLookup = SystemAPI.GetComponentLookup<PlayerGhost>();
                adminLookup = SystemAPI.GetComponentLookup<ConnectionAdminLevelCD>();
                RequireForUpdate<WorldInfoCD>();
            }
            syncAll = new(Allocator.Persistent);
            syncArchetype = EntityManager.CreateArchetype(typeof(ConfigSyncRpc), typeof(SendRpcCommandRequest));
            entryArchetype = EntityManager.CreateArchetype(typeof(ConfigEntryRpc), typeof(SendRpcCommandRequest));
            chunkArchetype = EntityManager.CreateArchetype(typeof(ConfigChunkRpc), typeof(SendRpcCommandRequest));
            query = SystemAPI.QueryBuilder().WithAll<ReceiveRpcCommandRequest>().WithAny<ConfigSyncRpc, ConfigEntryRpc, ConfigChunkRpc>().Build();
            syncLookup = SystemAPI.GetComponentLookup<ConfigSyncRpc>();
            entryLookup = SystemAPI.GetComponentLookup<ConfigEntryRpc>();
            chunkLookup = SystemAPI.GetComponentLookup<ConfigChunkRpc>();
            receiveLookup = SystemAPI.GetComponentLookup<ReceiveRpcCommandRequest>();
            collectors = new();
            pendingEntries = new();
            pendingChunks = new();
            base.OnCreate();
        }

        protected override void OnDestroy()
        {
            syncAll.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer ecb = CreateCommandBuffer();
            bool isServer = this.isServer;
            var send = this.send;
            var syncAll = this.syncAll;
            var syncLookup = this.syncLookup;
            var entryLookup = this.entryLookup;
            var chunkLookup = this.chunkLookup;
            var receiveLookup = this.receiveLookup;
            var collectors = this.collectors;
            var pendingEntries = this.pendingEntries;
            var pendingChunks = this.pendingChunks;

            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                ecb.DestroyEntity(entity);
                var source = receiveLookup[entity].SourceConnection;
                if (syncLookup.TryGetComponent(entity, out ConfigSyncRpc sync))
                {
                    var key = (sync.SyncID, source);
                    collectors[key] = new ConfigSyncCollector(sync);
                }
                else if (entryLookup.TryGetComponent(entity, out ConfigEntryRpc entry))
                {
                    var key = (entry.SyncID, source);
                    if (!pendingEntries.TryGetValue(key, out var list))
                        list = pendingEntries[key] = new();
                    list.Add(entry);
                }
                else if (chunkLookup.TryGetComponent(entity, out ConfigChunkRpc chunk))
                {
                    var key = (chunk.SyncID, source);
                    if (!pendingChunks.TryGetValue(key, out var list))
                        list = pendingChunks[key] = new();
                    list.Add(chunk);
                }
            }

            var entryKeys = pendingEntries.Keys.ToArray();
            foreach (var key in entryKeys)
            {
                if (collectors.TryGetValue(key, out var collector))
                {
                    var entries = pendingEntries[key];
                    int count = entries.Count;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        collector.CollectEntry(entries[i]);
                        entries.RemoveAt(i);
                    }
                }
            }
            var chunkKeys = pendingChunks.Keys.ToArray();
            foreach (var key in chunkKeys)
            {
                if (collectors.TryGetValue(key, out var collector))
                {
                    var chunks = pendingChunks[key];
                    int count = chunks.Count;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        if (collector.CollectChunk(chunks[i]))
                            chunks.RemoveAt(i);
                    }
                }
            }

            var collectorKeys = collectors.Keys.ToArray();
            foreach (var key in collectorKeys)
            {
                var collector = collectors[key];
                if (!collector.Completed)
                    continue;
                collectors.Remove(key);
                pendingEntries.Remove(key);
                pendingChunks.Remove(key);
                if (isServer)
                    ApplyIncoming(ecb, key.connect, collector);
                else
                    DeliverToMenu(collector);
            }

            while (send.Count > 0)
            {
                foreach (var (target, modifies) in send)
                {
                    int syncID = isServer ? 0 : ++sendTimes;
                    var changes = new Dictionary<int, ConfigEntryCollector>();
                    foreach (var (id, value) in modifies)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(value);
                        changes[id] = new()
                        {
                            ChunkCount = (bytes.Length - 1) / 16 + 1,
                            Value = bytes,
                        };
                    }
                    var player = isServer || !Manager.main.player ? Entity.Null : Manager.main.player.entity;
                    SendSnapshot(ecb, syncID, isServer ? target : Entity.Null, player, changes);
                }
                send.Clear();
            }

            if (isServer)
            {
                Entities.ForEach((Entity rpcEntity, in ReceiveRpcCommandRequest receive) =>
                {
                    syncAll.Enqueue(receive.SourceConnection);
                    ecb.DestroyEntity(rpcEntity);
                })
                    .WithBurst()
                    .WithAll<SyncAllConfigRpc>()
                    .Schedule();
            }

            while (syncAll.TryDequeue(out var connect))
            {
                if (isServer)
                    SendValues(ecb, connect);
                else
                {
                    var e = ecb.CreateEntity();
                    ecb.AddComponent<SyncAllConfigRpc>(e);
                    ecb.AddComponent<SendRpcCommandRequest>(e);
                }
            }
            base.OnUpdate();
        }
        private void ApplyIncoming(EntityCommandBuffer ecb, Entity connection, ConfigSyncCollector collector)
        {
            adminLookup.Update(this);
            playerLookup.Update(this);
            int privileges = 0;
            if (adminLookup.TryGetComponent(connection, out var admin))
                privileges = admin.adminPrivileges;
            if (privileges <= 0 && playerLookup.TryGetComponent(collector.Player, out var ghost))
                privileges = ghost.adminPrivileges;
            bool guest = SystemAPI.TryGetSingleton(out WorldInfoCD worldInfo) && worldInfo.guestMode;
            bool adminOnly = GeneralConfigMenuMod.config.AdminOnly.Value;
            bool accepted = false;
            foreach (var (id, change) in collector.Changes)
            {
                if ((uint)id >= (uint)configs.Count || configs[id] == null)
                {
                    Debug.Log("Rejected unknown config id " + id);
                    continue;
                }
                var config = configs[id];
                string name = config.GetFullName();
                string requested = Encoding.UTF8.GetString(change.Value);
                bool allowed = privileges > 0
                    || (!guest && !adminOnly && config.Scope.accessLevel == ConfigAccessLevel.Server);
                if (allowed)
                {
                    config.SetSerializedValue(requested);
                    accepted = true;
                    Debug.Log($"Accepted {name} = {config.GetSerializedValue()}");
                }
                else
                    Debug.Log($"Rejected {name}, privileges {privileges}, guest {guest}, adminOnly {adminOnly}");
                byte[] stored = Encoding.UTF8.GetBytes(config.GetSerializedValue());
                change.ChunkCount = (stored.Length - 1) / 16 + 1;
                change.Value = stored;
            }
            SendSnapshot(ecb, collector.SyncID, Entity.Null, collector.Player, collector.Changes);
            if (!accepted || !Manager.networking.currentSessionIsDedicatedServer)
                return;
            var saved = new HashSet<ConfigFile>();
            foreach (var id in collector.Changes.Keys)
            {
                var file = configs[id].ConfigFile;
                if (!saved.Add(file))
                    continue;
                file.Save();
                Debug.Log("Saved " + file.ConfigFilePath);
            }
        }

        private void DeliverToMenu(ConfigSyncCollector collector)
        {
            var menu = ModConfigMenu.Instance;
            if (menu != null && collector.SyncID != 0 && collector.SyncID == sendTimes)
                menu.SetWaitState(false);
            bool listenHost = Manager.ecs != null && Manager.ecs.ServerWorld != null;
            foreach (var (id, change) in collector.Changes)
            {
                string value = Encoding.UTF8.GetString(change.Value);
                if ((uint)id >= (uint)configs.Count)
                    continue;
                if (!(listenHost && collector.SyncID == 0))
                    configs[id].SetSerializedValue(value);
                if (menu != null)
                    menu.Receive.Add((id, value));
            }
        }

        private void SendSnapshot(EntityCommandBuffer ecb, int syncID, Entity target, Entity player, Dictionary<int, ConfigEntryCollector> changes)
        {
            var sync = ecb.CreateEntity(syncArchetype);
            ecb.SetComponent(sync, new ConfigSyncRpc(syncID, changes.Count, player));
            ecb.SetComponent(sync, new SendRpcCommandRequest { TargetConnection = target });
            foreach (var (id, change) in changes)
            {
                var bytes = change.Value;
                var entry = ecb.CreateEntity(entryArchetype);
                ecb.SetComponent(entry, new ConfigEntryRpc(syncID, id, change.ChunkCount, bytes.Length));
                ecb.SetComponent(entry, new SendRpcCommandRequest { TargetConnection = target });
                for (int i = 0; i < change.ChunkCount; i++)
                {
                    var chunk = ecb.CreateEntity(chunkArchetype);
                    ecb.SetComponent(chunk, new ConfigChunkRpc(syncID, id, i * 16, bytes));
                    ecb.SetComponent(chunk, new SendRpcCommandRequest { TargetConnection = target });
                }
            }
        }

        public ConfigEntryBase GetEntryByID(int id)
        {
            if ((uint)id >= (uint)configs.Count)
                return null;
            return configs[id];
        }
        public void RequestSyncAll() => syncAll.Enqueue(Entity.Null);
        public List<(int, string)> SendConfigChange()
        {
            List<(int, string)> modifies = new();
            send.Add((Entity.Null, modifies));
            return modifies;
        }

        public static List<ConfigEntryBase> OrderedSyncEntries()
        {
            var entries = new List<ConfigEntryBase>();
            foreach (var configFile in ConfigFile.AllConfigFilesReadOnly)
            {
                if (configFile.ConfigFilePath.StartsWith("CoreLib"))
                    continue;
                foreach (var entry in configFile.Entries.Values)
                {
                    if (entry.Scope.ShouldSync)
                        entries.Add(entry);
                }
            }
            entries.Sort(CompareSyncEntry);
            return entries;
        }

        private static int CompareSyncEntry(ConfigEntryBase a, ConfigEntryBase b)
        {
            int path = string.CompareOrdinal(a.ConfigFile.ConfigFilePath, b.ConfigFile.ConfigFilePath);
            if (path != 0)
                return path;
            int section = string.CompareOrdinal(a.Definition.Section, b.Definition.Section);
            if (section != 0)
                return section;
            return string.CompareOrdinal(a.Definition.Key, b.Definition.Key);
        }

        private static ConfigEntryCollector Pack(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            return new()
            {
                ChunkCount = bytes.Length == 0 ? 1 : (bytes.Length + 15) / 16,
                Value = bytes,
            };
        }
        private void SendValues(EntityCommandBuffer ecb, Entity connect)
        {
            var changes = new Dictionary<int, ConfigEntryCollector>();
            for (int i = 0; i < configs.Count; i++)
                changes[i] = Pack(configs[i].GetSerializedValue());
            SendSnapshot(ecb, 0, connect, Entity.Null, changes);
        }
    }
}