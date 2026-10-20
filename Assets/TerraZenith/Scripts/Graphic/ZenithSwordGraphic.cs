using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.Graphic
{
    public class ZenithSwordGraphic : Projectile
    {
        //public SpriteObject Blade;              // 剑身模型
        public MeshFilter Ribbon;               // 刀光网格
        public List<SpriteRenderer> trails;     // 残影列表

        private const int Split = 15;           // 原版残影间隔
        private int trailCount;

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Color[] _colors;
        private Vector2[] _uvs;
        private int[] _triangles;

        // 插值后的完整序列（用于网格 + 方向计算）
        private List<float2> _tempPositions = new List<float2>(256);
        private List<float> _tempTimers = new List<float>(256);

        // 原始记录点（不插值，用于残影位置 + 网格分段边界）
        private List<float2> _rawPositions = new List<float2>(64);
        private List<float> _rawTimers = new List<float>(64);
        // 原始点 -> _tempPositions 中的索引
        private List<int> _rawToTemp = new List<int>(64);

        // 残影标记：存的是 _rawPositions 的下标（每 stride 个原始点取一个）
        private List<int> _trailMarks = new List<int>(4);

        // 复用的映射数组，避免 BuildRibbon 每帧分配
        private int[] _markInTemp = new int[8];

        protected override void OnSpawn()
        {
            base.OnSpawn();
            EnsureMesh();
            HideTrail();
        }

        public override void ManagedLateUpdate()
        {
            base.ManagedLateUpdate();
            //transform.localPosition = Manager.main.player.WorldPosition;
            var orbit = world.EntityManager.GetComponentData<ZenithOrbitCD>(entity);
            if (orbit.Timer >= 2)
                return;
            ReadOrbit();
            BuildRibbon();
            PaintTrail();
        }

        private void EnsureMesh()
        {
            if (Ribbon == null || _mesh != null)
                return;

            _mesh = new Mesh();
            _mesh.name = "ZenithBladeLight";
            _mesh.MarkDynamic();
            Ribbon.sharedMesh = _mesh;

            int maxCapacity = 256;
            _vertices = new Vector3[maxCapacity * 2];
            _colors = new Color[maxCapacity * 2];
            _uvs = new Vector2[maxCapacity * 2];
            _triangles = new int[(maxCapacity - 1) * 12];
        }

        private void ClearMesh()
        {
            if (_mesh != null)
                _mesh.Clear();
            HideTrail();
        }

        private void ReadOrbit()
        {
            var manager = world.EntityManager;
            if (!manager.Exists(entity))
            {
                Debug.Log("entity not exists");
            }
            if (!manager.HasBuffer<ZenithTrail>(entity))
            {
                Debug.Log("no trail buffer");
                return;
            }
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

            // 倒序遍历：从最新（末尾）到最老（开头）
            for (int i = trailCount - 1; i >= 0; i--)
            {

                var current = zenithTrail[i];
                float2 pos = current.Pos;
                float timer = current.Timer;

                // ---- 记录原始点 ----
                _rawPositions.Add(pos);
                _rawTimers.Add(timer);
                // 记录该原始点在 _tempPositions 中的位置（在把原始点加入之前记录）
                _rawToTemp.Add(_tempPositions.Count);

                // ---- 把原始点也放进插值序列 ----
                _tempPositions.Add(pos);
                _tempTimers.Add(timer);
                if (i == 0)
                    continue;

                var older = zenithTrail[i - 1];
                float2 nextPos = older.Pos;
                float dist = math.length(nextPos - pos);

                if (dist >= 0.001f)
                {
                    // 用 float2 算方向夹角
                    float2 dir1 = (i < trailCount - 1)
                        ? math.normalizesafe(pos - zenithTrail[i + 1].Pos, new float2(1, 0))
                        : math.normalizesafe(nextPos - pos, new float2(1, 0));
                    float2 dir2 = math.normalizesafe(nextPos - pos, new float2(1, 0));
                    float angleDiff = Vector2.Angle(dir1, dir2) * Mathf.Deg2Rad;

                    float2 p0 = (i < trailCount - 1) ? zenithTrail[i + 1].Pos : pos + (pos - nextPos);
                    float2 p3 = (i - 1 >= 0) ? zenithTrail[i - 1].Pos : nextPos + (nextPos - pos);

                    int steps = Mathf.CeilToInt(angleDiff / (Mathf.PI / 24f));
                    if (steps > 1)
                    {
                        for (int s = 1; s < steps; s++)
                        {
                            float t = s / (float)steps;
                            float2 interpolatedPos = CatmullRom(p0, pos, nextPos, p3, t);
                            float interpolatedTimer = Mathf.Lerp(timer, older.Timer, t);

                            _tempPositions.Add(interpolatedPos);
                            _tempTimers.Add(interpolatedTimer);
                        }
                    }
                }
            }

            // ---- 所有点填完后，按间隔从原始点里挑残影位置 ----
            int stride = Mathf.Max(1, update * 8);
            for (int k = 0; k < _rawPositions.Count && _trailMarks.Count < 4; k += stride)
            {
                _trailMarks.Add(k);
            }
        }

        private void BuildRibbon()
        {
            if (_mesh == null || _tempPositions.Count < 2)
                return;

            // ---- 把 _trailMarks（原始点下标）映射到 _tempPositions（插值序列）下标 ----
            // 分段边界正好落在"每 N 次物理更新的原始记录点"上
            int markCount = _trailMarks.Count;
            if (_markInTemp.Length < markCount)
                _markInTemp = new int[markCount];

            for (int m = 0; m < markCount; m++)
            {
                int rawIndex = _trailMarks[m];
                if (rawIndex < _rawToTemp.Count)
                    _markInTemp[m] = _rawToTemp[rawIndex];
                else
                    _markInTemp[m] = _tempPositions.Count - 1;
            }

            int count = 0;
            float halfLength = 0.5f;

            int currentMarkIndex = 0;

            for (int i = 0; i < _tempPositions.Count; i++)
            {
                float2 center = _tempPositions[i];
                float timer = _tempTimers[i];

                // 前进到当前 i 所属的段边界
                while (currentMarkIndex < markCount && i >= _markInTemp[currentMarkIndex])
                {
                    currentMarkIndex++;
                }

                int segmentStart = (currentMarkIndex == 0) ? 0 : _markInTemp[currentMarkIndex - 1];
                int segmentEnd = (currentMarkIndex < markCount) ? _markInTemp[currentMarkIndex] : _tempPositions.Count - 1;

                float localProgress = 0f;
                if (segmentEnd > segmentStart)
                {
                    localProgress = (i - segmentStart) / (float)(segmentEnd - segmentStart);
                }

                // 用 float2 算法线
                float2 dir;
                if (i == 0)
                    dir = _tempPositions[1] - _tempPositions[0];
                else if (i == _tempPositions.Count - 1)
                    dir = _tempPositions[i] - _tempPositions[i - 1];
                else
                    dir = _tempPositions[i + 1] - _tempPositions[i - 1];

                dir = math.normalizesafe(dir, new float2(1, 0));
                float2 normal = new float2(dir.y, -dir.x);

                float2 a2 = center + normal * halfLength;
                float2 b2 = center - normal * halfLength;

                // 最后转成 Vector3 给网格
                Vector3 a = new Vector3(a2.x, 0f, a2.y);
                Vector3 b = new Vector3(b2.x, 0f, b2.y);

                int v = count * 2;
                _vertices[v] = a;
                _vertices[v + 1] = b;

                float globalProgress = i / (float)(_tempPositions.Count - 1);

                _uvs[v] = new Vector2(localProgress, 1f);
                _uvs[v + 1] = new Vector2(localProgress, 0f);

                Color c = Color.white;
                c.a *= 1f - globalProgress;
                c.a *= 1f - Mathf.InverseLerp(0.0f, 1.0f, localProgress);

                // Timer 是 0~2，算 spin 需要 /1
                float spin = Mathf.Clamp01(timer / 1f);
                c.a *= 1f - Mathf.InverseLerp(0.5f, 1f, spin);

                c.a *= 0.5f;

                _colors[v] = c;
                _colors[v + 1] = c;

                count++;
            }

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

        private void PaintTrail()
        {
            if (trails == null)
                return;

            for (int i = 0; i < trails.Count; i++)
            {
                SpriteRenderer sr = trails[i];
                if (sr == null)
                    continue;

                if (i >= _trailMarks.Count)
                {
                    sr.enabled = false;
                    continue;
                }

                int rawIndex = _trailMarks[i];          // 原始点下标
                if (rawIndex >= _rawPositions.Count)
                {
                    sr.enabled = false;
                    continue;
                }

                sr.enabled = true;

                // ---- 位置：直接取原始记录点 ----
                float2 pos2 = _rawPositions[rawIndex];
                sr.transform.localPosition = new Vector3(pos2.x, 0, pos2.y);

                // ---- 朝向：用插值后的邻点算（取原始点在 _tempPositions 中的前后各一点）----
                int tempIndex = _rawToTemp[rawIndex];
                int prevT = math.max(tempIndex - 1, 0);
                int nextT = math.min(tempIndex + 1, _tempPositions.Count - 1);

                float2 dir2 = math.normalizesafe(
                    _tempPositions[prevT] - _tempPositions[nextT],
                    new float2(1, 0));

                if (math.lengthsq(dir2) > 1e-6f)
                {
                    float angleRad = math.atan2(dir2.y, dir2.x);
                    sr.transform.localRotation = Quaternion.Euler(0, 0, angleRad * Mathf.Rad2Deg);
                }
                Debug.Log((sr.transform.position, sr.transform.rotation));
                // 否则保留上一帧 localRotation，不重置

                // ---- 透明度 ----
                float progress = rawIndex / (float)math.max(_rawPositions.Count - 1, 1);
                float alpha = Mathf.Lerp(1f, 0f, progress);
                sr.color = new Color(1, 1, 1, alpha);
            }
        }

        private void HideTrail()
        {
            if (trails == null)
                return;
            foreach (var sr in trails)
            {
                if (sr != null)
                    sr.enabled = false;
            }
        }

        // 【核心】：重构成 float2 的 Catmull-Rom
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