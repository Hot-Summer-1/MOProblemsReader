using ProblemViewer;
using Serilog;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Path = System.IO.Path;

namespace MOProblemsReader
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Dictionary<string, bool> loaddict = new()
        {
            ["china"] = false,
            ["abroad"] = false,
            ["university"] = false,
            ["original"] = false
        };
        /// <summary>
        /// 「加载中」占位子项的标记值，必须与 MainWindow.xaml 中占位项的 Tag 字面量保持一致。
        /// </summary>
        private const string LoadingPlaceholderTag = "LoadingPlaceholder";

        /// <summary>
        /// 「加载中」占位模板的资源键，定义在 MainWindow.xaml 的 Window.Resources 中。
        /// </summary>
        private const string LoadingIndicatorTemplateKey = "LoadingIndicatorTemplate";

        /// <summary>
        /// 「更新」命令，供菜单项与 Ctrl+U 快捷键共用同一执行入口。
        /// </summary>
        /// <remarks>
        /// 用 RoutedUICommand 而非额外引入 MVVM 命令库，可让 XAML 中的菜单、快捷键
        /// 与 code-behind 处理器在同一个路由命令上集中绑定，避免逻辑出现两份。
        /// </remarks>
        public static RoutedUICommand UpdateCommand { get; } =
            new("更新", nameof(UpdateCommand), typeof(MainWindow));

        public MainWindow()
        {
            Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
            InitializeComponent();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RepositoryManager_RepositoryUpdated(object? sender, RepositoryUpdatedEventArgs e)
        {
            if (e.Success)
            {
                Log.Information("Repository updated. Reload the nodes.");
                china.IsEnabled = true;
                abroad.IsEnabled = true;
                university.IsEnabled = true;
                original.IsEnabled = true;
                RefreshNode(china);
                RefreshNode(abroad);
                RefreshNode(university);
                RefreshNode(original);
            }
            else
            {
                Log.Warning("Repository update failed. Now disable the unloaded nodes.");
                ProtectNode(china);
                ProtectNode(abroad);
                ProtectNode(university);
                ProtectNode(original);
            }
        }

        private void ProtectNode(TreeViewItem node)
        {
            if (!loaddict[node.Name])
            {
                node.IsExpanded = false;
                node.IsEnabled = false;
            }
        }
        private void RefreshNode(TreeViewItem node)
        {
            if (loaddict[node.Name])
            {
                node.ItemsSource = new List<TreeViewItem> { CreateLoadingPlaceholder() };
                loaddict[node.Name] = false;
                if (node.IsExpanded)
                {
                    UpdateNode(node);
                }
            }

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
        /// 树节点展开时的统一入口：回收「加载中」占位子项，并触发该节点的子节点加载。
        /// </summary>
        /// <param name="sender">事件源。由于挂接在 TreeView 上，此处通常是 TreeView 而非被展开的节点。</param>
        /// <param name="e">路由事件参数，其中 OriginalSource 才是真正被展开的 TreeViewItem。</param>
        private void ProblemTreeView_ItemExpanded(object sender, RoutedEventArgs e)
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
            UpdateNode(node);
        }

        /// <summary>
        /// 加载指定节点的子节点。当前仅搭建 UI 架子，尚未接入真实题库数据。
        /// </summary>
        /// <param name="node">需要加载子节点的树节点（国内 / 国外 / 大学 / 原创 之一）。</param>
        private List<CompetitionInfo> LoadChildrenAsync(string nodeName)
        {
            // TODO: 接入真实数据时在此按 node 异步拉取题目集合并逐个添加为子节点。
            string CompetitonPath = Path.Combine(Settings.LocalRepositoryPath, "problems");
            switch (nodeName)
            {
                case "china":
                case "abroad":
                case "university":
                case "original":
                    CompetitonPath = Path.Combine(CompetitonPath, nodeName);
                    break;
                default:
                    throw new ArgumentException($"Invalid node name: {nodeName}.");
            }

            var compInfoItems = new List<CompetitionInfo>();
            foreach (string dir in Directory.EnumerateDirectories(CompetitonPath))
            {
                try
                {
                    string name = new DirectoryInfo(dir).Name, infofile = Path.Combine(dir, "info.json");
                    string jsonInfoString = File.ReadAllText(infofile);
                    CompetitionInfo compinfo = JsonSerializer.Deserialize<CompetitionInfo>(jsonInfoString)!;
                    compinfo.name = name;
                    compInfoItems.Add(compinfo);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Load competition {name} failed.", Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                }
            }
            return compInfoItems;
        }

        private async void UpdateNode(TreeViewItem node)
        {
            var nodeName = node.Name;
            Log.Debug("Begin to load competitions under node {nodeName}.", nodeName);
            try
            {
                var item = await Task.Run(() => LoadChildrenAsync(nodeName));
                var nodeitemlist = new List<TreeViewItem>();
                foreach (CompetitionInfo comp in item)
                {
                    var comptreeviewitem = new TreeViewItem
                    {
                        Name = comp.name,
                        Header = comp.display_name,
                        Tag="comp"
                    };
                    var yearlist = new List<TreeViewItem>();
                    foreach (YearInfo year in comp.years)
                    {
                        var yeartreeviewitem = new TreeViewItem
                        {
                            Header = year.name,
                            Tag="year"
                        };
                        yearlist.Add(yeartreeviewitem);
                    }
                    comptreeviewitem.ItemsSource = yearlist;
                    nodeitemlist.Add(comptreeviewitem);
                }
                RemoveLoadingPlaceholder(node);
                node.ItemsSource = nodeitemlist;
                loaddict[nodeName] = true;
            }
            catch (Exception ex)
            {
                // 记录节点名，便于根据日志定位是哪个分类的题库数据损坏
                Log.Error(ex, "Failed to load competitions under node {NodeName}.", nodeName);
                MessageBox.Show(this, $"加载竞赛条目时遇到错误: {ex.Message}", "竞赛条目加载错误", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                node.IsExpanded = false;
                node.IsEnabled = false;
                Settings.ForceUpdateRequired = true;
            }
            Log.Debug("Finish loading competitions under node {nodeName}.", nodeName);
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
            if (node.ItemsSource != null) return;
            for (int i = node.Items.Count - 1; i >= 0; i--)
            {
                if (node.Items[i] is TreeViewItem treeItem && LoadingPlaceholderTag.Equals(treeItem.Tag))
                {
                    node.Items.RemoveAt(i);
                }
            }
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RepositoryManager.RepositoryUpdated += RepositoryManager_RepositoryUpdated;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            RepositoryManager.RepositoryUpdated -= RepositoryManager_RepositoryUpdated;
        }

        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var senderItem = (TreeViewItem)sender;
            var source = e.OriginalSource as DependencyObject;
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);
            if (source == senderItem)
            {
                if (senderItem.Tag is not string tag||tag != "year") return; 
                var compItem = (TreeViewItem)ItemsControl.ItemsControlFromItemContainer(senderItem);
                var categoryItem = (TreeViewItem)ItemsControl.ItemsControlFromItemContainer(compItem);
                MessageBox.Show($"点击了{senderItem.Header}-{compItem.Header}({compItem.Name})-{categoryItem.Header}");
            }
        }
    }
}
