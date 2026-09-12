using System.Collections.Generic;

namespace TomeOfMimicry
{
    public static class TileCostTracker
    {
        public struct CostEntry
        {
            public ObjectID objectId;
            public int needed;
            public int available;

            public bool HasEnough => available >= needed;
        }

        public static readonly List<CostEntry> CurrentCost = new List<CostEntry>();

        public static bool IsCreativeMode { get; private set; }
        public static bool IsActive { get; private set; }
        public static bool CanAfford { get; private set; } = true;
        public static bool HasPlaceableContent { get; private set; } = true;

        public static bool IsBlocked => IsActive &&
            (!HasPlaceableContent || (!IsCreativeMode && !CanAfford));

        public static string BlockReason => !HasPlaceableContent
            ? "No empty target cells"
            : (!IsCreativeMode && !CanAfford ? "Missing materials" : "");

        public static void Publish(
            Dictionary<ObjectID, int> needed,
            Dictionary<ObjectID, int> available,
            bool isCreative,
            bool isActive,
            bool hasPlaceableContent = true)
        {
            IsCreativeMode = isCreative;
            IsActive = isActive;
            HasPlaceableContent = hasPlaceableContent;

            CurrentCost.Clear();

            if (!isActive || needed == null || needed.Count == 0)
            {
                CanAfford = true;
                return;
            }

            bool affordable = true;
            foreach (var kv in needed)
            {
                int have = isCreative
                    ? kv.Value
                    : available != null && available.TryGetValue(kv.Key, out int a) ? a : 0;
                CurrentCost.Add(new CostEntry
                {
                    objectId = kv.Key,
                    needed = kv.Value,
                    available = have,
                });
                if (!isCreative && have < kv.Value) affordable = false;
            }

            CurrentCost.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.objectId.ToString(), b.objectId.ToString());
                return c != 0 ? c : ((int)a.objectId).CompareTo((int)b.objectId);
            });

            CanAfford = affordable;
        }

        public static void Clear()
        {
            CurrentCost.Clear();
            CanAfford = true;
            IsActive = false;
            HasPlaceableContent = true;
        }
    }
}
