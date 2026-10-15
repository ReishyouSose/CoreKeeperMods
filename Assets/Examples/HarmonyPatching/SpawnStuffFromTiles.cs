using HarmonyLib;
using Unity.Mathematics;
using UnityEngine;
using PugMod;
using System;
using PlayerEquipment;
using Unity.Entities;

public class SpawnStuffFromTiles : IMod
{
    /* 
     * We have these flags and the entire script split into an IMod implementation and a Harmony patch because the API calls we use must run on Unitys' main thread.
     * If we tried to run them in ECS which is where ShovelSlot.PlayDigEffects() runs, it would break.
     * So the Harmony patch just detects when ShovelSlot.PlayDigEffects() has finished and sets a flag to true, 
     * and then the Update() method which runs every frame on the main thread checks that flag and runs our logic.
     */
    public static bool spawnObjectAndFX = false;
    public static float3 diggingPosition;
    private static Unity.Mathematics.Random random;

    public void EarlyInit()
    {
         // Can't call BurstDisabler here since we are missing some necessary type initialization at this point
     }

     public void Init()
     {
         // Call to disable burst for these specific systems so we can patch them
         // Note: Patches via HarmonyPatchAttribute are applied automatically unless disabled via ModBuilderSettings
         BurstDisabler.DisableBurstForSystem<LegacySpawnEnvironmentObjectInNewAreaSystem>();

         /*
          * Right now DisableBurstForSystem doesn't work reliably for jobs since
          * they might start outside of the OnUpdate call. This is a workaround
          * which disables burst for the entire group the system runs in which
          * typically gives the job enough time to start before burst is enabled
          * again.
          */
         BurstDisabler.DisableBurstForSystem<EquipmentUpdateSystem>();
     }

     public void ModObjectLoaded(UnityEngine.Object obj)
     {
     }

     public void Shutdown()
     {
     }

     public void Update()
     {
         var player = Manager.main.player;

         if (player == null)
         {
             return;
         }

         // Check if conditions have been met for us to run our function.
         if (spawnObjectAndFX)
         {
             // Reset the flag so that once it gets set to true it doesn't keep spawning FX and Objects forever. A good thing to know is that Update in Unity runs every frame.
             spawnObjectAndFX = false;
             SpawnObjectAndDoFX();
         }
     }

     private void SpawnObjectAndDoFX()
     {
         // Get the local players' instance.
         var player = Manager.main.player;

         if (player == null)
         {
             return;
         }

         // Random number generating utility that is thread-safe, use this over Random.Range when working in ECS.
         // CreateFromIndex is needed when creating it from sequential values like index or time/ticks.
         random = Unity.Mathematics.Random.CreateFromIndex((uint)DateTime.Now.Ticks);

         var playerPosition = player.transform.position;
         Vector3 FXPosition = new(playerPosition.x, 0.5f, playerPosition.z);

         var objectSpawnPosition = diggingPosition + new float3(0, 0.5f, 0);

         // Play VFX at local players' position.
         API.Effects.PlayPuff((int)PuffID.AncientEnergyBurst, FXPosition, 50);

         // Play a sound effect.
         API.Audio.PlaySfx(SfxTableID.acidLarvaDeath, FXPosition, pitchMultiplier: 1f, volumeMultiplier: 2f);

         // Get a random Object ID between 1001 and 1011, on Core Keepers' end these are all the bars from bronze to relucite.
         int randomObjectID = random.NextInt(1001, 1011);

         // Drop the object.
         if (API.Server.World != null)
         {
             API.Server.DropObject(randomObjectID, 0, 1, objectSpawnPosition);
         }
     }
}

[HarmonyPatch(typeof(ShovelSlot), "PlayDigEffects")]
public class SpawnObjectAndPlayFXAfterShoveling
{
     private static Unity.Mathematics.Random random;

     // Runs after ShovelSlot.PlayDigEffects() has already finished running.
     [HarmonyPostfix]
     public static void PostPlayDigEffects(float3 position, EquipmentUpdateAspect equipmentUpdateAspect)
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

         // Roll 1-100 and if we roll above 50 then the Object gets spawned and SFX+VFX play.
         if (random.NextInt(0, 100) < 50)
         {
             // Tile that's causing the effect to occur.
             SpawnStuffFromTiles.diggingPosition = position;

             // Tells Update that we're now meeting all the conditions to spawn the Object and play the SFX+VFX.
             SpawnStuffFromTiles.spawnObjectAndFX = true;
         }
     }
}

