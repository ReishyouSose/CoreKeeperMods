using Pug.UnityExtensions;
using PugTilemap;
using Unity.Collections;
using Unity.Mathematics;

namespace Assets.PointShop.Scripts.Core
{
    public class ZoneLookup
    {

        #region FixedString64Bytes
        public static readonly FixedString64Bytes ByTileLookup = "TileLookup";
        public static readonly FixedString64Bytes Dirt = "Dirt";
        public static readonly FixedString64Bytes Meadow = "Meadow";
        public static readonly FixedString64Bytes Clay = "Clay";
        public static readonly FixedString64Bytes LarvaHive = "LarvaHive";
        public static readonly FixedString64Bytes Stone = "Stone";
        public static readonly FixedString64Bytes Moss = "Moss";
        public static readonly FixedString64Bytes Nature = "Nature";
        public static readonly FixedString64Bytes Mold = "Mold";
        public static readonly FixedString64Bytes Sea = "Sea";
        public static readonly FixedString64Bytes City = "City";
        public static readonly FixedString64Bytes Desert = "Desert";
        public static readonly FixedString64Bytes Lava = "Lava";
        public static readonly FixedString64Bytes Crystal = "Crystal";
        public static readonly FixedString64Bytes Alien = "Alien";
        public static readonly FixedString64Bytes Oasis = "Oasis";
        public static readonly FixedString64Bytes Passage = "Passage";
        public static readonly FixedString64Bytes Excavation = "Excavation";
        public static readonly FixedString64Bytes Void = "Void";
        #endregion

        public static bool TryResolveGround(TileAccessor tileLookup, float3 position, out FixedString64Bytes zone)
        {
            if (!tileLookup.GetType(position.RoundToInt2(), TileType.ground, out TileCD tile))
            {
                zone = default;
                return false;
            }
            return TryFromTileset((Tileset)tile.tileset, out zone);
        }

        public static bool TryFromTileset(Tileset tileset, out FixedString64Bytes zone)
        {
            switch (tileset)
            {
                case Tileset.Dirt:
                case Tileset.Sand:
                case Tileset.Explosive:
                    zone = Dirt;
                    return true;
                case Tileset.Stone:
                case Tileset.DarkStone:
                case Tileset.GrimyStone:
                    zone = Stone;
                    return true;
                case Tileset.LarvaHive:
                    zone = LarvaHive;
                    return true;
                case Tileset.Nature:
                case Tileset.NaturalExplosive:
                case Tileset.ExplosiveForest:
                    zone = Nature;
                    return true;
                case Tileset.Mold:
                    zone = Mold;
                    return true;
                case Tileset.Sea:
                    zone = Sea;
                    return true;
                case Tileset.Clay:
                    zone = Clay;
                    return true;
                case Tileset.Desert:
                case Tileset.DesertTemple:
                case Tileset.DesertTemple2:
                case Tileset.ExplosiveDesert:
                    zone = Desert;
                    return true;
                case Tileset.City:
                    zone = City;
                    return true;
                case Tileset.Lava:
                    zone = Lava;
                    return true;
                case Tileset.Crystal:
                    zone = Crystal;
                    return true;
                case Tileset.Alien:
                    zone = Alien;
                    return true;
                case Tileset.Passage:
                    zone = Passage;
                    return true;
                case Tileset.Oasis:
                    zone = Oasis;
                    return true;
                case Tileset.Moss:
                    zone = Moss;
                    return true;
                case Tileset.Meadow:
                    zone = Meadow;
                    return true;
                case Tileset.Excavation:
                case Tileset.ExcavationDungeon:
                case Tileset.ExcavationRock:
                case Tileset.ExcavationBorder:
                    zone = Excavation;
                    return true;
                case Tileset.ExcavationVoid:
                    zone = Void;
                    return true;
                default:
                    zone = default;
                    return false;
            }
        }

        public static bool TryFromAreaLevel(AreaLevel level, out FixedString64Bytes zone)
        {
            switch (level)
            {
                case AreaLevel.Slime:
                    zone = Dirt;
                    return true;
                case AreaLevel.StartArea:
                case AreaLevel.Obsidian:
                    zone = string.Empty;
                    return false;
                default:
                    zone = level.ToString();
                    return true;
            }
        }

