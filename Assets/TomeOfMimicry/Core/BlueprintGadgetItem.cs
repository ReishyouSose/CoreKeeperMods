using PugMod;
using UnityEngine;

namespace TomeOfMimicry
{
    public static class BlueprintGadgetItem
    {
        private static ObjectID _resolvedID = ObjectID.None;
        private const string CustomItemName = "BuilderGadget:BlueprintGadget";

        public static ObjectID ObjectID
        {
            get
            {
                if (_resolvedID == ObjectID.None)
                {
                    var id = API.Authoring.GetObjectID(CustomItemName);
                    _resolvedID = id;
                    Debug.Log($"[Tome of Mimicry] Using item ObjectID: {_resolvedID}");
                }
                return _resolvedID;
            }
        }

        public static void Reset() => _resolvedID = ObjectID.None;
    }
}
