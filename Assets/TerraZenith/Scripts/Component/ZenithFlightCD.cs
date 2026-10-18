using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 天顶剑的服务器逻辑。发射对照玩家挥砍时连续生成 FinalFractal，飞行对照 Projectile.AI_182_FinalFractal。
namespace Assets.TerraZenith.Scripts.Component
{
    [GhostComponent]
    // 轨道参数。预制件上先放一个空组件，服务器在第一次看到 initialized==0 时写满，幽灵快照只带这一次。
    // age 和玩家位移不进快照，两端用同一组参数各自往下积分，刀光缓冲才能在本地采样。
    public struct ZenithFlightCD : IComponentData
    {
        [GhostField]
        public float2 velocity;    // 发射时瞄准向量的一半，之后不再改
        [GhostField]
        public float ellipse;      // ai[0]，椭圆的另一条半径，可正可负
        [GhostField]
        public int variation;      // 剑序号、相位、左右。原发射系统删了，改在这一次登记里算
        [GhostField]
        public byte initialized;   // 0 表示服务器还没写过参数
        [GhostField]
        public float age;          // localAI[0]，到 120 销毁。只由服务器累加，客户端靠它把漏掉的弧补进本地轨迹
        public float2 trailPlayer; // 上一拍玩家的地面坐标，用来给旧刀光加 TrailingMode 4 的位移
        public byte hasTrailPlayer;
    }
}
