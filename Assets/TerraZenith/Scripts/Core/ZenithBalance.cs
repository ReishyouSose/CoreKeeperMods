using Assets.TerraZenith.Scripts.Component;
using Pug.UnityExtensions;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Assets.TerraZenith.Scripts.Core
{
    public static class ZenithBalance
    {
        public const string WeaponName = "TerraZenith_Zenith";
        public const string SwordName = "TerraZenith_ZenithSword";
        public const string RecipeName = "TerraZenith_ZenithRecipe";

        // 泰拉像素 -> 本游戏世界单位
        // = (1/16 px->格) * (30/120 屏幕比例) * (1 格->世界单位)
        public const float Terra_PxFix = 1f / 16f
                                       * (30f / 120f)
                                       * 1f;

        // 索敌半径：400px
        public const float SearchRadiumSqrt = 400 * 400 * Terra_PxFix * Terra_PxFix;
        // 抖动半径：150px
        public const float JitterRadius = 150 * Terra_PxFix;
        // 原版 NearFix 的距离参考：900px（但是是全轴，这里折半对应半长轴）
        public const float NearDistance = 900f / 2 * Terra_PxFix;
        // 原版长轴后半段额外扩张：40px
        public const float LongAxisGrowth = 40 * Terra_PxFix;
        public const float MinLongAxis = 60 * Terra_PxFix;   // 60px 换算
        public const int ShortAxisRandom = 100;
        public const int TrailCount = 4;

        public static ZenithSlashColorDataBlock[] Slashs;

        public static float2 Rotate(float2 value, float angle)
        {
            math.sincos(angle, out float s, out float c);
            return new float2(c * value.x - s * value.y, s * value.x + c * value.y);
        }

        public static void LoadSlash()
        {
            if (!ScriptableData.TryGetDataBlocks<ZenithSlashColorDataBlock>(out var dataBlocks))
                return;
            Slashs = dataBlocks.ToArray();
        }

        // 原版：GetLerpValue(900f, 0f, velocity.Length() * 2f)
        // velocity.Length() = dist / 2 = longAxis，所以参考值取 900px 的一半 = 450px
        public static float NearFix(float longAxis)
        {
            float near = math.clamp(longAxis / NearDistance, 0f, 1f);   // 0 近，1 远
            return math.lerp(2f, 0.7f, near);                            // 近 2，远 0.7
        }

        public static void CalculateOrbit(ZenithOrbitCD orbit, float progress, float killTime, int updateCount,
            out float lifeRatio, out float ellipse, out float2 basePos, out float2 relativePos)
        {
            float total = killTime * updateCount;
            lifeRatio = progress / total;

            // 前 50% 转完一整圈
            ellipse = math.clamp(lifeRatio / 0.5f, 0f, 1f);

            // 旋转角。原版从 PI 起步，这里从 0 起步；
            // 发射端 orbit.Rotation = rot + PI，两者抵消，净角度与原版一致（右挥）
            float x = ellipse * math.PI2;

            // 原版：longAxis = velocity.Length() + GetLerpValue(0.5,1,lerp) * 40
            float longAxisNow = orbit.LongAxis + math.clamp((ellipse - 0.5f) / 0.5f, 0f, 1f) * LongAxisGrowth;

            // 原版：shortAxis = ai[0] * Lerp(2, 1, lerp)
            float shortAxisNow = orbit.ShortAxis * math.lerp(2f, 1f, ellipse);

            math.sincos(x, out var sin, out var cos);

            // 椭圆基础点
            basePos = new(cos, (shortAxisNow > 0 ? 1 : -1) * sin);
            float2 localPos = basePos * new float2(longAxisNow, math.abs(shortAxisNow));

            // 起手拉扯力（原版前 25% 寿命）
            // 原版：vector3 = (1 - GetLerpValue(0,0.5,lerpValue2))
            //                * new Vector2(sign(velocity.X)*(-num8)*0.1, (-ai[0])*0.3)
            // sign(velocity.X) = sign(Offset.x)（Offset = velocity = vector42/2，方向同 vector42）
            // num8 = longAxisNow（当前长轴），ai[0] = orbit.ShortAxis（原始短轴，不松弛）
            float leadEnvelope = 1f - math.clamp(lifeRatio / 0.25f, 0f, 1f);
            float dirXSign = orbit.Offset.x > 0f ? 1f : -1f;
            float2 leadOffset = leadEnvelope * new float2(dirXSign * -longAxisNow * 0.1f, -orbit.ShortAxis * 0.3f);

            // 拉扯力不参与旋转
            float2 rotatedOrbit = Rotate(localPos, orbit.Rotation);
            relativePos = rotatedOrbit + leadOffset;
        }

        /// <summary>
        /// 在候选敌人里索敌。不依赖任何 Boss 专用组件。
        /// findNearest = true  -> 在 maxDistanceSq 范围内找最近的目标；
        /// findNearest = false -> 在 maxDistanceSq 范围内找到第一个合法目标就立即返回。
        /// maxDistanceSq <= 0  -> 不做距离限制（视为无限范围）。
        /// </summary>
        public static bool FindTarget(NativeArray<Entity> enemies, float2 center, float maxDistanceSq, bool findNearest,
            ComponentLookup<HealthCD> healthLookup, ComponentLookup<LocalTransform> transformLookup,
            ComponentLookup<DisablePhysicsCD> disablePhysicsLookup, ComponentLookup<ImmuneToDamageCD> immuneLookup,
            BufferLookup<SummarizedConditionEffectsBuffer> summarizedConditionsLookup,
            out Entity target, out float2 targetPos, out float targetDisSq)
        {
            target = Entity.Null;
            targetPos = float2.zero;
            targetDisSq = float.MaxValue;

            bool hasRangeLimit = maxDistanceSq > 0f;
            float bestDisSq = hasRangeLimit ? maxDistanceSq : float.MaxValue;

            foreach (Entity e in enemies)
            {
                if (e == Entity.Null)
                    continue;

                if (!IsTargetable(e, healthLookup, transformLookup, disablePhysicsLookup, immuneLookup,
                                  summarizedConditionsLookup, out float2 pos))
                    continue;

                float dis = math.lengthsq(pos - center);
                if (hasRangeLimit && dis > maxDistanceSq)
                    continue;

                if (!findNearest)
                {
                    target = e;
                    targetPos = pos;
                    targetDisSq = dis;
                    return true;
                }

                if (dis >= bestDisSq)
                    continue;

                bestDisSq = dis;
                target = e;
                targetPos = pos;
            }

            targetDisSq = bestDisSq;
            return target != Entity.Null;
        }

        /// <summary>
        /// 通用"可受击/可索敌"判定。不依赖任何 Boss 专用组件。
        /// 判定顺序：存活 -> 位置 -> 物理开启 -> 免疫允许 -> 护甲已破。
        /// 注意：不排除 mainEntity == self 的主体（否则 SpiderRobot 腿全坏后本体打不到，Boss 打不死）。
        /// </summary>
        public static bool IsTargetable(Entity entity, ComponentLookup<HealthCD> healthLookup,
            ComponentLookup<LocalTransform> transformLookup, ComponentLookup<DisablePhysicsCD> disablePhysicsLookup,
            ComponentLookup<ImmuneToDamageCD> immuneLookup,
            BufferLookup<SummarizedConditionEffectsBuffer> summarizedConditionsLookup, out float2 position)
        {
            position = float2.zero;

            if (!healthLookup.TryGetComponent(entity, out var health) || health.health <= 0)
                return false;

            if (!transformLookup.TryGetComponent(entity, out var trans))
                return false;
            position = trans.Position.ToFloat2();

            // 物理关闭 = 打不到
            if (disablePhysicsLookup.IsComponentEnabled(entity))
                return false;

            // 免疫 = 不打（无组件视为可打）
            if (immuneLookup.TryGetComponent(entity, out var immune)
                && immune.Value == ImmuneToDamageState.Immune)
                return false;

            // 护甲未破 = 不打（槽位 98 是 ProtectiveArmor）
            if (summarizedConditionsLookup.TryGetBuffer(entity, out var conds)
                && conds.Length > 98
                && conds[98].value > 0)
                return false;

            return true;
        }

        /// <summary>
        /// 圆内均匀随机向量（等价泰拉 Main.rand.NextVector2Circular）。
        /// sqrt 保证面积均匀，而不是半径均匀。
        /// </summary>
        public static float2 NextVector2Circular(ref Unity.Mathematics.Random rng, float radius)
        {
            if (radius <= 0f)
                return float2.zero;

            float angle = rng.NextFloat(0f, math.PI2);
            float r = math.sqrt(rng.NextFloat()) * radius;
            return new float2(math.cos(angle), math.sin(angle)) * r;
        }
    }
}