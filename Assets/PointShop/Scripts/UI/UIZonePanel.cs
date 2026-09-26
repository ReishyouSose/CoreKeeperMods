using UnityEngine;

namespace Assets.PointShop.Scripts.UI
{
    [RequireComponent(typeof(UIScrollWindow))]
    public class UIZonePanel : UIelement, IScrollable
    {
        private LinearLayoutUIComponent layout;
        private Transform container;
        private UIScrollWindow scroll;
        private void Awake()
        {
            layout = GetComponentInChildren<LinearLayoutUIComponent>();
            scroll = GetComponent<UIScrollWindow>();
            container = layout.transform;
        }
        public float GetCurrentWindowHeight() => layout.GetUIComponentRenderHeight();

        public bool IsBottomElementSelected()
        {
            int index = container.childCount - 1;
            if (index < 0)
                return false;
            return container.GetChild(index) == Manager.ui.currentSelectedUIElement;
        }

        public bool IsTopElementSelected()
        {
            int count = container.childCount;
            if (count <= 0)
                return false;
            return container.GetChild(0) == Manager.ui.currentSelectedUIElement;
        }
        public void UpdateContainingElements(float _)
        {
            if (!layout)
                return;
            Vector3 center = scroll.transform.position + (Vector3)scroll.windowLocalCenter;
            Rect rect = new(center.x - scroll.windowWidth / 2f, center.y - scroll.windowHeight / 2f, scroll.windowWidth, scroll.windowHeight);
            foreach (Transform trans in layout.transform)
            {
                if (trans.TryGetComponent<UIComponentMonoBehaviour>(out var ui) && trans.TryGetComponent<BoxCollider>(out var box))
                {
                    float width = ui.GetUIComponentRenderWidth();
                    float height = ui.GetUIComponentRenderHeight();

                    // 根据 Pivot 计算左下角
                    float left = trans.position.x;
                    float bottom = trans.position.y;

                    if (ui.GetUIComponentPivotPosition() == UIComponentMonoBehaviour.PivotPosition.TopLeft)
                        bottom -= height;
                    else // MiddleLeft
                        bottom -= height / 2f;

                    box.enabled = new Rect(left, bottom, width, height).Overlaps(rect);
                }
            }
        }
    }
}
