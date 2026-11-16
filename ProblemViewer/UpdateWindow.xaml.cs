using Serilog;
using System.Windows;

namespace ProblemViewer
{

    /// <summary>
    /// 仓库更新进度窗口：显示期间在后台执行更新，结束后自动关闭并广播结果。
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

        /// <summary>
        /// 更新进行中阻止关闭窗口，避免用户误关打断更新流程。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">关闭事件参数，把 <c>Cancel</c> 置为 <c>true</c> 可取消本次关闭。</param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (RepositoryManager.Working) e.Cancel = true;
        }

        /// <summary>
        /// 窗口显示后在后台线程执行仓库更新并广播结果；无论成功与否最后都关闭本窗口。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
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
