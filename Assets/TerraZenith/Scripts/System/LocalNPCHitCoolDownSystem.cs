using Assets.TerraZenith.Scripts.Component;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.System
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(ProjectileMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class LocalHitCoolDownSystem : PugSimulationSystemBase
    {
        private NativeHashSet<(Entity, Entity)> entityRemoves;
        private NativeHashSet<(Entity, int2)> wallRemoves;
        private EntityStorageInfoLookup existLookup;
        private ComponentLookup<EntityDestroyedCD> destroyedLookup;
        protected override void OnCreate()
        {
            var singleton = EntityManager.CreateSingleton<LocalHitCoolDownCD>("LocalHitCoolDown");
            var cd = EntityManager.GetComponentData<LocalHitCoolDownCD>(singleton);
            cd.EntityCoolDown = new(16, Allocator.Persistent);
            cd.WallCoolDown = new(16, Allocator.Persistent);
            EntityManager.SetComponentData(singleton, cd);
            Debug.LogWarning("create cd");
            RequireForUpdate<LocalHitCoolDownCD>();
            RequireForUpdate<NetworkTime>();
            entityRemoves = new(16, Allocator.Persistent);
            wallRemoves = new(16, Allocator.Persistent);
            destroyedLookup = SystemAPI.GetComponentLookup<EntityDestroyedCD>();
            existLookup = SystemAPI.GetEntityStorageInfoLookup();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingleton(out LocalHitCoolDownCD cooldowns))
                return;
            if (!SystemAPI.TryGetSingleton(out NetworkTime netWorkTime))
                return;
            Dependency.Complete();
            var destroyedLookup = this.destroyedLookup;
            var existLookup = this.existLookup;
            var entityRemoves = this.entityRemoves;
            var wallRemoves = this.wallRemoves;
            var currentTick = netWorkTime.ServerTick;
            var entityCoolDown = cooldowns.EntityCoolDown;
            var wallCoolDown = cooldowns.WallCoolDown;
            foreach (var cd in entityCoolDown)
            {
                var key = cd.Key;
                Entity entity = key.Item1;
                Entity target = key.Item2;
                TickTimer timer = cd.Value;
                if (!existLookup.Exists(target) || !existLookup.Exists(entity))
                {
                    entityRemoves.Add(key);
                    continue;
                }
                if (destroyedLookup.HasComponent(target) || destroyedLookup.HasComponent(entity))
                {
                    entityRemoves.Add(key);
                    continue;
                }
                if (timer.IsTimerElapsed(currentTick))
                {
                    entityRemoves.Add(key);
                }
            }
            foreach (var remove in entityRemoves)
            {
                entityCoolDown.Remove(remove);
            }
            foreach (var cd in wallCoolDown)
            {
                var key = cd.Key;
                Entity entity = key.Item1;
                TickTimer timer = cd.Value;
                if (!existLookup.Exists(entity))
                {
                    wallRemoves.Add(key);
                    continue;
                }
                if (destroyedLookup.HasComponent(entity))
                {
                    wallRemoves.Add(key);
                    continue;
                }
                if (timer.IsTimerElapsed(currentTick))
                {
                    wallRemoves.Add(key);
                }
            }
            foreach (var remove in wallRemoves)
            {
                wallRemoves.Remove(remove);
            }
            base.OnUpdate();
        }
    }
}
