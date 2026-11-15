using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Globalization;
using System.Text;

namespace MOProblemsReader.Logging
{
    /// <summary>
    /// 全局日志基础设施：封装 Serilog 的初始化与关闭，供应用各层直接使用 <see cref="Log"/> 写日志。
    /// </summary>
    public static class LoggingExtension
    {
        public static readonly LoggingLevelSwitch LevelSwitch = new(LogEventLevel.Information);
        /// <summary>
        /// 日志文件名模板：位于当前用户的 LocalApplicationData 目录。
        /// 其中 <c>{Date}</c> 是 Serilog 的滚动日期占位符，按天滚动时被替换为 <c>yyyyMMdd</c>，
        /// 所以它本身不是可直接打开的文件路径；真实路径请用 <see cref="CurrentLogFilePath"/>。
        /// </summary>
        public static readonly string LogPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MOProblemsReader", "log", "reader-.log");

        /// <summary>
        /// 当天日志文件的真实路径（<c>reader-yyyyMMdd.log</c>），与 Serilog 按天滚动的命名结果一致。
        /// 与 <see cref="LogPath"/> 不同，该路径指向磁盘上实际存在的文件，可直接展示给用户。
        /// </summary>
        public static string CurrentLogFilePath =>
            LogPath.Replace("-.log", "-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");

        /// <summary>
        /// 文件与控制台共用的输出模板：时间、级别、消息正文与异常堆栈。
        /// </summary>
        private const string OutputTemplate =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} PID: {ProcessId} [{Level}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// 保留的历史日志文件个数，防止日志长期累积占满磁盘。
        /// </summary>
        private const int RetainedFileCountLimit = 14;

        /// <summary>
        /// 日志系统是否已完成初始化，用于避免重复配置造成日志重复输出。
        /// </summary>
        private static bool _initialized;

        /// <summary>
        /// 获取日志系统是否已初始化。
        /// </summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// 初始化全局日志配置。重复调用会被忽略。
        /// </summary>
        /// <param name="minimumLevel">最低记录级别，默认为 <see cref="LogEventLevel.Information"/>。</param>
        /// <exception cref="IOException">日志文件无法创建或写入时由 Serilog 文件 sink 抛出。</exception>
        /// <exception cref="UnauthorizedAccessException">日志所在目录无写入权限时由 Serilog 文件 sink 抛出。</exception>
        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }
#if DEBUG
            LevelSwitch.MinimumLevel = LogEventLevel.Debug;
#endif
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(LevelSwitch)
                .Enrich.WithProcessId()
                .Enrich.FromLogContext()
                // shared: true 允许同一用户同时运行的多个应用实例写同一个日志文件
                .WriteTo.File(
                    LogPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: RetainedFileCountLimit,
                    shared: true,
                    outputTemplate: OutputTemplate,
                    encoding: Encoding.UTF8)
#if DEBUG                // Debug sink 把日志同步到 Visual Studio 的「输出」窗口，便于调试期直接观察
                .WriteTo.Debug(outputTemplate: OutputTemplate)
#endif
                .CreateLogger();

            _initialized = true;
            // 日志文本统一使用英文，便于跨语言环境检索与检索工具处理
            Log.Information("Logging system started. Log file path: {LogFilePath}", CurrentLogFilePath);
        }

        /// <summary>
        /// 刷新缓冲并关闭日志系统。应在应用退出或进程即将终止时调用，否则最后几条日志可能丢失。
        /// </summary>
        public static void CloseAndFlush()
        {
            if (!_initialized)
            {
                return;
            }

            Log.Information("Logging system shut down.");
            Log.CloseAndFlush();
            _initialized = false;
        }
    }
}
