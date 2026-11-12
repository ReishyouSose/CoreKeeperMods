using Assets.BuildingBlueprint.Scripts.Systems;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using static PugDatabase;

namespace Assets.BuildingBlueprint.Scripts.Core
{
    [Serializable]
    public class BuildingInfo
    {
        public string Name;
        public string Description;
        [JsonIgnore]
        public string Header;
        public List<EntityInfo> EntityInfos;
        public List<SerializeTileInfo> TileInfos;
        public int2 Size;
        private List<MaterialInfo> materials;
        private List<int> variations;
        private float countedAt = float.NegativeInfinity;
        private const float RecountSeconds = 0.25f;

        // The required items are worked out once and cached. How many the player carries is counted again
        // when the tooltip asks, at most every RecountSeconds, so the list follows the inventory.
        public List<MaterialInfo> GetMaterails(out List<int> variations)
        {
            var player = Manager.main.player;
            variations = null;
            if (player == null)
                return null;
            if (materials == null)
            {
                variations = new();
                List<MaterialInfo> list = new();
                Dictionary<ObjectDataCD, int> requires = new();
                var ins = BuildingPlaceClient.Ins;
                foreach (var entities in EntityInfos)
                {
                    foreach (var entity in entities.Entities)
                    {
                        bool zero = ins.AlwaysDropZero(entity.ObjectID, entity.Variation);
                        ObjectDataCD obj = new()
                        {
                            objectID = entity.ObjectID,
                            variation = zero ? 0 : entity.Variation,
                        };
                        requires.TryGetValue(obj, out int value);
                        requires[obj] = ++value;
                    }
                }
                foreach (var tiles in TileInfos)
                {
                    foreach (var tile in tiles.Tiles)
                    {
                        var tileObj = ins.TileToObject(tile);
                        bool zero = ins.AlwaysDropZero(tileObj.objectID, tileObj.variation);
                        ObjectDataCD obj = new()
                        {
                            objectID = tileObj.objectID,
                            variation = tileObj.variation,
                        };
                        requires.TryGetValue(obj, out int value);
                        requires[obj] = ++value;
                    }
                }
                foreach (var (require, stack) in requires)
                {
                    list.Add(new(require.objectID, stack, 0, Entity.Null, null));
                    variations.Add(require.variation);
                }
                materials = list;
                this.variations = variations;
            }
            // The tooltip asks every frame, so recount at most a few times a second
            float now = UnityEngine.Time.unscaledTime;
            if (now - countedAt >= RecountSeconds)
            {
                countedAt = now;
                var client = BuildingPlaceClient.Ins;
                for (int i = 0; i < materials.Count; i++)
                {
                    var material = materials[i];
                    int available = client.GetExistObjectAmount(player.entity, material.objectID, this.variations[i]);
                    materials[i] = new(material.objectID, material.amountNeeded, available, Entity.Null, null);
                }
            }
            variations = this.variations;
            return materials;
        }
    }
}
