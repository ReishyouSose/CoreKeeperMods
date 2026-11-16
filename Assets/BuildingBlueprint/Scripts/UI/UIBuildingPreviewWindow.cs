using Assets.BuildingBlueprint.Scripts.Core;
using Assets.BuildingBlueprint.Scripts.Enums;
using PugTilemap;
using System.Collections.Generic;
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
        private readonly Dictionary<float3, Color> map = new();
        private readonly Dictionary<int2, TileCD> tiles = new();
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
            var origin = -info.Size / 2;
            EntityCommand entity = default;
            int2 pos = default;
            foreach (var command in info.Commands)
            {
                switch (command.Op)
                {
                    case PlaceOperator.Tile:
                        var tile = command.ReadAs<TileCommand>();
                        pos = tile.Pos + origin;
                        var tileCD = tile.TileCD;
                        if (!tiles.TryGetValue(pos, out var t) || t.tileType.GetSurfacePriority() < tileCD.tileType.GetSurfacePriority())
                            tiles[pos] = tileCD;
                        break;
                    case PlaceOperator.Entity:
                        entity = command.ReadAs<EntityCommand>();
                        pos = entity.Pos + origin;
                        break;
                    case PlaceOperator.Size:
                        var size = command.ReadAs<SizeCommand>();
                        var color = PugDatabase.GetObjectInfo(entity.ObjectID, entity.Variation).mapColor;
                        for (int x = 0; x < size.X; x++)
                        {
                            for (int y = 0; y < size.Y; y++)
                            {
                                map[new(pos.x + x, pos.y + y, 0)] = color;
                            }
                        }
                        break;
                }

            }
            foreach (var (p, tile) in tiles)
            {
                float3 key = new(p.x, p.y, 0);
                if (!map.ContainsKey(key))
                {
                    tileColors.TryGetValue(tile, out var color);
                    map[key] = color;
                }
            }
            int i = 0;
            foreach (var (p, color) in map)
            {
                var slot = GetOrCreateSlot(i++);
                slot.color = color;
                slot.transform.localPosition = p / 8;
            }
            tiles.Clear();
            map.Clear();
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
