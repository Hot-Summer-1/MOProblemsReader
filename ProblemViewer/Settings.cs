using Microsoft.Win32;
using System.IO;
using System.Windows.Markup;
using System.Windows.Media.Effects;
using MOProblemsReader.Logging;
using Serilog;
using Serilog.Events;

namespace ProblemViewer
{
    /// <summary>
    /// 应用程序的全局设置管理类。提供默认值并从注册表加载用户自定义覆盖。
    /// </summary>
    public static class Settings
    {
        /// <summary>
        /// 注册表键路径（HKCU 下），用于存储和读取用户自定义设置。
        /// </summary>
        public static readonly string RegKeyPath = @"Software\MOProblemsReader";

        /// <summary>
        /// 日志最低记录级别的后备字段。
        /// </summary>
        private static LogEventLevel _logLevel = LogEventLevel.Information;

        /// <summary>
        /// 日志最低记录级别。赋值后（Release 构建下）立即应用到全局级别开关，无需重启应用。
        /// </summary>
        public static LogEventLevel LogLevel
        {
            get => _logLevel;
            set
            {
                _logLevel = value;
#if !DEBUG
                LoggingExtension.LevelSwitch.MinimumLevel = value;
                Log.Information("Log level is set to {LogLevel}.",value);
#endif
            }
        }
        /// <summary>
        /// 题库仓库 URL 的后备字段，初值为内置的默认仓库。
        /// </summary>
        private static string _repositoryURL = "https://github.com/Hot-Summer-1/MOProblems.git";

        /// <summary>
        /// 题库仓库在本地的工作副本目录（<c>%LOCALAPPDATA%\MOProblemsReader\MOProblems</c>），clone/pull 与题库读取都以它为根。
        /// </summary>
        public static readonly string LocalRepositoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MOProblemsReader", "MOProblems");

        /// <summary>
        /// 「是否允许自动更新」的后备字段。
        /// </summary>
        private static bool _autoUpdate = true;

        /// <summary>
        /// 「是否需要强制更新」的后备字段。
        /// </summary>
        private static bool _forceupdaterequired = false;

        /// <summary>
        /// 是否需要强制更新：为 <c>true</c> 时下次启动会删除本地仓库再完整 clone。
        /// 赋值会立即写入注册表，因此不依赖 <see cref="SaveSettings"/> 持久化。
        /// </summary>
        public static bool ForceUpdateRequired
        {
            get => _forceupdaterequired;
            set
            {
                _forceupdaterequired = value;
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKeyPath, true);
                key.SetValue("ForceUpdateRequired", _forceupdaterequired ? 1 : 0, RegistryValueKind.DWord);
            }
        }
        /// <summary>
        /// 题库仓库的远端 URL（git 地址），由用户在题源设置窗口中修改。
        /// </summary>
        public static string RepositoryURL
        {
            get => _repositoryURL;
            set
            {
                _repositoryURL = value;
            }
        }

        /// <summary>
        /// 是否允许自动更新题库仓库。
        /// </summary>
        public static bool AutoUpdate
        {
            get => _autoUpdate;
            set
            {
                _autoUpdate = value;
            }
        }
        /// <summary>
        /// 从注册表 <see cref="RegKeyPath"/> 加载用户自定义设置，覆盖默认值。
        /// 若注册表项不存在或读取失败，则静默使用默认值。
        /// </summary>
        public static void LoadSettings()
        {

            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
            if (key == null)
            {
                Log.Information("Registry key not found, use default value.");
                return; // 注册表项不存在，使用默认值
            }
            if (key.GetValue("RepositoryURL") is string repoUrl && !string.IsNullOrWhiteSpace(repoUrl))
                _repositoryURL = repoUrl;
            if (key.GetValue("AutoUpdate") is int autoUpdateInt)
                _autoUpdate = autoUpdateInt != 0;
            if (key.GetValue("ForceUpdateRequired") is int forceUpdateRequiredInt)
                _forceupdaterequired = forceUpdateRequiredInt != 0;
            if (key.GetValue("LogLevel") is int logLevel && Enum.IsDefined(typeof(LogEventLevel), logLevel))
            {
                LogLevel = (LogEventLevel)logLevel;
            }
            key.Close();
            Log.Information("Settings Loaded.");
            Log.Debug("Loaded value: RepositoryURL: {RepositoryURL}; AutoUpdate: {AutoUpdate}; ForceUpdateRequired:{ForceUpdateRequired}", RepositoryURL, AutoUpdate, ForceUpdateRequired);
        }

        /// <summary>
        /// 把 URL、自动更新开关与日志级别写入注册表 <see cref="RegKeyPath"/>，注册表项不存在时自动创建。
        /// <see cref="ForceUpdateRequired"/> 由属性 setter 即时写入，不在此处处理。
        /// </summary>
        public static void SaveSettings()
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKeyPath, true);
            key.SetValue("RepositoryURL", _repositoryURL, RegistryValueKind.String);
            key.SetValue("AutoUpdate", _autoUpdate ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("LogLevel", (int)LogLevel);
            //key.SetValue("ForceUpdateRequired", _forceupdaterequired ? 1 : 0, RegistryValueKind.DWord);
            key.Close();
            Log.Information("Settings Saved.");
            Log.Debug("Saved value: RepositoryURL: {RepositoryURL}; AutoUpdate: {AutoUpdate}; LogLevel: {LogLevel}", RepositoryURL, AutoUpdate, LogLevel);
        }
    }
}