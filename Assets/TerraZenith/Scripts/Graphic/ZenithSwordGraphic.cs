using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
using Pug.Sprite;
using Pug.UnityExtensions;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Rng = UnityEngine.Random;

namespace Assets.TerraZenith.Scripts.Graphic
{
    public class ZenithSwordGraphic : Projectile
    {
        public MeshFilter Ribbon;               // 刀光网格
        public List<ZenithAfterImage> Trails;     // 残影列表
        public List<ZenithDust> Dusts;
        public ZenithStarSparkle Star;
        public SpriteObject Light;
        public List<SpriteRenderer> Colliders;
        public Color PrimaryColor;
        public Sprite PrimarySprite;
        public float2 PrimaryOffset;

        private int oldVari;
        private Color currentColor;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Color[] _colors;
        private Vector2[] _uvs;
        private readonly int[] trailTargets = new int[ZenithBalance.TrailCount];
        private readonly int[] trailTargets_simulate = new int[ZenithBalance.TrailCount + 1];
        private int[] _triangles;
        private float progress;
        private float killTime;
        private int totalUpdate;
        private ZenithOrbitCD orbit;
        private float nearFix;
        private int frameRate;
        private readonly List<ZenithTrailData> olds = new();
        private bool end;
        private float opacity;
        private int tickRate;
        protected override void OnSpawn()
        {
            base.OnSpawn();
            tickRate = PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            EnsureMesh();
            HideTrail();
            olds.Clear();
            Dusts.ForEach(d => d.gameObject.SetActive(false));
            oldVari = -2;
            progress = 0;
            end = false;
            var manager = world.EntityManager;
            killTime = manager.GetComponentData<LifeProgressCD>(entity).Max;
            var vari = manager.GetComponentData<ObjectDataCD>(entity).variation - 1;
            orbit = manager.GetComponentData<ZenithOrbitCD>(entity);
            totalUpdate = manager.GetComponentData<ExtraUpdateCD>(entity).TotalUpdate;
            nearFix = ZenithBalance.NearFix(orbit.LongAxis);
            CheckSlash(vari);
            Light.gameObject.SetActive(false);
            Light.emissiveColor = Color.white;
        }
        public override void ManagedLateUpdate()
        {
            base.ManagedLateUpdate();
            //DebugCollider(world.EntityManager.GetComponentData<ZenithOrbitCD>(entity), world.EntityManager.GetBuffer<ZenithTrail>(entity));
            if (end)
                return;
            SimulateOrbit(orbit);
            HandleVisual(orbit);
        }

