using System;
using System.IO;

namespace SalesChampion.Windows.Utils
{
    /// <summary>
    /// 日志记录器
    /// 记录应用程序运行日志和错误信息
    /// </summary>
    public static class Logger
    {
        private static readonly object _lockObject = new object();
        private static string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        /// <summary>
        /// 日志输出事件（用于输出到UI）
        /// </summary>
        public static event Action<string> OnLogMessage;

        /// <summary>
        /// 初始化日志目录
        /// </summary>
        static Logger()
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        public static void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        public static void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        public static void LogError(string message)
        {
            WriteLog("ERROR", message);
        }

        /// <summary>
        /// 记录错误日志（带异常信息）
        /// </summary>
        public static void LogError(string message, Exception ex)
        {
            string errorMessage = $"{message}\n异常: {ex.Message}\n堆栈: {ex.StackTrace}";
            WriteLog("ERROR", errorMessage);
        }

        /// <summary>
        /// 写入日志文件
        /// </summary>
        private static void WriteLog(string level, string message)
        {
            lock (_lockObject)
            {
                try
                {
                    string fileName = $"log_{DateTime.Now:yyyyMMdd}.txt";
                    string filePath = Path.Combine(_logDirectory, fileName);
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}\n";

                    // 写入文件
                    File.AppendAllText(filePath, logEntry);

                    // 触发UI日志事件
                    try
                    {
                        OnLogMessage?.Invoke($"[{level}] {message}");
                    }
                    catch
                    {
                        // 忽略UI日志输出失败
                    }
                }
                catch
                {
                    // 忽略日志写入失败
                }
            }
        }
    }
}

