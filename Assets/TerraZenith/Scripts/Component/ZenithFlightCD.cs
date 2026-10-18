using Unity.Entities;
using Unity.Mathematics;

// 天顶剑的服务器逻辑。发射对照玩家挥砍时连续生成 FinalFractal，飞行对照 Projectile.AI_182_FinalFractal。
namespace Assets.TerraZenith.Scripts.Component
{
    // 轨道参数。预测生成时两端用同一套输入各写一份，之后各自积分。
    // 不做成幽灵字段。快照再把 age、速度写回来，刀光就会被拉成一跳。
    public struct ZenithFlightCD : IComponentData
    {
        public float2 velocity;    // 发射时瞄准向量的一半，之后不再改
        public float ellipse;      // ai[0]，椭圆的另一条半径，可正可负
        public int variation;      // 剑序号、相位、左右。预测生成时用玩家的随机序列算出
        public byte initialized;   // 0 表示这发还没有飞行参数
        public float age;          // localAI[0]，到 120 销毁。两端各自累加
        public float2 trailPlayer; // 上一拍玩家的地面坐标，用来给旧刀光加 TrailingMode 4 的位移
        public byte hasTrailPlayer;
    }
}
