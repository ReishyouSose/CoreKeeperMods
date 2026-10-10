using Unity.Collections;
using Unity.NetCode;

namespace Assets.PointShop.Scripts.Core
{
    public struct PointGainReportRPC : IRpcCommand
    {
        public ObtainWay Way;
        public FixedString64Bytes Zone;
    }
}
