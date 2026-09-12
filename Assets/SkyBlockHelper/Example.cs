//using CoreLib.Submodule.Command.Data;
//using CoreLib.Submodule.Command.Interface;
//using Pug.UnityExtensions;
//using Unity.Collections;
//using Unity.Entities;
//using Unity.Mathematics;
//using Unity.NetCode;
//using Unity.Transforms;
//using UnityEngine;

//namespace Assets.SkyBlockHelper
//{
//    public struct ExampleRPC : IRpcCommand
//    {
//        public Entity Player;
//        public FixedString32Bytes sceneName;
//    }
//    public class SpawnSceneCommandHandler : IClientCommandHandler
//    {
//        public CommandOutput Execute(string[] parameters)
//        {
//            if (parameters.Length < 1)
//            {
//                return "Params count must 3";
//            }
//            ExampleClient.SpawnScene(parameters[0]);
//            return "try spawn " + parameters[0];
//        }

//        public string GetDescription()
//        {
//            return "Use /spawnscene <sceneName> <x> <y> to spawn special scene";
//        }

//        public string[] GetTriggerNames()
//        {
//            return new string[] { "spawnscene" };
//        }
//    }

//    [UpdateInGroup(typeof(RunSimulationSystemGroup))]
//    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
//    public partial class ExampleClient : PugSimulationSystemBase
//    {
//        private static ExampleClient ins;
//        private NativeQueue<ExampleRPC> query;
//        private EntityArchetype archetype;
//        protected override void OnCreate()
//        {
//            ins = this;
//            query = new NativeQueue<ExampleRPC>(Allocator.Persistent);
//            archetype = EntityManager.CreateArchetype(typeof(SendRpcCommandRequest), typeof(ExampleRPC));
//            base.OnCreate();
//        }
//        protected override void OnUpdate()
//        {
//            var ecb = CreateCommandBuffer();
//            while (query.TryDequeue(out ExampleRPC info))
//            {
//                var entity = ecb.CreateEntity(archetype);
//                ecb.SetComponent(entity, info);
//            }
//            base.OnUpdate();
//        }
//        public static void SpawnScene(string name)
//        {
//            ins.query.Enqueue(new()
//            {
//                Player = Manager.main.player.entity,
//                sceneName = name,
//            });
//        }
//    }

//    [UpdateInGroup(typeof(RunSimulationSystemGroup))]
//    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
//    public partial class ExampleServer : PugSimulationSystemBase
//    {
//        private ComponentLookup<LocalTransform> transLookup;
//        private NativeQueue<ExampleRPC> queue;
//        protected override void OnCreate()
//        {
//            transLookup = SystemAPI.GetComponentLookup<LocalTransform>();
//            queue = new(Allocator.Persistent);
//            base.OnCreate();
//        }
//        protected override void OnUpdate()
//        {
//            var ecb = CreateCommandBuffer();
//            var queue = this.queue;
//            Entities.ForEach((Entity e, in ExampleRPC rpc) =>
//            {
//                ecb.DestroyEntity(e);
//                queue.Enqueue(rpc);
//            })
//                .WithName("SpawnScene")
//                .WithAll<ReceiveRpcCommandRequest>()
//                .WithBurst()
//                .Schedule();

//            var scenes = SkyBlockHelper.sceneData;
//            var transLookup = this.transLookup;
//            while (queue.TryDequeue(out var rpc))
//            {
//                var name = rpc.sceneName;
//                var player = rpc.Player;
//                if (!scenes.TryFindSceneByName(name.ToString(), out var scene))
//                {
//                    Debug.Log("Can't find scene " + name);
//                    continue;
//                }
//                if (!transLookup.TryGetComponent(player, out var trans))
//                {
//                    Debug.Log("not contains localtransform!");
//                }
//                Entity block = ecb.CreateEntity();
//                float2 pos = trans.Position.RoundToInt2();
//                ;
//                ecb.AddComponent(block, new BlockedSpawnAreaCD(pos, scene.radius));
//                Entity spawn = ecb.CreateEntity();
//                ecb.AddComponent(spawn, LocalTransform.FromPosition((pos - scene.center).ToFloat3()));
//                ecb.AddComponent(spawn, new SpawnCustomSceneCD
//                {
//                    name = name,
//                    seed = PugRandom.GetSeed()
//                });
//            }
//            base.OnUpdate();
//        }
//    }
//}
