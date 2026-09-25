using System;
using UnityEngine;

namespace Assets.PointShop.Scripts
{
    public class ShopZoneDataBlock : ScriptableDataBlock
    {
        public ObjectID Boss;
        public ObjectID EnvironmentChest;
        public ObjectID BossChest;
        public Sprite Icon;

        [NonSerialized]
        public int ZoneID;
    }
}
