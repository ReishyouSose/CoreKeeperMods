using Assets.TerraZenith.Scripts.Component;
using System.Linq;
using Unity.Mathematics;

// 天顶剑的服务器逻辑。发射对照玩家挥砍时连续生成 FinalFractal，飞行对照 Projectile.AI_182_FinalFractal。
namespace Assets.TerraZenith.Scripts.Core
{
    public static class ZenithBalance
    {
        public const string WeaponName = "TerraZenith_Zenith";
        public const string SwordName = "TerraZenith_ZenithSword";
        public const string RecipeName = "TerraZenith_ZenithRecipe";
        public const float SearchRadiumSqrt = 36;    // 6格索敌半径
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
        public static float NearFix(float longAxis)
        {
            float near = math.clamp(longAxis / 7.5f, 0, 1);
            return math.lerp(1.5f, 0.7f, near);
        }
        public static void CalculateOrbit(ZenithOrbitCD orbit, float progress, float killTime, int updateCount, out float lifeRatio, out float ellipse, out float2 basePos, out float2 relativePos)
        {
            float total = killTime * updateCount;
            // 1. 当前在整个生命周期中的比例 (0 到 1)
            lifeRatio = progress / total;

            // 2. 旋转周期占比：原版前 60 帧转完，总 120 帧，所以前 50% 转完
            // 这里 total * 0.5f 就是你的 1 秒
            ellipse = math.clamp(lifeRatio / 0.5f, 0f, 1f);

            // 4. 旋转角度 (原版从 PI 开始)
            float x = /*math.PI +*/ ellipse * (math.PI * 2f);

            var longAxis = orbit.LongAxis;
            var shortAxis = orbit.ShortAxis;
            math.sincos(x, out var sin, out var cos);

            // 5. 椭圆基础点 (对应原版 spinningpoint)
            basePos = new(cos, (shortAxis > 0 ? 1 : -1) * sin);
            float2 localPos = basePos * new float2(longAxis, math.abs(shortAxis));

            // 6. 起手拉扯力：对应原版前 0.5 的 lerpValue2，在这里就是前 0.25f 的 currentRatio
            float leadEnvelope = 1f - math.clamp(lifeRatio / 0.25f, 0f, 1f);
            float2 leadOffset = leadEnvelope * new float2(-longAxis * 0.1f, -shortAxis * 0.3f);

            // 7. 计算世界位置（拉扯力不参与旋转）
            float2 rotatedOrbit = Rotate(localPos, orbit.Rotation);
            relativePos = rotatedOrbit + leadOffset;
        }
    }
}
