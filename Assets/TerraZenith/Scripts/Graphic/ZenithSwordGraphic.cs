using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
using Pug.Sprite;
using Unity.Entities;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.Graphic
{
    // 天顶剑的图像。刀光对照 FinalFractalHelper.Draw 里的 PrepareStrip，残影对照 Main 里 type 933 的 oldPos 绘制。
    // 点列来自弹幕上的 ZenithTrailPoint，飞行系统每改一次位置写一格。这里不再补弧线。
    // 画的时候减去镜头原点。地核用 XZ。泰拉 +Y 朝下，放在 +Z。贴图 +Y 朝上，V 在 shader 里翻转。
    public class ZenithSwordGraphic : Projectile
    {
        public SpriteObject blade;         // 剑身。新版本图像用 SpriteObject，不再用 SpriteRenderer
        public MeshFilter ribbon;          // 刀光网格
        public SpriteRenderer[] trail;     // 残影，实际只用前 3 个
        const int MaxPoints = ZenithTrail.Capacity;
        const int IllusionSpacing = 15;    // 残影取 oldPos[15]、[30]、[45]
        const float ReferenceLength = 70f; // 剑身缩放的参照长度
        static readonly Color[] TrailColors = // FinalFractalHelper 字典顺序的 21 种刀光颜色
        {
            new Color(236f / 255f, 62f / 255f, 192f / 255f),
            new Color(255f / 255f, 231f / 255f, 69f / 255f),
            new Color(122f / 255f, 66f / 255f, 191f / 255f),
            new Color(254f / 255f, 158f / 255f, 35f / 255f),
            new Color(107f / 255f, 203f / 255f, 0f / 255f),
            new Color(236f / 255f, 200f / 255f, 19f / 255f),
            new Color(236f / 255f, 200f / 255f, 19f / 255f),
            new Color(179f / 255f, 54f / 255f, 201f / 255f),
            new Color(179f / 255f, 54f / 255f, 201f / 255f),
            new Color(84f / 255f, 234f / 255f, 245f / 255f),
            new Color(91f / 255f, 158f / 255f, 232f / 255f),
            new Color(252f / 255f, 95f / 255f, 4f / 255f),
            new Color(254f / 255f, 194f / 255f, 250f / 255f),
            new Color(237f / 255f, 63f / 255f, 133f / 255f),
            new Color(80f / 255f, 222f / 255f, 122f / 255f),
            new Color(56f / 255f, 78f / 255f, 210f / 255f),
            new Color(237f / 255f, 28f / 255f, 36f / 255f),
            new Color(143f / 255f, 215f / 255f, 29f / 255f),
            new Color(178f / 255f, 255f / 255f, 180f / 255f),
            new Color(235f / 255f, 166f / 255f, 135f / 255f),
            new Color(178f / 255f, 255f / 255f, 180f / 255f)
        };
        static readonly float[] BladeLengths = // 对应上面每套的 fullBladeLength，单位是泰拉像素
        {
            48f, 48f, 48f, 76f, 70f, 70f, 70f, 70f, 70f, 70f,
            48f, 76f, 76f, 70f, 70f, 70f, 70f, 80f, 45f, 45f, 86f
        };
        Vector3[] history;    // 从缓冲抄出来的世界坐标，下标 0 是最新点
        Vector3[] tips;       // 该采样时刻剑尖的世界坐标
        Vector3[] hilts;      // 该采样时刻剑根的世界坐标
        float[] spins;        // 该采样在整段挥砍里的进度，后半段收尾变淡
        float[] oldRot;       // 泰拉的 projectile.rotation = 剑身角 + π/2
        Quaternion[] facing;  // 残影用的剑身旋转
        int filled;
        float size = 0.8f;
        float bladeSpan = 70f;
        Color trailColor = Color.white;
        int appliedVariation = -1;
        Mesh mesh;
        Vector3[] vertices;
        Color[] colors;
        Vector3[] uvs;
        int[] triangles;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            filled = 0;
            size = 0.8f;
            trailColor = Color.white;
            appliedVariation = -1;
            ApplyLook();
            EnsureMesh();
            ClearRibbon();
            HideTrail();
            PaintBlade();
        }

        public override void ManagedLateUpdate()
        {
            base.ManagedLateUpdate();
            ApplyLook();
            PaintBlade();
            if (hasExploded)
            {
                ClearRibbon();
                HideTrail();
                return;
            }
            ReadTrail();
            if (ribbon != null)
            {
                EnsureMesh();
                BuildRibbon();
            }
            PaintTrail();
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            ClearRibbon();
            HideTrail();
        }

        // 剑的颜色和长度跟着这发弹幕自己的 variation。生成当帧参数已经写上，这里再读一次即可。
        void ApplyLook()
        {
            if (!TryGetVariation(out int variation) || variation == appliedVariation)
                return;
            appliedVariation = variation;
            ZenithBalance.UnpackVariation(variation, out int index, out _, out _);
            if (index < 0)
                index = 0;
            if (index >= ZenithBalance.SwordTypes)
                index = ZenithBalance.SwordTypes - 1;
            size /*= index == 0 ? 0.55f : 0.8f*/= 1f;
            size *= BladeLengths[index] / ReferenceLength;
            bladeSpan = BladeLengths[index];
            trailColor = TrailColors[index];
        }

        bool TryGetVariation(out int variation)
        {
            variation = 0;
            var manager = world.EntityManager;
            if (!manager.Exists(entity) || !manager.HasComponent<ZenithFlightCD>(entity))
                return false;
            ZenithFlightCD flight = manager.GetComponentData<ZenithFlightCD>(entity);
            if (flight.initialized == 0)
                return false;
            variation = flight.variation;
            return true;
        }

        // 把飞行系统记下的位置抄到网格数组。剑尖方向用记录的剑身角相对当前朝向的差转出来。
        void ReadTrail()
        {
            if (history == null)
            {
                history = new Vector3[MaxPoints];
                tips = new Vector3[MaxPoints];
                hilts = new Vector3[MaxPoints];
                spins = new float[MaxPoints];
                oldRot = new float[MaxPoints];
                facing = new Quaternion[MaxPoints];
            }
            filled = 0;
            var manager = world.EntityManager;
            if (!manager.Exists(entity) || !manager.HasBuffer<ZenithTrail>(entity))
                return;
            DynamicBuffer<ZenithTrail> trail = manager.GetBuffer<ZenithTrail>(entity, true);
            filled = trail.Length;
            if (filled > MaxPoints)
                filled = MaxPoints;
            Transform body = SwordBody();
            Vector3 tipLocal = TipLocal(body);
            Vector3 tipNow = body.TransformPoint(tipLocal);
            Vector3 hiltNow = body.TransformPoint(-tipLocal);
            Vector3 pivotNow = body.position;
            Vector3 rootNow = transform.position;
            float current = BladeAngle();
            Quaternion face = body.rotation;
            for (int i = 0; i < filled; i++)
            {
                ZenithTrail point = trail[i];
                history[i] = new Vector3(point.position.x, point.position.y, point.position.z);
                // 相邻采样不该隔开十几格。隔开的是漏记的一跳，连上去会拉成三角形，后面的旧点也不要了。
                if (i > 0 && Vector3.Distance(history[i], history[i - 1]) > 12f)
                {
                    filled = i;
                    break;
                }
                // 几乎重叠的点直接沿用上一段，避免零距离把朝向算成奇点。
                if (i > 0 && Vector3.Distance(history[i], history[i - 1]) < 0.001f)
                {
                    tips[i] = tips[i - 1];
                    hilts[i] = hilts[i - 1];
                    facing[i] = facing[i - 1];
                    spins[i] = spins[i - 1];
                    oldRot[i] = oldRot[i - 1];
                    continue;
                }
                // 相对剑自身的偏航差，不绕世界 Y 轴，扫过特定角度时不会翻面。
                Quaternion targetRot = Quaternion.Euler(0f, point.angle * Mathf.Rad2Deg, 0f);
                Quaternion currentRot = Quaternion.Euler(0f, current * Mathf.Rad2Deg, 0f);
                Quaternion turn = targetRot * Quaternion.Inverse(currentRot);
                Vector3 root = history[i] - Manager.camera.RenderOrigo;
                Vector3 pivot = root + turn * (pivotNow - rootNow);
                tips[i] = pivot + turn * (tipNow - pivotNow);
                hilts[i] = pivot + turn * (hiltNow - pivotNow);
                facing[i] = turn * face;
                spins[i] = point.spin;
                oldRot[i] = point.angle + Mathf.PI * 0.5f;
            }
        }

        Transform SwordBody()
        {
            if (blade != null)
                return blade.transform;
            if (directionTransform != null)
            {
                Transform body = directionTransform.Find("Blade");
                if (body != null)
                    return body;
                return directionTransform;
            }
            return transform;
        }

        // 剑贴图的尖在网格右上。用网格自身的半宽，这样刀光外沿贴着现在这把剑的剑尖，缩放也算进去。
        Vector3 TipLocal(Transform body)
        {
            MeshFilter filter = body.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Vector3 extents = filter.sharedMesh.bounds.extents;
                if (extents.x > 0.001f || extents.y > 0.001f)
                    return new Vector3(extents.x, extents.y, 0f);
            }
            return new Vector3(1f, 1f, 0f).normalized * (bladeSpan * 0.5f / 16f);
        }

        // 方向节点的 forward 在地面上。Atan2(z, x) 里的 z 对应泰拉朝下的 Y。
        float BladeAngle()
        {
            Transform body = SwordBody();
            Transform aim = body.parent != null ? body.parent : body;
            return Mathf.Atan2(aim.forward.z, aim.forward.x);
        }

        // 三道残影，贴图用剑身自己的。透明度大约 0.75、0.5、0.25。
        void PaintTrail()
        {
            if (trail == null)
                return;
            for (int i = 0; i < trail.Length; i++)
            {
                SpriteRenderer ghost = trail[i];
                if (ghost == null)
                    continue;
                int sample = (i + 1) * IllusionSpacing;
                if (i >= 3 || sample >= filled)
                {
                    ghost.enabled = false;
                    continue;
                }
                ghost.enabled = true;
                ghost.gameObject.SetActive(true);
                ghost.transform.SetPositionAndRotation(history[sample] - Manager.camera.RenderOrigo, facing[sample]); // 画的时候回到镜头坐标
                float alpha = (MaxPoints - sample) / (float)MaxPoints;
                ghost.color = new Color(1f, 1f, 1f, alpha);
                ghost.transform.localScale = new Vector3(size, size, size);
            }
        }

        void HideTrail()
        {
            if (trail == null)
                return;
            for (int i = 0; i < trail.Length; i++)
            {
                if (trail[i] != null)
                    trail[i].enabled = false;
            }
        }

        void PaintBlade()
        {
            if (blade == null)
                return;
            blade.color = Color.Lerp(trailColor, Color.white, 0.35f);
            blade.transform.localScale = new Vector3(size, size, size);
            blade.ApplyVisualChange();
        }

        void EnsureMesh()
        {
            if (ribbon == null)
                return;
            if (mesh != null)
                return;
            mesh = new Mesh();
            mesh.name = "ZenithBladeLight";
            mesh.MarkDynamic();
            ribbon.sharedMesh = mesh;
            vertices = new Vector3[MaxPoints * 2];
            colors = new Color[MaxPoints * 2];
            uvs = new Vector3[MaxPoints * 2];
            triangles = new int[(MaxPoints - 1) * 12]; // 每段 6 个正面索引 + 6 个背面，includeBacksides
        }

        void ClearRibbon()
        {
            filled = 0;
            if (mesh != null)
                mesh.Clear();
        }

        void BuildRibbon()
        {
            if (mesh == null)
                return;
            int count = PrepareStrip(ribbon.transform);
            if (count < 2)
            {
                mesh.Clear();
                return;
            }
            mesh.Clear();
            mesh.SetVertices(vertices, 0, count * 2);
            mesh.SetColors(colors, 0, count * 2);
            mesh.SetUVs(0, uvs, 0, count * 2);
            mesh.SetTriangles(triangles, 0, (count - 1) * 12, 0, true, 0);
            mesh.RecalculateBounds();
            ribbon.sharedMesh = mesh; // Clear 之后渲染器会丢掉网格，要重新挂上
        }

        // 对照 VertexStrip.PrepareStrip + AddVertexPair。进度用 60 而不是当前点数，这样淡出和泰拉一样按整段缓存算。
        int PrepareStrip(Transform space)
        {
            int count = 0;
            for (int i = 0; i < filled; i++)
            {
                float progress = i / (float)(MaxPoints - 1);
                Vector3 a = space.InverseTransformPoint(tips[i]);  // 剑尖
                Vector3 b = space.InverseTransformPoint(hilts[i]); // 剑根
                float distance = Vector3.Distance(a, b) * 16f; // 还原成像素，shader 用这个当 V 的跨度
                int v = count * 2;
                vertices[v] = a;
                vertices[v + 1] = b;
                uvs[v] = new Vector3(progress, distance, distance); // 剑尖：V = 整段距离
                uvs[v + 1] = new Vector3(progress, 0f, distance);    // 剑根：V = 0。Unity 里再由 shader 翻转
                Color color = trailColor * (1f - Mathf.InverseLerp(0f, 0.98f, progress)); // 旧点变淡
                color.a *= 1f - Mathf.InverseLerp(0.5f, 1f, spins[i]); // 转过一半之后，收回身后的那段逐渐透明
                color.a /= 2f;
                colors[v] = colors[v + 1] = color;
                count++;
            }
            int tri = 0;
            for (int i = 0; i < count - 1; i++)
            {
                int v = i * 2;
                triangles[tri++] = v;       // 正面，对照 PrepareIndices 前 6 个
                triangles[tri++] = v + 1;
                triangles[tri++] = v + 2;
                triangles[tri++] = v + 2;
                triangles[tri++] = v + 1;
                triangles[tri++] = v + 3;
                triangles[tri++] = v + 2;   // 背面
                triangles[tri++] = v + 1;
                triangles[tri++] = v;
                triangles[tri++] = v + 2;
                triangles[tri++] = v + 3;
                triangles[tri++] = v + 1;
            }
            return count;
        }
    }
}
