using Pug.Conversion;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.Component
{
    public class LifeProgressAuthoring : MonoBehaviour
    {
        public float Value;
        public float DefaultRatio;
    }

    [GhostComponent]
    public struct LifeProgressCD : IComponentData
    {
        [GhostField]
        public float Max;
        [GhostField]
        public float Current;
        [GhostField]
        public float DefaultRatio;
        public readonly float Ratio() => Max <= 0 ? DefaultRatio : Mathf.Clamp01(Current / Max);
    }

    public class LifeProgressConverter : SingleAuthoringComponentConverter<LifeProgressAuthoring>
    {
        protected override void Convert(LifeProgressAuthoring authoring)
        {
            int scale = 1;
            if (authoring.TryGetComponent(out ExtraUpdateAuthoring exupdate) && exupdate.ScaleProgress)
                scale += exupdate.ExtraUpdates;
            AddComponentData(new LifeProgressCD() { Max = authoring.Value * scale, DefaultRatio = authoring.DefaultRatio });
        }
    }
}
