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
        public List<SpriteRenderer> trails;     // 残影列表


        public MeshRenderer ribbonRenderer;
        public Color PrimaryColor;
        public Sprite PrimarySprite;
        private int trailCount;
        private int oldVari;
        private Color currentColor;

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Color[] _colors;
        private Vector2[] _uvs; // 改回 Vector2
        private int[] _triangles;

        private List<float2> _tempPositions = new(512);
        private List<float> _tempTimers = new(512);

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
            if (vari < 0)
            {
                target = PrimarySprite;
                currentColor = PrimaryColor;
            }
            else
            {
                var slash = ZenithBalance.Slashs[vari];
                target = slash.Sprite;
                currentColor = slash.Color;
            }
            foreach (var trail in trails)
            {
                trail.sprite = target;
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
            _uvs = new Vector2[maxCapacity * 2]; // 初始化 Vector2 数组
            _triangles = new int[(maxCapacity - 1) * 12];
        }

        private void HideTrail()
        {
            if (trails == null)
                return;
            foreach (var sr in trails)
            { if (sr != null) sr.enabled = false; }
        }

        private void ReadOrbit()
        {
            var manager = world.EntityManager;
            if (!manager.Exists(entity) || !manager.HasBuffer<ZenithTrail>(entity))
                return;

            var zenithTrail = manager.GetBuffer<ZenithTrail>(entity, true);
            trailCount = zenithTrail.Length;

            _tempPositions.Clear();
            _tempTimers.Clear();
            _rawPositions.Clear();
            _rawTimers.Clear();
            _rawToTemp.Clear();
            _trailMarks.Clear();

            if (trailCount < 2)
                return;

            int update = ZenithBalance.ExtraUpdates;

            for (int i = trailCount - 1; i >= 0; i--)
            {
                var current = zenithTrail[i];
                float2 pos = current.Pos;
                float timer = current.Progress;

                _rawPositions.Add(pos);
                _rawTimers.Add(timer);
                _rawToTemp.Add(_tempPositions.Count);

                _tempPositions.Add(pos);
                _tempTimers.Add(timer);

                if (i == 0)
                    continue;

                var older = zenithTrail[i - 1];
                float2 nextPos = older.Pos;
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
                            _tempTimers.Add(Mathf.Lerp(timer, older.Progress, t));
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
            int killTime = ZenithBalance.ExtraUpdates * ZenithBalance.TimeLeft;
            float fix = Mathf.Clamp01(orbit.Timer / killTime);
            fix = Mathf.Clamp01((1f - fix) / 0.25f);
            bool reverse = orbit.ShortAxis < 0;
            int side = reverse ? -1 : 1;
            // 记录当前残影段的起始点索引
            int sectionStartIndex = 0;
            // 记录下一个残影段的结束点（即遇到下一个残影点的索引）
            int nextSectionEndIndex = (nextRawIndex >= 0) ? _rawToTemp[nextRawIndex] : _tempPositions.Count - 1;
            float invLast = 1f / (_tempPositions.Count - 1);
            float opacityPerTrail = 1f / trails.Count;
            for (int i = 0; i < _tempPositions.Count; i++)
            {
                float2 center = _tempPositions[i];
                float timer = _tempTimers[i];

                // ---- 0. 先计算切线方向 dir（残影和 ribbon 共用） ----
                float2 dir;
                if (i == 0)
                    dir = _tempPositions[1] - _tempPositions[0];
                else if (i == _tempPositions.Count - 1)
                    dir = _tempPositions[i] - _tempPositions[i - 1];
                else
                    dir = _tempPositions[i + 1] - _tempPositions[i - 1];

                dir = math.normalizesafe(dir, new float2(1, 0));
                float2 normal = new(dir.y, -dir.x);
                float fakeFrame = timer * 60;
                float fadeIn = Mathf.Clamp01(fakeFrame / 20f);
                float fadeOut = Mathf.Clamp01((60 - fakeFrame) / 8f);
                float baseAlpha = fadeIn * fadeOut * fix;

                // ---- 1. 残影（用切线方向代替 _tempRots） ----
                if (nextRawIndex >= 0 && i == _rawToTemp[nextRawIndex])
                {
                    if (trails != null && nextTrailIndex < trails.Count)
                    {
                        SpriteRenderer sr = trails[nextTrailIndex];
                        if (sr != null)
                        {
                            sr.flipX = reverse;
                            if (baseAlpha <= 0.01f)
                                sr.enabled = false;
                            else
                            {
                                sr.enabled = true;

                                // ★ 用该点处 ribbon 的切线方向来旋转残影
                                float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                                if (reverse)
                                    angleDeg += 180f;

                                sr.transform.SetLocalPositionAndRotation(
                                    new Vector3(center.x, ZenithBalance.GlobalOffset.y, center.y),
                                    Quaternion.Euler(0, 0, angleDeg));

                                float trailAlpha = 1.0f - (nextTrailIndex * opacityPerTrail);
                                sr.color = new Color(1, 1, 1, baseAlpha * trailAlpha);
                            }
                        }
                    }
                    nextTrailIndex++;
                    nextRawIndex = (nextTrailIndex < _trailMarks.Count) ? _trailMarks[nextTrailIndex] : -1;

                    // 【核心】命中新的残影点，这一段重置！
                    sectionStartIndex = i;
                    nextSectionEndIndex = (nextRawIndex >= 0) ? _rawToTemp[nextRawIndex] : _tempPositions.Count - 1;
                }

                // ---- 2. 计算段内进度 ----
                int sectionLength = nextSectionEndIndex - sectionStartIndex;
                if (sectionLength <= 0)
                    sectionLength = 1;

                float progressRatio = (i - sectionStartIndex) / (float)sectionLength;
                float localProgress = progressRatio;

                if (i == _tempPositions.Count - 1)
                    localProgress = 0f;

                localProgress = Mathf.Clamp01(localProgress);

                // ---- 3. 计算顶点（dir / normal 已在第 0 步算好） ----
                center += offset;

                float2 a2 = center + (2f * side) * halfWidth * normal;
                float2 b2 = center + (-1f * side) * halfWidth * normal;
                int v = count * 2;

                _vertices[v] = a2.X0Y();
                _vertices[v + 1] = b2.X0Y();

                // ---- 4. 写入 UV 和 颜色 ----
                _uvs[v] = new Vector2(localProgress, 1f);
                _uvs[v + 1] = new Vector2(localProgress, 0f);

                Color c = currentColor;
                c.a = localProgress;
                float t = i * invLast;
                float globalFade = 1f - math.smoothstep(0.2f, 1.0f, t);
                c.a *= globalFade * baseAlpha;

                _colors[v] = c;
                _colors[v + 1] = c;

                count++;
            }

            for (int i = nextTrailIndex; i < trails.Count; i++)
            {
                if (trails[i] != null)
                    trails[i].enabled = false;
            }

            // ---- 5. 生成网格索引 ----
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
                _triangles[tri++] = v + 2;
                _triangles[tri++] = v + 1;
                _triangles[tri++] = v;
                _triangles[tri++] = v + 2;
                _triangles[tri++] = v + 3;
                _triangles[tri++] = v + 1;
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices, 0, count * 2);
            _mesh.SetColors(_colors, 0, count * 2);
            _mesh.SetUVs(0, _uvs, 0, count * 2);
            _mesh.SetTriangles(_triangles, 0, (count - 1) * 12, 0, true, 0);
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