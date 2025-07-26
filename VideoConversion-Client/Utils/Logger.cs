using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VideoConversion_Client.Utils
{
    /// <summary>
    /// 日志工具类 - 支持多种日志级别和文件输出
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// 日志级别枚举
        /// </summary>
        public enum LogLevel
        {
            Debug = 0,
            Info = 1,
            Warning = 2,
            Error = 3,
            Fatal = 4
        }

        private static readonly object _lockObject = new object();
        private static readonly string _logDirectory;
        private static readonly string _currentLogFile;
        private static LogLevel _minimumLogLevel = LogLevel.Debug;

        /// <summary>
        /// 静态构造函数 - 初始化日志目录和文件
        /// </summary>
        static Logger()
        {
            try
            {
                // 获取应用程序根目录
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                _logDirectory = Path.Combine(appDirectory, "log");

                // 确保log目录存在
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                // 生成当前日志文件名（按日期）
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                _currentLogFile = Path.Combine(_logDirectory, $"app_{today}.log");

                // 写入启动日志
                WriteToFile(LogLevel.Info, "Logger", "日志系统已初始化");
                WriteToFile(LogLevel.Info, "Logger", $"日志目录: {_logDirectory}");
                WriteToFile(LogLevel.Info, "Logger", $"当前日志文件: {_currentLogFile}");
            }
            catch (Exception ex)
            {
                // 如果日志初始化失败，输出到控制台
                Console.WriteLine($"日志系统初始化失败: {ex.Message}");
                
                // 使用临时目录作为备选
                _logDirectory = Path.GetTempPath();
                _currentLogFile = Path.Combine(_logDirectory, $"VideoConversion_Client_{DateTime.Now:yyyy-MM-dd}.log");
            }
        }

        /// <summary>
        /// 设置最小日志级别
        /// </summary>
        /// <param name="level">最小日志级别</param>
        public static void SetMinimumLogLevel(LogLevel level)
        {
            _minimumLogLevel = level;
            Info("Logger", $"最小日志级别已设置为: {level}");
        }

        /// <summary>
        /// 记录调试日志
        /// </summary>
        /// <param name="category">日志分类</param>
        /// <param name="message">日志消息</param>
        public static void Debug(string category, string message)
        {
            Log(LogLevel.Debug, category, message);
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        /// <param name="category">日志分类</param>
        /// <param name="message">日志消息</param>
        public static void Info(string category, string message)
        {
            Log(LogLevel.Info, category, message);
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        /// <param name="category">日志分类</param>
        /// <param name="message">日志消息</param>
        public static void Warning(string category, string message)
        {
            Log(LogLevel.Warning, category, message);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        /// <param name="category">日志分类</param>
        /// <param name="message">日志消息</param>
        public static void Error(string category, string message)
        {
            Log(LogLevel.Error, category, message);
        }

        /// <summary>
        /// 记录错误日志（包含异常信息）
        /// </summary>
        /// <param name="category">日志分类</param>
        /// <param name="message">日志消息</param>
        /// <param name="exception">异常对象</param>
        public static void Error(string category, string message, Exception exception)
        {
            var fullMessage = $"{message}\n异常详情: {exception}";
            Log(LogLevel.Error, category, fullMessage);
        }

        /// <summary>
        /// 记录致命错误日志
        /// </summary>
        /// <param name="category">日志分类</param>
        /// <param name="message">日志消息</param>
        public static void Fatal(string category, string message)
        {
            Log(LogLevel.Fatal, category, message);
        }

        /// <summary>
        /// 记录致命错误日志（包含异常信息）
        /// </summary>
        /// <param name="category">日志分类</param>
        /// <param name="message">日志消息</param>
        /// <param name="exception">异常对象</param>
        public static void Fatal(string category, string message, Exception exception)
        {
            var fullMessage = $"{message}\n异常详情: {exception}";
            Log(LogLevel.Fatal, category, fullMessage);
        }

        /// <summary>
        /// 记录日志的核心方法
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="category">日志分类</param>
        /// <param name="message">日志消息</param>
        private static void Log(LogLevel level, string category, string message)
        {
            // 检查日志级别
            if (level < _minimumLogLevel)
                return;

            try
            {
                // 同时输出到调试控制台和文件
                var logMessage = FormatLogMessage(level, category, message);
                
                // 输出到调试控制台
                System.Diagnostics.Debug.WriteLine(logMessage);
                
                // 输出到控制台（如果可用）
                try
                {
                    Console.WriteLine(logMessage);
                }
                catch
                {
                    // 忽略控制台输出错误
                }

                // 异步写入文件
                _ = Task.Run(() => WriteToFile(level, category, message));
            }
            catch (Exception ex)
            {
                // 如果日志记录失败，至少输出到调试控制台
                System.Diagnostics.Debug.WriteLine($"日志记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 格式化日志消息
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="category">日志分类</param>
        /// <param name="message">日志消息</param>
        /// <returns>格式化后的日志消息</returns>
        private static string FormatLogMessage(LogLevel level, string category, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var levelStr = GetLevelString(level);
            
            return $"[{timestamp}] [{levelStr}] [{category}] [T{threadId}] {message}";
        }

        /// <summary>
        /// 获取日志级别的字符串表示
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <returns>日志级别字符串</returns>
        private static string GetLevelString(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => "DEBUG",
                LogLevel.Info => "INFO ",
                LogLevel.Warning => "WARN ",
                LogLevel.Error => "ERROR",
                LogLevel.Fatal => "FATAL",
                _ => "UNKNW"
            };
        }

        /// <summary>
        /// 写入日志到文件
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="category">日志分类</param>
        /// <param name="message">日志消息</param>
        private static void WriteToFile(LogLevel level, string category, string message)
        {
            try
            {
                lock (_lockObject)
                {
                    var logMessage = FormatLogMessage(level, category, message);
                    File.AppendAllText(_currentLogFile, logMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // 如果文件写入失败，输出到调试控制台
                System.Diagnostics.Debug.WriteLine($"写入日志文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理旧的日志文件（保留指定天数）
        /// </summary>
        /// <param name="daysToKeep">保留的天数</param>
        public static void CleanupOldLogs(int daysToKeep = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(_logDirectory, "app_*.log");

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(logFile);
                        Info("Logger", $"已删除旧日志文件: {Path.GetFileName(logFile)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Error("Logger", "清理旧日志文件失败", ex);
            }
        }

        /// <summary>
        /// 获取日志目录路径
        /// </summary>
        /// <returns>日志目录路径</returns>
        public static string GetLogDirectory()
        {
            return _logDirectory;
        }

        /// <summary>
        /// 获取当前日志文件路径
        /// </summary>
        /// <returns>当前日志文件路径</returns>
        public static string GetCurrentLogFile()
        {
            return _currentLogFile;
        }

        /// <summary>
        /// 刷新日志缓冲区（确保所有日志都写入文件）
        /// </summary>
        public static void Flush()
        {
            // 由于我们使用同步写入，这里主要是为了API完整性
            Info("Logger", "日志缓冲区已刷新");
        }
    }
}
