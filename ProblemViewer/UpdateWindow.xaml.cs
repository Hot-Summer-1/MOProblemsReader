using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ProblemViewer
{
    public class RepositoryUpdatedEventArgs(bool success) : EventArgs
    {
        public bool Success
        {
            get;
        } = success;
    }
    /// <summary>
    /// UpdateWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UpdateWindow : Window
    {
        public event EventHandler<RepositoryUpdatedEventArgs>? RepositoryUpdated;
        public UpdateWindow()
        {
            InitializeComponent();
            /*var UpdateThread = new Thread(() =>
            {
                RepositoryManager.UpdateRepositoryWorker();
                Dispatcher.Invoke(() => Close());
            });
            UpdateThread.Start();*/
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (RepositoryManager.Working) e.Cancel = true;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Run(() => { RepositoryManager.UpdateRepositoryWorker(); });
                RepositoryUpdated?.Invoke(this, new RepositoryUpdatedEventArgs(true));
            }
            catch (RepositoryUpdateException ex)
            {
                Log.Error(ex,
                    "Repository update failed. Local Repository Path: {LocalRepositoryPath}; RepositoryURL:{RepositoryURL}",
                    Settings.LocalRepositoryPath, Settings.RepositoryURL);
                MessageBox.Show(this, ex.Message, "存储库更新失败", MessageBoxButton.OK, MessageBoxImage.Error);
                RepositoryUpdated?.Invoke(this, new RepositoryUpdatedEventArgs(false));
                Close();
            }

            Close();
        }
    }
}
