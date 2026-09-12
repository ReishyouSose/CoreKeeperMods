using System;
using System.Collections.Generic;
using System.Text;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace TomeOfMimicry
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ServerBlueprintPasteSystem : PugSimulationSystemBase
    {
        private const int MaxPasteChunks = 1280;
        private const int MaxConcurrentUploadsPerConnection = 4;

        private sealed class PasteAssembly
        {
            public Entity player;
            public int2 origin;
            public int rotation;
            public bool flipped;
            public BlueprintPasteMode pasteMode;
            public string[] chunks;
            public int received;
            public float lastChunkTime;
        }

        private readonly Dictionary<string, PasteAssembly> _assemblies = new();
        private EntityQuery _playerQuery;
        private float _nextStaleSweep;

        protected override void OnCreate()
        {
            UpdatesInRunGroup();
            _playerQuery = GetEntityQuery(
                ComponentType.ReadOnly<ClientInput>(),
                ComponentType.ReadOnly<ContainedObjectsBuffer>());
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();

            Entities.ForEach((Entity rpcEntity, in BlueprintPasteRPC rpc, in ReceiveRpcCommandRequest req) =>
                {
                    ReceiveChunk(rpc, req.SourceConnection);
                    ecb.DestroyEntity(rpcEntity);
                })
                .WithoutBurst()
                .Run();

            if (World.Time.ElapsedTime > _nextStaleSweep)
            {
                _nextStaleSweep = (float)World.Time.ElapsedTime + 5f;
                float now = (float)World.Time.ElapsedTime;
                List<string> stale = null;
                foreach (var kv in _assemblies)
                {
                    if (now - kv.Value.lastChunkTime < 15f) continue;
                    stale ??= new List<string>();
                    stale.Add(kv.Key);
                }
                if (stale != null)
                {
                    foreach (var key in stale)
                    {
                        _assemblies.Remove(key);
                        Debug.LogWarning($"[Tome of Mimicry] Dropped incomplete paste request {key} (chunks lost)");
                    }
                }
            }

            base.OnUpdate();
        }

        private void ReceiveChunk(BlueprintPasteRPC rpc, Entity sourceConnection)
        {
            if (rpc.protocol != BlueprintPasteRPC.CurrentProtocol)
            {
                Debug.LogWarning($"[Tome of Mimicry] Ignored RPC from {sourceConnection}: " +
                                 $"mod version mismatch (their protocol {rpc.protocol}, ours {BlueprintPasteRPC.CurrentProtocol})");
                return;
            }

            if (!TryResolvePlayer(sourceConnection, out Entity player))
            {
                Debug.LogWarning($"[Tome of Mimicry] Rejected RPC from {sourceConnection}: no owned player");
                return;
            }

            if (rpc.command == 1)
            {
                UndoManager.QueueRequest(player, redo: false, connection: sourceConnection);
                Debug.Log($"[Tome of Mimicry] Undo requested by {sourceConnection}");
                return;
            }
            if (rpc.command == 2)
            {
                UndoManager.QueueRequest(player, redo: true, connection: sourceConnection);
                Debug.Log($"[Tome of Mimicry] Redo requested by {sourceConnection}");
                return;
            }

            if (rpc.command == 3)
            {
                var parts = rpc.payload.ToString().Split(';');
                if (parts.Length == 4 &&
                    int.TryParse(parts[0], out int x0) && int.TryParse(parts[1], out int y0) &&
                    int.TryParse(parts[2], out int x1) && int.TryParse(parts[3], out int y1))
                {
                    var min = new int2(math.min(x0, x1), math.min(y0, y1));
                    var max = new int2(math.max(x0, x1), math.max(y0, y1));
                    long area = (long)(max.x - min.x + 1) * (max.y - min.y + 1);
                    if (area > 0 && area <= 101 * 101)
                    {
                        BuilderGadgetSystem.EnqueueCut(new BuilderGadgetSystem.CutRequest
                        {
                            Player = player,
                            SourceConnection = sourceConnection,
                            Min = min,
                            Max = max,
                        });
                        Debug.Log($"[Tome of Mimicry] Cut {min}->{max} requested by {sourceConnection}");
                    }
                    else
                        Debug.LogWarning($"[Tome of Mimicry] Rejected oversized cut request ({area} cells)");
                }
                else
                    Debug.LogWarning("[Tome of Mimicry] Ignored malformed cut RPC");
                return;
            }

            if (rpc.totalChunks <= 0 ||
                rpc.chunkIndex < 0 ||
                rpc.chunkIndex >= rpc.totalChunks)
            {
                Debug.LogWarning("[Tome of Mimicry] Ignored malformed paste RPC chunk");
                return;
            }

            if (rpc.totalChunks > MaxPasteChunks)
            {
                Debug.LogWarning($"[Tome of Mimicry] Rejected oversized paste request ({rpc.totalChunks} chunks)");
                return;
            }

            string key = $"{sourceConnection.Index}:{sourceConnection.Version}:{rpc.requestId}";
            if (!_assemblies.TryGetValue(key, out var assembly))
            {
                string connectionPrefix = $"{sourceConnection.Index}:{sourceConnection.Version}:";
                int activeUploads = 0;
                foreach (string existingKey in _assemblies.Keys)
                    if (existingKey.StartsWith(connectionPrefix, StringComparison.Ordinal))
                        activeUploads++;
                if (activeUploads >= MaxConcurrentUploadsPerConnection)
                {
                    Debug.LogWarning($"[Tome of Mimicry] Rejected upload from {sourceConnection}: too many active requests");
                    return;
                }

                int rotation = ((rpc.rotation % 360) + 360) % 360;
                if ((rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270) ||
                    !Enum.IsDefined(typeof(BlueprintPasteMode), rpc.pasteMode))
                {
                    Debug.LogWarning($"[Tome of Mimicry] Rejected upload from {sourceConnection}: invalid transform or paste mode");
                    return;
                }

                assembly = new PasteAssembly
                {
                    player = player,
                    origin = new int2(rpc.originX, rpc.originY),
                    rotation = rotation,
                    flipped = rpc.flipped != 0,
                    pasteMode = (BlueprintPasteMode)rpc.pasteMode,
                    chunks = new string[rpc.totalChunks]
                };
                _assemblies[key] = assembly;
            }

            if (assembly.chunks.Length != rpc.totalChunks)
            {
                _assemblies.Remove(key);
                Debug.LogWarning("[Tome of Mimicry] Dropped paste RPC with inconsistent chunk count");
                return;
            }

            if (assembly.player != player || assembly.origin.x != rpc.originX ||
                assembly.origin.y != rpc.originY || assembly.rotation != ((rpc.rotation % 360) + 360) % 360 ||
                assembly.flipped != (rpc.flipped != 0) || assembly.pasteMode != (BlueprintPasteMode)rpc.pasteMode)
            {
                _assemblies.Remove(key);
                Debug.LogWarning("[Tome of Mimicry] Dropped paste RPC with inconsistent request metadata");
                return;
            }

            if (assembly.chunks[rpc.chunkIndex] == null)
            {
                assembly.chunks[rpc.chunkIndex] = rpc.payload.ToString();
                assembly.received++;
                assembly.lastChunkTime = (float)World.Time.ElapsedTime;
            }

            if (assembly.received < assembly.chunks.Length)
                return;

            _assemblies.Remove(key);

            try
            {
                var sb = new StringBuilder();
                for (int i = 0; i < assembly.chunks.Length; i++)
                    sb.Append(assembly.chunks[i]);

                var blueprint = BlueprintSerializer.Deserialize(sb.ToString());

                if (!ValidateNetworkBlueprint(blueprint, assembly.pasteMode, out string reason))
                {
                    Debug.LogWarning($"[Tome of Mimicry] Rejected paste from {sourceConnection}: {reason}");
                    return;
                }

                BuilderGadgetSystem.EnqueuePaste(new BuilderGadgetSystem.PasteRequest
                {
                    Blueprint = blueprint,
                    Player = assembly.player,
                    SourceConnection = sourceConnection,
                    Origin = assembly.origin,
                    Rotation = assembly.rotation,
                    Flipped = assembly.flipped,
                    PasteMode = assembly.pasteMode
                });

                Debug.Log($"[Tome of Mimicry] Received paste request {rpc.requestId} from {sourceConnection}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Tome of Mimicry] Failed to decode paste RPC: {ex.Message}");
            }
        }

        private bool TryResolvePlayer(Entity sourceConnection, out Entity player)
        {
            player = Entity.Null;
            if (sourceConnection != Entity.Null && EntityManager.Exists(sourceConnection) &&
                EntityManager.HasComponent<CommandTarget>(sourceConnection))
            {
                var target = EntityManager.GetComponentData<CommandTarget>(sourceConnection).targetEntity;
                if (target != Entity.Null && EntityManager.Exists(target) &&
                    EntityManager.HasComponent<ClientInput>(target) &&
                    EntityManager.HasBuffer<ContainedObjectsBuffer>(target))
                {
                    player = target;
                    return true;
                }
            }

            using var players = _playerQuery.ToEntityArray(Allocator.Temp);
            if (players.Length != 1)
                return false;

            player = players[0];
            return true;
        }

        private static bool ValidateNetworkBlueprint(Blueprint bp, BlueprintPasteMode mode, out string reason)
        {
            reason = null;
            if (bp == null) { reason = "null blueprint"; return false; }
            if (bp.schemaVersion < 1 || bp.schemaVersion > Blueprint.CurrentSchemaVersion)
            { reason = $"unsupported schema {bp.schemaVersion}"; return false; }
            if (bp.width < 1 || bp.width > 101 || bp.height < 1 || bp.height > 101)
            { reason = $"size {bp.width}x{bp.height}"; return false; }
            if (bp.tiles.Count > bp.width * bp.height)
            { reason = $"{bp.tiles.Count} tiles for {bp.width}x{bp.height}"; return false; }
            if (bp.objects.Count > bp.width * bp.height)
            { reason = $"{bp.objects.Count} objects for {bp.width}x{bp.height}"; return false; }
            if (!BlueprintWorkload.TryValidate(bp, mode, out reason))
                return false;

            var tilePositions = new HashSet<int2>();
            foreach (var t in bp.tiles)
            {
                if (t.relativePos.x < 0 || t.relativePos.x >= bp.width ||
                    t.relativePos.z < 0 || t.relativePos.z >= bp.height)
                { reason = $"tile out of bounds at {t.relativePos}"; return false; }
                if (!tilePositions.Add(new int2(t.relativePos.x, t.relativePos.z)))
                { reason = $"duplicate tile at {t.relativePos}"; return false; }

                if (t.hasFloor &&
                    t.floorTileKind != PugTilemap.TileType.floor &&
                    t.floorTileKind != PugTilemap.TileType.bridge &&
                    t.floorTileKind != PugTilemap.TileType.rail)
                { reason = $"disallowed floor kind {t.floorTileKind}"; return false; }

                if (t.extras != null)
                {
                    if (t.extras.Count > BlueprintLayers.Extras.Length)
                    { reason = $"too many extra layers at {t.relativePos}"; return false; }
                    foreach (var ex in t.extras)
                    {
                        if (!BlueprintLayers.IsExtra((PugTilemap.TileType)ex.x))
                        { reason = $"disallowed extra layer {ex.x}"; return false; }
                    }
                }
            }

            foreach (var o in bp.objects)
            {
                if (o.relativePos.x < 0 || o.relativePos.x >= bp.width ||
                    o.relativePos.y < 0 || o.relativePos.y >= bp.height)
                { reason = $"object out of bounds at {o.relativePos}"; return false; }
                if (o.objectId == ObjectID.None || o.variation < 0 ||
                    o.tileSize.x < 1 || o.tileSize.y < 1 || o.tileSize.x > 16 || o.tileSize.y > 16)
                { reason = $"invalid object data at {o.relativePos}"; return false; }
            }

            return true;
        }
    }
}

