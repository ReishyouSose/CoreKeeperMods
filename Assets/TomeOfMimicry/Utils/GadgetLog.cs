using UnityEngine;

namespace TomeOfMimicry
{
    public static class GadgetLog
    {
        public static bool Verbose = false;

        public static void Trace(string message)
        {
            if (Verbose) Debug.Log(message);
        }
    }
}
