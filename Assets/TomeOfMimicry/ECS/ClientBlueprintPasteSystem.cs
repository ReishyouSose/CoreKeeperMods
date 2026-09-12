using System;
using System.Collections.Generic;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace TomeOfMimicry
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ClientBlueprintPasteSystem : PugSimulationSystemBase
    {
        private const int ChunkChars = 400;
        private const int MaxSendsPerFrame = 2;

        private readonly Queue<BlueprintPasteRPC> _queue = new();
        private EntityArchetype _rpcArchetype;
        private int _nextRequestId = 1;

        protected override void OnCreate()
        {
            UpdatesInRunGroup();
            _rpcArchetype = EntityManager.CreateArchetype(
                typeof(BlueprintPasteRPC),
                typeof(SendRpcCommandRequest));
            base.OnCreate();
        }

        public static bool QueuePaste(
            Blueprint blueprint,
            int2 origin,
            int rotation,
            bool flipped,
            BlueprintPasteMode pasteMode)
        {
            var world = API.Client.World;
            var player = Manager.main?.player;
            if (world == null || player == null)
                return false;

            var system = world.GetOrCreateSystemManaged<ClientBlueprintPasteSystem>();
            return system.Enqueue(blueprint, player.entity, origin, rotation, flipped, pasteMode);
        }

        public static bool TrySendCommand(int command, string payload = "")
        {
            var world = API.Client.World;
            var player = Manager.main?.player;
            if (world == null || player == null)
                return false;

            var system = world.GetOrCreateSystemManaged<ClientBlueprintPasteSystem>();
            system._queue.Enqueue(new BlueprintPasteRPC
            {
                protocol = BlueprintPasteRPC.CurrentProtocol,
                command = command,
                player = player.entity,
                requestId = system._nextRequestId++,
                chunkIndex = 0,
                totalChunks = 1,
                payload = payload,
            });
            return true;
        }

        private bool Enqueue(
            Blueprint blueprint,
            Entity player,
            int2 origin,
            int rotation,
            bool flipped,
            BlueprintPasteMode pasteMode)
        {
            if (!BlueprintWorkload.TryValidate(blueprint, pasteMode, out string workloadReason))
            {
                BlueprintManager.PasteFailed = true;
                BlueprintManager.PasteFailureMessage = workloadReason;
                Debug.LogWarning($"[Tome of Mimicry] Paste rejected before upload: {workloadReason}");
                return false;
            }

            string json = BlueprintSerializer.Serialize(blueprint);

            for (int i = 0; i < json.Length; i++)
            {
                if (json[i] < 128) continue;
                var chars = json.ToCharArray();
                for (int j = i; j < chars.Length; j++)
                    if (chars[j] >= 128) chars[j] = '_';
                json = new string(chars);
                break;
            }

            int requestId = _nextRequestId++;
            if (_nextRequestId == int.MaxValue)
                _nextRequestId = 1;

            int totalChunks = Math.Max(1, (json.Length + ChunkChars - 1) / ChunkChars);
            for (int i = 0; i < totalChunks; i++)
            {
                int start = i * ChunkChars;
                int length = Math.Min(ChunkChars, json.Length - start);
                string chunk = json.Substring(start, length);

                _queue.Enqueue(new BlueprintPasteRPC
                {
                    protocol = BlueprintPasteRPC.CurrentProtocol,
                    player = player,
                    requestId = requestId,
                    chunkIndex = i,
                    totalChunks = totalChunks,
                    originX = origin.x,
                    originY = origin.y,
                    rotation = rotation,
                    flipped = flipped ? 1 : 0,
                    pasteMode = (int)pasteMode,
                    payload = chunk
                });
            }

            Debug.Log($"[Tome of Mimicry] Sent paste request {requestId} in {totalChunks} chunk(s)");
            return true;
        }

        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();
            for (int sent = 0; sent < MaxSendsPerFrame && _queue.Count > 0; sent++)
            {
                Entity e = ecb.CreateEntity(_rpcArchetype);
                ecb.SetComponent(e, _queue.Dequeue());
            }

            Entities.ForEach((Entity rpcEntity, in BlueprintPasteRPC rpc, in ReceiveRpcCommandRequest req) =>
                {
                    BlueprintManager.HandleServerFeedback(rpc.command);
                    ecb.DestroyEntity(rpcEntity);
                })
                .WithoutBurst()
                .Run();

            base.OnUpdate();
        }
    }
}
