using HarmonyLib;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Disables spawning of a bunch of environment objects, like grass and plants (outside scenes, dungeons, and sub-biomes).
/// </summary>
[HarmonyPatch(typeof(LegacySpawnEnvironmentObjectInNewAreaSystem), "OnUpdate")]
public static class DisableEnvironmentSpawnPatch
{
    // The Prefix runs right before the original function (return controls whether to continue with original function)
    [HarmonyPrefix]
    public static bool Prefix(LegacySpawnEnvironmentObjectInNewAreaSystem __instance)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        using var environmentSpawnQuery = __instance.EntityManager.CreateEntityQuery(
            ComponentType.ReadOnly<LegacySpawnEnvironmentObjectsInAreaCD>());

        // This is the minimal setup required to pass on the new area to the next step in the world gen pipeline
        ecb.RemoveComponent<LegacySpawnEnvironmentObjectsInAreaCD>(
            environmentSpawnQuery, EntityQueryCaptureMode.AtRecord);
        ecb.AddComponent<LegacySpawnTerritoriesInAreaCD>(
            environmentSpawnQuery, EntityQueryCaptureMode.AtRecord);

        ecb.Playback(__instance.EntityManager);
        ecb.Dispose();

        // Return false to not call the original method
        return false;
    }
}