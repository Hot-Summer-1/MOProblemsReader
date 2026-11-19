using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MOProblemsReader.ViewModels;

/// <summary>
/// 题库树中一个节点（分类 / 赛事 / 年份）的视图模型，承载显示文本、展开状态与懒加载元数据。
/// </summary>
public partial class ProblemNodeViewModel : ObservableObject
{
    /// <summary>
    /// 节点种类，创建后不可变。
    /// </summary>
    public ProblemNodeKind Kind { get; }

    /// <summary>
    /// 节点的键名：分类为仓库目录名（china/abroad/university/original），
    /// 赛事与年份为磁盘上的目录名，供读盘与调试定位使用。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 界面上显示的文本（分类为中文名，赛事/年份取 info.json 中的名称）。
    /// </summary>
    [ObservableProperty]
    private string _displayName;

    /// <summary>
    /// 节点是否处于展开状态，与 <c>TreeViewItem.IsExpanded</c> 双向绑定。
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// 节点是否可用；仓库更新失败或加载失败时置 <c>false</c> 禁用整棵子树。
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>
    /// 节点是否正在加载子项，驱动模板中的环形转圈显隐。
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 子节点集合：分类下为赛事，赛事下为年份。
    /// </summary>
    public ObservableCollection<ProblemNodeViewModel> Children { get; } = new();

    /// <summary>
    /// 父节点引用，供双击时拼接「年份-赛事-分类」提示文案。
    /// </summary>
    public ProblemNodeViewModel? Parent { get; set; }

    /// <summary>
    /// 是否已完成一次加载。<c>true</c> 表示无需再读盘：
    /// 分类节点在首次加载成功后置位，赛事节点的年份由构造时预填故直接为 <c>true</c>。
    /// </summary>
    public bool HasLoaded { get; set; }

    /// <summary>
    /// 加载子节点的回调，由 <see cref="MainWindowViewModel"/> 在构造节点时注入；
    /// 仅分类节点使用，赛事/年份节点为 <c>null</c>。
    /// </summary>
    public Func<ProblemNodeViewModel, Task>? ChildrenLoader { get; set; }

    /// <summary>
    /// 创建一个节点。
    /// </summary>
    /// <param name="kind">节点种类。</param>
    /// <param name="name">键名（分类目录名、赛事目录名或年份名）。</param>
    /// <param name="displayName">界面显示文本。</param>
    public ProblemNodeViewModel(ProblemNodeKind kind, string name, string displayName)
    {
        Kind = kind;
        Name = name;
        _displayName = displayName;
    }

    /// <summary>
    /// 展开状态变化时按需懒加载子节点：仅在展开、尚未加载且不在加载中时触发。
    /// </summary>
    /// <param name="value">新的展开状态。</param>
    partial void OnIsExpandedChanged(bool value)
    {
        if (!value || HasLoaded || IsLoading)
        {
            return;
        }

        // 展开即触发加载；此处不 await —— 加载过程内部的异常已在视图模型中处理完毕
        _ = ChildrenLoader?.Invoke(this);
    }
}
