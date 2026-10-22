using System.Linq;
using Unity.Mathematics;

// 天顶剑的服务器逻辑。发射对照玩家挥砍时连续生成 FinalFractal，飞行对照 Projectile.AI_182_FinalFractal。
namespace Assets.TerraZenith.Scripts.Core
{
    public static class ZenithBalance
    {
        public const string WeaponName = "TerraZenith_Zenith";
        public const string SwordName = "TerraZenith_ZenithSword";
        public const float SearchRadiumSqrt = 36;    // 6格索敌半径
        public const int UpdateCount = 2;
        public const int TimeLeft = 1;
        public const int TrailCount = 4;
        public static ZenithSlashColorDataBlock[] Slashs;
        public static float2 GlobalOffset = new float2(0, 0f);

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
    }
}
