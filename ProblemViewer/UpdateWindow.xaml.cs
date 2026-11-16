using Serilog;
using System.ComponentModel;
using System.Windows;

namespace ProblemViewer
{

    /// <summary>
    /// 仓库更新进度窗口：显示期间在后台执行更新，结束后自动关闭并按结果决定是否广播。
    /// </summary>
    public partial class UpdateWindow : Window
    {
        /// <summary>
        /// 本次更新的结果；窗口关闭后由 <see cref="RepositoryManager.UpdateRepository"/> 读取。
        /// </summary>
        public UpdateOutcome Outcome { get; private set; } = UpdateOutcome.Cancelled;

        /// <summary>
        /// 用户是否已请求取消本次更新；取消本身引起的中止不该再弹错误提示。
        /// </summary>
        private bool _cancelRequested;

        /// <summary>
        /// 创建更新进度窗口。
        /// </summary>
        public UpdateWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 更新进行中关闭窗口等同于请求取消：只发出取消请求，窗口仍需等待更新流程在安全点结束后才关闭。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">关闭事件参数，把 <c>Cancel</c> 置为 <c>true</c> 可取消本次关闭。</param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (RepositoryManager.Working)
            {
                RequestCancel();
                e.Cancel = true;
            }
        }

        /// <summary>
        /// 窗口显示后在后台线程执行仓库更新；成功后广播结果，不可恢复错误在此统一记录日志并弹错误提示，取消则静默结束。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Outcome = await Task.Run(() => RepositoryManager.UpdateRepositoryWorker());
                switch (Outcome)
                {
                    case UpdateOutcome.Success:
                        RepositoryManager.InvokeRepositoryUpdated(true);
                        break;

                    default:
                        // 取消是用户的预期操作：仓库保持更新前状态，既不提示错误也不广播事件
                        Log.Information("Repository update cancelled by user.");
                        break;
                }
            }
            catch (RepositoryUpdateException ex)
            {
                Outcome = UpdateOutcome.Failed;
                Log.Error(ex,
                    "Repository update failed. Local Repository Path: {LocalRepositoryPath}; RepositoryURL:{RepositoryURL}",
                    Settings.LocalRepositoryPath, Settings.RepositoryURL);

                // 本地仓库无法还原到更新前状态时必须通知界面按失败处理，否则界面会继续展示与实际磁盘不一致的题库
                if (!ex.Reverted)
                {
                    RepositoryManager.InvokeRepositoryUpdated(false);
                }

                // 异常本身只写阶段说明，真正的原因在内部异常里，弹窗需要两者都给用户看；
                // 取消后仍在执行的更新阶段失败不再弹窗，但取消收尾（回滚）阶段的失败必须告知用户
                if (!_cancelRequested || ex.Cancelled)
                {
                    MessageBox.Show(this, $"{ex.Message}\n\n{ex.InnerException?.Message}", "存储库更新失败",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                Close();
            }
        }

        /// <summary>
        /// 「取消」按钮：请求取消更新，并把按钮切换为不可再点击的状态。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RequestCancel();
        }

        /// <summary>
        /// 发出取消请求并反馈到界面。
        /// </summary>
        /// <remarks>
        /// 取消只在更新流程的安全点生效（正在覆盖本地仓库或对齐远端分支时会被忽略），
        /// 因此这里不关窗，窗口仍由更新结束后的收尾逻辑关闭。
        /// 重复调用安全：取消源已置为取消状态，按钮则会保持禁用。
        /// </remarks>
        private void RequestCancel()
        {
            _cancelRequested = true;
            RepositoryManager.Cancel();
            CancelButton.IsEnabled = false;
            Title= "正在取消";
        }
    }
}
