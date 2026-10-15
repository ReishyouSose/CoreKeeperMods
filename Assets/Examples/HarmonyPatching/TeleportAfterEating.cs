using HarmonyLib;
using Unity.Mathematics;
using PugTilemap;
using System;
using PlayerEquipment;
using PlayerState;
using Unity.Entities;

/*
 * This is another workaround for the problem with the job running with burst
 * since it starts after OnUpdate. This slows down the game since we wait for
 * all jobs to finish on the main thread, but more reliable since we
 * always guarantee that the job has started before we enable burst again at
 * the end of OnUpdate.
 */
[HarmonyPatch(typeof(EquipmentUpdateSystem), "OnUpdate")]
public static class ForceJobCompletePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.High)] // Not needed for ISystem, but for SystemBase we want to make sure this runs before burst is enabled again
    public static void Postfix(ref SystemState state)
    {
        state.Dependency.Complete();
    }
}

[HarmonyPatch(typeof(EatableSlot), "EatItem")]
public class TeleportAfterEatingPatch
{
    private static Unity.Mathematics.Random random;

    // We use HarmonyPostFix because we want EatableSlot.EatItem() to finish first, and then call the teleportation afterwards.
    [HarmonyPostfix]
    public static void TeleportIfViableTile(EquipmentUpdateAspect equipmentUpdateAspect)
    {
        // Get player instance
        var player = Manager.main.player;

        // Check if our player is the one that has eaten something and should be teleported.
        if (equipmentUpdateAspect.entity != player.entity)
        {
            // Return early if it's not our player so that we don't get teleported whenever other party members eat.
            return;
        }

        // Random number generating utility that is thread-safe, use this over Random.Range when working in ECS.
        random = new Unity.Mathematics.Random((uint)DateTime.Now.Ticks);

        // A way for us to look up tiles, good for checking if a target tile is of a specific TileType.
        var tileLookup = Manager.multiMap.GetTileLayerLookup();
        var playerPosition = player.WorldPosition;//get players' local position

        // Iterate over up to 100 tile lookups and see if we can find a tile that we can walk on.
        for (int i = 0; i < 100; i++)
        {
            // We offset the player by up to 50 tiles in any direction from the players' current position.
            var playerOffset = random.NextFloat2(-50f, 50f);

            // Try to place the player in this position.
            var targetTile = new float2(playerPosition.x, playerPosition.z) + playerOffset;

            // Check what the tile type is of the tile which we want to place our player on.
            var tile = tileLookup.GetTopTile((int2)math.round(targetTile)).tileType;

            if (tile != TileType.none && tile != TileType.water && tile != TileType.pit && TileTypeUtility.IsWalkableTile(tile) && !TileTypeUtility.IsBlockingTile(tile))//check if the tile type that we retrieved is a viable tile for the player to be on.
            {
                // Teleport the player, we do this instead of transform.position because Core Keeper is server authoritative, meaning you have to tell the server what to do.
                // If you try to do something like this on the client your player will rubberband because the server will see a mismatch on what is the players' actual position.
                player.QueueInputAction(new UIInputActionData
                {
                    // The actual action of teleporting.
                    action = UIInputAction.Teleport,

                    // Where to teleport the player to.
                    position = targetTile
                });

                // End the loop early since we found a viable tile to teleport the player to, so the loop doesn't keep running until i reaches 100.
                break;
            }
        }
    }
}
