using Serilog;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using LibGit2Sharp;

namespace ProblemViewer
{
    /// <summary>
    /// 仓库更新（删除旧目录、clone 或 pull）过程中发生错误时抛出的异常。
    /// </summary>
    public class RepositoryUpdateException : Exception
    {
        /// <summary>
        /// 本次失败时本地仓库是否已恢复到更新前的状态；
        /// 为 <c>false</c> 表示本地仓库可能处于半新半旧的状态，需要通知界面按失败处理。
        /// </summary>
        public bool Reverted { get; }

        /// <summary>
        /// 本次失败是否发生在用户取消更新之后的收尾补偿阶段（例如回滚备份分支失败）。
        /// </summary>
        public bool Cancelled { get; }

        /// <summary>
        /// 创建异常实例。
        /// </summary>
        /// <param name="message">错误说明，可为 <c>null</c>。</param>
        /// <param name="innerException">引起本次失败的原始异常，可为 <c>null</c>。</param>
        /// <param name="reverted">本地仓库是否已恢复到更新前的状态，默认 <c>true</c>。</param>
        /// <param name="cancelled">失败是否发生在取消后的收尾补偿阶段，默认 <c>false</c>。</param>
        public RepositoryUpdateException(string? message = null, Exception? innerException = null, bool reverted = true, bool cancelled = false) : base(message, innerException)
        {
            Reverted = reverted;
            Cancelled = cancelled;
        }
    }

    /// <summary>
    /// 仓库更新的最终结果，供更新窗口决定是否广播成功事件、是否弹错误提示。
    /// </summary>
    public enum UpdateOutcome
    {
        /// <summary>
        /// 更新成功，本地仓库已与远端一致。
        /// </summary>
        Success,

        /// <summary>
        /// 用户取消：本地仓库保持更新前的状态（已回滚或根本未被触碰）。
        /// </summary>
        Cancelled,

        /// <summary>
        /// 更新失败：本地仓库已尽量回滚到更新前的状态，具体原因由更新窗口从
        /// <see cref="RepositoryUpdateException"/> 中获取并提示。
        /// </summary>
        Failed,
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
        /// 强制更新时先克隆到的临时目录名（位于 <c>%TEMP%\MOProblemsReader</c> 下）。
        /// </summary>
        private const string TempRepositoryFolderName = "temp_repo";

        /// <summary>
        /// 检查题源 URL 可用性时使用的临时目录名（位于 <c>%TEMP%\MOProblemsReader</c> 下）。
        /// </summary>
        private const string CheckRepositoryFolderName = "check_repo";

        /// <summary>
        /// 当前是否正在执行仓库更新。更新期间为 <c>true</c>，进度窗口据此拒绝关闭。
        /// </summary>
        public static bool Working { get; private set; } = false;

        /// <summary>
        /// 当前更新流程的取消源；仅在 <see cref="UpdateRepositoryWorker"/> 执行期间非 <c>null</c>。
        /// </summary>
        private static CancellationTokenSource? _cts;

        /// <summary>
        /// 仓库更新结束时触发：成功时触发，失败但未能撤销更改时也会触发。
        /// 参数中的 <c>Success</c> 表示结果，失败时订阅方应按「题库数据可能不一致」处理。
        /// </summary>
        public static event EventHandler<RepositoryUpdatedEventArgs>? RepositoryUpdated;

        /// <summary>
        /// 请求取消正在进行的更新。只会影响「临时目录准备」这类可安全中断的阶段，
        /// 一旦开始覆盖本地仓库或强制对齐远端分支，请求会被忽略以免留下半新半旧的仓库。
        /// </summary>
        public static void Cancel() => _cts?.Cancel();

