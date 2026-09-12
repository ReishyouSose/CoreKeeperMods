using UnityEngine;

namespace TomeOfMimicry
{
    public static class CostHUD
    {
        private const float HOTBAR_H = 68f;
        private const float GAP = 6f;
        private const float ROW_H = 22f;
        private const float PAD_X = 14f;
        private const float PAD_Y = 8f;
        private const float MIN_W = 200f;
        private const float SWATCH_SZ = 10f;

        private static float _flashEndTime;
        private static string _flashMessage = "Not enough materials";
        private const float FLASH_DURATION = 2.2f;

        private static readonly Color C_Bg = new(0.04f, 0.07f, 0.13f, 0.88f);
        private static readonly Color C_Border = new(0.25f, 0.48f, 0.82f, 0.65f);
        private static readonly Color C_RowOk = new(0.20f, 0.85f, 0.40f, 1.00f);
        private static readonly Color C_RowShort = new(0.95f, 0.28f, 0.22f, 1.00f);
        private static readonly Color C_RowDimLabel = new(0.65f, 0.80f, 1.00f, 0.80f);
        private static readonly Color C_FlashBg = new(0.55f, 0.08f, 0.08f, 0.90f);
        private static readonly Color C_FlashBorder = new(1.00f, 0.30f, 0.20f, 0.90f);
        private static readonly Color C_FlashText = new(1.00f, 0.82f, 0.70f, 1.00f);

        private static Texture2D _white;
        private static GUIStyle _sLabel, _sCount, _sFlash;
        private static bool _ready;

        private static void EnsureReady()
        {
            if (_ready && _white != null) return;
            _ready = true;

            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();

            _sLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = C_RowDimLabel },
            };
            _sCount = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
            };
            _sFlash = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = C_FlashText },
                wordWrap = false,
            };
        }

        public static void Draw()
        {
            EnsureReady();

            if (BlueprintManager.PasteFailed)
            {
                BlueprintManager.PasteFailed = false;
                _flashMessage = string.IsNullOrEmpty(BlueprintManager.PasteFailureMessage)
                    ? "Not enough materials"
                    : BlueprintManager.PasteFailureMessage;
                BlueprintManager.PasteFailureMessage = null;
                _flashEndTime = Time.realtimeSinceStartup + FLASH_DURATION;
            }

            bool isFlashing = Time.realtimeSinceStartup < _flashEndTime;
            bool isActive = TileCostTracker.IsActive &&
                            !TileCostTracker.IsCreativeMode;

            if (!isActive && !isFlashing) return;

            var savedMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;

            if (isFlashing)
                DrawFlash();
            else
                DrawCostPanel();

            GUI.matrix = savedMatrix;
        }

        private static void DrawFlash()
        {
            string msg = _flashMessage;
            const float fw = 520f;
            const float fh = 32f;
            float fx = (Screen.width - fw) * 0.5f;
            float fy = Screen.height - HOTBAR_H - GAP - fh;

            Fill(fx, fy, fw, fh, C_FlashBg);
            Border(fx, fy, fw, fh, 1.5f, C_FlashBorder);
            GUI.Label(new Rect(fx + 8f, fy, fw - 16f, fh), msg, _sFlash);
        }

        private static void DrawCostPanel()
        {
            var cost = TileCostTracker.CurrentCost;
            if (cost.Count == 0) return;

            float nameColW = 110f;
            float countColW = 70f;
            float rowW = SWATCH_SZ + 6f + nameColW + countColW + PAD_X * 2f;
            float panelW = Mathf.Max(MIN_W, rowW);
            float panelH = PAD_Y * 2f + cost.Count * ROW_H;

            float px = (Screen.width - panelW) * 0.5f;
            float py = Screen.height - HOTBAR_H - GAP - panelH;

            Fill(px, py, panelW, panelH, C_Bg);
            Border(px, py, panelW, panelH, 1f, C_Border);

            float cx = px + PAD_X;
            float cy = py + PAD_Y;
            float inner = panelW - PAD_X * 2f;

            for (int i = 0; i < cost.Count; i++)
            {
                var e = cost[i];
                float ry = cy + i * ROW_H;
                Color col = e.HasEnough ? C_RowOk : C_RowShort;

                Fill(cx, ry + (ROW_H - SWATCH_SZ) * 0.5f, SWATCH_SZ, SWATCH_SZ, col);

                string name = FormatItemName(e.objectId);
                GUI.Label(new Rect(cx + SWATCH_SZ + 6f, ry, nameColW, ROW_H),
                          name, _sLabel);

                string countStr = $"{e.available} / {e.needed}";
                var cStyle = new GUIStyle(_sCount) { normal = { textColor = col } };
                GUI.Label(new Rect(cx + SWATCH_SZ + 6f + nameColW, ry, countColW, ROW_H),
                          countStr, cStyle);
            }
        }

        private static string FormatItemName(ObjectID id)
        {
            string raw = id.ToString();
            var sb = new System.Text.StringBuilder(raw.Length + 4);
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && char.IsLower(raw[i - 1]))
                    sb.Append(' ');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        private static void Fill(float x, float y, float w, float h, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), _white);
            GUI.color = prev;
        }

        private static void Border(float x, float y, float w, float h, float t, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, t), _white);
            GUI.DrawTexture(new Rect(x, y + h - t, w, t), _white);
            GUI.DrawTexture(new Rect(x, y, t, h), _white);
            GUI.DrawTexture(new Rect(x + w - t, y, t, h), _white);
            GUI.color = prev;
        }
    }
}
