using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Assets.BuildingBlueprint.Scripts.Core
{
    [Serializable]
    public struct SerializeEntities
    {
        public HashSet<SelectedEntity> Entities;
        public int2 Position;
    }
}
