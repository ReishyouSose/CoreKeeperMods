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
            if (player == null)
            {
                // No player to count for: return what is cached, as before
                variations = this.variations;
                return materials;
            }
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
                        // Tilled soil needs one hoe of any kind, which is not used up (see BuildingPlaceServer)
                        requires[obj] = tileObj.objectID == ObjectID.WoodHoe ? 1 : ++value;
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
                    int available = material.objectID == ObjectID.WoodHoe
                        ? (client.HasAnyHoe(player.entity) ? 1 : 0)
                        : client.GetExistObjectAmount(player.entity, material.objectID, this.variations[i]);
                    materials[i] = new(material.objectID, material.amountNeeded, available, Entity.Null, null);
                }
            }
            variations = this.variations;
            return materials;
        }

        private readonly List<MaterialInfo> shown = new();
        private readonly List<int> shownVariations = new();
        private readonly List<int> order = new();
        private Comparison<int> missingFirst;
        private float sortedAt = float.NaN;
        private int sortedRows;

        private static int TooltipRows()
        {
            var tooltip = Manager.ui != null && Manager.ui.mouse != null ? Manager.ui.mouse.slotHoverTooltip : null;
            return tooltip != null && tooltip.hoverMaterials != null ? tooltip.hoverMaterials.Count : int.MaxValue;
        }

        // How many kinds of items GetTooltipMaterials leaves out, for a "+N" line in the description
        public int GetHiddenTooltipCount()
        {
            return materials == null ? 0 : Math.Max(0, materials.Count - TooltipRows());
        }

        private int MissingFirst(int a, int b)
        {
            bool missingA = materials[a].amountAvailable < materials[a].amountNeeded;
            bool missingB = materials[b].amountAvailable < materials[b].amountNeeded;
            return missingA == missingB ? a.CompareTo(b) : (missingA ? -1 : 1);
        }

        // The game's tooltip has a fixed number of item rows (SlotHoverTooltip.hoverMaterials) and logs an
        // error every frame when it gets more. Show the items the player is missing first; drop the rest.
        public List<MaterialInfo> GetTooltipMaterials(out List<int> variations)
        {
            var all = GetMaterails(out variations);
            int rows = TooltipRows();
            if (all == null || all.Count <= rows)
                return all;
            // The amounts change only when GetMaterails recounts, so sort again only then
            if (sortedAt == countedAt && sortedRows == rows)
            {
                variations = shownVariations;
                return shown;
            }
            sortedAt = countedAt;
            sortedRows = rows;
            order.Clear();
            for (int i = 0; i < all.Count; i++)
                order.Add(i);
            missingFirst ??= MissingFirst;
            order.Sort(missingFirst);
            shown.Clear();
            shownVariations.Clear();
            for (int k = 0; k < rows; k++)
            {
                shown.Add(all[order[k]]);
                shownVariations.Add(variations[order[k]]);
            }
            variations = shownVariations;
            return shown;
        }
    }
}