        /// <summary>
        /// 在 UI 线程以模态方式显示进度窗口并执行仓库更新，等待更新结束后才返回。
        /// </summary>
        /// <param name="parentwindow">进度窗口的属主窗口，可为 <c>null</c>。</param>
        /// <returns>本次更新的结果；成功或失败且未能撤销更改时广播了 <see cref="RepositoryUpdated"/>。</returns>
        public static UpdateOutcome UpdateRepository(Window? parentwindow = null)
        {
            UpdateOutcome outcome = UpdateOutcome.Cancelled;

            Application.Current.Dispatcher.Invoke(() =>
            {

                var updateWindow = new UpdateWindow
                {
                    Owner = parentwindow,
                };

                updateWindow.ShowDialog();
                outcome = updateWindow.Outcome;

            });

            return outcome;
        }

        /// <summary>
        /// 在临时目录克隆指定仓库，用于设置界面验证题源 URL 是否可用。
        /// </summary>
        /// <remarks>
        /// 克隆正常完成即视为检查通过；克隆结果不参与后续更新，结束后立即清理临时目录。
        /// 异常不在此处包装，由调用方负责记录日志并向用户提示。
        /// </remarks>
        /// <param name="url">待验证的 git 仓库地址。</param>
        public static void CheckRepository(string url)
        {
            var tempPath = GetTempPath(CheckRepositoryFolderName);
            DeleteDirectoryIfExists(tempPath);
            try
            {
                Repository.Clone(url, tempPath);
            }
            finally
            {
                // 检查只是探活，克隆结果不再使用，立即清理避免长期占用磁盘
                TryDeleteDirectory(tempPath);
            }
        }

        /// <summary>
        /// 实际执行仓库更新（耗时操作，应在后台线程调用）。
        /// </summary>
        /// <remarks>
        /// 强制更新：先克隆到临时目录，可在此阶段取消；克隆完成后再覆盖本地仓库，此后不再响应取消。
        /// 非强制更新：先提交本地改动并建备份分支，再把 main 强制对齐远端；失败或取消时回滚到备份分支。
        /// </remarks>
        /// <param name="cancellationToken">外部取消令牌，可与 <see cref="Cancel"/> 叠加。</param>
        /// <returns>成功或取消时的结果；不可恢复错误以 <see cref="RepositoryUpdateException"/> 抛出。</returns>
        public static UpdateOutcome UpdateRepositoryWorker(CancellationToken cancellationToken = default)
        {
            Working = true;
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cts = linkedSource;
            Log.Information(
                "Begin repository update. RepositoryURL: {RepositoryURL}; ForceUpdateRequired: {ForceUpdateRequired}",
                Settings.RepositoryURL, Settings.ForceUpdateRequired);
            try
            {
                var outcome = Settings.ForceUpdateRequired
                    ? ForceUpdate(linkedSource.Token)
                    : IncrementalUpdate(linkedSource.Token);

                if (outcome == UpdateOutcome.Success)
                {
                    Settings.ForceUpdateRequired = false;
                    Log.Information("Repository update finished.");
                }
                else
                {
                    Log.Information("Repository update ended with outcome {Outcome}.", outcome);
                }

                return outcome;
            }
            finally
            {
                // 先清空引用再释放，避免 Cancel() 拿到已释放的取消源
                _cts = null;
                Working = false;
            }
        }

        /// <summary>
        /// 广播仓库更新结果，通知订阅者（如主窗口）刷新界面。
        /// </summary>
        /// <param name="Success">更新是否成功。</param>
        public static void InvokeRepositoryUpdated(bool Success) => RepositoryUpdated?.Invoke(null, new RepositoryUpdatedEventArgs(Success));

