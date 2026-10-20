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

        // 分段控制，决定每一节刀光有多少个插值点
        [Header("每节刀光的插值点数（越小越密集，越大越稀疏）")]
        public int segmentSize = 7;

        // 从 Sprite 中提取主色调
        [Header("拖入剑的 Sprite 用于提取主色")]
        public Sprite bladeSpriteToExtract;
        public MeshRenderer ribbonRenderer;

        private int trailCount;

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Color[] _colors;
        private Vector2[] _uvs; // 改回 Vector2
        private int[] _triangles;

        private List<float2> _tempPositions = new List<float2>(512);
        private List<float> _tempTimers = new List<float>(512);
        private List<float> _tempRots = new List<float>(512);

        private List<float2> _rawPositions = new List<float2>(128);
        private List<float> _rawTimers = new List<float>(128);
        private List<int> _rawToTemp = new List<int>(128);

        private List<int> _trailMarks = new List<int>(4);

        // 主色调缓存
        private Color _dominantColor = Color.white;
        private bool _hasExtractedColor = false;
        private static Dictionary<ObjectID, Color> bladeColors = new();

        protected override void OnSpawn()
        {
            base.OnSpawn();
            EnsureMesh();
            HideTrail();
        }

        public override void ManagedLateUpdate()
        {
            base.ManagedLateUpdate();
            if (!_hasExtractedColor)
            {
                TryExtractColor();
            }
            var manager = world.EntityManager;
            var orbit = manager.GetComponentData<ZenithOrbitCD>(entity);

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

        private void TryExtractColor()
        {
            var variation = objectData.variation;
            if (variation == 0 || _hasExtractedColor)
                return;

            ObjectID target = ZenithBalance.Blades[variation];
            if (bladeColors.TryGetValue(target, out var color))
            {
                _dominantColor = color;
                _hasExtractedColor = true;
                return;
            }

            if (!PugDatabase.TryGetObjectInfo(target, out var objInfo))
                return;
            bladeSpriteToExtract = objInfo.icon;
            if (bladeSpriteToExtract == null)
                return;

            Sprite bladeSprite = bladeSpriteToExtract;
            Texture2D tex = bladeSprite.texture;
            if (tex == null)
                return;

            Rect rect = bladeSprite.rect;
            int width = (int)rect.width;
            int height = (int)rect.height;

            // 借助 RenderTexture 读像素，绕过 Read/Write 限制
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D readableTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readableTex.ReadPixels(new Rect(rect.x, rect.y, rect.width, rect.height), 0, 0);
            readableTex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            Color[] pixels = readableTex.GetPixels();
            Object.Destroy(readableTex);

            float r = 0f, g = 0f, b = 0f;
            float totalWeight = 0f;

            foreach (var c in pixels)
            {
                if (c.a > 0.05f)
                {
                    // 过滤掉白色/灰色（白芯没有饱和度），只提取彩色的部分，防止颜色发灰
                    float saturation = Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b);
                    if (saturation > 0.1f)
                    {
                        float weight = c.a * saturation;
                        r += c.r * weight;
                        g += c.g * weight;
                        b += c.b * weight;
                        totalWeight += weight;
                    }
                }
            }

            // 如果图里全是黑白，没有彩色，那就走保底（直接取平均值）
            if (totalWeight <= 0.01f)
            {
                foreach (var c in pixels)
                {
                    if (c.a > 0.05f)
                    {
                        r += c.r * c.a;
                        g += c.g * c.a;
                        b += c.b * c.a;
                        totalWeight += c.a;
                    }
                }
            }

            if (totalWeight > 0.001f)
            {
                // 1. 先得到纯净的平均色
                _dominantColor = new Color(r / totalWeight, g / totalWeight, b / totalWeight, 1f);

                // 2. 将 RGB 转换为 HSV，直接拉高饱和度和亮度，让颜色更“纯”更“亮”
                Color.RGBToHSV(_dominantColor, out float h, out float s, out float v);

                // 提高饱和度（比如增加 1.5 倍），让它更鲜艳，不发灰
                s = 0.9f;
                // 提高亮度（比如增加 1.2 倍），让它发光
                v = 1;

                // 转回 RGB
                _dominantColor = Color.HSVToRGB(h, s, v);
                _dominantColor.a = 1f;

                bladeColors.Add(target, _dominantColor);
                _hasExtractedColor = true;

                foreach (var sr in trails)
                {
                    if (sr != null)
                        sr.sprite = bladeSprite;
                }
            }
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
            _tempRots.Clear();
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
                float rot = math.atan2(pos.y, pos.x);

                _rawPositions.Add(pos);
                _rawTimers.Add(timer);
                _rawToTemp.Add(_tempPositions.Count);

                _tempPositions.Add(pos);
                _tempTimers.Add(timer);
                _tempRots.Add(rot);

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

                    int steps = Mathf.CeilToInt(angleDiff / (Mathf.PI / 24f));
                    if (steps > 1)
                    {
                        for (int s = 1; s < steps; s++)
                        {
                            float t = s / (float)steps;
                            float2 interpPos = CatmullRom(p0, pos, nextPos, p3, t);
                            _tempPositions.Add(interpPos);
                            _tempTimers.Add(Mathf.Lerp(timer, older.Progress, t));
                            _tempRots.Add(math.atan2(interpPos.y, interpPos.x));
                        }
                    }
                }
            }

            int stride = Mathf.Max(1, update * 7);
            for (int k = 0; k < _rawPositions.Count && _trailMarks.Count < 4; k += stride)
            {
                _trailMarks.Add(k);
            }
        }
        private void BuildRibbonAndTrail(ZenithOrbitCD orbit)
        {
            if (_mesh == null || _tempPositions.Count < 2)
                return;

            int count = 0;
            float halfWidth = 0.5f;
            float2 offset = /*orbit.Offset*/ new float2(0, 0.5f);
            int nextTrailIndex = 0;
            int nextRawIndex = _trailMarks.Count > 0 ? _trailMarks[0] : -1;
            int killTime = ZenithBalance.ExtraUpdates * ZenithBalance.TimeLeft;
            float fix = Mathf.Clamp01(orbit.Timer / killTime);
            fix = Mathf.Clamp01((1f - fix) / 0.05f);
            bool reverse = orbit.ShortAxis < 0;
            // 记录当前残影段的起始点索引
            int sectionStartIndex = 0;
            // 记录下一个残影段的结束点（即遇到下一个残影点的索引）
            int nextSectionEndIndex = (nextRawIndex >= 0) ? _rawToTemp[nextRawIndex] : _tempPositions.Count - 1;

            for (int i = 0; i < _tempPositions.Count; i++)
            {
                float2 center = _tempPositions[i];
                float timer = _tempTimers[i];
                float rot = _tempRots[i];

                // ---- 1. 残影（保持你原版逻辑） ----
                if (nextRawIndex >= 0 && i == _rawToTemp[nextRawIndex])
                {
                    if (trails != null && nextTrailIndex < trails.Count)
                    {
                        SpriteRenderer sr = trails[nextTrailIndex];
                        if (sr != null)
                        {
                            float fakeFrame = timer * 60;
                            float fadeIn = Mathf.Clamp01(fakeFrame / 20f);
                            float fadeOut = Mathf.Clamp01((68f - fakeFrame) / 8f);
                            float baseAlpha = fadeIn * fadeOut * fix;

                            if (baseAlpha <= 0.01f)
                            { sr.enabled = false; }
                            else
                            {
                                sr.enabled = true;
                                sr.transform.localPosition = new Vector3(center.x, 0.5f, center.y);
                                sr.transform.localRotation = Quaternion.Euler(0, 0, rot * Mathf.Rad2Deg - 90);
                                float trailAlpha = 1.0f - (nextTrailIndex * 0.25f);

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
                // 段长 = 终点 - 起点。如果长度是0，强制设为1避免除零。
                int sectionLength = nextSectionEndIndex - sectionStartIndex;
                if (sectionLength <= 0)
                    sectionLength = 1;

                // 计算当前点相对于本段起点的比例：0 表示最靠近残影头（最亮），1 表示靠近尾部
                float progressRatio = (i - sectionStartIndex) / (float)sectionLength;

                // 我们想要：残影点（i == sectionStartIndex）是 1.0，到段尾是 0.0
                float localProgress = /*1.0f - */progressRatio;

                // 如果是数组最后一点，强制为 0 防止尾巴发亮
                if (i == _tempPositions.Count - 1)
                    localProgress = 0f;

                // 防止极小的负数溢出
                localProgress = Mathf.Clamp01(localProgress);

                // ---- 3. 计算顶点和法线 ----
                float2 dir;
                if (i == 0)
                    dir = _tempPositions[1] - _tempPositions[0];
                else if (i == _tempPositions.Count - 1)
                    dir = _tempPositions[i] - _tempPositions[i - 1];
                else
                    dir = _tempPositions[i + 1] - _tempPositions[i - 1];

                dir = math.normalizesafe(dir, new float2(1, 0));
                float2 normal = new float2(dir.y, -dir.x);

                center += offset;
                float2 a2 = center + normal * halfWidth;
                float2 b2 = center - normal * halfWidth;
                if (reverse)
                    (a2, b2) = (b2, a2);
                int v = count * 2;

                _vertices[v] = a2.X0Y();
                _vertices[v + 1] = b2.X0Y();

                /*Vector3 a3 = new Vector3(a2.x, 0.5f, a2.y);
                Vector3 b3 = new Vector3(b2.x, 0.5f, b2.y);

                _vertices[v] = Ribbon.transform.InverseTransformPoint(a3);
                _vertices[v + 1] = Ribbon.transform.InverseTransformPoint(b3);*/

                // ---- 4. 写入 UV 和 颜色 ----
                _uvs[v] = new Vector2(localProgress, 1f);
                _uvs[v + 1] = new Vector2(localProgress, 0f);

                Color c = _hasExtractedColor ? _dominantColor : Color.white;

                // 透明度应用段内渐变
                c.a = localProgress;

                // 应用全局淡出
                float globalFade = 1f - Mathf.InverseLerp(0.8f, 1.0f, i / (float)(_tempPositions.Count - 1));
                c.a *= globalFade * fix;
                // c.a *= 1f - Mathf.InverseLerp(0.5f, 0.6f, timer);

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