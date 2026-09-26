using UnityEngine;

namespace Assets.PointShop.Scripts
{
    /// <summary>
    /// 分隔条。放在 GridLayoutUIComponent 的子级中，用于在网格布局中强制换行/换列。
    /// 方向由所在 GridLayoutUIComponent 的 horizontal 决定：
    ///   - 横向布局中：本分隔条为一条横线，横向占满整行，纵向占用 thickness，作用是换行。
    ///   - 纵向布局中：本分隔条为一条竖线，纵向占满整列，横向占用 thickness，作用是换列。
    /// </summary>
    [RequireComponent(typeof(WrapperUIComponent))]
    public class UISeparator : MonoBehaviour
    {
        /// <summary>
        /// 垂直于分割方向的空间占用（横线的高度 / 竖线的宽度）。
        /// </summary>
        public float Thickness = 1f;

        /// <summary>
        /// 沿分割方向两端各向中间缩进的像素
        /// </summary>
        public float BorderPositive;

        /// <summary>
        /// 沿分割方向两端各向中间缩进的像素
        /// </summary>
        public float BorderNegative;

        /// <summary>
        /// 视觉表现用的 SpriteRenderer，是分隔条的子物体。
        /// 其 localPosition 与 size 由 ApplyVisualRect 设置。
        /// </summary>
        public SpriteRenderer SR;

        /// <summary>
        /// 由 GridLayoutUIComponent 调用：指定精灵在【分隔条本地坐标】中的矩形。
        /// 分隔条本地位于分割区间的"起点端"，x 向右为正、y 向上为正。
        /// </summary>
        /// <param name="localX">精灵左边缘相对分隔条原点的 x</param>
        /// <param name="localY">精灵上边缘相对分隔条原点的 y</param>
        /// <param name="width">精灵宽度</param>
        /// <param name="height">精灵高度</param>
        public void ApplyVisualRect(float localX, float localY, float width, float height)
        {
            if (SR == null)
                return;

            Vector3 p = SR.transform.localPosition;
            p.x = localX;
            p.y = localY;
            SR.transform.localPosition = p;

            SR.size = new Vector2(width, height);
        }

    }
}