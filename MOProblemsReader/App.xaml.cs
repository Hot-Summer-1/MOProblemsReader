using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using ProblemViewer;

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

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // SystemEvents 是静态事件，会持有 App 的强引用，不退订会导致应用对象无法回收
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            Settings.SaveSettings();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            InitializeAccentBrush();

            Settings.LoadSettings();
            if (Settings.ForceUpdateRequired)
            {
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
