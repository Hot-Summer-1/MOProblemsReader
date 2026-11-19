using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOProblemsReader.ViewModels;
using ProblemViewer;

namespace MOProblemsReader.Tests;

/// <summary>
/// 主窗口视图模型在仓库更新成功、仓库更新失败与竞赛条目加载出错三种场景下的行为测试。
/// </summary>
[TestClass]
public sealed class MainWindowViewModelTests
{
    /// <summary>
    /// 可控的赛事数据加载器：能返回指定赛事、抛出异常，并记录调用次数。
    /// </summary>
    private sealed class StubTreeLoader : IProblemTreeLoader
    {
        /// <summary>
        /// 实际执行加载的委托，默认返回空列表；替换它可模拟成功数据或失败。
        /// </summary>
        public Func<string, Task<IReadOnlyList<ProblemNodeViewModel>>> Handler { get; set; } =
            _ => Task.FromResult<IReadOnlyList<ProblemNodeViewModel>>([]);

        /// <summary>
        /// 已发生的加载调用次数，用于断言是否重复读盘。
        /// </summary>
        public int CallCount { get; private set; }

        /// <summary>
        /// 按当前委托返回赛事节点，并累加调用次数。
        /// </summary>
        /// <param name="categoryName">分类目录名。</param>
        /// <returns>赛事节点列表。</returns>
        public Task<IReadOnlyList<ProblemNodeViewModel>> LoadCompetitionsAsync(string categoryName)
        {
            CallCount++;
            return Handler(categoryName);
        }
    }

    /// <summary>
    /// 记录错误提示的替身，用于断言失败时是否提示过用户。
    /// </summary>
    private sealed class RecordingUserNotifier : IUserNotifier
    {
        /// <summary>
        /// 收到的错误提示（标题与正文）。
        /// </summary>
        public List<(string Title, string Message)> Errors { get; } = [];

