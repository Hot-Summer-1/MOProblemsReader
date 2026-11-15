using System.Reflection;
using System.Windows;

namespace MOProblemsReader
{
    /// <summary>
    /// "关于"对话框窗口。
    /// </summary>
    public partial class AboutWindow : Window
    {
        /// <summary>
        /// 初始化关于窗口，自动从程序集读取版本号和版权信息。
        /// </summary>
        public AboutWindow()
        {
            InitializeComponent();

            // 从程序集动态读取版本号，不硬编码
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionTextBlock.Text = $"版本 {version?.Major ?? 1}.{version?.Minor ?? 0}.{version?.Build ?? 0}";

            // 从程序集动态读取版权信息，不硬编码
            var copyright = Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)
                .OfType<AssemblyCopyrightAttribute>()
                .FirstOrDefault()?.Copyright;
            if (!string.IsNullOrEmpty(copyright))
            {
                CopyrightTextBlock.Text = copyright;
            }
        }

        /// <summary>
        /// 点击"确定"按钮关闭窗口。
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
