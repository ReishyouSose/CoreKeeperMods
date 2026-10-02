using Assets.CoreEnhance.Scripts.Cores;
using Assets.CoreEnhance.Scripts.Helpers;
using Pug.Properties;
using PugMod;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Assets.CoreEnhance.Scripts.Items
{
    public static class DataModify
    {
        public static void AuthoringPreModify(GameObject authoringData)
        {
            ExtraInventory(authoringData);
        }
        public static void AuthoringPostModify(Entity entity, GameObject authoringData, EntityManager entityManager)
        {
            GoldenPlantExtractToSeed(authoringData);
            ModifyLoot(entity, authoringData, entityManager);
        }
        private static void ExtraInventory(GameObject authoringData)
        {
            if (authoringData.HasComponent<PlayerAuthoring>())
            {
                int y = EnhanceConfig.TryGetValue<int>(EnhanceCategory.ExtraInventory, out var value) ? value.Value : 0;
                authoringData.GetComponent<InventoryAuthoring>().sizeY += y;
            }
        }
        private static void ModifyLoot(Entity entity, GameObject authoringData, EntityManager entityManager)
        {
            ObjectID id = authoringData.GetEntityObjectID(out _);
            List<DropsLootBuffer> add = new();
            switch (id)
            {
                case ObjectID.CoreBoss:
                    add.Add(new()
                    {
                        lootDropID = API.Authoring.GetObjectID("CoreEnhance_BoulderDemolish"),
                        amount = 3,
                        multiplayerAmountAdditionScaling = 1,
                    });
                    break;
            }
            if (add.Count == 0)
                return;
            DynamicBuffer<DropsLootBuffer> buffer;
            if (!entityManager.HasBuffer<DropsLootBuffer>(entity))
                buffer = entityManager.AddBuffer<DropsLootBuffer>(entity);
            else
                buffer = entityManager.GetBuffer<DropsLootBuffer>(entity);
            foreach (var drop in add)
            {
                buffer.Add(drop);
            }
        }
        private static void GoldenPlantExtractToSeed(GameObject authoringData)
        {
            if (!EnhanceConfig.TryGetValue<int>(EnhanceCategory.GoldenPlantToSeed, out var value))
                return;
            if (!authoringData.TryGetComponent<ExtractableAuthoring>(out var extractable))
                return;
            ObjectID seed = authoringData.GetEntityObjectID(out _) switch
            {
                ObjectID.HeartBerryRare => ObjectID.HeartBerrySeed,
                ObjectID.GlowingTulipFlowerRare => ObjectID.GlowingTulipSeed,
                ObjectID.BombPepperRare => ObjectID.BombPepperSeed,
                ObjectID.CarrockRare => ObjectID.CarrockSeed,
                ObjectID.GrumpkinRare => ObjectID.GrumpkinSeed,
                ObjectID.BloatOatRare => ObjectID.BloatOatSeed,
                ObjectID.PuffungiRare => ObjectID.PuffungiSeed,
                ObjectID.PewpayaRare => ObjectID.PewpayaSeed,
                ObjectID.PinegrappleRare => ObjectID.PinegrappleSeed,
                ObjectID.SunriceRare => ObjectID.SunriceSeed,
                ObjectID.LunacornRare => ObjectID.LunacornSeed,
                _ => ObjectID.None
            };
            if (seed == ObjectID.None)
                return;
            var crafts = extractable.extractedObject;
            for (int i = 0; i < crafts.Count; i++)
            {
                var craft = crafts[i];
                if (craft.objectID != ObjectID.GoldOre)
                    continue;
                int rng = value.Value;
                crafts[i] = new()
                {
                    objectID = seed,
                    variation = 0,
                    minMaxRandomAmountOverride = new(rng, rng)
                };
            }
        }
    }
}
