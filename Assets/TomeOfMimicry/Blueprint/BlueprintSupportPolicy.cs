using System;
using Pug.Automation;
using Unity.Entities;

namespace TomeOfMimicry
{
    public enum BlueprintObjectSupport
    {
        Exact,
        ShellOnly,
        Excluded,
    }

    public static class BlueprintSupportPolicy
    {
        public static BlueprintObjectSupport Classify(
            Entity entity,
            ObjectID objectId,
            EntityManager entityManager)
        {
            if (entityManager.HasComponent<PortalCD>(entity))
                return BlueprintObjectSupport.Excluded;

            string name = objectId.ToString();
            if (ContainsAny(name,
                    "Waypoint", "Tombstone", "Summoning", "Boss", "Merchant",
                    "Minecart", "Boat", "GoKart"))
                return BlueprintObjectSupport.Excluded;

            if (entityManager.HasBuffer<ContainedObjectsBuffer>(entity))
                return BlueprintObjectSupport.ShellOnly;

            return BlueprintObjectSupport.Exact;
        }

        private static bool ContainsAny(string value, params string[] parts)
        {
            foreach (string part in parts)
                if (value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }
    }
}
