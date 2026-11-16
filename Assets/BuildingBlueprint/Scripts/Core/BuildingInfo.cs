using Assets.BuildingBlueprint.Scripts.Components;
using Assets.BuildingBlueprint.Scripts.Enums;
using Assets.BuildingBlueprint.Scripts.Systems;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        public Version Version { get; } = new(2, 0);
        [JsonIgnore]
        public int ID;
        public string Name;
        public string Description;
        [JsonIgnore]
        public string Header;
        public List<PlaceCommand> Commands;
        public int2 Size;
        private List<MaterialInfo> materials;
        private List<int> variations;
        private float lastCountTime = float.NegativeInfinity;
        private const float RecountSeconds = 1f;
        public class Converter : JsonConverter<BuildingInfo>
        {
            [Serializable]
            internal struct LegacySerializeEntities
            {
                public HashSet<SelectedEntity> Entities;
                public int2 Position;
                [Serializable]
                public struct SelectedEntity
                {
                    public ObjectID ObjectID;
                    public int X;
                    public int Y;
                    public int Variation;
                    public int2 Direction;
                    public PaintableColor Color;
                }
            }
            public override bool CanWrite => false;   // 写走默认逻辑，JSON 里会正常出现 Version

            public override BuildingInfo ReadJson(JsonReader reader, Type objectType,
                BuildingInfo existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var jo = JObject.Load(reader);
                var info = new BuildingInfo
                {
                    Name = jo[nameof(Name)]?.ToString(),
                    Description = jo[nameof(Description)]?.ToString(),
                    Size = jo[nameof(Size)]?.ToObject<int2>(serializer) ?? default
                };

                // 判断 JSON 里有没有 Version：靠原始 token，不靠属性
                bool hasVersion = jo.TryGetValue(nameof(Version), out _);
                if (hasVersion)
                {
                    // 新存档：只读 Commands，旧字段忽略
                    info.Commands = jo[nameof(Commands)]?.ToObject<List<PlaceCommand>>(serializer);
                }
                else
                {
                    // 旧存档：读旧字段，转成 Commands
                    var result = new List<IPlaceCommand>();
                    var entityInfos = jo["EntityInfos"]?.ToObject<List<LegacySerializeEntities>>(serializer);
                    var tileInfos = jo["TileInfos"]?.ToObject<List<SerializeTiles>>(serializer);

                    // Tile：先构造 TileCommand，按 GetPlaceOrder 排序，再 WriteRPC 分配 Index
                    var tiles = new List<TileCommand>();
                    foreach (var tileInfo in tileInfos)
                    {
                        var pos = tileInfo.Position;
                        foreach (var tile in tileInfo.Tiles)
                        {
                            tiles.Add(new TileCommand
                            {
                                TileCD = new TileCD { tileset = tile.tileset, tileType = tile.tileType },
                                Pos = pos,
                            });
                        }
                    }
                    tiles.Sort((a, b) => MiscHelper.GetPlaceOrder(a.TileCD.tileType).CompareTo(MiscHelper.GetPlaceOrder(b.TileCD.tileType)));

                    foreach (var payload in tiles)
                    {
                        result.Add(payload);
                    }

                    // Entity + Ex
                    foreach (var entityInfo in entityInfos)
                    {
                        var pos = entityInfo.Position;
                        foreach (var entity in entityInfo.Entities)
                        {
                            var entityPayload = new EntityCommand
                            {
                                ObjectID = entity.ObjectID,
                                Variation = entity.Variation,
                                Pos = pos,
                            };
                            result.Add(entityPayload);
                            result.Add(new SizeCommand() { X = entity.X, Y = entity.Y });
                            if (!entity.Direction.Equals(int2.zero))
                            {
                                var dirPayload = new DirectionCommand { Direction = entity.Direction };
                                result.Add(dirPayload);
                            }
                            if (entity.Color != PaintableColor.Unpainted)
                            {
                                var paintPayload = new PaintCommand { Color = entity.Color };
                                result.Add(paintPayload);
                            }
                        }
                    }
                    info.Commands = result.ConvertAll(x => x.Write());
                }

                return info;
            }

            public override void WriteJson(JsonWriter writer, BuildingInfo value, JsonSerializer serializer)
                => throw new NotImplementedException();   // CanWrite=false，不会被调用
        }

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
                bool zero;
                ObjectDataCD obj;
                foreach (var command in Commands)
                {
                    switch (command.Op)
                    {
                        case PlaceOperator.Tile:
                            var tile = command.ReadAs<TileCommand>();
                            var tileObj = ins.TileToObject(tile.TileCD);
                            //bool zero = ins.AlwaysDropZero(tileObj.objectID, tileObj.variation);
                            obj = new()
                            {
                                objectID = tileObj.objectID,
                                variation = tileObj.variation,
                            };
                            requires.TryGetValue(obj, out int value);
                            requires[obj] = ++value;
                            break;
                        case PlaceOperator.Entity:
                            var entity = command.ReadAs<EntityCommand>();
                            zero = ins.AlwaysDropZero(entity.ObjectID, entity.Variation);
                            obj = new()
                            {
                                objectID = entity.ObjectID,
                                variation = zero ? 0 : entity.Variation,
                            };
                            requires.TryGetValue(obj, out value);
                            requires[obj] = ++value;
                            break;
                        case PlaceOperator.Container:
                            var container = command.ReadAs<ContainerCommand>();
                            zero = ins.AlwaysDropZero(container.ObjectID, container.Variation);
                            obj = new()
                            {
                                objectID = container.ObjectID,
                                variation = zero ? 0 : container.Variation,
                            };
                            requires.TryGetValue(obj, out value);
                            requires[obj] = ++value;
                            break;
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
            if (now - lastCountTime >= RecountSeconds)
            {
                lastCountTime = now;
                var client = BuildingPlaceClient.Ins;
                for (int i = 0; i < materials.Count; i++)
                {
                    var material = materials[i];
                    /* int available = material.objectID == ObjectID.WoodHoe
                         ? (client.HasAnyHoe(player.entity) ? 1 : 0)
                         : client.GetExistObjectAmount(player.entity, material.objectID, this.variations[i]);*/
                    int available = client.GetExistObjectAmount(player.entity, material.objectID, this.variations[i]);
                    materials[i].amountAvailable = available;
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
            if (sortedAt == lastCountTime && sortedRows == rows)
            {
                variations = shownVariations;
                return shown;
            }
            sortedAt = lastCountTime;
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
