using LibGit2Sharp;
using ProblemViewer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
            RepositoryURLTextBox.Text = Settings.RepositoryURL;
            AutoUpdateCheckBox.IsChecked = Settings.AutoUpdate;
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
            Settings.SaveSettings();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
        }
    }
}
