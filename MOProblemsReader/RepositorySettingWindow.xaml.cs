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
    public class RobocopyFailException : Exception
    {
        /// <summary>
        /// robocopy 进程的退出码，取值 ≥ 8 表示复制未成功。
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// robocopy 的标准输出，用于排查具体哪些文件没复制成功；未捕获输出时为 <c>null</c>。
        /// </summary>
        public string? Output { get; }

        /// <summary>
        /// 创建异常实例。
        /// </summary>
        /// <param name="exitCode">robocopy 的退出码。</param>
        /// <param name="output">robocopy 的标准输出，可为 <c>null</c>。</param>
        /// <param name="message">追加在默认错误信息之后的补充说明，可为 <c>null</c>。</param>
        /// <param name="innerException">引起本次失败的内层异常，可为 <c>null</c>。</param>
        public RobocopyFailException(int exitCode, string?output=null, string? message=null, Exception? innerException = null)
            :base($"Robocopy failed with exit code {exitCode}."+message,innerException)
        {
            ExitCode = exitCode;
            Output = output;
        }
    }
    /// <summary>
    /// 题源设置窗口：编辑题库仓库 URL、自动更新开关与日志级别，并支持导出诊断日志包。
    /// </summary>
    public partial class RepositorySettingWindow : Window
    {
        public RepositorySettingWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 「更新」按钮：URL 有改动时先确认并保存（同时要求下次强制更新），随后立即执行一次仓库更新。
        /// </summary>
        /// <param name="sender">事件源，此处不使用。</param>
        /// <param name="e">事件参数，此处不使用。</param>
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

                try
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "MOProblemsReader", "logtemp");
                    var robocopyprocinfo = new ProcessStartInfo
                    {
                        FileName = "robocopy.exe",
                        Arguments = $"\"{logDictionary}\" \"{tempDir}\" /MIR /MT /R:0 /W:0",
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
                        if (exitCode >= 8)
                        {
                            throw new RobocopyFailException(exitCode, output);
                        }
                    }

                    if (File.Exists(saveDialog.FileName))
                    {
                        File.Delete(saveDialog.FileName);
                        Log.Debug("Delete existed file {FileName}", saveDialog.FileName);
                    }

                    await ZipFile.CreateFromDirectoryAsync(tempDir, saveDialog.FileName); //cannot read
                    Log.Information("Logs exported to {FileName}", saveDialog.FileName);
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
