using PugMod;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.PointShop.Scripts
{
    public class ShopInfo : MonoBehaviour
    {
        private Dictionary<Zone, ShopData> shops;
        public void Init()
        {
            shops = new();
            Init(Zone.None, ObjectID.None, None());
            Init(Zone.Dirt, ObjectID.SlimeBoss, Dirt());
            Init(Zone.Clay, ObjectID.BossLarva, Clay());
            Init(Zone.LarvaHive, ObjectID.LarvaHiveBoss, LarvaHive());
            Init(Zone.Stone, ObjectID.ShamanBoss, Stone());
            Init(Zone.Nature, ObjectID.BirdBoss, Nature());
            Init(Zone.Mold, ObjectID.PoisonSlimeBoss, Mold());
            Init(Zone.Sea, ObjectID.OctopusBoss, Sea());
            Init(Zone.City, ObjectID.SlipperySlimeBoss, City());
            Init(Zone.Desert, ObjectID.ScarabBoss, Desert());
            Init(Zone.Lava, ObjectID.LavaSlimeBoss, Lava());
            Init(Zone.Oasis, ObjectID.GiantCicadaBoss, Oasis());
            Init(Zone.Crystal, ObjectID.HydraBossDesert, Crystal());
            Init(Zone.Alien, ObjectID.CoreBoss, Alien());
            Init(Zone.Passage, ObjectID.WallBoss, Passage());
            Init(Zone.Excavation, ObjectID.RobotBoss, Excavation());
        }
        public List<ShopItem> GetShop(Zone zone) => shops[zone].Items;
        public ObjectID GetBoss(Zone zone) => shops[zone].Boss;
        private void Init(Zone zone, ObjectID boss, List<ShopItem> items)
        {
            items.RemoveAll(x => x.Item.objectID == ObjectID.None);
            shops[zone] = new() { Boss = boss, Items = items };
        }
        private List<ShopItem> None()
        {
            return new()
            {
                new(ObjectID.AncientGemstone,10),
                new(ObjectID.MechanicalPart,10),
                (ObjectID.WayPoint, 100),
                ObjectID.PetBirdEgg,
                ObjectID.PetBunnyEgg,
                ObjectID.PetCatEgg,
                ObjectID.PetDogEgg,
                ObjectID.PetWarlockEgg,
                ObjectID.PetMothEgg,
                ObjectID.PetMagicEgg,
                new(ObjectID.Wool, 10),
                new(ObjectID.RolyPolyPlate, 10),
                (ObjectID.PetCandyEpic, 15),
                new("ToySoldierHelm", 50),
                new("ToySoldierBreastArmor", 50),
                new("ToySoldierPantsArmor", 50),
            };
        }
        private List<ShopItem> Dirt()
        {
            ObjectID chest = ObjectID.LockedCopperChest;
            return new()
            {
                (ObjectID.CopperOreBoulder, 50),
                (ObjectID.LockedCopperChest, 2),
                (ObjectID.KingSlimeSummoningItem, 5),
                (ObjectID.GlurchChest, 4),
                ObjectID.MeadowTree,
                ObjectID.PottedGoldenOrbBush,
                ObjectID.Stalagmite,
                (ObjectID.WoodenDestructible, 10),
                (ObjectID.Ocarina, 10),
                (ObjectID.ParsecPalsDolls, 10),
                (ObjectID.GiantMushroom2, 5),
                (ObjectID.ExplorerHelm, 20),
                new(ObjectID.FlintlockMusket, 10, chest),
                new(ObjectID.CopperSledge, 10, chest),
                new(ObjectID.DrillTool, 10, chest),
                (ObjectID.DiverPantsArmor, 5),
                new(ObjectID.RingOfStone, 10, chest),
                new(ObjectID.HeartBerryNecklace, 20, chest),
                ObjectID.SwiftFeather,
                ObjectID.CaveBag,
                (ObjectID.RangerHelm, 5),
                (ObjectID.RangerBreastArmor, 5),
                (ObjectID.RangerPantsArmor, 5),
                (ObjectID.ApprenticeHelm, 5),
                (ObjectID.ApprenticeBreastArmor, 5),
                (ObjectID.ApprenticePantsArmor, 5),
                (ObjectID.WitchDoctorHelm, 5),
                (ObjectID.WitchDoctorBreastArmor, 5),
                (ObjectID.WitchDoctorPantsArmor, 5),
                ObjectID.RuinsPiece1,
                ObjectID.RuinsPiece2,
                ObjectID.RuinsPiece3,
                ObjectID.RuinsPiece4,
            };
        }
        private List<ShopItem> Clay()
        {
            return new()
            {
                (ObjectID.TinOreBoulder, 60),
                (ObjectID.GhormChest, 8),
                ObjectID.PottedFlowerTree,
                (ObjectID.ColossCicada, 100),
                (ObjectID.ClayPot, 12),
                ObjectID.Kiln1,
                ObjectID.Kiln2,
                (ObjectID.AmmoniteNecklace, 10),
                (ObjectID.TinSledge, 5),
                (ObjectID.DiverNecklace, 10),
                (ObjectID.PotHelm, 10),
                (ObjectID.PotBreastArmor, 10),
                (ObjectID.ChieftainHelm, 10),
                (ObjectID.ChieftainBreastArmor, 10),
                (ObjectID.ChieftainPantsArmor, 10),
                new(ObjectID.CookingPotHelm, 5, ObjectID.GhormChest),
                new(ObjectID.CookingPotBreastArmor, 5, ObjectID.GhormChest),
                (ObjectID.ClayWormTrophy, 2000),
            };
        }
        private List<ShopItem> LarvaHive()
        {
            return new List<ShopItem>()
            {
                (ObjectID.InventoryLarvaHiveChest, 2),
                (ObjectID.HivemotherChest, 12),
                (ObjectID.HiveSpikeTrap, 2),
                (ObjectID.AmberLarva2, 10),
                (ObjectID.TomeOfMining, 10),
                (ObjectID.Lantern, 10),
                new(ObjectID.LarvaHelm, 5, ObjectID.HivemotherChest),
                new(ObjectID.LarvaBreastArmor, 5, ObjectID.HivemotherChest),
                (ObjectID.AcidLarvaTrophy, 2000),
            };
        }
        private List<ShopItem> Stone()
        {
            ObjectID bossChest = ObjectID.MalugazChest;
            ObjectID chest = ObjectID.LockedIronChest;
            return new()
            {
                (ObjectID.IronOreBoulder, 80),
                (ObjectID.GoldOreBoulder, 70),
                (chest, 3),
                (ObjectID.InventoryAncientChest, 2),
                (ObjectID.ShamanBossSummoningItem, 5),
                (bossChest, 15),
                (ObjectID.AFPortal, 100),
                ObjectID.StonePedestal,
                (ObjectID.AFVendingMachine, 10),
                (ObjectID.AFWaterCooler, 10),
                ObjectID.PottedLushTree,
                new(ObjectID.CavelingFloorTile, 10),
                new(ObjectID.CavelingFloorTileDark, 10),
                ObjectID.CavelingThrone,
                ObjectID.CavelingTable,
                ObjectID.CavelingChair,
                ObjectID.CavelingToilet,
                ObjectID.WaterWell,
                ObjectID.SignBrute,
                new ObjectData(){objectID = ObjectID.Stalagmite, amount = 1, variation = 1},
                ObjectID.AFWhiteBoard,
                (ObjectID.AFWoodenDestructible, 20),
                (ObjectID.AncientDestructible, 12),
                (ObjectID.AFScienceHelm, 30),
                (ObjectID.AFScienceBreastArmor, 30),
                (ObjectID.AFSciencePantsArmor, 30),
                (ObjectID.AFPipeClub, 50),
                (ObjectID.AFQuillRifle, 60),
                (ObjectID.ContributorItemDaresielSword, 100),
                new(ObjectID.ArcaneStaff, 11, bossChest),
                new(ObjectID.TomeOfFire, 11, bossChest),
                new(ObjectID.IronSledge, 10, chest),
                new(ObjectID.StoneSeeder, 10, chest),
                new(ObjectID.StoneMortar, 10, chest),
                (ObjectID.DiverRing, 10),
                new(ObjectID.RingOfRock, 10, chest),
                new(ObjectID.RiftLensOffhand, 5, bossChest),
                new(ObjectID.BlastHelm, 12, chest),
                new(ObjectID.BlastBreastArmor, 12, chest),
                new(ObjectID.BlastPantsArmor, 12, chest),
                (ObjectID.CavelingHelm, 12),
                (ObjectID.CavelingBreastArmor, 12),
                (ObjectID.CavelingPantsArmor, 12),
                new(ObjectID.SoaringHelm, 10, chest),
                new(ObjectID.SoaringBreastArmor, 10, chest),
                new(ObjectID.SoaringPantsArmor, 10, chest),
                new("JuggernautHelm", 13, chest),
                new("JuggernautBreastArmor", 13, chest),
                new("JuggernautPantsArmor", 13, chest),
            };
        }
        private List<ShopItem> Nature()
        {
            ObjectID bossChest = ObjectID.BirdBossChest;
            ObjectID chest = ObjectID.LockedScarletChest;
            return new()
            {
                (ObjectID.ScarletOreBoulder, 100),
                (chest, 6),
                (bossChest, 20),
                (ObjectID.LargeShinyGlimmeringObject, 8),
                (ObjectID.EasterTitanChest, 20),
                ObjectID.GraveTree,
                (ObjectID.BigJungleTree, 30),
                ObjectID.PottedLushBush,
                ObjectID.PottedGlowberryTree,
                ObjectID.PottedCherryTree,
                new(ObjectID.WovenMat, 10),
                new ObjectData(){objectID = ObjectID.Stalagmite, amount = 1, variation = 2},
                (ObjectID.NatureDestructible, 15),
                ObjectID.Bush,
                ObjectID.TallGrass,
                ObjectID.WaterKelp,
                ObjectID.WaterLily,
                (ObjectID.NatureWoodenDestructible, 15),
                (ObjectID.CavelingDoll, 10),
                (ObjectID.LegendarySwordGemstone, 40),
                (ObjectID.LegendarySwordBlade, 40),
                (ObjectID.FruitBasket, 25),
                (ObjectID.NatureCicadaSummoningItem, 5),
                (ObjectID.AncientGuardianNecklace, 40),
                (ObjectID.CavelingMothersRing, 40),
                new(ObjectID.TomeOfPoison, 8, bossChest),
                new(ObjectID.ScarletSledge, 10, chest),
                new(ObjectID.DrillToolScarlet, 10, chest),
                new(ObjectID.PetalRing, 10, chest),
                new(ObjectID.RemedaisyNecklace, 20, chest),
                (ObjectID.LargeSeedAndCropsPouch, 50),
                new(ObjectID.LargeValuablePouch, 8, bossChest),
                new(ObjectID.FarmerHelm, 10, chest),
                new("MuseHelm", 180),
                new("MuseBreastArmor", 180),
                new("MusePantsArmor", 180),
                new("WhispererHelm", 13, bossChest),
                new("WhispererBreastArmor", 13, bossChest),
                new("WildernessVinyl", 10, chest),
            };
        }
        private List<ShopItem> Mold()
        {
            ObjectID bossChest = ObjectID.IvyChest;
            return new()
            {
                (ObjectID.IvyChest, 18),
                ObjectID.MoldTree,
                ObjectID.PottedPuffSticks,
                (ObjectID.InventoryMoldDungeonChest, 2),
                (ObjectID.MoldDestructible, 15),
                (ObjectID.MoldCicadaWithoutSickle, 100),
                (ObjectID.Blowpipe, 10),
                (ObjectID.TomeOfMelee, 10),
                (ObjectID.DiverArmor, 20),
                new("MaidHelm", 13, bossChest),
                new("MaidBreastArmor", 13, bossChest),
                new("MaidPantsArmor", 13, bossChest),
                new("MikoHelm", 125),
                new("MikoBreastArmor", 125),
                new("MikoPantsArmor", 125),
                new("MoldVinyl", 100),
            };
        }
        private List<ShopItem> Sea()
        {
            ObjectID bossChest = ObjectID.OmorothBossChest;
            ObjectID chest = ObjectID.LockedOctarineChest;
            return new()
            {
                (ObjectID.OctarineOreBoulder, 110),
                (chest, 8),
                (ObjectID.BaitOctopusBoss, 15),
                (ObjectID.InventorySeaBiomeChest, 3),
                (bossChest, 20),
                ObjectID.PottedLandKelp,
                (ObjectID.SeaWoodenDestructible, 18),
                (ObjectID.JellyfishDestructable, 18),
                ObjectID.LandKelp,
                ObjectID.TallLandKelp,
                (ObjectID.ConchShellNecklace, 10),
                (ObjectID.SpineRing, 100),
                (ObjectID.OceanHeartNecklace, 10),
                (ObjectID.TurtleShell, 10),
                ObjectID.FishingNetRack,
                ObjectID.DriftwoodTable,
                ObjectID.DriftwoodStool,
                (ObjectID.DiverHelm, 20),
                (ObjectID.LargeFishPouch, 50),
                (ObjectID.ScholarHelm, 20),
                (ObjectID.ScholarArmor, 20),
                new(ObjectID.AnchorAxe, 5, bossChest),
                new(ObjectID.TomeOfOrbit, 15, chest),
                new(ObjectID.OctarineSledge, 10, chest),
                new(ObjectID.TopazRing, 20, chest),
                new(ObjectID.RustedNecklace, 20, chest),
                new(ObjectID.SorcererHelm, 10, chest),
                new(ObjectID.SorcererBreastArmor, 10, chest),
                new(ObjectID.SorcererPantsArmor, 10, chest),
                new(ObjectID.SunglassesHelm, 10, chest),
                new(ObjectID.TowelBreastArmor, 10, chest),
                new(ObjectID.BikiniBreastArmorBlue, 10, chest),
                new(ObjectID.BikiniPantsArmorBlue, 10, chest),
                new(ObjectID.SwimingPantsArmorBlue, 10, chest),
                new(ObjectID.BikiniBreastArmorRed, 10, chest),
                new(ObjectID.BikiniPantsArmorRed, 10, chest),
                new(ObjectID.SwimingPantsArmorRed, 10, chest),
                new(ObjectID.BikiniBreastArmorGreen, 10, chest),
                new(ObjectID.BikiniPantsArmorGreen, 10, chest),
                new(ObjectID.SwimingPantsArmorGreen, 10, chest),
                new("SeaVinyl", 10, chest),
            };
        }
        private List<ShopItem> City()
        {
            ObjectID bossChest = ObjectID.MorphaChest;
            return new()
            {
                (ObjectID.MorphaChest, 20),
                ObjectID.RuinsPedestal,
                (ObjectID.VendingMachine, 20),
                ObjectID.PlanterBox,
                ObjectID.CrystalLamp,
                ObjectID.RuinsStool,
                ObjectID.RuinsTableSmall,
                ObjectID.RuinsTable,
                ObjectID.RuinsShelfSmall,
                ObjectID.RuinsShelfLarge,
                ObjectID.RuinsFireplace,
                (ObjectID.CityDestructible, 20),
                new ObjectData(){objectID = ObjectID.Stalagmite, amount = 1, variation = 3},
                (ObjectID.LegendaryBowPart1, 50),
                (ObjectID.LegendaryBowPart2, 50),
                (ObjectID.LegendaryBowPart3, 50),
                (ObjectID.LegendaryBowParchment, 50),
                (ObjectID.CavelingID, 50),
                (ObjectID.GolemShield, 50),
                new("BuccaneerHelm", 11, bossChest),
                new("BuccaneerBreastArmor", 11, bossChest),
                new("BuccaneerPantsArmor", 11, bossChest),
                new("MetropolisVinyl", 100),
            };
        }
        private List<ShopItem> Desert()
        {
            ObjectID chest = ObjectID.LockedGalaxiteChest;
            return new()
            {
                (ObjectID.GalaxiteOreBoulder, 120),
                (chest, 10),
                (ObjectID.Thumper, 20),
                (ObjectID.InventoryDesertBiomeChest, 4),
                (ObjectID.UnlockedKingChest, 20),
                (ObjectID.UnlockedQueenChest, 20),
                (ObjectID.UnlockedPrinceChest, 20),
                ObjectID.PlumeTree,
                ObjectID.DesertRockSmall,
                ObjectID.DesertRock1,
                (ObjectID.DesertDestructible, 20),
                (ObjectID.GreenDesertDestructible, 20),
                ObjectID.TemplePillar,
                ObjectID.TempleThrone,
                ObjectID.TemplePedestal,
                (ObjectID.DesertTempleDestructible, 20),
                ObjectID.PottedPlumeTree,
                new ObjectData(){objectID = ObjectID.Stalagmite, amount = 1, variation = 4},
                (ObjectID.FrozenFlame, 10),
                (ObjectID.WhiteWhistle, 10),
                (ObjectID.CavelingProphetMask, 10),
                (ObjectID.BindingString, 50),
                (ObjectID.CrystalMeteorShard, 5),
                (ObjectID.GodsentHelm, 100),
                (ObjectID.GodsentBreastArmor, 100),
                (ObjectID.GodsentPantsArmor, 100),
                new(ObjectID.GalaxiteSledge, 10, chest),
                new(ObjectID.SleepyHelm, 20, chest),
                new(ObjectID.SleepyBreastArmor, 20, chest),
                new(ObjectID.SleepyPantsArmor, 20, chest),
                (ObjectID.ScarfHelm, 100),
                (ObjectID.BandanaHelm, 100),
                (ObjectID.PuppetRing, 60),
                (ObjectID.ConceiledBlade, 50),
                (ObjectID.AncientSpear, 50),
                (ObjectID.CavelingMummyTrophy, 3000),
                new("DesertVinyl", 10, chest)
            };
        }
        private List<ShopItem> Lava()
        {
            ObjectID bossChest = ObjectID.LavaSlimeBossChest;
            return new()
            {
                (ObjectID.InventoryLavaChest, 4),
                (bossChest, 20),
                ObjectID.PottedMagmaTulip,
                new(ObjectID.MetalGrateBridge, 10),
                (ObjectID.LavaWoodenDestructible, 20),
                new ObjectData(){objectID = ObjectID.Stalagmite, amount = 1, variation = 5},
                (ObjectID.LiquidMetal, 50),
                (ObjectID.LavaMortar, 80),
                new(ObjectID.LegendaryMiningPickParchment, 5, bossChest),
                new(ObjectID.LargeOreAndBlockPouch, 10, bossChest),
                (ObjectID.SmithingGlove, 50),
                (ObjectID.MinerHelm, 40),
                (ObjectID.MinerBreastArmor, 40),
                (ObjectID.MinerPantsArmor, 40),
                (ObjectID.BlackNecklace, 40),
                (ObjectID.BlackRing, 40),
                (ObjectID.GrimHelm, 40),
                (ObjectID.GrimBreastArmor, 40),
                (ObjectID.GrimPantsArmor, 40),
                (ObjectID.FlameNecklace, 40),
                (ObjectID.FlameRing, 40),
                new("ObsidianGreatsword", 13, bossChest),
                new("ScourgerHelm", 13, bossChest),
                new("ScourgerBreastArmor", 13, bossChest),
                new("ScourgerPantsArmor", 13, bossChest),
                new("LavaVinyl", 100),
            };
        }
        private List<ShopItem> Oasis()
        {
            return new()
            {
                (ObjectID.GiantCicadaChest, 20),
                (ObjectID.PassageChest, 10),
                ObjectID.OasisSucculent,
                ObjectID.OasisSucculentLarge,
                ObjectID.WaterReed,
                ObjectID.OasisStalagmite,
                ObjectID.GrimyStoneBookshelf,
                ObjectID.GrimyStoneBookshelfLarge,
                new(ObjectID.GrimyStoneFloor, 10),
                ObjectID.GrimyStoneTable,
                ObjectID.StoneLantern,
                (ObjectID.CultistColossCicada, 100),
                new(ObjectID.BlastingDung, 5),
                (ObjectID.GoldenBombScarabTrophy, 4000),
                (ObjectID.CicadaNymphTrophy, 4000),
            };
        }
        private List<ShopItem> Crystal()
        {
            ObjectID chest = ObjectID.LockedSolariteChest;
            return new()
            {
                (ObjectID.SolariteOreBoulder, 150),
                (chest, 20),
                (ObjectID.AtlantianWormChest, 20),
                (ObjectID.HydraBossNatureChest, 20),
                (ObjectID.HydraBossSeaChest, 20),
                (ObjectID.HydraBossDesertChest, 20),
                ObjectID.LucentOakTree,
                ObjectID.PottedLucentTree,
                (ObjectID.RadiationCrystal, 5),
                (ObjectID.CrystalDestructible, 20),
                ObjectID.SmallCrystalGrass,
                ObjectID.TallCrystalGrass,
                new ObjectData(){objectID = ObjectID.Stalagmite, amount = 1, variation = 6},
                (ObjectID.CrystalTent, 30),
                (ObjectID.CrystalCicada, 100),
                (ObjectID.AgarthaReport, 10),
                (ObjectID.TowerShellNecklace, 10),
                (ObjectID.SoulNecklace, 40),
                (ObjectID.ScholarBag, 50),
                new(ObjectID.TomeOfRadiation, 12, chest),
                new(ObjectID.LaserDrillTool, 40, chest),
                new(ObjectID.NinjaHelm, 10, chest),
                new(ObjectID.NinjaBreastArmor, 10, chest),
                new(ObjectID.NinjaPantsArmor, 10, chest),
                new(ObjectID.ArcaneMonkHelm, 10, chest),
                new(ObjectID.ArcaneMonkBreastArmor, 10, chest),
                new(ObjectID.ArcaneMonkPantsArmor, 10, chest),
                new("ArcaneSeerHel", 13, chest),
                new("ArcaneSeerBreastArmor", 13, chest),
            };
        }
        private List<ShopItem> Alien()
        {
            ObjectID bossChest = ObjectID.CoreCommanderChest;
            ObjectID chest = ObjectID.AlienChest;
            return new()
            {
                ObjectID.PottedAlienFlower,
                (chest, 10),
                (bossChest, 20),
                (ObjectID.EnemySpawnerPlatform, 10),
                new(ObjectID.GroundAlienBlock, 10),
                new(ObjectID.AlienFloorVent, 10),
                (ObjectID.AlienTechDestructible, 20),
                new ObjectData(){objectID = ObjectID.Stalagmite, amount = 1, variation = 7},
                (ObjectID.NatureGemstone, 15),
                (ObjectID.SeaGemstone, 15),
                (ObjectID.DesertGemstone, 15),
                new(ObjectID.LaserDrillTool, 10, chest),
                new(ObjectID.CrystalRing, 7, chest),
                new(ObjectID.CrystalNecklace, 7, chest),
                new(ObjectID.AlienTechHelm, 7, chest),
                new(ObjectID.AlienTechBreastArmor, 7, chest),
                new(ObjectID.AlienTechPantsArmor, 7, chest),
                new(new ObjectData(){ objectID= API.Authoring.GetObjectID("CoreEnhance_BoulderDemolish"),amount = 3 }, 70),
                new("MilitaryTranceiver", 4, bossChest)
            };
        }
        private List<ShopItem> Passage()
        {
            ObjectID bossChest = ObjectID.WallBossChest;
            return new()
            {
                (ObjectID.PandoriumOreBoulder, 160),
                (ObjectID.PassageCraftingStatue, 100),
                ObjectID.SulfurTree,
                new ObjectData(){objectID = ObjectID.Stalagmite, amount = 1, variation = 8},
                (ObjectID.PassageDestructible, 20),
                new("ChronolithBeacon", 4, bossChest)
            };
        }
        private List<ShopItem> Excavation()
        {
            ObjectID bossChest = ObjectID.RobotBossChest;
            return new()
            {
                (ObjectID.ReluciteOreBoulder, 180),
                (ObjectID.LockedReluciteChest, 20),
                (ObjectID.InventoryExcavationBiomeChest, 10),
                (ObjectID.LegendaryMortarPart1, 200),
                (ObjectID.LegendaryMortarPart2, 200),
                (ObjectID.ExcavationDestructibleSteelBox, 25),
                (ObjectID.ExcavationDestructibleReinforcedBox, 25),
                ObjectID.MetalPallet,
                ObjectID.MetalBarricade,
                (ObjectID.MetalBarrel, 5),
                ObjectID.WallThermostat,
                ObjectID.WallGauge,
                ObjectID.WallMetalSheet,
                ObjectID.DeadTree,
                ObjectID.CorruptedAlloy,
                ObjectID.HydraBossVoidCraftingItem,
                new(ObjectID.GlowingMushroom, 10),
                (ObjectID.VoidLarvaCocoonTrophy, 5000),
                new("BlackMikoHelm", 125),
                new("BlackMikoBreastArmor", 125),
                new("BlackMikoPantsArmor", 125),
                new("AdvancedExtractionLog", 5, bossChest)
            };
        }
    }
}