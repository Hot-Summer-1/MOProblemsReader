using MOProblemsReader.ViewModels;
using ProblemViewer;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MOProblemsReader
{

    /// <summary>
    /// 主窗口：题库树的数据、加载与刷新由 <see cref="MainWindowViewModel"/> 负责，
    /// 这里只保留命令、菜单与窗口级事件接线。
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 「更新」命令，供菜单项与 Ctrl+U 快捷键共用同一执行入口。
        /// </summary>
        /// <remarks>
        /// 用 RoutedUICommand 而非额外引入 MVVM 命令库，可让 XAML 中的菜单、快捷键
        /// 与 code-behind 处理器在同一个路由命令上集中绑定，避免逻辑出现两份。
        /// </remarks>
        public static RoutedUICommand UpdateCommand { get; } =
            new("更新", nameof(UpdateCommand), typeof(MainWindow));

        /// <summary>
        /// 题库树视图模型，窗口级唯一实例；树的节点集合与状态全部由它持有。
        /// </summary>
        private readonly MainWindowViewModel _viewModel;

        /// <summary>
        /// 创建主窗口：装配视图模型并把退出模式恢复为「关闭最后一个窗口即退出」。
        /// </summary>
        public MainWindow()
        {
            // 启动强制更新时退出模式被临时改为 OnExplicitShutdown，主窗口出现后必须改回，否则关窗不会退出
            Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
            InitializeComponent();

            // 视图负责提供数据来源与提示出口，使视图模型不直接依赖 WPF
            _viewModel = new MainWindowViewModel(new ProblemTreeLoader(), new MessageBoxUserNotifier(this));
            DataContext = _viewModel;
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 仓库更新完成的回调：交给视图模型决定分类节点的可用性与是否需要重新加载。
        /// </summary>
        /// <param name="sender">事件源（<see cref="RepositoryManager"/>），此处不使用。</param>
        /// <param name="e">事件参数，其 <c>Success</c> 表示更新是否成功。</param>
        private async void RepositoryManager_RepositoryUpdated(object? sender, RepositoryUpdatedEventArgs e)
            => await _viewModel.OnRepositoryUpdatedAsync(e.Success);

        /// <summary>
        /// 打开"关于"对话框。
        /// </summary>
        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow
            {
                Owner = this  // 设置 Owner 使关于窗口居中于主窗口
            };
            aboutWindow.ShowDialog();  // 模态显示
        }

        /// <summary>
        /// 「更新」命令的执行处理器：无论是点击菜单项还是按下 Ctrl+U，都从这里进入更新流程。
        /// </summary>
        /// <param name="sender">命令路由的发送者，此处不使用。</param>
        /// <param name="e">命令执行参数，此处不使用。</param>
        private void UpdateCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            RepositoryManager.UpdateRepository(this);
        }

        private void SourceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var sourceWindow = new RepositorySettingWindow
            {
                Owner = this
            };
            sourceWindow.ShowDialog();
        }

        /// <summary>
        /// 窗口加载时订阅仓库更新事件，使题库树能随更新结果刷新或禁用。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RepositoryManager.RepositoryUpdated += RepositoryManager_RepositoryUpdated;
        }

        /// <summary>
        /// 窗口关闭时退订仓库更新事件。<see cref="RepositoryManager.RepositoryUpdated"/> 是静态事件，不退订会一直持有窗口引用。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
        private void Window_Closed(object sender, EventArgs e)
        {
            RepositoryManager.RepositoryUpdated -= RepositoryManager_RepositoryUpdated;
        }

        /// <summary>
        /// 双击年份节点时的入口：目前仅以提示框呈现「年份-赛事-分类」定位信息，
        /// 真实题目内容视图尚未接入（保留原有 TODO）。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">鼠标事件参数，其 <c>OriginalSource</c> 用于定位被双击的树节点。</param>
        private void ProblemTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!TryFindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject, out var item))
            {
                return;
            }

            // 模板渲染下 TreeViewItem 的 DataContext 就是对应的节点视图模型
            if (item.DataContext is not ProblemNodeViewModel { Kind: ProblemNodeKind.Year } node)
            {
                return;
            }

            // 文案与重构前保持一致：年份 - 赛事 - 分类
            MessageBox.Show(
                $"{node.DisplayName}\n{node.Parent?.DisplayName}\n{node.Parent?.Parent?.DisplayName}",
                "题目",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// 从命中的可视元素向上查找指定类型的祖先容器。
        /// </summary>
        /// <typeparam name="T">要查找的容器类型。</typeparam>
        /// <param name="source">事件命中的原始可视元素，可为 <c>null</c>。</param>
        /// <param name="result">找到的祖先容器；未找到时为 <c>null</c>。</param>
        /// <returns>找到指定类型的祖先容器时为 <c>true</c>。</returns>
        private static bool TryFindAncestor<T>(DependencyObject? source, out T result) where T : DependencyObject
        {
            while (source is not null)
            {
                if (source is T match)
                {
                    result = match;
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            result = null!;
            return false;
        }
    }
}
