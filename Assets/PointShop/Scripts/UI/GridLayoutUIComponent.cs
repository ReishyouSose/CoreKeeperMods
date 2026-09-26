using PimDeWitte.UnityMainThreadDispatcher;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.PointShop.Scripts.UI
{

    /// <summary>
    /// Please add script <see cref="UILocator"/> at template prefab 
    /// </summary>
    public class GridLayoutUIComponent : UIComponentMonoBehaviour
    {
        [Header("Layout Settings")]
        public bool horizontal = true;
        public int gapHorizontal = 0;
        public int gapVertical = 0;

        [Header("Padding")]
        public float paddingLeft = 0f;
        public float paddingRight = 0f;
        public float paddingTop = 0f;
        public float paddingBottom = 0f;

        [Header("Other")]
        public SpriteRenderer background;
        public UIScrollWindow ScrollWindow;
        public bool renderFrameLate;

        private float totalWidth;
        private float totalHeight;
        private int totalRow;
        private int totalCol;
        public override void RenderUIComponent(bool force = false)
        {
            if (!(Dirty || force))
            {
                return;
            }

            if (renderFrameLate)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(delegate
                {
                    RenderUIComponentLate(force);
                });
            }
            else
            {
                RenderUIComponentLate(force);
            }
        }

        private void RenderUIComponentLate(bool force)
        {
            base.RenderUIComponent(force);
            ArrangeGridChildren();
            UpdateBackground();
        }

        protected override bool IsUIComponentRenderingDependentOnChildren()
        {
            return true;
        }

        private void ArrangeGridChildren()
        {
            List<UIComponentMonoBehaviour> activeChildren = GetActiveChildren();
            if (activeChildren.Count == 0)
            {
                totalWidth = 0;
                totalHeight = 0;
                return;
            }
            SpaceElement(activeChildren);
        }

        private List<UIComponentMonoBehaviour> GetActiveChildren()
        {
            List<UIComponentMonoBehaviour> allChildren = GetDirectUIComponentChildren();
            List<UIComponentMonoBehaviour> activeChildren = new();

            foreach (UIComponentMonoBehaviour child in allChildren)
            {
                if (PlatformStorefrontUtility.MatchesCurrent(child.activeInPlatforms, child.activeInStoreFronts)
                    && child.gameObject.activeInHierarchy && child.GetUIComponentPivotPosition() == PivotPosition.TopLeft)
                {
                    activeChildren.Add(child);
                }
            }

            return activeChildren;
        }
        private void SpaceElement(List<UIComponentMonoBehaviour> children)
        {
            float startX = 0.0625f * paddingLeft;
            float startY = 0.0625f * paddingTop;
            float gapX = 0.0625f * gapHorizontal;
            float gapY = 0.0625f * gapVertical;
            float currentX = startX;
            float currentY = startY;
            float maxWidth = ScrollWindow.windowWidth;
            float maxHeight = ScrollWindow.windowHeight;
            float maxLength = 0f;
            int itemsInCurrent = 0;
            totalWidth = 0f;
            totalHeight = 0f;
            totalRow = 0;
            totalCol = 0;
            int row = 0;
            int col = 0;

            for (int i = 0; i < children.Count; i++)
            {
                UIComponentMonoBehaviour child = children[i];
                if (child.TryGetComponent<UISeparator>(out var separator))
                {
                    // ==================== 分隔条 ====================
                    // 方向完全由所在布局决定：
                    //   horizontal=true  → 横线 → 换行
                    //   horizontal=false → 竖线 → 换列
                    float thickness = separator.Thickness * 0.0625f;
                    float borderPositive = separator.BorderPositive * 0.0625f;
                    float borderNegative = separator.BorderNegative * 0.0625f;

                    var ui = separator.GetComponent<WrapperUIComponent>();
                    if (horizontal)
                    {
                        // ---- 横线：换行 ----
                        // 换行前的推进（加上本行最高的元素高度）
                        currentY += maxLength + gapY;
                        // 横线：横向占满整行，纵向占用 thickness
                        float lineWidth = maxWidth - borderPositive - borderNegative;
                        if (lineWidth < 0f)
                            lineWidth = 0f;
                        float lineHeight = thickness;
                        ui.renderWidthPixels = (int)(16 * maxWidth);
                        ui.renderHeightPixels = (int)(16 * lineHeight);
                        // 分隔条本体放在当前行的"起点端"：
                        //   x = startX + borderPositive（线左端）
                        //   y = -currentY              （线上端）
                        Vector3 pos = separator.transform.localPosition;
                        pos.x = startX;
                        pos.y = -currentY;
                        separator.transform.localPosition = pos;

                        // 精灵子物体：相对分隔条本体的矩形
                        //   左上角相对本体原点 = (0, 0)
                        //   宽 = lineWidth，高 = lineHeight
                        separator.ApplyVisualRect(borderPositive, 0f, lineWidth, lineHeight);

                        // 分隔条占据的垂直空间 + gapY，再开始新的一行
                        currentY += lineHeight + gapY;

                        // 重置新行的横向状态
                        currentX = startX;
                        col = 0;
                        totalRow = Math.Max(totalRow, ++row);
                        itemsInCurrent = 0;
                        maxLength = 0f;

                        totalWidth = Math.Max(totalWidth, currentX);
                        totalHeight = Math.Max(totalHeight, currentY);
                    }
                    else
                    {
                        // ---- 竖线：换列 ----
                        currentX += maxLength + gapX;

                        // 竖线：纵向占满整列，横向占用 thickness
                        float lineWidth = thickness;
                        float lineHeight = maxHeight - borderPositive - borderNegative;
                        if (lineHeight < 0f)
                            lineHeight = 0f;
                        ui.renderWidthPixels = (int)(16 * lineWidth);
                        ui.renderHeightPixels = (int)(16 * maxHeight);

                        // 分隔条本体放在当前列的"起点端"：
                        //   x = currentX        （线左端）
                        //   y = -(startY + borderPositive)（线上端）
                        Vector3 pos = separator.transform.localPosition;
                        pos.x = currentX;
                        pos.y = -startY;
                        separator.transform.localPosition = pos;

                        // 精灵子物体：相对分隔条本体的矩形，左上角在本体原点
                        separator.ApplyVisualRect(0f, -borderPositive, lineWidth, lineHeight);

                        // 分隔条占据的水平空间 + gapX，再开始新的一列
                        currentX += lineWidth + gapX;

                        // 重置新列的纵向状态
                        currentY = startY;
                        row = 0;
                        totalCol = Math.Max(totalCol, ++col);
                        itemsInCurrent = 0;
                        maxLength = 0f;

                        totalWidth = Math.Max(totalWidth, currentX);
                        totalHeight = Math.Max(totalHeight, currentY);
                    }

                    // 分隔条不写 Locator（不参与导航编号）
                }
                else
                {
                    // ==================== 普通子元素 ====================
                    float width = child.GetUIComponentRenderWidth();
                    float height = child.GetUIComponentRenderHeight();
                    bool wrap = false;

                    if (horizontal && currentX + width > maxWidth)
                    {
                        currentY += maxLength + gapY;
                        currentX = startX;
                        col = 0;
                        totalRow = Math.Max(totalRow, ++row);
                        wrap = true;
                    }
                    else if (!horizontal && currentY + height > maxHeight)
                    {
                        currentX += maxLength + gapX;
                        currentY = startY;
                        row = 0;
                        totalCol = Math.Max(totalCol, ++col);
                        wrap = true;
                    }

                    if (wrap)
                    {
                        itemsInCurrent = 0;
                        maxLength = 0f;
                    }

                    Vector3 pos = child.transform.localPosition;
                    pos.x = currentX;
                    pos.y = -currentY;
                    child.transform.localPosition = pos;

                    totalWidth = Math.Max(totalWidth, currentX + width);
                    totalHeight = Math.Max(totalHeight, currentY + height);

                    child.GetComponent<UILocator>().Locator = new(col, row);

                    if (horizontal)
                    {
                        currentX += width + gapX;
                        totalCol = Math.Max(totalCol, ++col);
                    }
                    else
                    {
                        currentY += height + gapY;
                        totalRow = Math.Max(totalRow, ++row);
                    }

                    maxLength = Math.Max(maxLength, horizontal ? height : width);
                    itemsInCurrent++;
                }
            }

            totalWidth += 0.0625f * paddingRight;
            totalHeight += 0.0625f * paddingBottom;
        }

        private void UpdateBackground()
        {
            if (background == null)
                return;

            Vector2 size = new(totalWidth, totalHeight);
            background.transform.localPosition = new Vector3((0f - size.x) / 2f, (0f - size.y) / 2f, 0f);
            background.size = size;
        }

        public override float GetUIComponentRenderWidth()
        {
            return totalWidth;
        }

        public override float GetUIComponentRenderHeight()
        {
            return totalHeight;
        }
        public bool IsTopElementSelected() => Manager.ui.currentSelectedUIElement.GetComponent<UILocator>().Locator.y == 0;
        public bool IsBottomElemntSelected() => Manager.ui.currentSelectedUIElement.GetComponent<UILocator>().Locator.y == totalRow;
    }
}