        private void SimulateOrbit(ZenithOrbitCD orbit)
        {
            float step = Time.deltaTime * nearFix;
            int updateCount = totalUpdate;
            while (updateCount-- > 0)
            {
                progress += step;
                ZenithBalance.CalculateOrbit(orbit, progress, killTime, totalUpdate, out float lifeRatio, out float ellipse, out float2 basePos, out float2 relativePos);
                opacity = math.clamp(lifeRatio / 0.05f, 0f, 1f) * math.clamp((1f - lifeRatio) / 0.05f, 0f, 1f);
                if (lifeRatio >= 1)
                {
                    HideTrail();
                    end = true;
                    return;
                }
                olds.Add(new ZenithTrailData { RelativePos = relativePos, Rot = ZenithBalance.Rotate(basePos, orbit.Rotation), LifeRatio = lifeRatio, Ellipse = ellipse });
            }
        }
        private float LifeFade(float lifeRatio)
            => math.clamp(lifeRatio / 0.166f, 0f, 1f) * math.clamp((0.566f - lifeRatio) / 0.066f, 0f, 1f);
        private void HandleVisual(ZenithOrbitCD orbit)
        {
            if (end)
                return;
            bool reverse = orbit.ShortAxis < 0;
            float halfWidth = 0.75f;
            int maxMeshPoints = math.min(olds.Count, frameRate);
            int meshVerticeCount = 0;
            int trailIndex = 0;
            int trailCount = ZenithBalance.TrailCount;
            for (int i = 0; i < maxMeshPoints; i++)
            {
                int index = olds.Count - 1 - i;
                var old = olds[index];
                var center = old.RelativePos;
                var dir = old.Rot;
                var ellipse = old.Ellipse;
                float3 a2 = (center - halfWidth * dir).X0Y();
                float3 b2 = (center + halfWidth * dir).X0Y();
                var lifeRatio = old.LifeRatio;
                if (trailIndex < trailCount && i == trailTargets[trailIndex])
                {
                    float lifeFade = LifeFade(lifeRatio);
                    float trailOpacity = (trailCount - trailIndex) / (float)trailCount;
                    Color trailColor = Color.white * trailOpacity * opacity * lifeFade;
                    while (Trails.Count <= trailIndex)
                    {
                        Trails.Add(Instantiate(Trails[0], Trails[0].transform.parent));
                    }
                    var trail = Trails[trailIndex++];
                    if (trailColor.a < 0.01f)
                    {
                        trail.gameObject.SetActive(false);
                    }
                    else
                    {
                        trail.gameObject.SetActive(true);
                        SpriteRenderer sr = trail.Sprite;
                        sr.flipX = reverse;
                        float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90;
                        trail.transform.SetLocalPositionAndRotation(center.X0Y(), Quaternion.Euler(0, 0, angleDeg));
                        sr.color = trailColor;
                    }
                    if (i == 0)
                    {
                        if (lifeFade < 0.01f)
                        {
                            Star.gameObject.SetActive(false);
                        }
                        else
                        {
                            Star.gameObject.SetActive(true);
                            Star.Set(currentColor, lifeFade, lifeRatio);
                            Star.transform.localPosition = b2;
                        }
                        if (ellipse >= 1)
                        {
                            Light.gameObject.SetActive(false);
                        }
                        else
                        {
                            Light.gameObject.SetActive(true);
                            Light.emissiveColor = currentColor * 0.5f;
                            Light.emissiveColor.a = opacity * 0.5f;
                            Light.transform.localPosition = new Vector3(center.x, 0, center.y);
                            if (Rng.value < 0.11f)
                            {
                                float density = 1f - math.abs(ellipse * 2f - 1f);
                                int baseCount = 1 + (int)(orbit.LongAxis * 2f / 7.5f);
                                int attempts = math.max(1, (int)(baseCount * density));
                                var vel = ZenithBalance.Rotate(dir, (reverse ? -1 : 1) * math.PI / 2);
                                for (int k = 0; k < attempts; k++)
                                {
                                    float randomOffset = Rng.Range(0.5f, 1f);
                                    float3 spawnPos = transform.position.ToFloat3() + (center + halfWidth * randomOffset * dir).X0Y();
                                    SpawnDust(spawnPos, vel, currentColor);
                                }
                            }
                        }
                    }
                }

                // 原版 progressOnStrip = i / (expectedVertexPairsAmount - 1) = i / 59
                float stripProgress = i / (float)(frameRate - 1);
                // 原版 StripColors：1 - GetLerpValue(0, 0.98, progress)
                float stripFade = 1f - math.clamp(stripProgress / 0.98f, 0f, 1f);
                Color c = currentColor * stripFade;
                c.a *= 0.5f;

                int v = meshVerticeCount * 2;
                if (v + 1 < _vertices.Length)
                {
                    _vertices[v] = a2;
                    _uvs[v] = new Vector2(stripProgress, 1f);
                    _colors[v] = c;

                    _vertices[v + 1] = b2;
                    _uvs[v + 1] = new Vector2(stripProgress, 0f);
                    _colors[v + 1] = c;

                    meshVerticeCount++;
                }
            }
            int tri = 0;
            for (int k = 0; k < meshVerticeCount - 1; k++)
            {
                int v = k * 2;
                _triangles[tri++] = v;
                _triangles[tri++] = v + 1;
                _triangles[tri++] = v + 2;
                _triangles[tri++] = v + 2;
                _triangles[tri++] = v + 1;
                _triangles[tri++] = v + 3;
            }

            int verticeCount = meshVerticeCount * 2;
            //_mesh.Clear();
            _mesh.SetVertices(_vertices, 0, verticeCount);
            _mesh.SetColors(_colors, 0, verticeCount);
            _mesh.SetUVs(0, _uvs, 0, verticeCount);
            _mesh.SetTriangles(_triangles, 0, tri, 0, false, 0);
            _mesh.RecalculateBounds();
            //Ribbon.sharedMesh = _mesh;
        }
        private void SpawnDust(float3 worldPos, float2 tangent, Color color)
        {
            // 1. 原版一次生成 1 到 3 个粒子 (Main.rand.Next(1, 4))
            int count = Rng.Range(1, 4);

            for (int j = 0; j < count; j++)
            {
                // 从对象池中找闲置的粒子
                int i = Dusts.FindIndex(0, x => !x.gameObject.activeSelf);
                if (i < 0)
                {
                    Dusts.Add(Instantiate(Dusts[0], Dusts[0].transform.parent));
                    i = Dusts.Count - 1;
                }
                ZenithDust dust = Dusts[i];
                dust.gameObject.SetActive(true);

                // 2. 颜色微调：原版是 Color.Lerp(trailColor, Color.White, rand * 0.3f)
                // 这会让粒子有一点泛白的高光感
                Color dustColor = Color.Lerp(color, Color.white, Rng.Range(0f, 0.3f));

                // 3. 速度：原版是沿切线方向，速度 3~7
                // 不要加随机乱飞的抖动 (randomJitter)，原版的粒子是顺着轨道飞的
                float speed = Rng.Range(3f, 7f)/* * 3*/;
                float2 velocity = tangent * speed;

                // 传给 Init
                dust.Init(worldPos, velocity, dustColor);

                // 4. (可选，根据你的 ZenithDust 组件) 
                // 原版的粒子缩放是 0.4，fadeIn 是 0.4~0.7
                // 如果你的 Init 没处理，你需要在这里或者 Init 里设置
                // dust.transform.localScale = Vector3.one * 0.4f; 
            }
        }