        /// <summary>
        /// 强制更新：把远端完整克隆到临时目录，再用 robocopy 镜像覆盖本地仓库。
        /// </summary>
        /// <param name="cancellationToken">取消令牌，仅覆盖本地仓库之前的阶段有效。</param>
        /// <returns>本次更新的结果。</returns>
        /// <exception cref="RepositoryUpdateException">克隆或覆盖本地仓库失败时抛出。</exception>
        private static UpdateOutcome ForceUpdate(CancellationToken cancellationToken)
        {
            var tempPath = GetTempPath(TempRepositoryFolderName);
            try
            {
                DeleteDirectoryIfExists(tempPath);
                Log.Debug("Force update is required. Cloning repository into temp directory {TempPath}.", tempPath);
                Repository.Clone(Settings.RepositoryURL, tempPath);
            }
            catch (Exception ex)
            {
                // LibGit2Sharp 的克隆无法中途取消；此时本地仓库完全未被触碰，直接丢弃临时目录即可
                Log.Error(ex, "Error cloning repository into temp directory {TempPath}.", tempPath);
                TryDeleteDirectory(tempPath);
                throw new RepositoryUpdateException("在临时目录克隆题源仓库失败。", ex);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Log.Debug("Force update cancelled by user after cloning into temp directory, local repository untouched.");
                TryDeleteDirectory(tempPath);
                return UpdateOutcome.Cancelled;
            }

            try
            {
                // 从这里开始覆盖本地仓库：不再响应取消，否则会留下半新半旧的仓库
                Log.Debug("Cloning finished. Now overwriting local repository {LocalRepositoryPath}.", Settings.LocalRepositoryPath);
                MirrorDirectory(tempPath, Settings.LocalRepositoryPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error overwriting local repository from temp directory {TempPath}.", tempPath);
                // robocopy 已写入的部分无法撤销，属于「未能撤销更改」的失败，需要让界面按失败处理
                throw new RepositoryUpdateException("用克隆结果覆盖本地仓库失败，本地仓库可能处于部分更新的状态。", ex, reverted: false);
            }
            finally
            {
                TryDeleteDirectory(tempPath);
            }

            return UpdateOutcome.Success;
        }

        /// <summary>
        /// 非强制更新：提交本地改动 → 建备份分支 → 拉取并强制 main 对齐远端 → 删除备份分支。
        /// </summary>
        /// <param name="cancellationToken">取消令牌，各阶段之间检查；取消时回滚到备份分支。</param>
        /// <returns>本次更新的结果。</returns>
        /// <exception cref="RepositoryUpdateException">准备或执行更新失败时抛出（若已改动分支，抛出前已回滚）。</exception>
        private static UpdateOutcome IncrementalUpdate(CancellationToken cancellationToken)
        {
            try
            {
                using var repo = new Repository(Settings.LocalRepositoryPath);
                var signature = new Signature("AutoUpdater", "auto.updater@example.com", DateTimeOffset.Now);

                // 先把本地改动全部提交，保证后续强制对齐远端时工作区是干净的、不会静默丢失内容
                if (repo.RetrieveStatus(new StatusOptions()).IsDirty)
                {
                    Commands.Stage(repo, "*");
                    repo.Commit("Auto commit before update", signature, signature);
                    Log.Debug("Local changes committed before update.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                var workingBranch = repo.Branches["main"] ?? repo.Head;
                if (repo.Head.CanonicalName != workingBranch.CanonicalName)
                {
                    Log.Debug("Checking out branch {Branch} before update.", workingBranch.FriendlyName);
                    Commands.Checkout(repo, workingBranch);
                }

                // 备份分支指向更新前的提交，是失败/取消时唯一的还原依据
                var backupTip = workingBranch.Tip;
                var backupName = "backup-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                if (backupTip != null)
                {
                    repo.Branches.Add(backupName, backupTip);
                    Log.Debug("Backup branch {BackupBranch} created at {Sha}.", backupName, backupTip.Sha);
                }

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var remote = repo.Network.Remotes["origin"];
                    var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification).ToList();
                    Commands.Fetch(repo, remote.Name, refSpecs, null, null);

                    cancellationToken.ThrowIfCancellationRequested();

                    var remoteTip = FindRemoteMainTip(repo);
                    if (remoteTip != null && repo.Head.Tip?.Sha != remoteTip.Sha)
                    {
                        Log.Debug("Resetting branch {Branch} to remote tip {Sha}.", repo.Head.FriendlyName, remoteTip.Sha);
                        repo.Reset(ResetMode.Hard, remoteTip);
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Debug("Update cancelled by user, rolling back to backup branch {BackupBranch}.", backupName);
                    var reverted = RestoreFromBackup(repo, backupName, backupTip, signature);
                    if (!reverted)
                    {
                        // 取消后连收尾回滚都失败：仓库可能停在中间状态，必须让用户知道
                        throw new RepositoryUpdateException("更新已取消，但未能恢复到更新前的状态。", null, reverted: false, cancelled: true);
                    }

                    return UpdateOutcome.Cancelled;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error updating existed repository, rolling back to backup branch {BackupBranch}.", backupName);
                    var reverted = RestoreFromBackup(repo, backupName, backupTip, signature);
                    throw new RepositoryUpdateException(
                        reverted ? "更新本地仓库失败，已回滚到更新前状态。" : "更新本地仓库失败，且未能恢复到更新前的状态。",
                        ex, reverted);
                }

                // 只有更新成功才删除备份分支；删不掉只记警告，不影响本次已成功的更新结果
                DeleteBackupBranch(repo, backupName);
                return UpdateOutcome.Success;
            }
            catch (OperationCanceledException)
            {
                return UpdateOutcome.Cancelled;
            }
            catch (RepositoryUpdateException)
            {
                // 内层已回滚并给出可读说明，原样向外冒泡，避免被下面的兜底 catch 重新包装
                throw;
            }
            catch (Exception ex)
            {
                // 例如仓库无法打开、提交本地改动失败：此时尚未改动远端相关内容，直接按失败处理
                Log.Error(ex, "Error preparing repository update. Local RepositoryPath: {LocalRepositoryPath}", Settings.LocalRepositoryPath);
                throw new RepositoryUpdateException("准备更新本地仓库失败。", ex);
            }
        }

        /// <summary>
        /// 把 main 分支恢复到备份分支：优先合并备份分支，冲突时以备份分支内容硬回滚。
        /// </summary>
        /// <param name="repo">已打开的本地仓库。</param>
        /// <param name="backupBranchName">备份分支名；为空表示没有创建备份。</param>
        /// <param name="backupTip">备份分支指向的提交；为 <c>null</c> 表示没有可回滚的内容。</param>
        /// <param name="signature">合并提交使用的签名。</param>
        /// <returns>本地仓库是否处于更新前的状态；<c>false</c> 表示回滚失败，内容可能已不一致。</returns>
        private static bool RestoreFromBackup(Repository repo, string? backupBranchName, Commit? backupTip, Signature signature)
        {
            if (backupTip == null)
            {
                // 没有备份就没有需要撤销的内容，按已恢复处理
                return true;
            }

            try
            {
                if (repo.Head.Tip?.Sha == backupTip.Sha)
                {
                    // main 没有被改动过，无需回滚
                    DeleteBackupBranch(repo, backupBranchName);
                    return true;
                }

                var backupBranch = backupBranchName == null ? null : repo.Branches[backupBranchName];
                if (backupBranch != null)
                {
                    var mergeResult = repo.Merge(backupBranch, signature, new MergeOptions());
                    if (mergeResult.Status == MergeStatus.Conflicts)
                    {
                        // 服务器内容与本地提交冲突时必须让用户回到更新前的状态，而不是留下冲突标记
                        repo.Reset(ResetMode.Hard, backupTip);
                    }
                }
                else
                {
                    repo.Reset(ResetMode.Hard, backupTip);
                }

                if (repo.Index.Conflicts.Any())
                {
                    repo.Reset(ResetMode.Hard, backupTip);
                }

                DeleteBackupBranch(repo, backupBranchName);
                Log.Debug("Local branch restored to previous state {Sha}.", backupTip.Sha);
                return true;
            }
            catch (Exception ex)
            {
                // 回滚本身失败无法再向用户解释清楚，但必须让调用方知道内容可能没被撤销
                Log.Error(ex, "Failed to restore local branch from backup {Sha}.", backupTip.Sha);
                return false;
            }
        }

        /// <summary>
        /// 删除备份分支；失败只记录警告，不影响更新结果。
        /// </summary>
        /// <param name="repo">已打开的本地仓库。</param>
        /// <param name="backupBranchName">备份分支名，可为 <c>null</c>。</param>
        private static void DeleteBackupBranch(Repository repo, string? backupBranchName)
        {
            if (backupBranchName == null)
            {
                return;
            }

            try
            {
                if (repo.Branches[backupBranchName] != null)
                {
                    repo.Branches.Remove(backupBranchName);
                    Log.Debug("Backup branch {BackupBranch} removed.", backupBranchName);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to remove backup branch {BackupBranch}.", backupBranchName);
            }
        }

        /// <summary>
        /// 取远端 main 分支的最新提交，用于把本地 main 强制对齐到服务器内容。
        /// </summary>
        /// <param name="repo">已打开并完成 fetch 的本地仓库。</param>
        /// <returns>远端 main 的提交；无法确定时返回 <c>null</c>，调用方应跳过对齐步骤。</returns>
        private static Commit? FindRemoteMainTip(Repository repo)
        {
            var remoteMain = repo.Branches.FirstOrDefault(branch => branch.IsRemote && branch.FriendlyName == "origin/main");
            if (remoteMain?.Tip != null)
            {
                return remoteMain.Tip;
            }

            var tracked = repo.Head.TrackedBranch;
            if (tracked?.Tip != null)
            {
                return tracked.Tip;
            }

            Log.Warning("Cannot determine remote main branch tip, skip resetting local branch.");
            return null;
        }

        /// <summary>
        /// 用 robocopy 把源目录镜像到目标目录（目标多出的文件会被删除）。
        /// </summary>
        /// <remarks>
        /// 选择 robocopy 而不是「删除本地目录再 Move」：TreeView 可能仍持有仓库内文件的句柄，
        /// 整目录删除容易失败，而镜像复制可以逐文件覆盖。
        /// </remarks>
        /// <param name="source">源目录。</param>
        /// <param name="destination">目标目录，不存在时自动创建。</param>
        /// <exception cref="RepositoryUpdateException">robocopy 退出码 ≥ 8（复制失败）时抛出。</exception>
        private static void MirrorDirectory(string source, string destination)
        {
            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "robocopy.exe",
                Arguments = $"\"{source}\" \"{destination}\" /MIR /MT /R:0 /W:0 /NP /NJH /NJS /NC /NS",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            using var process = Process.Start(startInfo)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            var output = stdout.GetAwaiter().GetResult();

            // robocopy 退出码 1-7 都算正常（含「确有文件被复制」），≥ 8 才表示失败
            if (process.ExitCode >= 8 || process.ExitCode==0)
            {
                throw new RepositoryUpdateException($"Robocopy failed with exit code {process.ExitCode}. {output}");
            }
        }

        /// <summary>
        /// 取 <c>%TEMP%\MOProblemsReader</c> 下的临时目录路径。
        /// </summary>
        /// <param name="folderName">子目录名。</param>
        /// <returns>临时目录的完整路径。</returns>
        private static string GetTempPath(string folderName) => Path.Combine(Path.GetTempPath(), "MOProblemsReader", folderName);

        /// <summary>
        /// 删除目录（存在时）；删除失败直接抛出，交由调用方判断是否为致命错误。
        /// </summary>
        /// <param name="path">要删除的目录路径。</param>
        private static void DeleteDirectoryIfExists(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            // 仓库内的 .git 对象文件是只读的，先恢复属性再删除
            foreach (var file in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, true);
        }

        /// <summary>
        /// 尽力删除目录，失败只记录警告（临时目录残留不影响更新结果）。
        /// </summary>
        /// <param name="path">要删除的目录路径。</param>
        private static void TryDeleteDirectory(string path)
        {
            try
            {
                DeleteDirectoryIfExists(path);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to delete temporary directory {TempPath}.", path);
            }
        }
    }
}
