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
    public class RobocopyFailException : Exception
    {
        public int ExitCode { get; }
        public string? Output { get; }

        public RobocopyFailException(int exitCode, string?output=null, string? message=null, Exception? innerException = null)
            :base($"Robocopy failed with exit code {exitCode}."+message,innerException)
        {
            ExitCode = exitCode;
            Output = output;
        }
    }
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