        /// <summary>
        /// 记录一次错误提示。
        /// </summary>
        /// <param name="title">提示框标题。</param>
        /// <param name="message">提示框正文。</param>
        /// <returns>已完成的任务。</returns>
        public Task ShowErrorAsync(string title, string message)
        {
            Errors.Add((title, message));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 创建一组互不影响的视图模型、加载器与提示器。
    /// </summary>
    private static (MainWindowViewModel ViewModel, StubTreeLoader Loader, RecordingUserNotifier Notifier) CreateViewModel()
    {
        var loader = new StubTreeLoader();
        var notifier = new RecordingUserNotifier();
        return (new MainWindowViewModel(loader, notifier), loader, notifier);
    }

    /// <summary>
    /// 按分类目录名取出分类节点。
    /// </summary>
    /// <param name="viewModel">视图模型。</param>
    /// <param name="name">分类目录名，如 <c>china</c>。</param>
    private static ProblemNodeViewModel Category(MainWindowViewModel viewModel, string name) =>
        viewModel.Categories.First(category => category.Name == name);

    /// <summary>
    /// 构造一个赛事节点。
    /// </summary>
    /// <param name="name">赛事目录名与显示文本。</param>
    private static ProblemNodeViewModel Competition(string name) =>
        new(ProblemNodeKind.Competition, name, name);

    /// <summary>
    /// 更新成功时，处于展开状态的已加载分类应清空并立即重新读盘，让用户看到新数据。
    /// </summary>
    [TestMethod]
    public async Task OnRepositoryUpdatedAsync_Success_ReloadsExpandedCategory()
    {
        var (viewModel, loader, _) = CreateViewModel();
        var china = Category(viewModel, "china");
        await viewModel.LoadChildrenAsync(china);
        china.IsExpanded = true;

        loader.Handler = _ => Task.FromResult<IReadOnlyList<ProblemNodeViewModel>>(
            [Competition("new-a"), Competition("new-b")]);
        var callsBeforeUpdate = loader.CallCount;

        await viewModel.OnRepositoryUpdatedAsync(true);

        Assert.AreEqual(callsBeforeUpdate + 1, loader.CallCount, "展开中的分类应重新读盘一次");
        Assert.AreEqual(2, china.Children.Count, "子项应换成新数据");
        Assert.IsTrue(china.Children.All(child => child.Kind == ProblemNodeKind.Competition));
        Assert.IsTrue(china.HasLoaded);
        Assert.IsTrue(china.IsEnabled, "更新成功后分类应恢复可用");
    }

    /// <summary>
    /// 更新成功时，已加载但仍折叠的分类只作废缓存，不应立即读盘。
    /// </summary>
    [TestMethod]
    public async Task OnRepositoryUpdatedAsync_Success_ResetsLoadedButCollapsedCategory()
    {
        var (viewModel, loader, _) = CreateViewModel();
        var china = Category(viewModel, "china");
        await viewModel.LoadChildrenAsync(china);
        var callsBeforeUpdate = loader.CallCount;

        await viewModel.OnRepositoryUpdatedAsync(true);

        Assert.AreEqual(callsBeforeUpdate, loader.CallCount, "折叠的分类不应被动读盘");
        Assert.AreEqual(1, china.Children.Count);
        Assert.AreEqual(ProblemNodeKind.Placeholder, china.Children[0].Kind, "应退回占位子项以保留展开箭头");
        Assert.IsFalse(china.HasLoaded, "缓存应被作废，下次展开重新读取");
        Assert.IsTrue(china.IsEnabled);
    }

    /// <summary>
    /// 更新失败时只禁用尚未加载的分类，已加载内容保持可用可浏览。
    /// </summary>
    [TestMethod]
    public async Task OnRepositoryUpdatedAsync_Failure_DisablesOnlyUnloadedCategories()
    {
        var (viewModel, loader, _) = CreateViewModel();
        loader.Handler = _ => Task.FromResult<IReadOnlyList<ProblemNodeViewModel>>([Competition("loaded-a")]);
        var china = Category(viewModel, "china");
        await viewModel.LoadChildrenAsync(china);

        await viewModel.OnRepositoryUpdatedAsync(false);

        Assert.IsTrue(china.IsEnabled, "已加载的分类应保持可用");
        Assert.AreEqual(1, china.Children.Count, "已加载内容不应被清空");
        Assert.AreEqual(ProblemNodeKind.Competition, china.Children[0].Kind);
        Assert.IsFalse(Category(viewModel, "abroad").IsEnabled);
        Assert.IsFalse(Category(viewModel, "university").IsEnabled);
        Assert.IsFalse(Category(viewModel, "original").IsEnabled);
    }

    /// <summary>
    /// 竞赛条目读取失败时应折叠并禁用节点、标记需要强制更新，并提示用户一次。
    /// </summary>
    [TestMethod]
    public async Task LoadChildrenAsync_WhenLoaderThrows_MarksForcedUpdateAndDisablesNode()
    {
        var (viewModel, loader, notifier) = CreateViewModel();
        var abroad = Category(viewModel, "abroad");
        loader.Handler = _ => throw new InvalidOperationException("info.json 缺失");

        await viewModel.LoadChildrenAsync(abroad);

        Assert.IsFalse(abroad.HasLoaded, "加载失败不应标记为已加载");
        Assert.IsFalse(abroad.IsExpanded);
        Assert.IsFalse(abroad.IsEnabled, "加载失败的分类应被禁用");
        Assert.IsFalse(abroad.IsLoading, "失败后必须收起转圈");
        Assert.AreEqual(1, notifier.Errors.Count, "失败应恰好提示一次");
        Assert.AreEqual("竞赛条目加载错误", notifier.Errors[0].Title);
        Assert.IsTrue(Settings.ForceUpdateRequired, "本地题库不可用时应标记强制更新");

        // 该静态值会写入注册表，测试收尾置回，避免同进程其他用例误读
        Settings.ForceUpdateRequired = false;
    }

    /// <summary>
    /// 竞赛条目读取成功时应替换占位子项、回填父节点引用且不提示任何错误。
    /// </summary>
    [TestMethod]
    public async Task LoadChildrenAsync_Success_PopulatesChildrenAndParents()
    {
        var (viewModel, loader, notifier) = CreateViewModel();
        loader.Handler = _ => Task.FromResult<IReadOnlyList<ProblemNodeViewModel>>(
            [Competition("2024-cmo"), Competition("2024-cpho")]);
        var china = Category(viewModel, "china");

        await viewModel.LoadChildrenAsync(china);

        Assert.AreEqual(2, china.Children.Count);
        Assert.IsTrue(china.Children.All(child => child.Kind == ProblemNodeKind.Competition), "占位子项应被真实数据替换");
        Assert.IsTrue(china.Children.All(child => ReferenceEquals(child.Parent, china)), "子节点应回指父分类");
        Assert.IsTrue(china.HasLoaded);
        Assert.IsTrue(china.IsEnabled, "加载成功不应改变可用状态");
        Assert.IsFalse(china.IsLoading);
        Assert.AreEqual(0, notifier.Errors.Count);
    }
}
