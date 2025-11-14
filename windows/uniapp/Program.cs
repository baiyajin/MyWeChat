using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace uniapp
{
    class Program
    {
        private static string _logFilePath = string.Empty;

        /// <summary>
        /// 写入日志（简单直接的文件写入，不依赖任何其他组件）
        /// </summary>
        private static void WriteLog(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_logFilePath))
                {
                    // 如果日志路径未初始化，尝试初始化
                    string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                    if (!Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    _logFilePath = Path.Combine(logDir, $"uniapp_launcher_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                }

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
                
                // 使用 FileStream 确保立即写入磁盘
                using (FileStream fs = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8))
                {
                    sw.Write(logEntry);
                    sw.Flush();
                    fs.Flush(true); // 强制刷新到磁盘
                }
                
                // 同时输出到控制台
                Console.WriteLine(message);
            }
            catch (Exception ex)
            {
                // 如果日志写入失败，至少尝试输出到控制台
                try
                {
                    Console.WriteLine($"[日志写入失败] {message}");
                    Console.WriteLine($"[日志写入失败原因] {ex.Message}");
                }
                catch { }
            }
        }

        /// <summary>
        /// 写入错误日志（包含异常信息）
        /// </summary>
        private static void WriteErrorLog(string message, Exception? ex = null)
        {
            string errorMessage = message;
            if (ex != null)
            {
                errorMessage += $"\n异常类型: {ex.GetType().Name}\n异常消息: {ex.Message}\n堆栈跟踪: {ex.StackTrace}";
            }
            WriteLog($"[错误] {errorMessage}");
        }

        static void Main(string[] args)
        {
            // ========== 立即输出诊断信息到控制台（不依赖日志系统） ==========
            Console.WriteLine("========================================");
            Console.WriteLine("启动器开始执行");
            Console.WriteLine("========================================");
            Console.WriteLine($"[诊断] 当前时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"[诊断] 进程ID: {Process.GetCurrentProcess().Id}");
            Console.WriteLine($"[诊断] 进程名称: {Process.GetCurrentProcess().ProcessName}");
            
            // 诊断：检查 BaseDirectory
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Console.WriteLine($"[诊断] AppDomain.BaseDirectory: {baseDir}");
            Console.WriteLine($"[诊断] BaseDirectory 是否存在: {Directory.Exists(baseDir)}");
            
            // 诊断：检查当前工作目录
            string currentDir = Directory.GetCurrentDirectory();
            Console.WriteLine($"[诊断] 当前工作目录: {currentDir}");
            Console.WriteLine($"[诊断] 工作目录是否存在: {Directory.Exists(currentDir)}");
            
            // 诊断：检查可执行文件路径
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            Console.WriteLine($"[诊断] 可执行文件路径: {exePath}");
            Console.WriteLine($"[诊断] 可执行文件是否存在: {File.Exists(exePath)}");
            
            // 诊断：检查命令行参数
            Console.WriteLine($"[诊断] 命令行参数数量: {args.Length}");
            if (args.Length > 0)
            {
                Console.WriteLine($"[诊断] 命令行参数: {string.Join(" ", args)}");
            }
            
            Console.WriteLine("========================================");
            Console.WriteLine();
            
            // ========== 尝试多个日志路径，确保能记录日志 ==========
            string[] logDirCandidates = new string[]
            {
                Path.Combine(baseDir, "Logs"),                    // 首选：BaseDirectory/Logs
                Path.Combine(currentDir, "Logs"),                // 备选1：当前工作目录/Logs
                Path.Combine(Path.GetDirectoryName(exePath) ?? baseDir, "Logs"), // 备选2：exe所在目录/Logs
                Path.Combine(Path.GetTempPath(), "uniapp_logs"),  // 备选3：临时目录
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "uniapp_logs") // 备选4：用户目录
            };
            
            bool logInitialized = false;
            foreach (string logDir in logDirCandidates)
            {
                try
                {
                    Console.WriteLine($"[诊断] 尝试日志目录: {logDir}");
                    
                    if (!Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                        Console.WriteLine($"[诊断] 已创建日志目录: {logDir}");
                    }
                    
                    // 立即初始化日志文件路径
                    _logFilePath = Path.Combine(logDir, $"uniapp_launcher_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    
                    // 写入第一条日志，确认日志系统可用
                    string firstLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========== 启动器开始执行 ==========\n";
                    File.AppendAllText(_logFilePath, firstLog);
                    
                    Console.WriteLine($"[成功] 日志文件已创建: {_logFilePath}");
                    logInitialized = true;
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[失败] 无法使用日志目录 {logDir}: {ex.Message}");
                    // 继续尝试下一个目录
                }
            }
            
            if (!logInitialized)
            {
                Console.WriteLine("[严重错误] 所有日志目录都失败，只能使用控制台输出");
                Console.WriteLine("程序将继续执行，但无法记录日志");
                Console.WriteLine("按任意键继续...");
                try
                {
                    Console.ReadKey();
                }
                catch
                {
                    // 如果无法读取按键，继续执行
                    System.Threading.Thread.Sleep(2000);
                }
            }
            
            // 确保控制台窗口保持打开（用于调试）
            // 注意：当通过 UAC 启动时，会创建新的控制台窗口
            Console.Title = "启动器 - 请勿关闭此窗口";

            WriteLog($"启动器目录: {AppDomain.CurrentDomain.BaseDirectory}");
            WriteLog($"当前工作目录: {Directory.GetCurrentDirectory()}");
            WriteLog($"可执行文件路径: {System.Reflection.Assembly.GetExecutingAssembly().Location}");
            WriteLog($"命令行参数: {string.Join(" ", args)}");
            
            try
            {
                // 获取启动器所在目录（使用多个候选路径）
                string launcherDir = AppDomain.CurrentDomain.BaseDirectory;
                string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? launcherDir;
                string currentWorkDir = Directory.GetCurrentDirectory();
                
                WriteLog($"========== 目录诊断 ==========");
                WriteLog($"AppDomain.BaseDirectory: {launcherDir}");
                WriteLog($"可执行文件所在目录: {exeDir}");
                WriteLog($"当前工作目录: {currentWorkDir}");
                
                // 优先使用可执行文件所在目录
                if (Directory.Exists(exeDir))
                {
                    launcherDir = exeDir;
                    WriteLog($"使用可执行文件所在目录作为工作目录: {launcherDir}");
                }
                else if (Directory.Exists(launcherDir))
                {
                    WriteLog($"使用 BaseDirectory 作为工作目录: {launcherDir}");
                }
                else
                {
                    launcherDir = currentWorkDir;
                    WriteLog($"回退到当前工作目录: {launcherDir}");
                }
                
                WriteLog($"最终工作目录: {launcherDir}");

                // 主程序exe路径（编译后的固定名称）
                string sourceExePath = System.IO.Path.Combine(launcherDir, "app.exe");
                WriteLog($"检查主程序: {sourceExePath}");

                if (!File.Exists(sourceExePath))
                {
                    WriteErrorLog($"找不到主程序: {sourceExePath}");
                    Console.WriteLine($"错误: 找不到主程序: {sourceExePath}");
                    Console.WriteLine($"日志文件: {_logFilePath}");
                    Console.WriteLine("按任意键退出...");
                    Console.ReadKey();
                    return;
                }
                WriteLog($"主程序存在: {sourceExePath}");
                
                // 启动主程序
                WriteLog("========== 步骤1: 检查管理员权限 ==========");
                bool isAdmin = false;
                try
                {
                    WindowsIdentity identity = WindowsIdentity.GetCurrent();
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                    WriteLog($"当前进程管理员权限检查结果: {isAdmin}");
                }
                catch (Exception ex)
                {
                    WriteErrorLog("检查管理员权限时出错", ex);
                    isAdmin = false;
                }
                
                WriteLog("========== 步骤2: 配置进程启动信息 ==========");
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = sourceExePath,
                    WorkingDirectory = launcherDir,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal // 确保窗口显示
                };
                
                // 如果启动器本身没有管理员权限，则请求管理员权限
                // 如果启动器已有管理员权限，则直接启动（避免二次UAC弹窗）
                if (!isAdmin)
                {
                    startInfo.Verb = "runas"; // 请求管理员权限
                    WriteLog("启动器无管理员权限，将请求UAC提升（Verb = runas）");
                }
                else
                {
                    WriteLog("启动器已有管理员权限，直接启动主程序（无需UAC，Verb = 空）");
                }

                // 传递命令行参数
                if (args.Length > 0)
                {
                    startInfo.Arguments = string.Join(" ", args);
                    WriteLog($"命令行参数: {startInfo.Arguments}");
                }
                else
                {
                    WriteLog("无命令行参数");
                }

                WriteLog($"启动进程路径: {sourceExePath}");
                WriteLog($"工作目录: {launcherDir}");
                WriteLog($"UseShellExecute: {startInfo.UseShellExecute}");
                WriteLog($"WindowStyle: {startInfo.WindowStyle}");
                WriteLog($"Verb: {startInfo.Verb ?? "(空)"}");

                WriteLog("========== 步骤3: 执行 Process.Start() ==========");
                Process? process = null;
                try
                {
                    WriteLog("正在调用 Process.Start()...");
                    process = Process.Start(startInfo);
                    WriteLog($"Process.Start() 返回值: {(process != null ? $"非空，PID: {process.Id}" : "null")}");
                }
                catch (Exception ex)
                {
                    WriteErrorLog($"Process.Start 抛出异常", ex);
                    Console.WriteLine($"[错误] 启动进程时发生异常: {ex.Message}");
                    Console.WriteLine($"日志文件: {_logFilePath}");
                    Console.WriteLine("按任意键退出...");
                    Console.ReadKey();
                    return;
                }
                
                if (process != null)
                {
                    WriteLog("========== Process.Start() 返回非空，使用直接验证方式 ==========");
                    WriteLog($"进程启动成功，PID: {process.Id}");
                    WriteLog($"等待进程初始化（1秒）...");

                    // 等待一小段时间确保进程真正启动
                    System.Threading.Thread.Sleep(1000);

                    // 检查进程是否还在运行
                    WriteLog("========== 步骤4: 验证进程状态 ==========");
                    try
                    {
                        process.Refresh();
                        WriteLog($"进程刷新完成，HasExited: {process.HasExited}");
                        if (process.HasExited)
                        {
                            WriteErrorLog($"进程启动后立即退出，退出代码: {process.ExitCode}");
                            Console.WriteLine("========================================");
                            Console.WriteLine("[错误] 程序启动后立即退出了");
                            Console.WriteLine("========================================");
                            Console.WriteLine($"退出代码: {process.ExitCode}");
                            Console.WriteLine($"进程路径: {sourceExePath}");
                            Console.WriteLine();
                            Console.WriteLine("可能的原因：");
                            Console.WriteLine("1. 程序需要管理员权限但启动失败");
                            Console.WriteLine("2. 程序启动时遇到异常");
                            Console.WriteLine("3. 缺少必要的依赖文件");
                            Console.WriteLine();
                            Console.WriteLine($"日志文件: {_logFilePath}");
                            Console.WriteLine("按任意键退出...");
                            Console.ReadKey();
                            return;
                        }
                        WriteLog($"进程仍在运行，PID: {process.Id}");
                    }
                    catch (Exception ex)
                    {
                        WriteErrorLog($"检查进程状态时出错", ex);
                        Console.WriteLine($"[警告] 检查进程状态时出错: {ex.Message}");
                        // 继续执行，可能进程还在运行
                    }
                    
                    // 再次确认进程是否还在运行
                    try
                    {
                        WriteLog($"通过 Process.GetProcessById({process.Id}) 再次验证...");
                        Process? checkProcess = Process.GetProcessById(process.Id);
                        if (checkProcess == null || checkProcess.HasExited)
                        {
                            WriteErrorLog($"进程已退出，PID: {process.Id}");
                            Console.WriteLine("[错误] 进程已退出");
                            Console.WriteLine($"日志文件: {_logFilePath}");
                            Console.WriteLine("按任意键退出...");
                            Console.ReadKey();
                            return;
                        }
                        WriteLog($"确认进程运行中，PID: {process.Id}");
                    }
                    catch (Exception ex)
                    {
                        WriteErrorLog($"无法确认进程状态", ex);
                        Console.WriteLine("[警告] 无法确认进程状态，但继续执行...");
                    }

                    WriteLog("========== 程序启动成功 ==========");
                    Console.WriteLine("========================================");
                    Console.WriteLine("[成功] 程序已启动");
                    Console.WriteLine("========================================");
                    Console.WriteLine($"[信息] 进程ID: {process.Id}");
                    Console.WriteLine($"[信息] 进程路径: {sourceExePath}");
                    Console.WriteLine($"[提示] 在任务管理器中查找进程名称: app");
                    Console.WriteLine();
                    Console.WriteLine("如果看不到程序窗口，请检查：");
                    Console.WriteLine("1. 任务管理器中是否有该进程");
                    Console.WriteLine("2. 系统托盘是否有程序图标");
                    Console.WriteLine($"3. 日志文件: {Path.Combine(launcherDir, "Logs")}");
                    Console.WriteLine($"4. 启动器日志: {_logFilePath}");
                    Console.WriteLine("========================================");
                    Console.WriteLine();
                    Console.WriteLine("启动器将在10秒后自动关闭（或按任意键立即关闭）...");
                    
                    // 等待用户按键或10秒超时
                    var keyTask = Task.Run(() =>
                    {
                        try
                        {
                            Console.ReadKey(true);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    });
                    
                    var delayTask = Task.Delay(10000);
                    Task.WaitAny(keyTask, delayTask);
                    
                    WriteLog("启动器正常退出");
                }
                else
                {
                    WriteLog("========== Process.Start() 返回 null ==========");
                    WriteLog("注意：使用 Verb = 'runas' 时，即使进程成功启动，Process.Start() 也可能返回 null");
                    WriteLog("这是 Windows 的安全机制，通常表示进程已启动但无法获取 Process 对象");
                    
                    // 等待几秒，让进程有时间启动（UAC 提升需要时间）
                    WriteLog("等待进程启动（3秒）...");
                    System.Threading.Thread.Sleep(3000);
                    
                    // 通过进程名查找验证进程是否已启动
                    WriteLog("========== 步骤4: 通过进程名验证进程是否启动 ==========");
                    bool processFound = false;
                    int foundProcessId = 0;
                    WriteLog($"查找进程名: app");
                    
                    try
                    {
                        Process[] processes = Process.GetProcessesByName("app");
                        WriteLog($"找到 {processes.Length} 个同名进程");
                        
                        if (processes.Length > 0)
                        {
                            // 找到进程，验证是否是我们要启动的进程（通过路径匹配）
                            foreach (Process proc in processes)
                            {
                                try
                                {
                                    string? procPath = proc.MainModule?.FileName;
                                    WriteLog($"进程 PID: {proc.Id}, 路径: {procPath ?? "(无法获取)"}");
                                    
                                    if (!string.IsNullOrEmpty(procPath))
                                    {
                                        // 比较路径（不区分大小写）
                                        if (string.Equals(procPath, sourceExePath, StringComparison.OrdinalIgnoreCase))
                                        {
                                            processFound = true;
                                            foundProcessId = proc.Id;
                                            WriteLog($"✓ 找到匹配的进程！PID: {proc.Id}, 路径: {procPath}");
                                            break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    WriteLog($"检查进程 {proc.Id} 时出错: {ex.Message}");
                                    // 如果无法获取路径，但进程名匹配，也认为可能是目标进程
                                    if (!processFound)
                                    {
                                        processFound = true;
                                        foundProcessId = proc.Id;
                                        WriteLog($"✓ 找到可能的匹配进程（无法验证路径）PID: {proc.Id}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            WriteLog("未找到同名进程");
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteErrorLog("通过进程名查找进程时出错", ex);
                    }
                    
                    if (processFound)
                    {
                        WriteLog("========== 程序启动成功（通过进程名验证） ==========");
                        Console.WriteLine("========================================");
                        Console.WriteLine("[成功] 程序已启动（通过进程名验证）");
                        Console.WriteLine("========================================");
                        Console.WriteLine($"[信息] 进程ID: {foundProcessId}");
                        Console.WriteLine($"[信息] 进程路径: {sourceExePath}");
                        Console.WriteLine($"[提示] 在任务管理器中查找进程名称: app");
                        Console.WriteLine();
                        Console.WriteLine("如果看不到程序窗口，请检查：");
                        Console.WriteLine("1. 任务管理器中是否有该进程");
                        Console.WriteLine("2. 系统托盘是否有程序图标");
                        Console.WriteLine($"3. 日志文件: {Path.Combine(launcherDir, "Logs")}");
                        Console.WriteLine($"4. 启动器日志: {_logFilePath}");
                        Console.WriteLine("========================================");
                        Console.WriteLine();
                        Console.WriteLine("启动器将在10秒后自动关闭（或按任意键立即关闭）...");
                        
                        // 等待用户按键或10秒超时
                        var keyTask = Task.Run(() =>
                        {
                            try
                            {
                                Console.ReadKey(true);
                                return true;
                            }
                            catch
                            {
                                return false;
                            }
                        });
                        
                        var delayTask = Task.Delay(10000);
                        Task.WaitAny(keyTask, delayTask);
                        
                        WriteLog("启动器正常退出");
                    }
                    else
                    {
                        WriteErrorLog("Process.Start 返回 null 且未找到进程（可能是 UAC 被拒绝或启动失败）");
                        WriteLog("========== 启动失败 ==========");
                        Console.WriteLine("========================================");
                        Console.WriteLine("[错误] 程序启动失败");
                        Console.WriteLine("========================================");
                        Console.WriteLine("可能的原因：");
                        Console.WriteLine("1. UAC 权限被拒绝");
                        Console.WriteLine("2. 文件权限不足");
                        Console.WriteLine("3. 缺少必要的依赖");
                        Console.WriteLine("4. 进程启动后立即崩溃");
                        Console.WriteLine();
                        Console.WriteLine($"进程路径: {sourceExePath}");
                        Console.WriteLine($"日志文件: {_logFilePath}");
                        Console.WriteLine();
                        Console.WriteLine("请检查：");
                        Console.WriteLine("1. 是否点击了 UAC 确认对话框");
                        Console.WriteLine("2. 任务管理器中是否有相关进程");
                        Console.WriteLine("3. 主程序的日志文件（Logs 目录）");
                        Console.WriteLine("========================================");
                        Console.WriteLine("按任意键退出...");
                        Console.ReadKey();
                    }
                }
            }
            catch (Exception ex)
            {
                WriteErrorLog($"启动失败（未捕获的异常）", ex);
                Console.WriteLine($"启动失败: {ex.Message}");
                Console.WriteLine($"日志文件: {_logFilePath}");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
            finally
            {
                try
                {
                    WriteLog("========== 启动器执行结束 ==========");
                    
                    // 确保所有日志都写入磁盘
                    if (!string.IsNullOrEmpty(_logFilePath) && File.Exists(_logFilePath))
                    {
                        using (FileStream fs = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                        using (StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8))
                        {
                            sw.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [最终] 日志文件路径: {_logFilePath}");
                            sw.Flush();
                            fs.Flush(true);
                        }
                    }
                }
                catch
                {
                    // 忽略最终日志写入失败
                }
            }
        }
    }
}

