using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyWeChat.Windows.Utils
{
    /// <summary>
    /// 日志记录器
    /// 记录应用程序运行日志和错误信息
    /// </summary>
    public static class Logger
    {
        private static readonly object _lockObject = new object();
        private static string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        
        // 日志切割配置
        private const long MaxLogFileSize = 10 * 1024 * 1024; // 10MB，超过此大小则切割
        private const int MaxLogFiles = 10; // 最多保留10个日志文件
        private const int MaxLogDays = 7; // 最多保留7天的日志
        
        // 性能优化：减少文件大小检查频率
        private static long _lastSizeCheck = 0;
        private static long _lastSizeCheckTime = 0;
        private const long SizeCheckInterval = 1024 * 1024; // 每1MB检查一次文件大小
        private const long SizeCheckTimeInterval = 60000; // 每60秒检查一次文件大小

        /// <summary>
        /// 日志输出事件（用于输出到UI）
        /// </summary>
        public static event Action<string>? OnLogMessage;

        /// <summary>
        /// 初始化日志目录
        /// </summary>
        static Logger()
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
            
            // 启动时清理旧日志
            CleanOldLogs();
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
        /// 记录成功日志
        /// </summary>
        public static void LogSuccess(string message)
        {
            WriteLog("SUCCESS", message);
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
                    
                    // 性能优化：减少文件大小检查频率
                    // 只在写入前检查，且使用缓存避免频繁IO操作
                    bool needCheckSize = false;
                    long currentTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    
                    if (File.Exists(filePath))
                    {
                        // 如果距离上次检查时间超过60秒，或者文件可能已经增长很多，才检查
                        if (currentTime - _lastSizeCheckTime > SizeCheckTimeInterval)
                        {
                            needCheckSize = true;
                        }
                        else
                        {
                            // 估算：如果上次检查后写入的数据可能超过1MB，才检查
                            // 这里简化处理，每次写入都更新计数器
                            _lastSizeCheck += message.Length + 100; // 估算每条日志约100字节开销
                            if (_lastSizeCheck >= SizeCheckInterval)
                            {
                                needCheckSize = true;
                                _lastSizeCheck = 0;
                            }
                        }
                        
                        if (needCheckSize)
                        {
                            try
                            {
                                FileInfo fileInfo = new FileInfo(filePath);
                                if (fileInfo.Length >= MaxLogFileSize)
                                {
                                    // 切割日志文件，添加时间戳后缀
                                    string timestamp = DateTime.Now.ToString("HHmmss");
                                    string newFileName = $"log_{DateTime.Now:yyyyMMdd}_{timestamp}.txt";
                                    string newFilePath = Path.Combine(_logDirectory, newFileName);
                                    File.Move(filePath, newFilePath);
                                    
                                    // 异步清理旧日志，避免阻塞
                                    _ = Task.Run(() => CleanOldLogs());
                                }
                                _lastSizeCheckTime = currentTime;
                            }
                            catch
                            {
                                // 忽略文件检查失败，继续写入
                            }
                        }
                    }
                    
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

        /// <summary>
        /// 清理旧日志文件
        /// </summary>
        private static void CleanOldLogs()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    return;
                }

                // 获取所有日志文件
                var logFiles = Directory.GetFiles(_logDirectory, "log_*.txt")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                // 按天数清理：删除超过MaxLogDays天的日志
                DateTime cutoffDate = DateTime.Now.AddDays(-MaxLogDays);
                var filesToDelete = logFiles.Where(f => f.CreationTime < cutoffDate).ToList();
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        File.Delete(file.FullName);
                    }
                    catch
                    {
                        // 忽略删除失败
                    }
                }

                // 按数量清理：如果文件数量超过MaxLogFiles，删除最旧的
                var remainingFiles = Directory.GetFiles(_logDirectory, "log_*.txt")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (remainingFiles.Count > MaxLogFiles)
                {
                    var filesToDeleteByCount = remainingFiles.Skip(MaxLogFiles).ToList();
                    foreach (var file in filesToDeleteByCount)
                    {
                        try
                        {
                            File.Delete(file.FullName);
                        }
                        catch
                        {
                            // 忽略删除失败
                        }
                    }
                }
            }
            catch
            {
                // 忽略清理失败
            }
        }
    }
}

