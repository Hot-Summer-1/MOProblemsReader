using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ProblemViewer;
namespace MOProblemsReader
{
    
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Settings.LoadSettings();
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
            RepositoryManager.UpdateRepository();
        }

        private void SourceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var sourceWindow = new RepositorySettingWindow
            {
                Owner = this
            };
            sourceWindow.ShowDialog();
        }
    }
}