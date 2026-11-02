using Assets.TerraZenith.Scripts.Core;
using PugMod;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace Assets.PointShop.Scripts.Systems
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateAfter(typeof(DropLootSystem))]
    public partial class DropZenithRecipeSystem : PugSimulationSystemBase
    {
        private struct RolledZenithRecipeCD : IComponentData { }
        private ObjectID recipe;
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
            NeedDatabase();
            base.OnCreate();
        }
        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            recipe = API.Authoring.GetObjectID(ZenithBalance.RecipeName);
        }
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingleton<NetworkTime>(out var networkTime) || !networkTime.IsFirstTimeFullyPredictingTick)
                return;
            var ecb = CreateCommandBuffer();
            var recipe = this.recipe;
            var database = this.database;
            var world = this.World;
            Entities.ForEach((Entity entity, ref RandomCD random, in KilledByPlayerCD killed, in LocalTransform trans, in LevelCD level) =>
            {
                ecb.AddComponent<RolledZenithRecipeCD>(entity);
                if (level.areaLevel < AreaLevel.Crystal)
                    return;
                if (random.Value.NextInt(100) == 0)
                {
                    EntityUtility.CreateAndDropItem(recipe, 0, 1, trans.Position, Entity.Null, database, ecb);
                }

            })
                .WithName("DropZenithRecipeSystem")
                .WithBurst()
                .WithAll<BossCD>()
                .WithAll<DropsLootFromLootTableCD>()
                .WithNone<RolledZenithRecipeCD>()
                .Schedule();
            base.OnUpdate();
        }
    }
}
