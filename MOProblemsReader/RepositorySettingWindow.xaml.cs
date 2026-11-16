using System.Diagnostics;
using Microsoft.Win32;
using MOProblemsReader.Logging;
using ProblemViewer;
using Serilog.Events;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows;
using Serilog;

namespace MOProblemsReader
{
    /// <summary>
    /// robocopy 复制文件失败（退出码 ≥ 8）时抛出的异常。
    /// </summary>
    /// <param name="exitCode">robocopy 的退出码。</param>
    /// <param name="output">robocopy 的标准输出，可为 <c>null</c>。</param>
    /// <param name="message">追加在默认错误信息之后的补充说明，可为 <c>null</c>。</param>
    /// <param name="innerException">引起本次失败的内层异常，可为 <c>null</c>。</param>
    public class RobocopyFailException(int exitCode, string? output = null, string? message = null, Exception? innerException = null)
        : Exception($"Robocopy failed with exit code {exitCode}." + (message == null ? " " : "") + message, innerException)
    {
        /// <summary>
        /// robocopy 进程的退出码，取值 ≥ 8 表示复制未成功。
        /// </summary>
        public int ExitCode { get; } = exitCode;

        /// <summary>
        /// robocopy 的标准输出，用于排查具体哪些文件没复制成功；未捕获输出时为 <c>null</c>。
        /// </summary>
        public string? Output { get; } = output;
    }

    /// <summary>
    /// 题源设置窗口：编辑题库仓库 URL、自动更新开关与日志级别，并支持导出诊断日志包。
    /// </summary>
    public partial class RepositorySettingWindow : Window
    {
        /// <summary>
        /// 题源 URL 输入框末尾状态指示器的呈现状态。
        /// </summary>
        private enum CheckState
        {
            /// <summary>
            /// 无指示器（尚未检查或 URL 已改动）。
            /// </summary>
            Hidden,

            /// <summary>
            /// 正在检查，显示旋转的环形进度条。
            /// </summary>
            Running,

            /// <summary>
            /// 检查通过，显示打勾图标。
            /// </summary>
            Passed,

            /// <summary>
            /// 检查失败，显示打叉图标。
            /// </summary>
            Failed,
        }

        /// <summary>
        /// 最近一次通过检查的题源 URL；为 <c>null</c> 或与输入框文本不一致时表示该 URL 尚未检查通过。
        /// </summary>
        private string? _verifiedUrl;

        /// <summary>
        /// 是否正在检查或更新，用于阻止并发触发并抑制期间文本变化对指示器的干扰。
        /// </summary>
        private bool _checking;

        /// <summary>
        /// 创建题源设置窗口。
        /// </summary>
        public RepositorySettingWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 「更新」按钮：URL 有改动时先确认并保存（同时要求下次强制更新），随后立即执行一次仓库更新。
        /// 更新本身即对仓库可用性的完整验证，因此成功后把当前 URL 视为已检查通过；
        /// 按需求更新期间不显示转圈，只在结束后显示打勾或打叉。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_checking) return;

            // 空 URL 保存下去会导致下次强制更新直接克隆失败，先回落默认值
            RestoreDefaultUrlIfEmpty();
            var url = RepositoryURLTextBox.Text;
            var oldurl = Settings.RepositoryURL;
            if (url != Settings.RepositoryURL)
            {
                if (MessageBox.Show("是否保存对题源URL的更改？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                Settings.RepositoryURL = url;
                Settings.ForceUpdateRequired = true;
                //Settings.SaveSettings();

            }

            _checking = true;
            try
            {
                var outcome = RepositoryManager.UpdateRepository(Application.Current.MainWindow);
                if (outcome == UpdateOutcome.Success)
                {
                    _verifiedUrl = url;
                    Settings.SaveSettings();
                    ShowCheckState(CheckState.Passed);
                }
                else if (outcome == UpdateOutcome.Failed)
                {
                    _verifiedUrl = null;
                    Settings.RepositoryURL = oldurl;
                    ShowCheckState(CheckState.Failed);
                }
                else
                {
                    Settings.RepositoryURL = oldurl;
                    // 用户取消：本地仓库未被改动，保留原有的检查结论即可
                    ShowCheckState(url == _verifiedUrl ? CheckState.Passed : CheckState.Hidden);
                }
            }
            finally
            {
                _checking = false;
            }
        }

        /// <summary>
        /// 把界面选项写回 <see cref="Settings"/> 并保存；URL 变更时标记强制更新，使下次启动重新拉取仓库。
        /// </summary>
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

        /// <summary>
        /// 「确定」：校验题源 URL 已通过检查后应用设置并关闭窗口。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!await EnsureUrlVerifiedAsync()) return;
            ApplySettings();
            Close();
        }

