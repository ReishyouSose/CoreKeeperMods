using Assets.PointShop.Scripts.Core;
using I2.Loc;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Assets.PointShop.Scripts.Systems
{
    [UpdateInGroup(typeof(RunSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class PointGainReportSystem : PugSimulationSystemBase
    {
        private NativeQueue<PointGainReportRPC> queue;
        private float timer;
        protected override void OnCreate()
        {
            queue = new(Allocator.Persistent);
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            var queue = this.queue;
            var ecb = CreateCommandBuffer();
            Entities.ForEach((Entity e, in PointGainReportRPC report) =>
            {
                ecb.DestroyEntity(e);
                queue.Enqueue(report);
            })
                .WithName("PointGainReportSystem")
                .WithAll<ReceiveRpcCommandRequest>()
                .WithBurst()
                .Schedule();
            timer -= World.Time.DeltaTime;
            while (timer <= 0 && queue.TryDequeue(out var rpc))
            {
                timer = 0.1f;
                var player = Manager.main.player;
                if (!player)
                    break;
                var position = player.RenderPosition + Vector3.up * 1.7f;
                CombatText.SpawnCombatText($"ItemCategory/Environment_{rpc.Zone}Biome", CombatText.NumberColor.Green, position, true, true, true);
                position += Vector3.down;
                CombatText.SpawnCombatText($"Items/PointShop_{rpc.Way}Point", CombatText.NumberColor.Green, position, true, true, true);
            }

            base.OnUpdate();
        }
    }
}
