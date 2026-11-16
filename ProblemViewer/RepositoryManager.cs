using Serilog;
using System.IO;
using System.Windows;

namespace ProblemViewer
{
    /// <summary>
    /// 仓库更新（删除旧目录、clone 或 pull）过程中发生错误时抛出的异常。
    /// </summary>
    public class RepositoryUpdateException : Exception
    {
        /// <summary>
        /// 创建异常实例。
        /// </summary>
        /// <param name="message">错误说明，可为 <c>null</c>。</param>
        /// <param name="innerException">引起本次失败的原始异常，可为 <c>null</c>。</param>
        public RepositoryUpdateException(string? message = null, Exception? innerException = null) : base(message, innerException)
        {

        }
    }

    /// <summary>
    /// 仓库更新结束时携带的事件数据。
    /// </summary>
    /// <param name="success">更新是否成功。</param>
    public class RepositoryUpdatedEventArgs(bool success) : EventArgs
    {
        /// <summary>
        /// 更新是否成功。
        /// </summary>
        public bool Success
        {
            get;
        } = success;
    }


    /// <summary>
    /// 题库仓库（git 仓库）的更新入口：用模态进度窗口驱动后台 clone/pull，并在结束后广播结果。
    /// </summary>
    public static class RepositoryManager

    {
        /// <summary>
        /// 当前是否正在执行仓库更新。更新期间为 <c>true</c>，进度窗口据此拒绝关闭。
        /// </summary>
        public static bool Working { get; private set; } = false;

        /// <summary>
        /// 仓库更新结束时触发（成功与失败都会触发），参数中的 <c>Success</c> 表示结果。
        /// </summary>
        public static event EventHandler<RepositoryUpdatedEventArgs>? RepositoryUpdated;

        /// <summary>
        /// 在 UI 线程以模态方式显示进度窗口并执行仓库更新，等待更新结束后才返回。
        /// </summary>
        /// <param name="parentwindow">进度窗口的属主窗口，可为 <c>null</c>。</param>
        public static void UpdateRepository(Window? parentwindow = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {

                var updateWindow = new UpdateWindow
                {
                    Owner = parentwindow,
                };

                updateWindow.ShowDialog();

            });

        }
        /// <summary>
        /// 实际执行仓库更新的工作方法（耗时操作，应在后台线程调用）：按需清空旧目录后 clone 或 pull，
        /// 成功后清除强制更新标记。
        /// </summary>
        /// <exception cref="RepositoryUpdateException">删除旧目录、clone 或 pull 任一步骤失败时抛出。</exception>
        public static void UpdateRepositoryWorker()
        {
            Working = true;
            Log.Information("Begin repository update. RepositoryURL: {RepositoryURL}", Settings.RepositoryURL);
            if (Settings.ForceUpdateRequired)
            {
                /*var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c rd /s /q " + Settings.LocalRepositoryPath,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var p = Process.Start(startInfo);
                p.WaitForExit();*/
                Log.Information("Force update is required. Now deleting existed repository.");
                try
                {
                    foreach (string file in Directory.EnumerateFileSystemEntries(Settings.LocalRepositoryPath, "*",
                                 SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }

                    Directory.Delete(Settings.LocalRepositoryPath, true);
                }
                catch (DirectoryNotFoundException)
                {
                    // 本地题库目录本就不存在属于预期情况，不算错误，仅留下可追溯的记录
                    Log.Information("Repository directory to clean does not exist, skipping deletion. RepositoryPath: {RepositoryPath}", Settings.LocalRepositoryPath);
                }
                catch (Exception ex)
                {
                    // 删除旧题库失败会导致后续 clone 在脏目录上进行，必须记录完整异常以便排查残留文件权限问题
                    Working = false;
                    //RepositoryUpdated?.Invoke(null, new RepositoryUpdatedEventArgs(false));
                    throw new RepositoryUpdateException($"Error deleting existed files: {ex.Message}", ex);
                }

            }
            Settings.ForceUpdateRequired = true;

            // Check if the local repository exists
            if (!Directory.Exists(Path.Combine(Settings.LocalRepositoryPath, ".git")))
            {
                // Clone the repository if it doesn't exist
                try
                {
                    CloneRepository();
                }
                catch (Exception ex)
                {
                    Working = false;
                    //RepositoryUpdated?.Invoke(null, new RepositoryUpdatedEventArgs(false));
                    throw new RepositoryUpdateException($"Error cloning repository: {ex.Message}", ex);
                }
            }
            else
            {
                // Pull the latest changes if it exists
                try
                {
                    PullRepository();
                }
                catch (Exception ex)
                {
                    Working = false;
                    //RepositoryUpdated?.Invoke(null, new RepositoryUpdatedEventArgs(false));
                    throw new RepositoryUpdateException($"Error updating existed repository: {ex.Message}", ex);
                }
            }
            Log.Information("Repository update finished.");
            Settings.ForceUpdateRequired = false;
            Working = false;
        }
        /// <summary>
        /// 广播仓库更新结果，通知订阅者（如主窗口）刷新界面。
        /// </summary>
        /// <param name="Success">更新是否成功。</param>
        public static void InvokeRepositoryUpdated(bool Success) => RepositoryUpdated?.Invoke(null, new RepositoryUpdatedEventArgs(Success));

        /// <summary>
        /// 首次使用或本地仓库缺失时，把远端题库仓库完整克隆到 <see cref="Settings.LocalRepositoryPath"/>。
        /// </summary>
        private static void CloneRepository()
        {
            // Use LibGit2Sharp to clone the repository


            LibGit2Sharp.Repository.Clone(Settings.RepositoryURL, Settings.LocalRepositoryPath);
        }
        /// <summary>
        /// 本地仓库已存在时，从 origin 拉取最新提交并合并到当前分支。
        /// </summary>
        private static void PullRepository()
        {
            using var repo = new LibGit2Sharp.Repository(Settings.LocalRepositoryPath);
            var remote = repo.Network.Remotes["origin"];
            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
            LibGit2Sharp.Commands.Fetch(repo, remote.Name, refSpecs, null, null);
            var mergeResult = repo.MergeFetchedRefs(
                new LibGit2Sharp.Signature("AutoUpdater", "auto.updater@example.com", DateTimeOffset.Now),
                null);


        }
    }
}