        // 箱子、挖掘点和可破坏物。表写在预制件上，跨场景复用时也不换表。
        // 怪物表不在这里，击杀仍用物体或 AreaLevel 上的环境。
        public static bool TryFromLootTable(LootTableID id, out FixedString64Bytes zone)
        {
            switch (id)
            {
                case LootTableID.CopperChest:
                case LootTableID.WorldChest:
                case LootTableID.LargeSlimeDestructible:
                case LootTableID.WoodenDestructible:
                case LootTableID.LargeWoodenDestructible:
                case LootTableID.DiggingSpot:
                case LootTableID.SlimeBlobs:
                    zone = Dirt;
                    return true;
                case LootTableID.ClayPot:
                    zone = Clay;
                    return true;
                case LootTableID.IronChest:
                case LootTableID.FallRockDestructible:
                case LootTableID.CavelingDestructbile:
                case LootTableID.AncientDestructible:
                case LootTableID.LargeAncientDestructible:
                case LootTableID.AbioticFactorDestructible:
                case LootTableID.AbioticFactorLargeDestructible:
                    zone = Stone;
                    return true;
                case LootTableID.LarvaHiveDestructible:
                case LootTableID.HiveDungeon:
                    zone = LarvaHive;
                    return true;
                case LootTableID.ScarletChest:
                case LootTableID.WorldChestNature:
                case LootTableID.DiggingSpotNature:
                case LootTableID.NatureDestructible:
                case LootTableID.LargeNatureDestructable:
                case LootTableID.WoodenNatureDestructable:
                case LootTableID.LargeWoodenNatureDestructable:
                case LootTableID.PoisonSlimeBlobs:
                    zone = Nature;
                    return true;
                case LootTableID.OctarineChest:
                case LootTableID.SeaBiomeChest:
                case LootTableID.DiggingSpotSea:
                case LootTableID.JellyfishDestructible:
                case LootTableID.LargeJellyfishDestructible:
                    zone = Sea;
                    return true;
                case LootTableID.GalaxiteChest:
                case LootTableID.DesertDungeonChest:
                case LootTableID.DiggingSpotDesert:
                case LootTableID.DesertDestructible:
                case LootTableID.GreenDesertDestructible:
                case LootTableID.LargeDesertDestructible:
                case LootTableID.GreenLargeDesertDestructible:
                case LootTableID.DesertTempleDestructible:
                case LootTableID.LargeDesertTempleDestructible:
                    zone = Desert;
                    return true;
                case LootTableID.SolariteChest:
                case LootTableID.CrystalDestructible:
                case LootTableID.LargeCrystalDestructible:
                    zone = Crystal;
                    return true;
                case LootTableID.AlienChest:
                case LootTableID.AlienTechDestructible:
                case LootTableID.LargeAlienTechDestructible:
                    zone = Alien;
                    return true;
                case LootTableID.PassageChest:
                case LootTableID.PassageDestructible:
                case LootTableID.LargePassageDestructible:
                    zone = Passage;
                    return true;
                case LootTableID.CicadaDungeonChest:
                case LootTableID.OasisChest:
                    zone = Oasis;
                    return true;
                case LootTableID.MoldDungeonChest:
                case LootTableID.MoldDestructable:
                case LootTableID.LargeMoldDestructable:
                    zone = Mold;
                    return true;
                case LootTableID.CityDungeonChest:
                case LootTableID.CityDestructible:
                case LootTableID.LargeCityDestructible:
                    zone = City;
                    return true;
                case LootTableID.LavaDungeonChest:
                case LootTableID.DiggingSpotLava:
                case LootTableID.LavaWoodenDestructible:
                case LootTableID.LargeLavaWoodenDestructible:
                    zone = Lava;
                    return true;
                case LootTableID.ReluciteChest:
                case LootTableID.DiggingSpotExcavation:
                case LootTableID.ExcavationDestructible:
                case LootTableID.LargeExcavationDestructible:
                case LootTableID.FallExcavationRockDestructible:
                case LootTableID.BrokenRobotDestructibleSmall:
                case LootTableID.BrokenRobotDestructibleBig:
                    zone = Excavation;
                    return true;
                case LootTableID.MossDestructible:
                case LootTableID.MossDestructibleBig:
                    zone = Moss;
                    return true;
                default:
                    zone = default;
                    return false;
            }
        }

        public static bool TryFromObject(ObjectID id, out FixedString64Bytes zone)
        {
            switch (id)
            {
                // 能敲或能挖、并且有掉落表的，已经在前面按地面分了。
                case ObjectID.EerieChest:
                case ObjectID.GingerbreadChest:
                case ObjectID.InventoryAncientChest:
                case ObjectID.InventoryChest:
                case ObjectID.ValentineChest:
                    zone = ByTileLookup;
                    break;
                case ObjectID.AlienChest:
                    zone = Alien;
                    break;
                case ObjectID.SlipperySlimeBoss:
                    zone = City;
                    break;
                case ObjectID.SolariteChest:
                case ObjectID.FloatingTitanChest:
                case ObjectID.TitanChest:
                case ObjectID.HydraBossNature:
                case ObjectID.HydraBossSea:
                case ObjectID.HydraBossDesert:
                    zone = Crystal;
                    break;
                case ObjectID.GalaxiteChest:
                case ObjectID.InventoryDesertBiomeChest:
                case ObjectID.UnlockedKingChest:
                case ObjectID.UnlockedPrinceChest:
                case ObjectID.UnlockedQueenChest:
                    zone = Desert;
                    break;
                case ObjectID.CopperChest:
                    zone = Dirt;
                    break;
                case ObjectID.InventoryExcavationBiomeChest:
                case ObjectID.ReluciteChest:
                    zone = Excavation;
                    break;
                // 幼虫不按 AreaLevel 分，黏土变体也归虫巢。
                case ObjectID.Larva:
                case ObjectID.BigLarva:
                case ObjectID.AcidLarva:
                case ObjectID.InventoryLarvaHiveChest:
                    zone = LarvaHive;
                    break;
                case ObjectID.InventoryLavaChest:
                    zone = Lava;
                    break;
                case ObjectID.InventoryMoldDungeonChest:
                case ObjectID.PoisonSlimeBoss:
                    zone = Mold;
                    break;
                case ObjectID.ScarletChest:
                    zone = Nature;
                    break;
                case ObjectID.PassageChest:
                    zone = Passage;
                    break;
                case ObjectID.InventorySeaBiomeChest:
                case ObjectID.OctarineChest:
                    zone = Sea;
                    break;
                case ObjectID.IronChest:
                case ObjectID.AFPestElectric:
                    zone = Stone;
                    break;
                case ObjectID.HydraBossVoid:
                case ObjectID.VoidCaveling:
                case ObjectID.VoidCavelingBrute:
                case ObjectID.VoidCavelingShaman:
                case ObjectID.VoidLarva:
                case ObjectID.VoidLarvaCocoon:
                    zone = Void;
                    break;
                case ObjectID.GoldenBombScarab:
                case ObjectID.CicadaNymph:
                case ObjectID.GiantCicadaBoss:
                    zone = Oasis;
                    break;
                default:
                    zone = string.Empty;
                    return false;
            }
            return true;
        }
    }
}
