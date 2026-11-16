using Assets.BuildingBlueprint.Scripts.Enums;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.Components
{
    [GhostComponent]
    public struct SelectionOptionCD : IComponentData
    {
        [GhostField]
        public SelectionLayer Layer;

        [GhostField]
        public SelectionMode Mode;

        [GhostField]
        public int Operator;

        [GhostField]
        public bool Place;

        [GhostField]
        public bool Open;

        public int2? Start;
        public int2 Current;
        public bool InteractHeld;

        public static implicit operator BoxOperator(SelectionOptionCD cd) => (BoxOperator)cd.Operator;
        public static implicit operator ClickOperator(SelectionOptionCD cd) => (ClickOperator)cd.Operator;
    }
}
