namespace Assets.BuildingBlueprint.Scripts.Enums
{
    /// <summary>
    /// 选区运算(用于<see cref="SelectionMode.Box"/>模式）
    /// </summary>
    public enum BoxOperator
    {
        New,       // 另起
        Add,       // 添加
        Subtract,  // 排除重叠部分
        Intersect  // 选中重叠部分
    }
}
