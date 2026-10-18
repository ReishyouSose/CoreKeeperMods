using Unity.Mathematics;

// 天顶剑的服务器逻辑。发射对照玩家挥砍时连续生成 FinalFractal，飞行对照 Projectile.AI_182_FinalFractal。
namespace Assets.TerraZenith.Scripts.Core
{
    public static class ZenithBalance
    {
        public const string WeaponName = "TerraZenith_Zenith";
        public const string SwordName = "TerraZenith_ZenithSword";
        public const int SwordTypes = 21;       // FinalFractalHelper 的 21 套颜色和剑长
        public const float HomeRadius = 25f;    // 400 像素 / 16，第 2、3 把剑的索敌半径
        public const float MinAimDistance = 4f; // 准星贴在身上时给一个最短瞄准，避免零向量
        // 现在一次挥砍只出一把，预测生成时剑序号固定为 0。
        // 改成大于 1 后，每发弹幕在生成时从玩家的随机序列里取一个 1..20 的序号。
        public const int ShotsPerSwing = 1;

        // 塞进弹幕 variation：低 8 位剑序号，接着 8 位相位，再 1 位左右。
        public static int PackVariation(int index, int phase, int side)
        {
            return index | (phase << 8) | (side << 16);
        }

        public static void UnpackVariation(int variation, out int index, out float phase, out float side)
        {
            index = variation & 255;
            phase = ((variation >> 8) & 255) * (math.PI * 2f / 255f); // 0..255 还原成 0..2π
            side = ((variation >> 16) & 1) == 0 ? -1f : 1f;
        }

        public static float2 ClampAim(float2 player, float2 mouse)
        {
            float2 delta = mouse - player;
            if (math.lengthsq(delta) < MinAimDistance * MinAimDistance)
                delta = math.normalizesafe(delta, new float2(1f, 0f)) * MinAimDistance;
            return player + delta;
        }
    }
}
