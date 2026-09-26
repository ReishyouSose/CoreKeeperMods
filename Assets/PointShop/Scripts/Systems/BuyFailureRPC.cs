using Unity.NetCode;

namespace Assets.PointShop.Scripts.Systems
{
    public struct BuyFailureRPC : IRpcCommand
    {
        public int Reason;
    }
}
