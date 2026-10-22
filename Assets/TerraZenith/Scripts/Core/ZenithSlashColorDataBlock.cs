using Unity.Mathematics;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.Core
{
    public class ZenithSlashColorDataBlock : ScriptableDataBlock
    {
        public DataBlockRef<EntityAuthoringDataBlock> AuthoringRef;
        public Sprite Sprite;
        public Color Color;
        public float2 Offset;
        private void OnValidate()
        {
            CheckRef();
        }
        private bool CheckRef()
        {
            if (!AuthoringRef.TryGet(out var authoring))
                return false;
            var authoringData = authoring.prefab;
            if (authoringData.TryGetComponent(out EntityMonoBehaviourData vanilla))
            {
                Sprite = vanilla.ObjectInfo.icon;
                Offset = vanilla.objectInfo.iconOffset;
                return true;
            }
            else if (authoringData.TryGetComponent(out InventoryItemAuthoring mod))
            {
                Sprite = mod.icon;
                Offset = mod.iconOffset;
                return true;
            }
            name = authoring.name;
            return false;   
        }
    }
}