        /// <summary>
        /// 「取消」：放弃未应用的修改并关闭窗口。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 「应用」：校验题源 URL 已通过检查后保存设置。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!await EnsureUrlVerifiedAsync()) return;
            ApplySettings();
        }

        /// <summary>
        /// 窗口加载时把当前设置填入各控件。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RepositoryURLTextBox.Text = Settings.RepositoryURL;
            AutoUpdateCheckBox.IsChecked = Settings.AutoUpdate;
            LogLevelComboBox.SelectedItem = Settings.LogLevel;
        }

        /// <summary>
        /// 题源 URL 被编辑后，之前的检查结论立即失效，必须重新检查才能应用。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
        private void RepositoryURLTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // 检查/更新期间指示器由对应流程控制，避免文本框内容的程序化变化把状态清掉
            if (_checking) return;

            if (RepositoryURLTextBox.Text == _verifiedUrl)
            {
                ShowCheckState(CheckState.Passed);
                return;
            }

            _verifiedUrl = null;
            ShowCheckState(CheckState.Hidden);
        }

        /// <summary>
        /// 「检查」按钮：在临时目录克隆当前输入的题源 URL，并据此更新指示器。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
        private async void CheckButton_OnClick(object sender, RoutedEventArgs e)
        {
            await RunCheckAsync();
        }

        /// <summary>
        /// 在临时目录克隆当前输入的题源 URL 以验证是否可用，并同步更新输入框末尾的指示器。
        /// </summary>
        /// <returns>检查是否通过。</returns>
        private async Task<bool> RunCheckAsync()
        {
            var url = RepositoryURLTextBox.Text;
            if (_checking || string.IsNullOrWhiteSpace(url)) return false;

            _checking = true;
            ShowCheckState(CheckState.Running);
            try
            {
                await Task.Run(() => RepositoryManager.CheckRepository(url));
                _verifiedUrl = url;
                ShowCheckState(CheckState.Passed);
                return true;
            }
            catch (Exception ex)
            {
                // 克隆失败可能来自地址错误、网络不可达或权限问题，原始异常即最准确的诊断信息；
                // 失败只记 error 日志并在输入框末尾显示打叉，不再弹框打断用户
                _verifiedUrl = null;
                Log.Error(ex, "Failed to check repository. RepositoryURL: {RepositoryURL}", url);
                ShowCheckState(CheckState.Failed);
                return false;
            }
            finally
            {
                _checking = false;
            }
        }

        /// <summary>
        /// 题源 URL 输入框为空时恢复内置默认仓库地址。
        /// </summary>
        /// <returns>是否发生了恢复。</returns>
        private bool RestoreDefaultUrlIfEmpty()
        {
            // 必须同时刷新界面文本，否则设置里已是默认值而输入框仍是空的，用户看到的与实际不一致
            if (!string.IsNullOrWhiteSpace(RepositoryURLTextBox.Text)) return false;
            RepositoryURLTextBox.Text = Settings.DefaultRepositoryURL;
            return true;
        }

        /// <summary>
        /// 确认题源 URL 已通过检查：未改动则直接放行，改动过则询问用户是否立即检查。
        /// </summary>
        /// <returns>可以继续应用设置时为 <c>true</c>。</returns>
        private async Task<bool> EnsureUrlVerifiedAsync()
        {
            // 空 URL 既无法检查也无法克隆，先回落默认值，再按正常流程判断是否需要确认
            RestoreDefaultUrlIfEmpty();
            var url = RepositoryURLTextBox.Text;

            // URL 与已保存值一致时无需重新检查：该地址此前已能正常克隆出本地仓库
            if (url == Settings.RepositoryURL) return true;
            if (url == _verifiedUrl) return true;

            if (MessageBox.Show(this, "题源URL已修改但尚未检查，是否立即检查？", "尚未检查", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return false;
            }

            return await RunCheckAsync();
        }

        /// <summary>
        /// 切换输入框末尾的状态指示器（转圈 / 打勾 / 打叉 / 不显示）。
        /// </summary>
        /// <param name="state">要显示的状态。</param>
        private void ShowCheckState(CheckState state)
        {
            CheckProgressBar.Visibility = state == CheckState.Running ? Visibility.Visible : Visibility.Collapsed;
            CheckOkTextBlock.Visibility = state == CheckState.Passed ? Visibility.Visible : Visibility.Collapsed;
            CheckFailTextBlock.Visibility = state == CheckState.Failed ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 导出诊断数据：用 robocopy 把日志目录镜像到临时目录，再打包成 zip 保存到用户选定位置。
        /// </summary>
        /// <remarks>
        /// robocopy 退出码 0-7 都算正常（含「确有文件被复制」等情况），只有 ≥ 8 才表示失败。
        /// </remarks>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
        private async void ExportButton_OnClick(object sender, RoutedEventArgs e)
        {
            var logDictionary = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MOProblemsReader", "log");
            var saveDialog = new SaveFileDialog
            {
                Title = "导出文件",
                Filter = "压缩文件 (*.zip)|*.zip",
                DefaultExt = ".zip",
                FileName =
                $"logs-{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.zip"
            };
            if (saveDialog.ShowDialog(this) == true)
            {
                Log.Information("Logs begin to be exported to {FileName}.", saveDialog.FileName);
                try
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "MOProblemsReader", "logtemp");
                    var robocopyprocinfo = new ProcessStartInfo
                    {
                        FileName = "robocopy.exe",
                        Arguments = $"\"{logDictionary}\" \"{tempDir}\" /MIR /MT /R:0 /W:0 /NP /NJH /NC /NS",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };
                    using (var robocopyproc = Process.Start(robocopyprocinfo)!)
                    {

                        _ = robocopyproc.StandardError.ReadToEndAsync();
                        var stdout = robocopyproc.StandardOutput.ReadToEndAsync();
                        await robocopyproc.WaitForExitAsync();
                        string output = await stdout;
                        var exitCode = robocopyproc.ExitCode;
                        if (exitCode==0||exitCode >= 8)
                        {
                            throw new RobocopyFailException(exitCode, output,"Failed to copy logs into temp dir.");
                        }
                    }

                    if (File.Exists(saveDialog.FileName))
                    {
                        File.Delete(saveDialog.FileName);
                        Log.Debug("Delete existed file {FileName}", saveDialog.FileName);
                    }

                    await ZipFile.CreateFromDirectoryAsync(tempDir, saveDialog.FileName);
                }
                catch (RobocopyFailException ex)
                {
                    Log.Error(ex, "Failed to copy logs into temp dir. Exit code: {ExitCode} Output: {Output}", ex.ExitCode,ex.Output);
                    MessageBox.Show($"导出诊断数据失败: {ex.Message}", "导出诊断数据失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error exporting logs. FileName: {FileName}", saveDialog.FileName);
                    MessageBox.Show($"导出诊断数据失败: {ex.Message}", "导出诊断数据失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
