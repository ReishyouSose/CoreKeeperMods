using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// 天顶剑的服务器逻辑。发射对照玩家挥砍时连续生成 FinalFractal，飞行对照 Projectile.AI_182_FinalFractal。
namespace Assets.TerraZenith.Scripts.System
{
    // 轨道。放在弹幕位移之后、弹幕结算之前。
    // 瞄准、椭圆、剑序号只在服务器第一次补登记时计算，经幽灵组件同步一次。之后两端用这组参数各自积分。
    // 刀光缓冲不同步，图形只读自己这个世界里的记录。血量和销毁只在服务器写。
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileMovementSystem))] // 确保它原有的物理移动先算完，然后我们再强行覆盖位置
    [UpdateBefore(typeof(ProjectileSystem))]        // 确保覆盖了位置之后，再进行伤害判定
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)] // 双端同时跑，各自算轨迹
    public partial class ZenithFlightSystem : PugSimulationSystemBase
    {
        private EntityQuery enemies;
        private ComponentLookup<ClientInput> inputLookup;
        private ComponentLookup<LocalTransform> transformLookup;
        private ComponentLookup<HealthCD> healthLookup;
        private ComponentLookup<EntityDestroyedCD> destroyLookup;

        protected override void OnCreate()
        {
            // 获取所有敌人的位置和血量，用于“索敌”
            enemies = GetEntityQuery(ComponentType.ReadOnly<EnemyCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<HealthCD>());
            inputLookup = SystemAPI.GetComponentLookup<ClientInput>(true);
            transformLookup = SystemAPI.GetComponentLookup<LocalTransform>();
            healthLookup = SystemAPI.GetComponentLookup<HealthCD>();
            destroyLookup = SystemAPI.GetComponentLookup<EntityDestroyedCD>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            // 获取当前世界的 EntityManager（用于直接修改血量）
            //var entityManager = state.EntityManager;

            // 按照你的习惯，局部捕获 Lookup，方便在 Lambda 中使用
            var inputLookup = this.inputLookup;
            var transformLookup = this.transformLookup;
            var healthLookup = this.healthLookup;
            var destroyLookup = this.destroyLookup;

            // 将敌人的组件转为 NativeArray，提高遍历性能（注意：在 Schedule 前分配，由系统保证 Job 安全）
            using var enemyTransforms = enemies.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var enemyHealth = enemies.ToComponentDataArray<HealthCD>(Allocator.Temp);

            float dt = SystemAPI.Time.DeltaTime;
            bool server = (World.Flags & WorldFlags.GameServer) != 0;

            // 遍历所有带有天顶剑轨道组件、轨迹缓冲区、并且还没死的实体
            Entities.ForEach((Entity entity, ref ZenithFlightCD flight, ref LocalTransform transform, ref ProjectileCD projectile,
                ref ObjectDataCD objectData, ref DynamicBuffer<ZenithTrail> trail, in OwnerReferenceCD owner) =>
            {
                Entity ownerEntity = owner.owner;
                if (!transformLookup.TryGetComponent(ownerEntity, out var ownerTransform))
                {
                    // 主人找不到了，服务器直接销毁弹幕
                    /*if (server)
                    {
                        health.health = 0;
                        healthLookup.GetRefRW(entity).ValueRW = health;
                    }*/
                    destroyLookup.SetComponentEnabled(entity, true);
                    return;
                }

                float2 playerPos = ownerTransform.Position.xz;

                // 参数还是空的。客户端等服务器的那一次同步，不自己采样鼠标。
                if (flight.initialized == 0)
                {
                    if (!server)
                        return;
                    if (!inputLookup.TryGetComponent(ownerEntity, out var input))
                        return;

                    // 原发射系统没了，剑序号、相位、左右都在这一次里定。
                    uint seed = (uint)entity.Index * 73856093u ^ (uint)(SystemAPI.Time.ElapsedTime * 1000.0);
                    if (seed == 0)
                        seed = 1;
                    Random random = Random.CreateFromIndex(seed);
                    int index = 0;
                    if (ZenithBalance.ShotsPerSwing > 1)
                        index = random.NextInt(1, ZenithBalance.SwordTypes);
                    int packed = ZenithBalance.PackVariation(index, random.NextInt(0, 256), random.NextInt(0, 2));
                    ZenithBalance.UnpackVariation(packed, out index, out float phase, out _);
                    flight.variation = packed;
                    objectData.variation = packed;

                    float2 aim = ZenithBalance.ClampAim(playerPos, input.mouseOrJoystickWorldPoint);
                    float2 vector = aim - playerPos;

                    // 序号 1、2 会在准星附近找敌人。2 一定加一圈抖动，1 只有没找到目标时才加。
                    if (index == 1 || index == 2)
                    {
                        bool homed = TryHome(aim, enemyTransforms, enemyHealth, out float2 home);
                        if (homed)
                            vector = home - playerPos;
                        if (index == 2 || !homed)
                            vector += new float2(math.cos(phase), math.sin(phase)) * (150f / 16f);
                    }

                    flight.velocity = vector * 0.5f;
                    flight.ellipse = math.lerp(-100f, 101f, phase / (math.PI * 2f)) / 16f;
                    flight.initialized = 1;
                }

                float2 velocity = flight.velocity;
                float ellipse = flight.ellipse;

                // 还原成像素距离，用来算“准星有多近”（越近转得越快）
                float distancePixels = math.length(velocity) * 2f * 16f;
                float closeness = Saturate((900f - distancePixels) / 900f);

                float age = flight.age;
                float2 pos = transform.Position.xz;
                float bladeAngle = 0f;
                bool alive = true;

                // 计算这一帧玩家移动了多少距离（用于拖尾跟随）
                float2 playerDelta = flight.hasTrailPlayer != 0 ? playerPos - flight.trailPlayer : float2.zero;

                // ==========================================
                // 2. 推进时间与计算位置
                // ==========================================

                // 一帧只走一步、只压一个轨迹点。末尾这个 0.5 是推进倍率，觉得快就调小。
                float ageStep = math.lerp(0.7f, 2f, closeness) * dt * 60f * 0.5f;
                age += ageStep;

                // 寿命到了，准备销毁
                if (age >= 120f)
                {
                    alive = false;
                }
                else
                {
                    // spin：0 到 1 代表前半段旋转进度（对应原版转完一圈）
                    float spin = Saturate(age / 60f);
                    float aimAngle = math.atan2(velocity.y, velocity.x); // 瞄准方向的弧度
                    float signX = velocity.x > 0f ? 1f : -1f;            // 朝右还是朝左

                    // 当前旋转角（泰拉 num7）
                    float sweep = math.PI + signX * spin * (math.PI * 2f);

                    // 半径：基础半径 + 后半段额外扩张 40 像素
                    float radius = math.length(velocity) + Saturate((spin - 0.5f) / 0.5f) * (40f / 16f);
                    if (radius < 60f / 16f)
                        radius = 60f / 16f; // 最短半径限制

                    // 计算椭圆上的点（乘以 ellipse 形成椭圆，再按 closeness 收缩）
                    float2 spun = new float2(math.cos(sweep), math.sin(sweep)) * new float2(radius, ellipse * math.lerp(2f, 1f, closeness));

                    // 椭圆中心：玩家位置 + 半程瞄准向量；再旋转到瞄准方向
                    float2 orbit = playerPos + velocity + Rotate(spun, aimAngle);

                    // 起手偏移：前半段有一股反向拉扯，转到一半时消失
                    float lead = 1f - Saturate(spin / 0.5f);
                    pos = orbit + lead * new float2(signX * -radius * 0.1f, -ellipse * 0.3f);

                    // 剑身朝向：如果是负椭圆反向转，则反转旋转方向
                    bladeAngle = ellipse < 0f ? math.PI - signX * spin * (math.PI * 2f) + aimAngle : sweep + aimAngle;

                    // 写入轨迹缓冲：让图形能够读出这条华丽的螺旋线
                    PushTrail(trail, new float3(pos.x, transform.Position.y, pos.y), bladeAngle, spin, playerDelta, true);
                }

                // 写回组件数据
                flight.age = age;
                flight.trailPlayer = playerPos;
                flight.hasTrailPlayer = 1;

                // 如果寿命到了，服务器负责结束它的生命
                if (!alive)
                {
                    /*if (server)
                    {
                        health.health = 0;
                        healthLookup.GetRefRW(entity).ValueRW = health;
                    }*/
                    destroyLookup.SetComponentEnabled(entity, true);
                    return;
                }

                // 强行覆盖实体的位置（因为已经自己算好了，不需要物理引擎移动）
                transform.Position = new float3(pos.x, transform.Position.y, pos.y);
                // 设置朝向，让预制件贴图转过去
                projectile.SetDirection(new float3(math.cos(bladeAngle), 0f, math.sin(bladeAngle)));

            }).Schedule();

            base.OnUpdate();
        }

        // 对照 TrailingMode 4：旧点先整体后移，第一步再加玩家这一拍的地面位移，最新位置写在下标 0。
        private static void PushTrail(DynamicBuffer<ZenithTrail> trail, float3 position, float angle, float spin, float2 playerDelta, bool applyPlayerDelta)
        {
            if (trail.Length < ZenithTrail.Capacity)
                trail.Add(default);

            // 数组后移操作：从后往前遍历，把前一个点的数据赋给后一个点
            for (int i = trail.Length - 1; i > 0; i--)
            {
                ZenithTrail point = trail[i - 1];
                // 如果玩家移动了，历史轨迹也要跟着平移，否则残影会被留在原地扯断
                if (applyPlayerDelta)
                    point.position += new float3(playerDelta.x, 0f, playerDelta.y);
                trail[i] = point;
            }

            // 最新计算出的位置放在 0 号位
            trail[0] = new ZenithTrail { position = position, angle = angle, spin = spin };
        }

        // 准星周围 25 格内最近的活着的敌人。对照 GetZenithTarget。
        private static bool TryHome(float2 aim, NativeArray<LocalTransform> transforms, NativeArray<HealthCD> healths, out float2 home)
        {
            float best = ZenithBalance.HomeRadius * ZenithBalance.HomeRadius; // 半径平方，避免开方
            home = aim;
            bool found = false;

            for (int i = 0; i < transforms.Length; i++)
            {
                if (healths[i].health <= 0)
                    continue; // 忽略死掉的敌人

                float2 point = transforms[i].Position.xz;
                float distance = math.lengthsq(point - aim);
                if (distance >= best)
                    continue; // 离得比当前最近的还远

                best = distance;
                home = point;
                found = true;
            }
            return found;
        }

        // 数值钳制到 0~1
        private static float Saturate(float value)
        {
            return math.saturate(value);
        }

        // 2D 向量旋转（绕原点）
        private static float2 Rotate(float2 value, float angle)
        {
            math.sincos(angle, out float s, out float c);
            return new float2(c * value.x - s * value.y, s * value.x + c * value.y);
        }
    }
}