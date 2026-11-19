using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ProblemViewer;
using Serilog;

namespace MOProblemsReader.ViewModels;

/// <summary>
/// 主窗口题库树的视图模型：持有四个分类节点，负责懒加载、仓库更新后的刷新与加载失败处理。
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    /// <summary>
    /// 分类定义表：目录名（供读盘拼路径）与中文显示名，二者只在此处维护一份。
    /// </summary>
    private static readonly (string Name, string DisplayName)[] CategoryDefinitions =
    {
        ("china", "国内"),
        ("abroad", "国外"),
        ("university", "大学"),
        ("original", "原创")
    };

    private readonly IProblemTreeLoader _loader;
    private readonly IUserNotifier _notifier;

    /// <summary>
    /// 四个分类节点，顺序与 <see cref="CategoryDefinitions"/> 一致。
    /// </summary>
    public ObservableCollection<ProblemNodeViewModel> Categories { get; } = new();

    /// <summary>
    /// 创建视图模型并填充分类节点；分类节点初始为折叠、可用、未加载状态。
    /// </summary>
    /// <param name="loader">赛事数据加载服务。</param>
    /// <param name="notifier">失败提示出口，由视图层注入。</param>
    public MainWindowViewModel(IProblemTreeLoader loader, IUserNotifier notifier)
    {
        _loader = loader;
        _notifier = notifier;

        foreach (var (name, displayName) in CategoryDefinitions)
        {
            var category = new ProblemNodeViewModel(ProblemNodeKind.Category, name, displayName)
            {
                ChildrenLoader = LoadChildrenAsync
            };
            ResetChildren(category);
            Categories.Add(category);
        }
    }

    /// <summary>
    /// 清空节点的子项并挂上一个占位子节点。
    /// </summary>
    /// <param name="node">要重置子项的节点。</param>
    /// <remarks>
    /// 必须保留占位子项：WPF TreeViewItem 的展开箭头由 HasItems 决定，
    /// 子项为空时折叠态就没有箭头，用户无法展开尚未加载的分类。
    /// 占位节点的容器在 XAML 中被折叠，因此不会显示为空行。
    /// </remarks>
    private static void ResetChildren(ProblemNodeViewModel node)
    {
        node.Children.Clear();
        node.Children.Add(new ProblemNodeViewModel(ProblemNodeKind.Placeholder, string.Empty, string.Empty)
        {
            // 占位节点自身不需要加载，置位以免被误判为未加载节点
            HasLoaded = true
        });
    }

    /// <summary>
    /// 加载指定节点的子项；分类节点在此处真正读盘。
    /// 失败时记录日志、折叠并禁用该节点、标记需要强制更新，并提示用户。
    /// </summary>
    /// <param name="node">要加载子项的节点。</param>
    public async Task LoadChildrenAsync(ProblemNodeViewModel node)
    {
        if (node.IsLoading || node.HasLoaded)
        {
            return;
        }

        node.IsLoading = true;
        try
        {
            var children = await _loader.LoadCompetitionsAsync(node.Name);

            // 这里不能用 ResetChildren：占位子项只是撑起展开箭头的哨兵，
            // 真实数据到达后必须清空，否则列表里会多出一个空白行
            node.Children.Clear();
            foreach (var child in children)
            {
                child.Parent = node;
                child.IsEnabled = node.IsEnabled;
                node.Children.Add(child);
            }

            node.HasLoaded = true;
        }
        catch (Exception ex)
        {
            // 本地题库不可用时下次启动应完整重拉，因此标记强制更新（与重构前语义一致）
            node.HasLoaded = false;
            node.IsExpanded = false;
            node.IsEnabled = false;
            Settings.ForceUpdateRequired = true;

            Log.Error(ex, "Failed to load competitions. Category: {Category}", node.Name);
            await _notifier.ShowErrorAsync("竞赛条目加载错误", "加载竞赛条目时遇到错误");
        }
        finally
        {
            node.IsLoading = false;
        }
    }

    /// <summary>
    /// 响应仓库更新结果：成功则作废已加载内容并按需重新加载，失败则只禁用未加载过的分类。
    /// </summary>
    /// <param name="success">仓库更新是否成功。</param>
    public async Task OnRepositoryUpdatedAsync(bool success)
    {
        if (success)
        {
            // 题库内容可能整体变化：四个分类一律恢复可用，已加载节点清空后按需重读
            foreach (var category in Categories)
            {
                category.IsEnabled = true;
                if (!category.HasLoaded)
                {
                    continue;
                }

                ResetChildren(category);
                category.HasLoaded = false;

                // 展开中的节点立即重读，让用户直接看到新数据（转圈随 IsLoading 显示）
                if (category.IsExpanded)
                {
                    await LoadChildrenAsync(category);
                }
            }
        }
        else
        {
            // 本地仓库可能已不可用：禁用未缓存的分类以免点开即报错，已加载内容仍可浏览
            foreach (var category in Categories.Where(c => !c.HasLoaded))
            {
                category.IsEnabled = false;
            }
        }
    }
}
