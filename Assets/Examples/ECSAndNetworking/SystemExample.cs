using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(RunSimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(SendClientInputSystem))]
public partial class InvertPlayerMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var clientInputData in
                 SystemAPI.Query<RefRW<ClientInputData>>().WithAll<GhostOwnerIsLocal>()) // GhostOwnerIsLocal => this is the local player
        {
            // ClientInputData used to avoid unnecessary bytes being sent. ClientInput is set in the PredictedSimulattionSystemGroup from current tick.
            // Outside of PredictedSimulationSystemGroup, ClientInputData has to be used to get and set this frames input data.
            var clientInput = UnsafeUtility.As<ClientInputData, ClientInput>(ref clientInputData.ValueRW);
            clientInput.movementDirection *= -1f;
			clientInputData.ValueRW = UnsafeUtility.As<ClientInput, ClientInputData>(ref clientInput);
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class MoveCowsAwayFromPlayerSystem : PugSimulationSystemBase
{
    protected override void OnUpdate()
    {
        // No need to dispose World allocator
        var playerPositions = new NativeList<float3>(World.UpdateAllocator.ToAllocator);

        Entities.WithAll<PlayerGhost>().ForEach((in LocalTransform translation) =>
        {
            playerPositions.Add(translation.Position);
        }).Schedule();
        
        var deltaTime = SystemAPI.Time.DeltaTime;
        var distanceToMoveSq = 5 * 5;
        // Filter on cattle just to avoid looping through everything
        Entities.WithAll<CattleCD>().ForEach((ref LocalTransform translation, in ObjectDataCD objectData) =>
            {
                if (objectData.objectID != ObjectID.Cow)
                {
                    return;
                }
                
                foreach (var playerPos in playerPositions)
                {
                    if (math.distancesq(translation.Position, playerPos) < distanceToMoveSq)
                    {
                        var dir = math.normalizesafe(translation.Position - playerPos);
                        translation.Position += dir * deltaTime;
                    }
                }
            }).Schedule();
        
        base.OnUpdate();
    }
}
