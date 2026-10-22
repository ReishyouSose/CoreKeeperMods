using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
using Pug.UnityExtensions;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.Graphic
{
    public class ZenithSwordGraphic : Projectile
    {
        public MeshFilter Ribbon;               // 刀光网格
        public List<ZenithAfterImage> trails;     // 残影列表


        public MeshRenderer ribbonRenderer;
        public Color PrimaryColor;
        public Sprite PrimarySprite;
        public float2 PrimaryOffset;
        private int trailCount;
        private int oldVari;
        private Color currentColor;

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Color[] _colors;
        private Vector2[] _uvs;
        private int[] _triangles;

        private List<float2> _tempPositions = new(512);
        private List<float> _tempProgress = new(512);
        private List<float2> _tempDirs = new(512);      // ★ 与 _tempPositions 同索引的剑身方向（单位向量）

        private List<float2> _rawPositions = new(128);
        private List<float> _rawTimers = new(128);
        private List<int> _rawToTemp = new(128);

        private List<int> _trailMarks = new(4);
        protected override void OnSpawn()
        {
            base.OnSpawn();
            EnsureMesh();
            HideTrail();
            oldVari = -2;
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
            foreach (var trail in trails)
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
            CheckSlash(vari);
            ReadOrbit();
            BuildRibbonAndTrail(orbit);
        }

        private void EnsureMesh()
        {
            if (Ribbon == null)
                return;
            if (_mesh != null)
                return;

            _mesh = new Mesh();
            _mesh.name = "ZenithBladeLight";
            _mesh.MarkDynamic();
            Ribbon.sharedMesh = _mesh;

            int maxCapacity = 1024;
            _vertices = new Vector3[maxCapacity * 2];
            _colors = new Color[maxCapacity * 2];
            _uvs = new Vector2[maxCapacity * 2];
            _triangles = new int[(maxCapacity - 1) * 12];
        }

        private void HideTrail()
        {
            if (trails == null)
                return;
            foreach (var sr in trails)
                sr.gameObject.SetActive(false);
        }

        private void ReadOrbit()
        {
            var manager = world.EntityManager;
            if (!manager.Exists(entity) || !manager.HasBuffer<ZenithTrail>(entity))
                return;

            var zenithTrail = manager.GetBuffer<ZenithTrail>(entity, true);
            trailCount = zenithTrail.Length;

            _tempPositions.Clear();
            _tempProgress.Clear();
            _tempDirs.Clear();                              // ★
            _rawPositions.Clear();
            _rawTimers.Clear();
            _rawToTemp.Clear();
            _trailMarks.Clear();

            if (trailCount < 2)
                return;

            int update = ZenithBalance.UpdateCount;

            for (int i = trailCount - 1; i >= 0; i--)
            {
                var current = zenithTrail[i];
                float2 pos = current.Pos;
                float timer = current.Progress;
                float2 dir = current.Dir;                   // ★ 读方向

                _rawPositions.Add(pos);
                _rawTimers.Add(timer);
                _rawToTemp.Add(_tempPositions.Count);

                _tempPositions.Add(pos);
                _tempProgress.Add(timer);
                _tempDirs.Add(dir);                         // ★

                if (i == 0)
                    continue;

                var older = zenithTrail[i - 1];
                float2 nextPos = older.Pos;
                float2 nextDir = older.Dir;                 // ★
                float dist = math.length(nextPos - pos);

                if (dist >= 0.001f)
                {
                    float2 dir1 = (i < trailCount - 1)
                        ? math.normalizesafe(pos - zenithTrail[i + 1].Pos, new float2(1, 0))
                        : math.normalizesafe(nextPos - pos, new float2(1, 0));
                    float2 dir2 = math.normalizesafe(nextPos - pos, new float2(1, 0));
                    float angleDiff = Vector2.Angle(dir1, dir2) * Mathf.Deg2Rad;

                    float2 p0 = (i < trailCount - 1) ? zenithTrail[i + 1].Pos : pos + (pos - nextPos);
                    float2 p3 = (i - 1 >= 0) ? zenithTrail[i - 1].Pos : nextPos + (nextPos - pos);

                    int steps = Mathf.CeilToInt(angleDiff / (Mathf.PI / 12f));
                    if (steps > 1)
                    {
                        for (int s = 1; s < steps; s++)
                        {
                            float t = s / (float)steps;
                            float2 interpPos = CatmullRom(p0, pos, nextPos, p3, t);
                            _tempPositions.Add(interpPos);
                            _tempProgress.Add(Mathf.Lerp(timer, older.Progress, t));
                            // ★ 方向向量线性插值后归一化（不是 LerpAngle）
                            _tempDirs.Add(math.normalizesafe(math.lerp(dir, nextDir, t), dir));
                        }
                    }
                }
            }

            int stride = Mathf.Max(1, update * 5);
            for (int k = 0; k < _rawPositions.Count && _trailMarks.Count < trails.Count; k += stride)
            {
                _trailMarks.Add(k);
            }
        }
        private void BuildRibbonAndTrail(ZenithOrbitCD orbit)
        {
            if (_mesh == null || _tempPositions.Count < 2)
                return;

            int count = 0;
            float halfWidth = 0.75f;
            float2 offset = ZenithBalance.GlobalOffset;
            int nextTrailIndex = 0;
            int nextRawIndex = _trailMarks.Count > 0 ? _trailMarks[0] : -1;
            int killTime = ZenithBalance.UpdateCount * ZenithBalance.TimeLeft;

            float fix = Mathf.Clamp01(orbit.Timer / killTime);
            fix = Mathf.Clamp01((1f - fix) / 0.25f);
            bool reverse = orbit.ShortAxis < 0;

            int sectionStartIndex = 0;
            int nextSectionEndIndex = (nextRawIndex >= 0) ? _rawToTemp[nextRawIndex] : _tempPositions.Count - 1;
            float invLast = 1f / (_tempPositions.Count - 1);
            float opacityPerTrail = trails.Count > 0 ? 1f / trails.Count : 0f;

            for (int i = 0; i < _tempPositions.Count; i++)
            {
                float2 center = _tempPositions[i];
                float progress = _tempProgress[i];
                float2 dir = _tempDirs[i];

                float2 normal = dir;

                float fadeIn = Mathf.Clamp01(progress / 0.2f);
                float fadeOut = Mathf.Clamp01((0.6f - progress) / 0.1f);
                float baseAlpha = fadeIn * fadeOut * fix;

                bool isBoundary = (nextRawIndex >= 0 && i == _rawToTemp[nextRawIndex]);

                if (isBoundary)
                {
                    if (trails != null && nextTrailIndex < trails.Count)
                    {
                        var trail = trails[nextTrailIndex];
                        if (baseAlpha <= 0.01f)
                        {
                            trail.gameObject.SetActive(false);
                        }
                        else
                        {
                            trail.gameObject.SetActive(true);
                            SpriteRenderer sr = trail.Sprite;
                            sr.flipX = reverse;
                            float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90;

                            // 修正位置和旋转
                            trail.transform.SetLocalPositionAndRotation(
                                new Vector3(center.x, ZenithBalance.GlobalOffset.y, center.y),
                                Quaternion.Euler(0, 0, angleDeg));

                            float trailAlpha = 1.0f - (nextTrailIndex * opacityPerTrail);
                            Color finalTrailColor = new Color(1, 1, 1, baseAlpha * trailAlpha);
                            sr.color = finalTrailColor;

                            // ★★★ 核心修改：处理星光 ★★★
                            if (trail.StarSparkle != null)
                            {
                                // 1. 设定颜色（受基础透明度和残影衰减影响）
                                Color starColor = currentColor; // 使用当前刀光的颜色
                                starColor.a = finalTrailColor.a;
                                trail.StarSparkle.Set(starColor);

                                // 2. 设定脉冲缩放
                                // 原版效果：星星有一个先变大再变小的过程，我们利用 progress 来模拟
                                // 假设 progress 0->1 是一把剑的寿命。
                                // 我们让它在 0.2 之前变大，0.6 之后缩小。
                                float starScale = Mathf.Clamp01(progress / 0.2f) * Mathf.Clamp01((0.8f - progress) / 0.2f);
                                starScale = Mathf.Lerp(0.5f, 1.5f, starScale) / 8f; // 最小 0.5，最大 1.5 倍缩放

                                trail.StarSparkle.transform.localScale = new float3(starScale, starScale, 1);
                            }
                        }
                    }
                    nextTrailIndex++;
                    nextRawIndex = (nextTrailIndex < _trailMarks.Count) ? _trailMarks[nextTrailIndex] : -1;

                    sectionStartIndex = i;
                    nextSectionEndIndex = (nextRawIndex >= 0) ? _rawToTemp[nextRawIndex] : _tempPositions.Count - 1;
                }

                // 处理网格（保持原样）
                int sectionLength = nextSectionEndIndex - sectionStartIndex;
                if (sectionLength <= 0)
                    sectionLength = 1;
                float progressRatio = (i - sectionStartIndex) / (float)sectionLength;
                float localProgress = progressRatio;
                if (i == _tempPositions.Count - 1)
                    localProgress = 0f;
                localProgress = Mathf.Clamp01(localProgress);

                center += offset;
                float t = i * invLast;
                float globalFade = 1f - math.smoothstep(0.2f, 1.0f, t);
                float finalBaseAlpha = globalFade * baseAlpha;

                float2 a2 = center - 2f * halfWidth * normal;
                float2 b2 = center + 1f * halfWidth * normal;

                if (isBoundary && i > 0)
                {
                    // ... 边界断点逻辑保持不变 ...
                    int v0 = count * 2;
                    _vertices[v0] = a2.X0Y();
                    _vertices[v0 + 1] = b2.X0Y();
                    _uvs[v0] = new Vector2(1f, 1f);
                    _uvs[v0 + 1] = new Vector2(1f, 0f);
                    Color c0 = currentColor;
                    c0.a = 0f;
                    _colors[v0] = c0;
                    _colors[v0 + 1] = c0;
                    count++;

                    int v1 = count * 2;
                    _vertices[v1] = a2.X0Y();
                    _vertices[v1 + 1] = b2.X0Y();
                    _uvs[v1] = new Vector2(0f, 1f);
                    _uvs[v1 + 1] = new Vector2(0f, 0f);
                    Color c1 = currentColor;
                    c1.a = finalBaseAlpha;
                    _colors[v1] = c1;
                    _colors[v1 + 1] = c1;
                    count++;
                }
                else
                {
                    int v = count * 2;
                    _vertices[v] = a2.X0Y();
                    _vertices[v + 1] = b2.X0Y();
                    _uvs[v] = new Vector2(localProgress, 1f);
                    _uvs[v + 1] = new Vector2(localProgress, 0f);
                    Color c = currentColor;
                    c.a = localProgress * finalBaseAlpha;
                    _colors[v] = c;
                    _colors[v + 1] = c;
                    count++;
                }
            }

            for (int i = nextTrailIndex; i < trails.Count; i++)
            {
                if (trails[i] != null)
                    trails[i].enabled = false;
            }

            // ... 生成网格索引和 Mesh 刷新逻辑保持不变 ...
            int tri = 0;
            for (int i = 0; i < count - 1; i++)
            {
                int v = i * 2;
                _triangles[tri++] = v;
                _triangles[tri++] = v + 1;
                _triangles[tri++] = v + 2;
                _triangles[tri++] = v + 2;
                _triangles[tri++] = v + 1;
                _triangles[tri++] = v + 3;
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices, 0, count * 2);
            _mesh.SetColors(_colors, 0, count * 2);
            _mesh.SetUVs(0, _uvs, 0, count * 2);
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