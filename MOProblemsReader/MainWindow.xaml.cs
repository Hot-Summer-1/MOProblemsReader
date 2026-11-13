using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LibGit2Sharp;
using ProblemViewer;
using Path = System.IO.Path;

namespace MOProblemsReader
{
    
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 「加载中」占位子项的标记值，必须与 MainWindow.xaml 中占位项的 Tag 字面量保持一致。
        /// </summary>
        private const string LoadingPlaceholderTag = "LoadingPlaceholder";

        /// <summary>
        /// 「加载中」占位模板的资源键，定义在 MainWindow.xaml 的 Window.Resources 中。
        /// </summary>
        private const string LoadingIndicatorTemplateKey = "LoadingIndicatorTemplate";

        public MainWindow()
        {
            Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
            InitializeComponent();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

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

        private void UpdateMenuItem_Click(object sender, RoutedEventArgs e)
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
        /// 树节点展开时的统一入口：回收「加载中」占位子项，并触发该节点的子节点加载。
        /// </summary>
        /// <param name="sender">事件源。由于挂接在 TreeView 上，此处通常是 TreeView 而非被展开的节点。</param>
        /// <param name="e">路由事件参数，其中 OriginalSource 才是真正被展开的 TreeViewItem。</param>
        private async void ProblemTreeView_ItemExpanded(object sender, RoutedEventArgs e)
        {
            // Expanded 是冒泡路由事件：任意深度节点展开都会冒泡到 TreeView，
            // 因此必须用 OriginalSource 定位真正展开的节点，不能直接强转 sender。
            if (e.OriginalSource is not TreeViewItem node)
            {
                return;
            }

            if (ItemsControl.ItemsControlFromItemContainer(node) is not TreeView)
            {
                return;
            }
            // 只有仍处于「占位状态」的节点才需要走首次加载；
            // 已填充真实数据的节点再次展开时不应重复请求，避免反复加载。
            if (!HasLoadingPlaceholder(node))
            {
                return;
            }
            var nodeName = node.Name;
            try
            {
                var item = await Task.Run(() => LoadChildrenAsync(nodeName));
                RemoveLoadingPlaceholder(node);
                var nodeitemlist = new List<TreeViewItem>();
                foreach (CompetitionInfo comp in item)
                {
                    var comptreeviewitem = new TreeViewItem
                    {
                        Name = comp.name,
                        Header = comp.display_name
                    };
                    var yearlist = new List<TreeViewItem>();
                    foreach (YearInfo year in comp.years)
                    {
                        var yeartreeviewitem = new TreeViewItem
                        {
                            Header = year.name
                        };
                        yearlist.Add(yeartreeviewitem);
                    }
                    comptreeviewitem.ItemsSource = yearlist;
                    nodeitemlist.Add(comptreeviewitem);
                }
                node.ItemsSource = nodeitemlist;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"加载竞赛条目时遇到错误: {ex.Message}", "竞赛条目加载错误", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                node.IsExpanded = false;
                node.IsEnabled = false;
            }
            
        }

        /// <summary>
        /// 加载指定节点的子节点。当前仅搭建 UI 架子，尚未接入真实题库数据。
        /// </summary>
        /// <param name="node">需要加载子节点的树节点（国内 / 国外 / 大学 / 原创 之一）。</param>
        private List<CompetitionInfo> LoadChildrenAsync(string nodeName)
        {
            // TODO: 接入真实数据时在此按 node 异步拉取题目集合并逐个添加为子节点。
            string CompetitonPath = Path.Combine(Settings.LocalRepositoryPath,"problems");
            switch (nodeName)
            {
                case "china":
                case "abroad":
                case "university":
                case "original": 
                    CompetitonPath = Path.Combine(CompetitonPath, nodeName);
                    break;
                default:
                    throw new ArgumentException($"Invalid node name:{nodeName}.");
            }

            var compInfoItems = new List<CompetitionInfo>();
            foreach (string dir in Directory.EnumerateDirectories(CompetitonPath))
            {
                string name = new DirectoryInfo(dir).Name, infofile = Path.Combine(dir, "info.json");
                string jsonInfoString = File.ReadAllText(infofile);
                CompetitionInfo compinfo = JsonSerializer.Deserialize<CompetitionInfo>(jsonInfoString)!;
                compinfo.name = name;
                compInfoItems.Add(compinfo);
            }
            return compInfoItems;
        }

        /// <summary>
        /// 判断指定节点下是否还存在「加载中」占位子项。
        /// </summary>
        /// <param name="node">待检查的树节点。</param>
        /// <returns>存在占位子项返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        private static bool HasLoadingPlaceholder(TreeViewItem node)
        {
            foreach (object item in node.Items)
            {
                if (item is TreeViewItem treeItem && LoadingPlaceholderTag.Equals(treeItem.Tag))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 移除指定节点下所有「加载中」占位子项，保留真实数据项。
        /// </summary>
        /// <param name="node">待清理的树节点。</param>
        private static void RemoveLoadingPlaceholder(TreeViewItem node)
        {
            // 按 Tag 识别而不是按下标删除：节点一旦填充真实数据，
            // 占位项的位置就不再固定，按下标会误删真实数据项。
            // 倒序遍历可避免边删边改集合导致索引错位。
            for (int i = node.Items.Count - 1; i >= 0; i--)
            {
                if (node.Items[i] is TreeViewItem treeItem && LoadingPlaceholderTag.Equals(treeItem.Tag))
                {
                    node.Items.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 在指定节点下重新显示「加载中」转圈占位，供后续异步加载过程中复用。
        /// </summary>
        /// <param name="node">需要显示加载状态的树节点。</param>
        private void BeginLoading(TreeViewItem node)
        {
            // 幂等保护：已有占位时直接返回，避免反复展开/加载叠加出多个转圈。
            if (HasLoadingPlaceholder(node))
            {
                return;
            }

            node.Items.Add(CreateLoadingPlaceholder());
        }

        /// <summary>
        /// 隐藏指定节点下的「加载中」占位，表示加载已结束。
        /// </summary>
        /// <param name="node">需要结束加载状态的树节点。</param>
        private void EndLoading(TreeViewItem node)
        {
            RemoveLoadingPlaceholder(node);
        }

        /// <summary>
        /// 以代码方式创建一个复用 XAML 模板的「加载中」占位子项。
        /// </summary>
        /// <returns>已配置模板与标记的占位 <see cref="TreeViewItem"/>。</returns>
        private static TreeViewItem CreateLoadingPlaceholder()
        {
            var placeholder = new TreeViewItem
            {
                // Header 必须是非 null 值，否则 ContentPresenter 不会套用 HeaderTemplate；
                // 实际显示被模板完全覆盖，用户看不到这个占位文本。
                Header = "Loading",
                Tag = LoadingPlaceholderTag,
                IsHitTestVisible = false,
                Focusable = false
            };

            // 用资源引用而非直接塞模板实例，样式改动只需改 XAML 一处。
            placeholder.SetResourceReference(HeaderedItemsControl.HeaderTemplateProperty, LoadingIndicatorTemplateKey);
            return placeholder;
        }
    }
}
