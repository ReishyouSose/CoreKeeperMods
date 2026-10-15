using Pug.UnityExtensions;
using PugMod;
using QFSW.QC;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Scripting;

public class MyNewCommands : MonoBehaviour
{
    [CommandPrefix("modding.")]
    public static class ModdingCommands
    {
        [Preserve]
        [CommandWithModSupport("spawnCustomScene", "Spawns a custom scene near the player. Optional X/Z offsets default to 8/0 tiles. Host only.", paramsInGlobalSuggestions: 1)]
        public static void SpawnCustomScene(string sceneName, int offsetX = 8, int offsetZ = 0)
        {
            var player = Manager.main.player;

            if (player == null)
            {
                Manager.menu.quantumConsole.LogToConsole("You must be in-game to use this command.");
                return;
            }

            var world = Manager.ecs.ServerWorld;

            if (world == null || !world.IsCreated)
            {
                Manager.menu.quantumConsole.LogToConsole("Custom scenes can only be spawned by the server host.");
                return;
            }

            bool sceneExists = false;

            if (!string.IsNullOrWhiteSpace(sceneName) && ScriptableData.TryGetDataBlocks<CustomSceneDataBlock>(out var scenes))
            {
                foreach (var scene in scenes)
                {
                    if (scene.sceneName == sceneName)
                    {
                        sceneExists = true;
                        break;
                    }
                }
            }

            if (!sceneExists)
            {
                Manager.menu.quantumConsole.LogToConsole($"Custom scene '{sceneName}' was not found. Use the sceneName from its CustomSceneDataBlock.");
                return;
            }

            var request = new SpawnCustomSceneCD
            {
                name = new FixedString32Bytes(sceneName),
                seed = PugRandom.GetSeed(),
            };

            Vector3 position = player.WorldPosition;
            position += new Vector3(offsetX, 0, offsetZ);

            var entity = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
            world.EntityManager.AddComponentData(entity, request);
            Manager.menu.quantumConsole.LogToConsole($"Queued custom scene '{sceneName}' at {position}. Existing terrain in the scene area may be overwritten.");
        }

        [Preserve]
        [CommandWithModSupport("playPuffVFX", "Plays a visual Puff effect at the players' position.", paramsInGlobalSuggestions: 1)]
        public static void PlayPuffVFX(PuffID puffID, int particleCount = 10)
        {
            if (API.Client.LocalPlayer == null)
            {
                Manager.menu.quantumConsole.LogToConsole("You must be in-game to use this command.");
                return;
            }

            Vector3 playerPosition = API.Client.LocalPlayer.transform.position;
            API.Effects.PlayPuff((int)puffID, playerPosition, particleCount);
            Manager.menu.quantumConsole.LogToConsole($"Played visual effect with ID: {puffID} and particle amount: {particleCount}");
        }
    }
}
