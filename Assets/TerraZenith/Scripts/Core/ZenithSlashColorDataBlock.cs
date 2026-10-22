using UnityEngine;

namespace Assets.TerraZenith.Scripts.Core
{
    public class ZenithSlashColorDataBlock : ScriptableDataBlock
    {
        public DataBlockRef<EntityAuthoringDataBlock> AuthoringRef;
        public Sprite Sprite;
        public Color Color;
        private void OnValidate()
        {
            if (!AuthoringRef.TryGet(out var authoring))
                return;
            var authoringData = authoring.prefab;
            if (authoringData.TryGetComponent(out EntityMonoBehaviourData vanilla))
            {
                Sprite = vanilla.ObjectInfo.icon;
            }
            else if (authoringData.TryGetComponent(out InventoryItemAuthoring mod))
                Sprite = mod.icon;
            if (Sprite != null)
                name = authoring.name;
        }
    }
}
