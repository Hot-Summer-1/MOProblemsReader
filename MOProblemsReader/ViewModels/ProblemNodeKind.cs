namespace MOProblemsReader.ViewModels;

/// <summary>
/// 题库树节点的种类，决定节点的子节点来源与交互行为。
/// </summary>
public enum ProblemNodeKind
{
    /// <summary>
    /// 分类节点（国内/国外/大学/原创），展开时触发读盘加载赛事。
    /// </summary>
    Category,

    /// <summary>
    /// 赛事节点，创建时即预填年份子节点。
    /// </summary>
    Competition,

    /// <summary>
    /// 年份节点，双击时进入（待接入的）题目视图。
    /// </summary>
    Year,

    /// <summary>
    /// 占位子节点：仅用于让父节点在折叠态就存在子项（WPF 据此显示展开箭头），
    /// 其容器在 ItemContainerStyle 中被折叠，用户看不到它，也不参与任何加载逻辑。
    /// </summary>
    Placeholder
}
