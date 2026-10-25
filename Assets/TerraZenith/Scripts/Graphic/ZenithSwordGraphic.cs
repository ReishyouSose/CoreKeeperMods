using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
using Pug.UnityExtensions;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.Graphic
{
    public class ZenithSwordGraphic : Projectile
    {
        public MeshFilter Ribbon;               // 刀光网格
        public List<ZenithAfterImage> Trails;     // 残影列表
        public List<ZenithStarSparkle> Stars;
        public List<SpriteRenderer> Colliders;


        public MeshRenderer ribbonRenderer;
        public Color PrimaryColor;
        public Sprite PrimarySprite;
        public float2 PrimaryOffset;
        private int oldVari;
        private Color currentColor;

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Color[] _colors;
        private Vector2[] _uvs;
        private int[] _triangles;
        private float timer;
        private const int TrailLength = 512;
        private readonly List<float2> oldPos = new(TrailLength);
        private readonly List<float2> oldRot = new(TrailLength);
        private readonly List<float> oldLife = new(TrailLength);
        private float oldLong;
        private float oldShort;
        protected override void OnSpawn()
        {
            base.OnSpawn();
            EnsureMesh();
            oldPos.Clear();
            oldRot.Clear();
            oldLife.Clear();
            HideTrail();
            oldVari = -2;
            timer = 0;
            var manager = world.EntityManager;
            var orbit = manager.GetComponentData<ZenithOrbitCD>(entity);
            var vari = manager.GetComponentData<ObjectDataCD>(entity).variation - 1;
            CheckSlash(vari);
            oldLong = orbit.LongAxis;
            oldShort = orbit.ShortAxis;
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
                trail.Mask.transform.localPosition = new(-offset.x, 0, -offset.y);
            }
        }
        public override void ManagedLateUpdate()
        {
            base.ManagedLateUpdate();
            var manager = world.EntityManager;
            var orbit = manager.GetComponentData<ZenithOrbitCD>(entity);
            var vari = manager.GetComponentData<ObjectDataCD>(entity).variation - 1;
            // CheckSlash(vari);
            SimulateOrbit(orbit, manager.GetBuffer<ZenithTrail>(entity));
            BuildRibbonAndTrail(orbit);
        }

        private void DebugCollider(DynamicBuffer<ZenithTrail> zenithTrails)
        {
            for (int i = 0; i < zenithTrails.Length; i++)
            {
                while (Colliders.Count <= i)
                {
                    Colliders.Add(Instantiate(Colliders[0], Colliders[0].transform.parent));
                }
                var collider = Colliders[i];
                collider.color = currentColor;
                collider.gameObject.SetActive(true);
                collider.transform.localPosition = zenithTrails[i].Pos.X0Y();
            }
        }

        private void EnsureMesh()
        {
            if (Ribbon == null)
                return;
            if (_mesh != null)
                return;

            _mesh = new Mesh
            {
                name = "ZenithBladeLight"
            };
            _mesh.MarkDynamic();
            Ribbon.sharedMesh = _mesh;

            int maxCapacity = TrailLength;
            _vertices = new Vector3[maxCapacity * 2];
            _colors = new Color[maxCapacity * 2];
            _uvs = new Vector2[maxCapacity * 2];
            _triangles = new int[(maxCapacity - 1) * 6];
        }

        private void HideTrail()
        {
            for (int i = 0; i < Trails.Count; i++)
            {
                Trails[i].gameObject.SetActive(false);
                Stars[i].gameObject.SetActive(false);
            }
            foreach (var go in Colliders)
            {
                go.gameObject.SetActive(false);
            }
        }

        private void SimulateOrbit(ZenithOrbitCD orbit, DynamicBuffer<ZenithTrail> zenithTrails)
        {
            float longAxis = orbit.LongAxis;
            float shortAxis = orbit.ShortAxis;
            if (longAxis != oldLong || shortAxis != oldShort)
            {
                Debug.Log($"跳变！ long:{oldLong} {longAxis}, short:{oldShort} {shortAxis} timer:{timer} ");
                oldLong = longAxis;
                oldShort = shortAxis;
            }
            float near = math.clamp(longAxis / 14f, 0, 1);
            float step = math.lerp(2f, 0.7f, near) * Time.deltaTime;
            int updateCount = ZenithBalance.UpdateCount;
            int killTime = ZenithBalance.TimeLeft * updateCount;
            float life = 0;
            if (timer >= killTime)
            {
                HideTrail();
                return;
            }
            DebugCollider(zenithTrails);
            for (int i = 0; i < updateCount; i++)
            {
                timer = math.min(timer + step, killTime);
                life = timer / killTime;

                //在一半的时间内就完成一圈，剩余的时间给拖尾
                float progress = math.clamp(life / 0.5f, 0f, 1f);
                float x = progress * math.PI * 2f;

                math.sincos(x, out var sin, out var cos);
                float2 basePos = new(cos, (shortAxis > 0 ? 1 : -1) * sin);
                float2 localPos = basePos * new float2(longAxis, math.abs(shortAxis));

                float lead = 1f - math.clamp(progress / 0.5f, 0f, 1f);
                float2 leadOffset = lead * new float2(-longAxis * 0.1f, shortAxis * 0.3f);
                localPos += leadOffset;

                float2 worldPos = ZenithBalance.Rotate(localPos, orbit.Rotation);
                oldPos.Add(worldPos);
                oldRot.Add(ZenithBalance.Rotate(basePos, orbit.Rotation));
                oldLife.Add(life);
                if (timer >= killTime)
                    return;
            }
        }
        private void BuildRibbonAndTrail(ZenithOrbitCD orbit)
        {
            int i = oldPos.Count - 1;
            if (i < 2)
                return;
            float halfWidth = 0.75f;
            int killTime = ZenithBalance.UpdateCount * ZenithBalance.TimeLeft;

            float fix = Mathf.Clamp01(timer / killTime);
            fix = Mathf.Clamp01((1f - fix) / 0.25f);
            bool reverse = orbit.ShortAxis < 0;

            int trailCount = ZenithBalance.TrailCount;
            float lifePerTrail = 1f / trailCount;
            float trailOpacity = 1 + lifePerTrail;
            int trailIndex = -1;
            int trailInterval;
            int trailStart = i;
            int trailEnd = 0;
            int j = 0;
            float nextInterval = oldLife[^1];
            int total = TrailLength * 2;
            while (i > 0)
            {
                float2 center = oldPos[i];
                float2 dir = oldRot[i];
                float life = oldLife[i];
                bool mark = false;
                if (life <= nextInterval)
                {
                    trailIndex++;
                    if (trailIndex >= trailCount)
                        break;
                    mark = true;
                    trailOpacity -= lifePerTrail;//模拟从1开始
                    trailStart = i;
                    nextInterval -= lifePerTrail;
                    bool find = false;
                    for (int k = i - 1; k > 0; k--)
                    {
                        if (oldLife[k] <= nextInterval)
                        {
                            trailEnd = k;
                            find = true;
                            break;
                        }
                    }
                    if (!find)
                        trailEnd = 0;
                }
                trailInterval = trailStart - trailEnd;
                if (trailInterval < 1)
                    break;
                float fadeIn = Mathf.Clamp01(life / 0.2f);
                float fadeOut = Mathf.Clamp01((0.6f - life) / 0.1f);
                float baseAlpha = fadeIn * fadeOut * fix;
                float trailProgress = 1 - (i - trailEnd) / (float)trailInterval;
                float currentOpacity = trailOpacity - math.lerp(lifePerTrail, 0, trailProgress);
                currentOpacity *= baseAlpha;
                Color c = currentColor;
                c.a = currentOpacity;
                int amount = 1;
                float3 a2 = (center - 2f * halfWidth * dir).X0Y();
                float3 b2 = (center + 1f * halfWidth * dir).X0Y();
                if (mark)
                {
                    amount = 2;
                    while (Trails.Count <= trailIndex)
                    {
                        Trails.Add(Instantiate(Trails[0], Trails[0].transform.parent));
                        Stars.Add(Instantiate(Stars[0], Stars[0].transform.parent));
                    }
                    var trail = Trails[trailIndex];
                    var star = Stars[trailIndex];
                    if (baseAlpha <= 0.01f)
                    {
                        trail.gameObject.SetActive(false);
                        star.gameObject.SetActive(false);
                    }
                    else
                    {
                        trail.gameObject.SetActive(true);
                        star.gameObject.SetActive(true);
                        SpriteRenderer sr = trail.Sprite;
                        sr.flipX = reverse;
                        float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90;

                        // 修正位置和旋转
                        trail.transform.SetLocalPositionAndRotation(
                            new Vector3(center.x, 0, center.y),
                            Quaternion.Euler(0, 0, angleDeg));

                        Color finalTrailColor = new(1, 1, 1, currentOpacity);
                        sr.color = finalTrailColor;

                        star.Set(c);

                        // 2. 设定脉冲缩放
                        // 原版效果：星星有一个先变大再变小的过程，我们利用 progress 来模拟
                        // 假设 progress 0->1 是一把剑的寿命。
                        // 我们让它在 0.2 之前变大，0.6 之后缩小。
                        float starScale = Mathf.Clamp01(life / 0.2f) * Mathf.Clamp01((0.8f - life) / 0.2f);
                        starScale = Mathf.Lerp(0.5f, 1.5f, starScale) / 8f; // 最小 0.5，最大 1.5 倍缩放
                        star.transform.localPosition = b2;
                        star.transform.localScale = new float3(starScale, starScale, 1);
                    }
                }

                for (int l = 0; l < amount; l++)
                {
                    if (l == 1)
                    {
                        c.a = 0;
                    }
                    int v = j * 2;
                    if (v + 1 < total)
                    {
                        _vertices[v] = a2;
                        _vertices[v + 1] = b2;
                        _uvs[v] = new Vector2(trailProgress, 1f);
                        _uvs[v + 1] = new Vector2(trailProgress, 0f);
                        _colors[v] = c;
                        _colors[v + 1] = c;
                        j++;
                    }
                }
                i--;
            }
            int tri = 0;
            for (int k = 0; k < j - 1; k++)
            {
                int v = k * 2;
                _triangles[tri++] = v;
                _triangles[tri++] = v + 1;
                _triangles[tri++] = v + 2;
                _triangles[tri++] = v + 2;
                _triangles[tri++] = v + 1;
                _triangles[tri++] = v + 3;
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices, 0, j * 2);
            _mesh.SetColors(_colors, 0, j * 2);
            _mesh.SetUVs(0, _uvs, 0, j * 2);
            _mesh.SetTriangles(_triangles, 0, tri, 0, true, 0);
            _mesh.RecalculateBounds();
            Ribbon.sharedMesh = _mesh;
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