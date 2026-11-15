using Serilog;
using System.Windows;

namespace ProblemViewer
{

    /// <summary>
    /// UpdateWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UpdateWindow : Window
    {
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
                RepositoryManager.InvokeRepositoryUpdated(true);
            }
            catch (RepositoryUpdateException ex)
            {
                Log.Error(ex,
                    "Repository update failed. Local Repository Path: {LocalRepositoryPath}; RepositoryURL:{RepositoryURL}",
                    Settings.LocalRepositoryPath, Settings.RepositoryURL);
                RepositoryManager.InvokeRepositoryUpdated(false);
                MessageBox.Show(this, ex.Message, "存储库更新失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Close();
            }
        }
    }
}
