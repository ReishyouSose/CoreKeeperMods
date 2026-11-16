using Assets.BuildingBlueprint.Scripts.Components;
using Assets.BuildingBlueprint.Scripts.Enums;
using CoreLib.Util.Extension;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.Systems
{

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class BlueprintStateChangeClient : PugSimulationSystemBase
    {
        private static BlueprintStateChangeClient ins;
        private EntityArchetype archetype;
        private NativeQueue<BlueprintStateChangeRpc> queue;
        protected override void OnCreate()
        {
            ins = this;
            queue = new(Allocator.Persistent);
            archetype = EntityManager.CreateArchetype(typeof(BlueprintStateChangeRpc), typeof(SendRpcCommandRequest));
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();
            while (queue.TryDequeue(out var rpc))
            {
                var e = ecb.CreateEntity(archetype);
                ecb.SetComponent(e, rpc);
            }
            base.OnUpdate();
        }
        public static void SwitchState(BlueprintUIAction action, int value)
        {
            if (Manager.main.player)
                ins.queue.Enqueue(new()
                {
                    Action = action,
                    Value = value,
                    Player = Manager.main.player.entity,
                });
        }
    }
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class BlueprintStateChangeServer : PugSimulationSystemBase
    {
        private BufferLookup<TileTargetStateBuffer> tileTargetLookup;
        private ComponentLookup<SelectionOptionCD> optionLookup;
        private BufferLookup<SelectedEntityBuffer> entityLookup;
        private BufferLookup<SelectedTileBuffer> tileLookup;
        protected override void OnCreate()
        {
            tileTargetLookup = SystemAPI.GetBufferLookup<TileTargetStateBuffer>();
            optionLookup = SystemAPI.GetComponentLookup<SelectionOptionCD>();
            entityLookup = SystemAPI.GetBufferLookup<SelectedEntityBuffer>();
            tileLookup = SystemAPI.GetBufferLookup<SelectedTileBuffer>();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();
            var tileTargetLookup = this.tileTargetLookup;
            var optionLookup = this.optionLookup;
            var entityLookup = this.entityLookup;
            var tileLookup = this.tileLookup;
            Entities.ForEach((Entity e, in BlueprintStateChangeRpc rpc) =>
            {
                ecb.DestroyEntity(e);
                var player = rpc.Player;
                ref var option = ref optionLookup.GetRefRW(player).ValueRW;
                int value = rpc.Value;
                switch (rpc.Action)
                {
                    case BlueprintUIAction.SwitchUIState:
                        option.Open = value != 0;
                        break;
                    case BlueprintUIAction.Layer:
                        option.Layer = (SelectionLayer)value;
                        break;
                    case BlueprintUIAction.Mode:
                        var newMode = (SelectionMode)value;
                        if (option.Mode != newMode)
                        {
                            option.Mode = newMode;
                            option.Operator = 0;
                        }
                        break;
                    case BlueprintUIAction.Operator:
                        option.Operator = value;
                        break;
                    case BlueprintUIAction.TileTarget:
                        if (tileTargetLookup.TryGetBuffer(player, out var targets))
                        {
                            if (value >= 0)
                                targets[value] = new() { State = !targets[value].State };
                            else
                            {
                                bool? state = value switch
                                {
                                    -1 => true,
                                    -2 => false,
                                    _ => null
                                };
                                for (int i = 0; i < targets.Length; i++)
                                {
                                    targets[i] = new() { State = state ?? !targets[i].State };
                                }
                            }
                        }
                        break;
                    case BlueprintUIAction.Place:
                        option.Place = value != 0;
                        break;
                    case BlueprintUIAction.Release:
                        option.InteractHeld = value != 0;
                        break;
                }
            })
                .WithName("BlueprintStateChange")
                .WithAll<ReceiveRpcCommandRequest>()
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }
    }
}
