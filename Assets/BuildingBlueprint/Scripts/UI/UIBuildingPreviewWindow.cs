using Assets.BuildingBlueprint.Scripts.Core;
using PugTilemap;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.UI
{
    public class UIBuildingPreviewWindow : UIelement, IOverrideHoverMaterialVariation
    {
        public SpriteRenderer SlotTemplate;
        public Transform SlotContainer;

        [HideInInspector]
        public List<SpriteRenderer> Slots;

        [HideInInspector]
        public SelectionLayer CheckLayer;
        private BuildingInfo info;

        private Dictionary<TileCD, Color> tileColors;
        private List<int> variations;
        private void Awake()
        {
            tileColors = new();
            foreach (TileTypeColorTable.TileSetColors tileSetColors in Resources.Load<TileTypeColorTable>("TileTypeColorTable").tileSetColors)
            {
                foreach (TileTypeColorTable.TileColor tileColor in tileSetColors.tileColors)
                {
                    tileColors.Add(new TileCD
                    {
                        tileset = (int)tileSetColors.pugMapTileset,
                        tileType = tileColor.tileType
                    }, tileColor.color);
                }
            }
        }
        public void Refresh(BuildingInfo info)
        {
            this.info = info;
            SlotContainer.localPosition = Vector3.zero;
            SlotTemplate.gameObject.SetActive(false);
            Refresh();
        }
        private void Refresh()
        {
            Dictionary<float3, Color> map = new();
            var size = -info.Size / 2;
            foreach (var tile in info.TileInfos)
            {
                var top = tile.Tiles.Aggregate((a, b) => a.tileType.GetSurfacePriority() > b.tileType.GetSurfacePriority() ? a : b);
                var pos = tile.Position + size;
                tileColors.TryGetValue(top, out var color);
                map[new(pos.x, pos.y, 0)] = color;
            }
            foreach (var entities in info.EntityInfos)
            {
                foreach (var entity in entities.Entities)
                {
                    var pos = entity.Position + size;
                    var color = PugDatabase.GetObjectInfo(entity.ObjectID, entity.Variation).mapColor;
                    for (int x = 0; x < entity.X; x++)
                    {
                        for (int y = 0; y < entity.Y; y++)
                        {
                            map[new(pos.x + x, pos.y + y, 0)] = color;
                        }
                    }
                }
            }
            int i = 0;
            foreach (var (pos, color) in map)
            {
                var slot = GetOrCreateSlot(i++);
                slot.color = color;
                slot.transform.localPosition = pos / 8;
            }
            DeactiveExcessSlot(i);
        }
        private SpriteRenderer GetOrCreateSlot(int index)
        {
            if (index >= Slots.Count)
                Slots.Add(Instantiate(SlotTemplate, SlotContainer));
            Slots[index].gameObject.SetActive(true);
            return Slots[index];
        }
        private void DeactiveExcessSlot(int index)
        {
            for (int i = index; i < Slots.Count; i++)
            {
                Slots[i].gameObject.SetActive(false);
            }
        }
        public override List<PugDatabase.MaterialInfo> GetRequiredMaterials(bool isRepairing, bool isReinforcing)
        {
            return info.GetTooltipMaterials(out variations);
        }
        public override List<TextAndFormatFields> GetHoverDescription()
        {
            var lines = new List<TextAndFormatFields>
            {
                new()
                {
                   text = "BuildingBlueprint/MaterialTipDesc",
                },
                new()
                {
                    text = info.Description,
                    dontLocalize = true
                }
            };
            // Item kinds that did not fit in the tooltip's rows; no words, so it needs no translation
            int hidden = info.GetHiddenTooltipCount();
            if (hidden > 0)
                lines.Add(new() { text = $"+{hidden} …", dontLocalize = true });
            return lines;
        }
        public override bool ShowRequiredMaterialsAmountNumberColor() => true;

        public int GetOverrideVariation(int index) => variations[index];
    }
}