        private void EnsureMesh()
        {
            if (Ribbon == null)
                return;
            if (_mesh == null)
            {
                _mesh = new Mesh
                {
                    name = "ZenithBladeLight"
                };
                _mesh.MarkDynamic();
                Ribbon.sharedMesh = _mesh;
            }

            int maxCapacity = Manager.prefs.targetFrameRate;
            if (frameRate != maxCapacity)
            {
                _vertices = new Vector3[maxCapacity * 2];
                _colors = new Color[maxCapacity * 2];
                _uvs = new Vector2[maxCapacity * 2];
                _triangles = new int[(maxCapacity - 1) * 6];
                frameRate = maxCapacity;
                int j = 0;
                int count = ZenithBalance.TrailCount;
                for (int i = 0; i < maxCapacity; i++)
                {
                    float phase = i / (float)maxCapacity * count;
                    if (phase >= j)
                    {
                        trailTargets[j] = i;
                        j++;
                    }
                }
                j = 0;
                for (int i = 0; i < tickRate; i++)
                {
                    float phase = (i + 1) / (float)tickRate * count;
                    if (phase >= j)
                    {
                        trailTargets_simulate[j] = i;
                        j++;
                    }
                }
                //Debug.Log(tickRate);
            }
        }

        private void CheckSlash(int vari)
        {
            if (oldVari == vari)
                return;
            oldVari = vari;
            Sprite target;
            float2 offset;
            if (vari < 0)
            {
                target = PrimarySprite;
                currentColor = PrimaryColor;
                offset = PrimaryOffset;
            }
            else
            {
                var slash = ZenithBalance.Slashs[vari];
                target = slash.Sprite;
                currentColor = slash.Color;
                offset = slash.Offset;
            }
            foreach (var trail in Trails)
            {
                trail.Sprite.sprite = target;
                trail.Sprite.transform.localPosition = offset.X0Y();
            }
            Light.emissiveColor = currentColor * 0.5f;
            Light.emissiveColor.a = 1;
        }

        private void HideTrail()
        {
            for (int i = 0; i < Trails.Count; i++)
            {
                Trails[i].gameObject.SetActive(false);
            }
            Star.gameObject.SetActive(false);
            Light.gameObject.SetActive(false);
            foreach (var go in Colliders)
            {
                go.gameObject.SetActive(false);
            }
            _mesh.Clear();
        }
        private void DebugCollider(ZenithOrbitCD orbit, DynamicBuffer<ZenithTrail> trails)
        {
            var length = trails.Length;
            //Debug.Log("current life " + orbit.LifeRatio);
            var lifeRatio = orbit.LifeRatio;
            if (lifeRatio >= 1)
                return;
            //Debug.Log("current " + lifeRatio);
            for (int i = 0; i < trailTargets_simulate.Length; i++)
            {
                int p = trailTargets_simulate[i];
                int j = length - 1 - p;
                if (j < 0)
                    return;
                //Debug.Log(j);
                int old = ZenithBalance.GetOldIndex(j);
                if (old == 0)
                    return;
                var trail = trails[j];
                if (!ZenithBalance.CanAttack(trail.LifeRatio, p, tickRate))
                {
                    //Debug.Log("cant attack");
                    continue;
                }
                var perTrail = trails[old];
                while (Colliders.Count <= i)
                {
                    Colliders.Add(Instantiate(Colliders[0], Colliders[0].transform.parent));
                }
                var trailPos = trail.Pos;
                float2 prePos = perTrail.Pos;
                float2 castStart = prePos;
                var totar = trailPos - prePos;
                var dir = math.normalize(totar);
                var collider = Colliders[i];
                var color = currentColor;
                color.a = 0.5f;
                collider.color = color;
                collider.size = new(1.25f, math.length(totar));
                collider.gameObject.SetActive(true);
                float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90;
                collider.transform.SetLocalPositionAndRotation((castStart + totar / 2f).X0Y(), Quaternion.Euler(0, 0, angleDeg));
            }
        }

        private float2 CatmullRom(float2 p0, float2 p1, float2 p2, float2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }
    }
}