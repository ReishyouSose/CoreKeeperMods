using Unity.Collections;
using Unity.Entities;

namespace Assets.PointShop.Scripts.Core
{
    public struct PointsGrantedCD : IComponentData
    {
    }

    public static class PointGain
    {
        public const uint ExplorePoint = 1;
        public const uint EnemyPoint = 1;
        public const uint ChestPoint = 1;
        public const uint BossPoint = 1;

        public static void Add(DynamicBuffer<PointCurrencyBuffer> buffer, FixedString64Bytes zone, ObtainWay way, uint amount)
        {
            if (amount == 0 || zone.Length == 0)
                return;
            for (int i = 0; i < buffer.Length; i++)
            {
                var slot = buffer[i];
                if (slot.Zone != zone)
                    continue;
                switch (way)
                {
                    case ObtainWay.Explore:
                        slot.Explore += amount;
                        break;
                    case ObtainWay.Chest:
                        slot.Chest += amount;
                        break;
                    case ObtainWay.Enemy:
                        slot.Enemy += amount;
                        break;
                    case ObtainWay.Boss:
                        slot.Boss += amount;
                        break;
                }
                buffer[i] = slot;
                return;
            }
        }

        public static uint BossAmount(ObjectID id)
        {
            switch (id)
            {
                case ObjectID.SlimeBoss:
                    return 1;
                case ObjectID.BossLarva:
                    return 2;
                case ObjectID.KingSlime:
                    return 3;
                case ObjectID.LarvaHiveBoss:
                    return 3;
                case ObjectID.ShamanBoss:
                    return 4;
                case ObjectID.BirdBoss:
                    return 10;
                case ObjectID.PoisonSlimeBoss:
                    return 8;
                case ObjectID.OctopusBoss:
                    return 20;
                case ObjectID.SlipperySlimeBoss:
                    return 12;
                case ObjectID.ScarabBoss:
                    return 30;
                case ObjectID.LavaSlimeBoss:
                    return 40;
                case ObjectID.SnakeBossSegment:
                    return 50;
                case ObjectID.HydraBossNature:
                    return 40;
                case ObjectID.HydraBossSea:
                    return 60;
                case ObjectID.HydraBossDesert:
                    return 80;
                case ObjectID.CoreBoss:
                    return 100;
                case ObjectID.GiantCicadaBoss:
                    return 90;
                case ObjectID.WallBoss:
                    return 200;
                case ObjectID.RobotBoss:
                    return 120;
                case ObjectID.HydraBossVoid:
                    return 150;
                default:
                    return 1;
            }
        }
    }
}
