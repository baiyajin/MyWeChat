using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyWeChat.Windows.Utils
{
    public static class Logger
    {
        private static readonly ConcurrentQueue<LogEntry> _logQueue = new ConcurrentQueue<LogEntry>();
        private static readonly Task _writeTask;
        private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        
        private const long MaxLogFileSize = 10 * 1024 * 1024;
        private const int MaxLogFiles = 10;
        private const int MaxLogDays = 7;
        
        private static long _lastSizeCheck = 0;
        private static long _lastSizeCheckTime = 0;
        private const long SizeCheckInterval = 1024 * 1024;
        private const long SizeCheckTimeInterval = 60000;
        
        private class LogEntry
        {
            public required string Level { get; set; }
            public required string Message { get; set; }
            public DateTime Timestamp { get; set; }
        }

        static Logger()
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
            
            _ = Task.Run(() => CleanOldLogs());
            
            _writeTask = Task.Run(async () => await ProcessLogQueueAsync(_cancellationTokenSource.Token));
        }

        public static void LogInfo(string message)
        {
            _logQueue.Enqueue(new LogEntry { Level = "INFO", Message = message, Timestamp = DateTime.Now });
        }

        public static void LogWarning(string message)
        {
            _logQueue.Enqueue(new LogEntry { Level = "WARN", Message = message, Timestamp = DateTime.Now });
        }

        public static void LogError(string message)
        {
            _logQueue.Enqueue(new LogEntry { Level = "ERROR", Message = message, Timestamp = DateTime.Now });
        }

        public static void LogError(string message, Exception ex)
        {
            string errorMessage = $"{message}\n异常: {ex.Message}\n堆栈: {ex.StackTrace}";
            _logQueue.Enqueue(new LogEntry { Level = "ERROR", Message = errorMessage, Timestamp = DateTime.Now });
        }

        public static void LogSuccess(string message)
        {
            _logQueue.Enqueue(new LogEntry { Level = "SUCCESS", Message = message, Timestamp = DateTime.Now });
        }

        private static async Task ProcessLogQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_logQueue.TryDequeue(out LogEntry? entry))
                    {
                        await WriteLogAsync(entry);
                    }
                    else
                    {
                        await Task.Delay(10, cancellationToken);
                    }
                }
                catch
                {
                }
            }
        }

        private static async Task WriteLogAsync(LogEntry entry)
        {
            try
            {
                string fileName = $"log_{entry.Timestamp:yyyyMMdd}.txt";
                string filePath = Path.Combine(_logDirectory, fileName);
                
                bool needCheckSize = false;
                long currentTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                
                if (File.Exists(filePath))
                {
                    if (currentTime - _lastSizeCheckTime > SizeCheckTimeInterval)
                    {
                        needCheckSize = true;
                    }
                    else
                    {
                        _lastSizeCheck += entry.Message.Length + 100;
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
                                string timestamp = entry.Timestamp.ToString("HHmmss");
                                string newFileName = $"log_{entry.Timestamp:yyyyMMdd}_{timestamp}.txt";
                                string newFilePath = Path.Combine(_logDirectory, newFileName);
                                File.Move(filePath, newFilePath);
                                
                                _ = Task.Run(() => CleanOldLogs());
                            }
                            _lastSizeCheckTime = currentTime;
                        }
                        catch
                        {
                        }
                    }
                }
                
                string logEntry = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Message}\n";

                // 加密日志内容（使用本地密钥）
                try
                {
                    string encryptedLogEntry = EncryptionService.EncryptStringForLog(logEntry);
                    await File.AppendAllTextAsync(filePath, encryptedLogEntry + "\n");
                }
                catch (Exception encryptEx)
                {
                    // 如果加密失败，回退到明文（避免递归调用 Logger.LogError）
                    // 直接写入错误信息到文件
                    try
                    {
                        string errorLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] 日志加密失败，使用明文: {encryptEx.Message}\n";
                        await File.AppendAllTextAsync(filePath, errorLog);
                        await File.AppendAllTextAsync(filePath, logEntry);
                    }
                    catch
                    {
                        // 如果写入也失败，忽略
                    }
                }

                // 设置日志文件为隐藏属性
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.Hidden);
                    }
                }
                catch
                {
                    // 忽略设置隐藏属性失败
                }
            }
            catch
            {
            }
        }

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
