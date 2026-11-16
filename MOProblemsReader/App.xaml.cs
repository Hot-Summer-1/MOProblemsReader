using System.IO;
using Microsoft.Win32;
using MOProblemsReader.Logging;
using ProblemViewer;
using Serilog;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MOProblemsReader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 应用资源中强调色画刷的键名，界面通过 DynamicResource 引用它。
        /// </summary>
        private const string AccentBrushResourceKey = "SystemAccentBrush";

        /// <summary>
        /// 系统强调色所在的注册表子键（相对 HKEY_CURRENT_USER）。
        /// </summary>
        private const string DwmRegistryKeyPath = @"Software\Microsoft\Windows\DWM";

        /// <summary>
        /// 注册表值名：Windows 强调色，类型为 REG_DWORD，字节序为 ABGR。
        /// </summary>
        private const string AccentColorValueName = "AccentColor";

        /// <summary>
        /// 应用退出前的收尾：退订静态事件、保存设置并刷写日志缓冲。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">退出事件参数，此处不使用。</param>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // SystemEvents、AppDomain、TaskScheduler 都是静态事件源，会持有 App 的强引用，
            // 不退订会导致应用对象无法回收
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
            Settings.SaveSettings();
            if (Settings.ForceUpdateRequired) Log.Information("ForceUpdateRequired is set to true, the repository will be forced to update at next launch.");
            Log.Information("Application exited normally.");
            // 退出前必须刷盘，否则缓冲区中最后几条日志会随进程结束而丢失
            LoggingExtension.CloseAndFlush();
        }

        /// <summary>
        /// UI 线程未处理异常的回调：记录致命日志、提示用户并立即刷盘。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，携带导致崩溃的异常。</param>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (Settings.ForceUpdateRequired) Log.Information("ForceUpdateRequired is set to true, the repository will be forced to update at next launch.");
            Log.Fatal(e.Exception, "Unhandled exception on UI thread, application is shutting down.");
            // 未设置 e.Handled，应用随后会被终止，走不到 Application_Exit 的刷盘逻辑，所以此处显式刷盘
            LoggingExtension.CloseAndFlush();
            MessageBox.Show($"应用程序遇到错误: {e.Exception.Message}", "应用程序错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// 非 UI 线程上未处理异常的回调：此时进程通常会被运行时终止，只能尽量留下现场记录。
        /// </summary>
        /// <param name="sender">事件源（当前 AppDomain）。</param>
        /// <param name="e">事件参数，<see cref="UnhandledExceptionEventArgs.IsTerminating"/> 指示进程是否会终止。</param>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (Settings.ForceUpdateRequired) Log.Information("ForceUpdateRequired is set to true, the repository will be forced to update at next launch.");
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception on a non-UI thread, process is terminating. IsTerminating: {IsTerminating}", e.IsTerminating);
            // 进程马上终止，先把这条日志落盘
            LoggingExtension.CloseAndFlush();
        }

        /// <summary>
        /// 未被观察的任务异常回调：常见于 <c>async void</c> 或未 await 的 Task，记录后可定位遗漏的 await。
        /// </summary>
        /// <param name="sender">事件源（通常为 null）。</param>
        /// <param name="e">事件参数，包含未被观察的聚合异常。</param>
        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unobserved exception in a background task.");
        }

        /// <summary>
        /// 启动流程：先建立日志与全局异常处理，再跟随系统强调色、加载设置；仓库为空或需要强制更新时先更新仓库。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">启动事件参数，此处不使用。</param>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 日志必须在其它初始化之前建立，否则启动阶段的异常会无处记录
            LoggingExtension.Initialize();
            Log.Information("Application started.");

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            InitializeAccentBrush();

            Settings.LoadSettings();
            if (!Directory.Exists(Settings.LocalRepositoryPath) ||
                !Directory.EnumerateFileSystemEntries(Settings.LocalRepositoryPath).Any())
                Settings.ForceUpdateRequired = true;
            if (Settings.ForceUpdateRequired)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                RepositoryManager.UpdateRepository();
            }
        }

        /// <summary>
        /// 注册系统强调色画刷到应用资源，并订阅系统颜色变化事件。
        /// </summary>
        private void InitializeAccentBrush()
        {
            Resources[AccentBrushResourceKey] = CreateAccentBrush();

            // 系统在运行中修改强调色或切换浅色/深色时即时跟随，无需重启应用
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        /// <summary>
        /// 依据当前系统强调色创建画刷。
        /// </summary>
        /// <returns>已冻结的强调色画刷，可安全跨线程共享。</returns>
        private static SolidColorBrush CreateAccentBrush()
        {
            var brush = new SolidColorBrush(ReadSystemAccentColor());
            // 画刷创建后不再修改（刷新时整体替换），冻结可省去一次渲染侧的拷贝开销
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// 读取 Windows 强调色。
        /// </summary>
        /// <returns>取到有效值时返回用户强调色；读取失败或类型不符时退化为系统高亮色。</returns>
        private static Color ReadSystemAccentColor()
        {
            object? value = Registry.GetValue(
                @"HKEY_CURRENT_USER\" + DwmRegistryKeyPath,
                AccentColorValueName,
                null);

            if (value is not int raw)
            {
                // 拿不到注册表值时给出一个可用颜色，避免画刷退化成默认黑色
                return SystemColors.HighlightColor;
            }

            // AccentColor 虽是 REG_DWORD，但字节排列是 ABGR：最低字节为 R，其次 G、B，最高字节是 alpha（通常为 0）
            byte r = (byte)(raw & 0xFF);
            byte g = (byte)((raw >> 8) & 0xFF);
            byte b = (byte)((raw >> 16) & 0xFF);
            return Color.FromRgb(r, g, b);
        }

        /// <summary>
        /// 系统偏好变更回调：强调色或浅色/深色变化时刷新强调色画刷。
        /// </summary>
        /// <param name="sender">事件源（静态 SystemEvents）。</param>
        /// <param name="e">事件参数，其 Category 指明变更类别。</param>
        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // 强调色与浅色/深色切换会落在 General 或 VisualStyle 类别，其余类别无关
            if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.VisualStyle)
            {
                return;
            }

            Log.Debug("Accent color changed.");
            // 该回调可能由非 UI 线程触发，操作 ResourceDictionary 必须切回 UI 线程
            Dispatcher.Invoke(RefreshAccentBrush);
        }

        /// <summary>
        /// 用最新的系统强调色替换应用资源中的画刷实例，已绑定 DynamicResource 的界面会自动更新。
        /// </summary>
        private void RefreshAccentBrush()
        {
            Resources[AccentBrushResourceKey] = CreateAccentBrush();
        }
    }

}
