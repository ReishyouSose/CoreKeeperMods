using Pug.Conversion;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.Component
{
    [RequireComponent(typeof(ProjectileAuthoring))]
    public class ExtraUpdateAuthoring : MonoBehaviour
    {
        public int ExtraUpdates;
        public bool ScaleProgress;
        private void OnValidate()
        {
            ExtraUpdates = Mathf.Max(ExtraUpdates, 0);
        }
    }

    [GhostComponent]
    public struct ExtraUpdateCD : IComponentData
    {
        [GhostField]
        public int TotalUpdate;
    }
    public class ExtraUpdateConverter : SingleAuthoringComponentConverter<ExtraUpdateAuthoring>
    {
        protected override void Convert(ExtraUpdateAuthoring authoring)
        {
            AddComponentData(new ExtraUpdateCD { TotalUpdate = authoring.ExtraUpdates + 1 });
        }
    }
}
