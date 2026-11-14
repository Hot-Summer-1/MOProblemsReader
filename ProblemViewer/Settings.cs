using Microsoft.Win32;
using System.IO;
using Serilog;

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

        private static string _repositoryURL = "https://github.com/Hot-Summer-1/MOProblems.git";
        public static readonly string LocalRepositoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MOProblemsReader", "MOProblems");
        private static bool _autoUpdate = true;
        private static bool _forceupdaterequired = false;

        /// <summary>
        /// 属性
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
        public static string RepositoryURL
        {
            get => _repositoryURL;
            set
            {
                _repositoryURL = value;
            }
        }

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

            RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
            if (key == null)
            {
                ForceUpdateRequired = true;
                Log.Information("Registry key not found, use default value.");
                return; // 注册表项不存在，使用默认值
            }
            if (key.GetValue("RepositoryURL") is string repoUrl && !string.IsNullOrWhiteSpace(repoUrl))
                _repositoryURL = repoUrl;
            if (key.GetValue("AutoUpdate") is int autoUpdateInt)
                _autoUpdate = autoUpdateInt != 0;
            if (key.GetValue("ForceUpdateRequired") is int forceUpdateRequiredInt)
                _forceupdaterequired = forceUpdateRequiredInt != 0;
            key.Close();
            Log.Information("Settings Loaded.");
            Log.Debug("Loaded value: RepositoryURL: {RepositoryURL}; AutoUpdate: {AutoUpdate}; ForceUpdateRequired:{ForceUpdateRequired}",RepositoryURL,AutoUpdate,ForceUpdateRequired);
        }

        /// <summary>
        /// 将当前所有设置持久化到注册表 <see cref="RegKeyPath"/>。
        /// 若注册表项不存在则会自动创建。
        /// </summary>
        public static void SaveSettings()
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKeyPath, true);
            key.SetValue("RepositoryURL", _repositoryURL, RegistryValueKind.String);
            key.SetValue("AutoUpdate", _autoUpdate ? 1 : 0, RegistryValueKind.DWord);
            //key.SetValue("ForceUpdateRequired", _forceupdaterequired ? 1 : 0, RegistryValueKind.DWord);
            key.Close();
            Log.Information("Settings Saved.");
            Log.Debug("Saved value: RepositoryURL: {RepositoryURL}; AutoUpdate: {AutoUpdate}", RepositoryURL, AutoUpdate);

        }
    }
}
