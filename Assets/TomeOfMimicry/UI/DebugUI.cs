using UnityEngine;

namespace TomeOfMimicry
{
    public class DebugUI : MonoBehaviour
    {
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        public static bool IsInWorld
        {
            get
            {
                var player = Manager.main?.player;
                var ui = Manager.ui;
                return player != null &&
                       player.gameObject.activeInHierarchy &&
                       ui != null &&
                       ui.UICamera != null &&
                       ui.UICamera.activeInHierarchy &&
                       ui.playerHealthBarUI != null;
            }
        }

        private void OnGUI()
        {
            if (!IsInWorld)
                return;

            var e = Event.current;

            if (e.type == EventType.KeyDown &&
                e.keyCode == KeyCode.U &&
                e.control &&
                BlueprintManager.IsGadgetEquipped)
            {
                e.Use();
            }

            if (!BlueprintManager.IsGadgetEquipped)
                return;

            DrawDebugPanel();

            BuilderPanelUI.Draw();
        }

        private void DrawDebugPanel()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(0.80f, 0.88f, 1f, 0.90f) }
                };
                _boxStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeTex(1, 1, new Color(0f, 0f, 0.05f, 0.65f)) }
                };
            }

            var savedMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;

            var player = Manager.main?.player;
            var pos = player != null ? player.transform.position : Vector3.zero;
            var cursorTile = BlueprintManager.CachedCursorTilePos;
            var (selMin, selMax) = BlueprintManager.GetSelectionBounds();
            var selSize = selMax - selMin + new Unity.Mathematics.int2(1, 1);

            var lines = new[]
            {
                $"State: {BlueprintManager.State}",
                $"Player: ({pos.x:F1}, {pos.z:F1})",
                $"Cursor tile: {cursorTile}",
                $"Selection: {selMin} -> {selMax}",
                $"Selection size: {selSize.x} x {selSize.y}",
                $"Pending capture: {BlueprintManager.PendingCapture}",
                $"Blueprint: {BlueprintManager.ActiveBlueprint?.tiles.Count.ToString() ?? "none"} tiles",
                $"Undo: {(BlueprintManager.CanUndo ? "yes" : "-")}  Redo: {(BlueprintManager.CanRedo ? "yes" : "-")}",
            };

            const float panelW = 240f;
            const float lineH = 17f;
            const float padding = 6f;
            float panelH = lines.Length * lineH + padding * 2f;
            float panelX = Screen.width - panelW - 10f;
            float panelY = 10f;

            GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none, _boxStyle);
            for (int i = 0; i < lines.Length; i++)
            {
                GUI.Label(
                    new Rect(panelX + padding, panelY + padding + i * lineH, panelW - padding * 2f, lineH),
                    lines[i],
                    _labelStyle);
            }

            GUI.matrix = savedMatrix;
        }

        private static Texture2D MakeTex(int w, int h, Color color)
        {
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            var texture = new Texture2D(w, h);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
