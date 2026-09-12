using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Assets.CoreEnhance.Scripts.Systems.Misc
{
    public struct TriedDropTrophyCD : IComponentData { }

    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateAfter(typeof(DropLootSystem))]
    public partial class DropTrophySystem : PugSimulationSystemBase
    {
        private NativeHashSet<int> trophys;
        private ComponentLookup<SeasonalLootCD> seasonLookup;
        protected override void OnCreate()
        {
            if (ScriptableData.TryGetDataBlocks<ObjectIDCategoryDataBlock>(out var dataBlocks))
            {
                ObjectIDCategoryDataBlock trophys = dataBlocks.First(x => x.category == "Trophy");
                trophys.UpdateObjectIdsSet();
                this.trophys = new(trophys.ObjectIds.Count, Allocator.Persistent);
                foreach (var trophy in trophys.ObjectIds)
                {
                    this.trophys.Add((int)trophy);
                }
            }
            else
            {
                Debug.LogError("未找到 ObjectIDCategoryDataBlock 数据块");
                Enabled = false;
            }
            NeedDatabase();
            NeedLootBank();
            seasonLookup = SystemAPI.GetComponentLookup<SeasonalLootCD>();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            float chance = TrophyKeeper.TrophyKeeper.DropChance.Value;
            var ecb = CreateCommandBuffer();
            var database = this.database;
            var lootBank = this.lootBank;
            var trophys = this.trophys;
            var seasonLookup = this.seasonLookup;
            Entities.ForEach((Entity e, ref RandomCD rng, in DropsLootFromLootTableCD lootCD, in LocalTransform local) =>
            {
                ecb.AddComponent<TriedDropTrophyCD>(e);
                ref var lootTables = ref lootBank.Value.lootTables;
                int length = lootTables.Length;
                for (int i = 0; i < length; i++)
                {
                    ref var lootTable = ref lootTables[i];
                    if (lootTable.lootTableID != lootCD.lootTableID)
                    {
                        continue;
                    }
                    ref var loots = ref lootTable.lootTable;
                    int count = loots.Length;
                    for (int j = 0; j < count; j++)
                    {
                        var loot = loots[j].objectID;
                        if (!trophys.Contains((int)loot))
                            continue;
                        if (rng.Value.NextFloat() > chance)
                            continue;
                        EntityUtility.CreateAndDropItem(loot, 0, 1, local.Position, Entity.Null, database, ecb);
                    }
                }
                if (!seasonLookup.TryGetComponent(e, out var season))
                    return;
                if (!season.requirementToDropFulfilled)
                    return;
                ref var seasonLoot = ref season.lootBlob.Value;
                length = seasonLoot.Length;
                for (int i = 0; i < length; i++)
                {
                    var loot = seasonLoot[i].lootDropID;
                    if (!trophys.Contains((int)loot))
                        continue;
                    if (rng.Value.NextFloat() > chance)
                        continue;
                    EntityUtility.CreateAndDropItem(loot, 0, 1, local.Position, Entity.Null, database, ecb);
                }
            })
                .WithName("TrophyDrop")
                .WithAll<EnemyCD>()
                .WithAll<StartDroppingLootCD>()
                .WithNone<DontDropLootCD>()
                .WithNone<BossCD>()
                .WithNone<TriedDropTrophyCD>()
                .WithNone<SpawnedByStreamIntegrationCD>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }
    }
}
