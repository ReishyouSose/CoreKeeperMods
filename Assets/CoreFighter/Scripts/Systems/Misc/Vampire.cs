using Assets.CoreFighter.Scripts.Cores;
using System;
using Unity.Entities;

namespace Assets.CoreFighter.Scripts.Systems.Misc
{
    [UpdateInGroup(typeof(UpdateHealthSystemGroup))]
    [UpdateBefore(typeof(UpdateHealthFromBufferSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class VampireSystem : PugSimulationSystemBase
    {
        private ComponentLookup<EnemyCD> enemyLookup;
        private ComponentLookup<PlayerGhost> playerLookup;
        private ComponentLookup<OwnerReferenceCD> ownerLookup;
        protected override void OnCreate()
        {
            RequireForUpdate<HealthChangeBuffer>();
            enemyLookup = SystemAPI.GetComponentLookup<EnemyCD>();
            playerLookup = SystemAPI.GetComponentLookup<PlayerGhost>();
            ownerLookup = SystemAPI.GetComponentLookup<OwnerReferenceCD>();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            if (!FighterConfig.TryGetValue<float>(FighterCategory.Vampire, out var value))
                return;
            if (!SystemAPI.TryGetSingletonBuffer<HealthChangeBuffer>(out var buffer))
                return;
            float percent = value.Value;
            foreach (var change in buffer)
            {
                var changer = change.healthChange;
                int amount = changer.amount;
                if (amount >= 0)
                    continue;
                amount = Math.Abs(amount);
                var source = changer.causedByEntity;
                var target = changer.entity;
                if (source == Entity.Null || target == Entity.Null)
                    continue;
                if (!enemyLookup.HasComponent(target))
                    continue;
                source = FindOwner(source);
                if (source == Entity.Null)
                    continue;
                buffer.Add(new()
                {
                    healthChange = new()
                    {
                        entity = source,
                        causedByEntity = source,
                        amount = Math.Max(1, (int)Math.Round(amount * percent)),
                        applyToNonPredicted = true
                    }
                });
            }
            base.OnUpdate();
        }
        private Entity FindOwner(Entity source)
        {
            if (playerLookup.HasComponent(source))
                return source;
            if (!ownerLookup.TryGetComponent(source, out var owner))
                return Entity.Null;
            var current = owner.owner;
            for (int i = 0; i < 10 && current != Entity.Null; i++)
            {
                if (playerLookup.HasComponent(current))
                    return current;
                if (!ownerLookup.TryGetComponent(current, out owner))
                    break;
                current = owner.owner;
            }
            return Entity.Null;
        }
    }
}
