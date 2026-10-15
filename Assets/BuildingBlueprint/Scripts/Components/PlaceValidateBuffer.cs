using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.Components
{
    [InternalBufferCapacity(0)]
    public struct PlaceValidateBuffer : IBufferElementData
    {
        [GhostField]
        public int ID;

        [GhostField]
        public bool Placing;

        [GhostField]
        public int Count_2;
        [GhostField]
        public int Count_3;
        [GhostField]
        public int Count_4;
        [GhostField]
        public int Count_5;
        [GhostField]
        public int Current;
        [GhostField]
        public int Next;
        [GhostField]
        public int State;
        [GhostField]
        public NetworkTick OpenTick;
        public void Start(StartPlaceRPC start)
        {
            ID = start.ID;
            Placing = true;
            Count_2 = start.Count_2;
            Count_3 = start.Count_3;
            Count_4 = start.Count_4;
            Count_5 = start.Count_5;
            Current = 0;
            State = 0;
            Next = start.Count_1;
            OpenTick = default;
            SkipEmpty();
        }
        public bool Collect(int level, NetworkTick tick)
        {
            if (level != State || Next <= 0)
                return false;
            if (OpenTick.IsValid && !tick.IsNewerThan(OpenTick))
                return false;
            Current++;
            if (Current >= Next)
            {
                State++;
                Current = 0;
                Next = CountOf(State);
                SkipEmpty();
                if (Next > 0)
                    OpenTick = tick;
                //Debug.Log($"Enter State {State}");
            }
            return true;
        }
        public readonly bool Finished => State > 4 || (State == 4 && Next <= 0);
        private void SkipEmpty()
        {
            while (Next <= 0 && State < 4)
            {
                State++;
                Current = 0;
                Next = CountOf(State);
            }
        }
        private readonly int CountOf(int state)
        {
            return state switch
            {
                1 => Count_2,
                2 => Count_3,
                3 => Count_4,
                4 => Count_5,
                _ => 0,
            };
        }
    }
}
