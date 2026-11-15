using ProblemViewer;
using System.Windows;
using Serilog.Events;

namespace MOProblemsReader
{
    /// <summary>
    /// RepositorySettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class RepositorySettingWindow : Window
    {
        public RepositorySettingWindow()
        {
            InitializeComponent();
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (RepositoryURLTextBox.Text != Settings.RepositoryURL)
            {
                if (MessageBox.Show("是否保存对题源URL的更改？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                Settings.RepositoryURL = RepositoryURLTextBox.Text;
                Settings.ForceUpdateRequired = true;
                Settings.SaveSettings();

            }
            RepositoryManager.UpdateRepository(Application.Current.MainWindow);

        }
        private void ApplySettings()
        {
            if (Settings.RepositoryURL != RepositoryURLTextBox.Text)
            {
                Settings.ForceUpdateRequired = true;
            }
            Settings.RepositoryURL = RepositoryURLTextBox.Text;
            Settings.AutoUpdate = AutoUpdateCheckBox.IsChecked ?? true;
            Settings.LogLevel = (LogEventLevel)LogLevelComboBox.SelectedItem;
            Settings.SaveSettings();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RepositoryURLTextBox.Text = Settings.RepositoryURL;
            AutoUpdateCheckBox.IsChecked = Settings.AutoUpdate;
            LogLevelComboBox.SelectedItem = Settings.LogLevel;
        }
    }
}
