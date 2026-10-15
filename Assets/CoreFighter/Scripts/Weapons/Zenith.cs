using PlayerEquipment;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Assets.CoreFighter.Scripts.Weapons
{
    public struct ZenithSwingCD : IComponentData
    {
        public uint swingStart;
        public int elapsed;
        public int spawned;
        public int window;
    }

    public struct ZenithFlightCD : IComponentData
    {
        public float age;
    }

    public static class ZenithBalance
    {
        public const string WeaponName = "CoreFighter_Zenith";
        public const string SwordName = "CoreFighter_ZenithSword";
        public const int SwordTypes = 21;
        public const float FlightSeconds = 0.5f;
        public const float HomeRadius = 20f;
        public const float ReachX = 60f;
        public const float ReachY = 37.5f;
        public const float MinAimDistance = 4f;

        public static int PackVariation(int index, int phase, int side)
        {
            return index | (phase << 8) | (side << 16);
        }

        public static void UnpackVariation(int variation, out int index, out float phase, out float side)
        {
            index = variation & 255;
            phase = ((variation >> 8) & 255) * (math.PI * 2f / 255f);
            side = ((variation >> 16) & 1) == 0 ? -1f : 1f;
        }

        public static float2 ClampAim(float2 player, float2 mouse)
        {
            float2 delta = mouse - player;
            delta.x = math.clamp(delta.x, -ReachX, ReachX);
            delta.y = math.clamp(delta.y, -ReachY, ReachY);
            if (math.lengthsq(delta) < MinAimDistance * MinAimDistance)
                delta = math.normalizesafe(delta, new float2(1f, 0f)) * MinAimDistance;
            return player + delta;
        }
    }

    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ZenithAttackSystem : PugSimulationSystemBase
    {
        private ObjectID weaponId;
        private ObjectID swordId;
        private ComponentLookup<RandomCD> randomLookup;
        private ComponentLookup<BehaviourTagsCD> tagsLookup;
        private ComponentLookup<FactionCD> factionLookup;
        private ComponentLookup<CooldownCD> cooldownLookup;
        private ComponentLookup<WeaponDamageCD> weaponDamageLookup;
        private ComponentLookup<PiercingProjectileCD> piercingLookup;
        private BufferLookup<SummarizedConditionsBuffer> summarizedLookup;

        protected override void OnCreate()
        {
            NeedDatabase();
            randomLookup = SystemAPI.GetComponentLookup<RandomCD>();
            tagsLookup = SystemAPI.GetComponentLookup<BehaviourTagsCD>();
            factionLookup = SystemAPI.GetComponentLookup<FactionCD>();
            cooldownLookup = SystemAPI.GetComponentLookup<CooldownCD>();
            weaponDamageLookup = SystemAPI.GetComponentLookup<WeaponDamageCD>();
            piercingLookup = SystemAPI.GetComponentLookup<PiercingProjectileCD>();
            summarizedLookup = SystemAPI.GetBufferLookup<SummarizedConditionsBuffer>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            if (weaponId == ObjectID.None)
            {
                weaponId = API.Authoring.GetObjectID(ZenithBalance.WeaponName);
                swordId = API.Authoring.GetObjectID(ZenithBalance.SwordName);
            }
            if (weaponId == ObjectID.None || swordId == ObjectID.None || !database.IsCreated)
                return;
            uint tickRate = 60;
            if (SystemAPI.TryGetSingleton<ClientServerTickRate>(out var tickRateCd) && tickRateCd.SimulationTickRate > 0)
                tickRate = (uint)tickRateCd.SimulationTickRate;
            int interval = math.max(1, (int)(tickRate / 6));
            randomLookup.Update(this);
            tagsLookup.Update(this);
            factionLookup.Update(this);
            cooldownLookup.Update(this);
            weaponDamageLookup.Update(this);
            piercingLookup.Update(this);
            summarizedLookup.Update(this);
            var ecb = CreateCommandBuffer();
            var table = SystemAPI.GetSingleton<ConditionsTableCD>();
            foreach (var (equipped, attack, input, transform, swing, player) in SystemAPI.Query<RefRO<EquippedObjectCD>, RefRO<PlayerAttackCD>, RefRO<ClientInput>, RefRO<LocalTransform>, RefRW<ZenithSwingCD>>().WithAll<PlayerGhost>().WithEntityAccess())
            {
                if (equipped.ValueRO.containedObject.objectID != weaponId || equipped.ValueRO.isBroken || attack.ValueRO.heldItemIsBroken)
                {
                    swing.ValueRW.swingStart = 0;
                    continue;
                }
                var hit = attack.ValueRO.hitDuration;
                if (hit.isRunning && hit.startTick.SerializedData != swing.ValueRO.swingStart)
                {
                    float cooldown = 0.5f;
                    if (cooldownLookup.TryGetComponent(equipped.ValueRO.equipmentPrefab, out var cooldownCd))
                        cooldown = cooldownCd.cooldown;
                    swing.ValueRW.swingStart = hit.startTick.SerializedData;
                    swing.ValueRW.elapsed = 0;
                    swing.ValueRW.spawned = 0;
                    swing.ValueRW.window = math.max(1, (int)NetworkTimeUtilities.SecondsToTicks(cooldown, tickRate));
                }
                if (swing.ValueRO.swingStart == 0 || swing.ValueRO.elapsed >= swing.ValueRO.window)
                    continue;
                if (!randomLookup.HasComponent(player) || !tagsLookup.TryGetComponent(player, out var tags) || !factionLookup.TryGetComponent(player, out var faction) || !summarizedLookup.HasBuffer(player))
                {
                    swing.ValueRW.elapsed++;
                    continue;
                }
                int due = 1 + swing.ValueRO.elapsed / interval;
                int damage = attack.ValueRO.meleeDamage;
                if (damage <= 0 && weaponDamageLookup.TryGetComponent(equipped.ValueRO.equipmentPrefab, out var weaponDamage))
                    damage = weaponDamage.GetDamage(false);
                if (damage <= 0)
                    damage = 1;
                float2 playerPos = transform.ValueRO.Position.xz;
                float2 aim = ZenithBalance.ClampAim(playerPos, input.ValueRO.mouseOrJoystickWorldPoint);
                float3 direction = new float3(aim.x - playerPos.x, 0f, aim.y - playerPos.y);
                if (math.lengthsq(direction) < 0.0001f)
                    direction = new float3(1f, 0f, 0f);
                ref var randomCd = ref randomLookup.GetRefRW(player).ValueRW;
                while (swing.ValueRO.spawned < due)
                {
                    var rng = randomCd.Value;
                    int index = swing.ValueRO.spawned == 0 ? 0 : rng.NextInt(1, ZenithBalance.SwordTypes);
                    int phase = rng.NextInt(0, 256);
                    int side = rng.NextInt(0, 2);
                    randomCd.Value = rng;
                    int variation = ZenithBalance.PackVariation(index, phase, side);
                    EntityUtility.SpawnProjectile(ecb, transform.ValueRO.Position, database, swordId, damage, 0f, direction, 0f, player, tags, summarizedLookup, faction, table, randomLookup.GetRefRW(player), piercingLookup, variation, 1f);
                    swing.ValueRW.spawned++;
                }
                swing.ValueRW.elapsed++;
            }
            base.OnUpdate();
        }
    }

    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileMovementSystem))]
    [UpdateBefore(typeof(ProjectileSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ZenithFlightSystem : PugSimulationSystemBase
    {
        private ObjectID weaponId;
        private EntityQuery enemies;
        private ComponentLookup<ClientInput> inputLookup;
        private ComponentLookup<LocalTransform> transformLookup;
        private ComponentLookup<EquippedObjectCD> equippedLookup;
        private ComponentLookup<HealthCD> healthLookup;

        protected override void OnCreate()
        {
            enemies = GetEntityQuery(ComponentType.ReadOnly<EnemyCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<HealthCD>());
            inputLookup = SystemAPI.GetComponentLookup<ClientInput>(true);
            transformLookup = SystemAPI.GetComponentLookup<LocalTransform>();
            equippedLookup = SystemAPI.GetComponentLookup<EquippedObjectCD>(true);
            healthLookup = SystemAPI.GetComponentLookup<HealthCD>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            if (weaponId == ObjectID.None)
                weaponId = API.Authoring.GetObjectID(ZenithBalance.WeaponName);
            inputLookup.Update(this);
            transformLookup.Update(this);
            equippedLookup.Update(this);
            healthLookup.Update(this);
            using var enemyTransforms = enemies.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var enemyHealth = enemies.ToComponentDataArray<HealthCD>(Allocator.Temp);
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var (flight, transform, projectile, objectData, owner, entity) in SystemAPI.Query<RefRW<ZenithFlightCD>, RefRW<LocalTransform>, RefRW<ProjectileCD>, RefRO<ObjectDataCD>, RefRO<OwnerReferenceCD>>().WithEntityAccess())
            {
                if (!healthLookup.TryGetComponent(entity, out var health) || health.health <= 0)
                    continue;
                Entity ownerEntity = owner.ValueRO.owner;
                if (!transformLookup.TryGetComponent(ownerEntity, out var ownerTransform) || !inputLookup.TryGetComponent(ownerEntity, out var input) || !equippedLookup.TryGetComponent(ownerEntity, out var equipped) || equipped.containedObject.objectID != weaponId)
                {
                    health.health = 0;
                    healthLookup.GetRefRW(entity).ValueRW = health;
                    continue;
                }
                if (flight.ValueRO.age >= ZenithBalance.FlightSeconds)
                {
                    health.health = 0;
                    healthLookup.GetRefRW(entity).ValueRW = health;
                    continue;
                }
                float2 playerPos = ownerTransform.Position.xz;
                float2 aim = ZenithBalance.ClampAim(playerPos, input.mouseOrJoystickWorldPoint);
                ZenithBalance.UnpackVariation(objectData.ValueRO.variation, out int index, out float phase, out float side);
                float2 forward = math.normalizesafe(aim - playerPos, new float2(1f, 0f));
                float2 perp = new float2(-forward.y, forward.x);
                float2 start = playerPos + perp * side * 4f - forward * 2.5f;
                float2 end = playerPos - perp * side * 1.5f;
                float2 control = aim + perp * side * (6f + math.sin(phase) * 4f);
                if (index != 0 && TryHome(aim, enemyTransforms, enemyHealth, out float2 home))
                    control = math.lerp(control, home, 0.8f);
                float t = math.saturate(flight.ValueRO.age / ZenithBalance.FlightSeconds);
                float2 pos = Bezier(start, control, end, t);
                float2 previous = transform.ValueRO.Position.xz;
                transform.ValueRW.Position = new float3(pos.x, transform.ValueRO.Position.y, pos.y);
                float2 delta = pos - previous;
                if (math.lengthsq(delta) > 0.0001f)
                    projectile.ValueRW.SetDirection(new float3(delta.x, 0f, delta.y));
                flight.ValueRW.age += dt;
            }
            base.OnUpdate();
        }

        private static bool TryHome(float2 aim, NativeArray<LocalTransform> transforms, NativeArray<HealthCD> healths, out float2 home)
        {
            float best = ZenithBalance.HomeRadius * ZenithBalance.HomeRadius;
            home = aim;
            bool found = false;
            for (int i = 0; i < transforms.Length; i++)
            {
                if (healths[i].health <= 0)
                    continue;
                float2 point = transforms[i].Position.xz;
                float distance = math.lengthsq(point - aim);
                if (distance >= best)
                    continue;
                best = distance;
                home = point;
                found = true;
            }
            return found;
        }

        private static float2 Bezier(float2 start, float2 control, float2 end, float t)
        {
            return math.lerp(math.lerp(start, control, t), math.lerp(control, end, t), t);
        }
    }
}
