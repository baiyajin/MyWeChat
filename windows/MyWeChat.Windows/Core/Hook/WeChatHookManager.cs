using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyWeChat.Windows.Core.DLLWrapper;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Core.Hook
{
    /// <summary>
    /// MyWeChat管理器
    /// 负责Hook注入到微信进程，建立通信通道
    /// </summary>
    public class WeChatHookManager
    {
        // P/Invoke声明：检查窗口是否可见
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        
        // P/Invoke声明：检查窗口是否响应
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
        
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private const uint WM_NULL = 0x0000;
        private WeChatHelperWrapperBase? _dllWrapper;
        
        // 回调函数委托
        private WeChatHelperWrapperBase.AcceptCallback? _acceptCallback;
        private WeChatHelperWrapperBase.ReceiveCallback? _receiveCallback;
        private WeChatHelperWrapperBase.CloseCallback? _closeCallback;
        
        private string? _weChatVersion;
        private int _clientId;
        private bool _isHooked;
        private int _weChatProcessId; // 保存微信进程ID，用于检查进程是否还在运行
        private string? _randomDllPath; // 保存随机文件名的DLL路径
        private readonly object _lockObject = new object();

        /// <summary>
        /// 客户端ID
        /// </summary>
        public int ClientId => _clientId;

        /// <summary>
        /// Hook状态
        /// </summary>
        public bool IsHooked => _isHooked;

        /// <summary>
        /// 微信版本号
        /// </summary>
        public string? WeChatVersion => _weChatVersion;

        /// <summary>
        /// Hook事件
        /// </summary>
        public event EventHandler<int>? OnHooked;
        public event EventHandler? OnUnhooked;
        public event EventHandler<string>? OnMessageReceived;
        
        /// <summary>
        /// 进度更新事件（步骤，总步骤，状态）
        /// </summary>
        public event EventHandler<(int step, int totalSteps, string status)>? OnProgressUpdate;

        /// <summary>
        /// 初始化Hook管理器
        /// </summary>
        public bool Initialize(string weChatVersion)
        {
            try
            {
                _weChatVersion = weChatVersion;
                string? dllDirectory = WeChatVersionDetector.GetDllDirectoryPath(weChatVersion);
                
                if (string.IsNullOrEmpty(dllDirectory) || !Directory.Exists(dllDirectory))
                {
                    Logger.LogError($"DLL目录不存在: {dllDirectory}");
                    return false;
                }

                string dllPath = Path.Combine(dllDirectory, "WxHelp.dll");
                string actualDllDirectory = dllDirectory;
                string? sourceDllPath = null;
                
                if (!File.Exists(dllPath))
                {
                    // 如果当前版本目录没有WxHelp.dll，尝试从其他版本查找
                    Logger.LogWarning($"当前版本目录没有WxHelp.dll: {dllPath}");
                    sourceDllPath = FindWxHelpDll(dllDirectory);
                    if (string.IsNullOrEmpty(sourceDllPath))
                    {
                        Logger.LogError($"无法找到WxHelp.dll，请确保DLL文件存在");
                        return false;
                    }
                    Logger.LogInfo($"从其他目录找到WxHelp.dll: {sourceDllPath}");
                    
                    // 将找到的DLL复制到当前版本目录，以便DllImport能够正确加载
                    try
                    {
                        Logger.LogInfo($"正在复制WxHelp.dll到当前版本目录: {dllPath}");
                        File.Copy(sourceDllPath, dllPath, true);
                        Logger.LogInfo($"WxHelp.dll复制成功");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"复制WxHelp.dll失败: {ex.Message}");
                        // 如果复制失败，尝试使用源路径
                        dllPath = sourceDllPath;
                        actualDllDirectory = Path.GetDirectoryName(sourceDllPath) ?? string.Empty;
                    }
                }

                // 确保DLL文件存在
                if (!File.Exists(dllPath))
                {
                    Logger.LogError($"WxHelp.dll不存在: {dllPath}");
                    return false;
                }

                // 生成随机文件名并复制DLL（反检测措施）
                string randomDllName = GenerateRandomDllName();
                string randomDllPath = Path.Combine(actualDllDirectory, randomDllName);
                
                try
                {
                    File.Copy(dllPath, randomDllPath, true);
                    _randomDllPath = randomDllPath;
                    Logger.LogInfo($"DLL已复制为随机文件名: {randomDllName}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"复制DLL为随机文件名失败: {ex.Message}，将使用原始文件名");
                    _randomDllPath = dllPath; // 回退到原始路径
                }

                // 设置DLL搜索路径（包含实际找到的DLL目录）
                string? pathEnv = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
                if (pathEnv != null && !pathEnv.Contains(actualDllDirectory))
                {
                    Environment.SetEnvironmentVariable("PATH", 
                        $"{actualDllDirectory};{dllDirectory};{pathEnv}", 
                        EnvironmentVariableTarget.Process);
                    // 注意：PATH环境变量设置的详细信息是内部实现细节，不再输出
                }

                // 使用SetDllDirectory API设置DLL搜索路径（优先级更高）
                try
                {
                    // 获取包含DLL的目录（不是文件路径）
                    string? dllDir = Path.GetDirectoryName(_randomDllPath ?? dllPath);
                    if (!string.IsNullOrEmpty(dllDir))
                    {
                        // 直接调用SetDllDirectory设置DLL搜索路径
                        bool result = WeChatHelperWrapper_3_9_12_45.SetDllDirectory(dllDir);
                        if (!result)
                        {
                            int errorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                            Logger.LogWarning($"SetDllDirectory失败，错误码: {errorCode}，将使用PATH环境变量");
                        }
                        // 注意：SetDllDirectory成功的详细信息是内部实现细节，不再输出
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"设置SetDllDirectory失败: {ex.Message}，将使用PATH环境变量");
                }

                _dllWrapper = WeChatHelperWrapperFactory.Create(weChatVersion, _randomDllPath ?? dllPath);
                if (_dllWrapper == null)
                {
                    Logger.LogError($"无法创建DLL封装实例，版本: {weChatVersion}");
                    return false;
                }
                
                // 检查DLL架构是否匹配（减少日志输出）
                string? dllArchitecture = GetDllArchitecture(dllPath);
                string appArchitecture = IntPtr.Size == 8 ? "64位" : "32位";
                
                if (!string.IsNullOrEmpty(dllArchitecture))
                {
                    bool isDll64Bit = dllArchitecture.Contains("64") || dllArchitecture.Contains("x64");
                    bool isApp64Bit = IntPtr.Size == 8;
                    
                    if (isDll64Bit != isApp64Bit)
                    {
                        string errorMsg = $"架构不匹配！应用程序是{appArchitecture}，但DLL是{dllArchitecture}。请确保DLL架构与应用程序架构匹配。";
                        Logger.LogError(errorMsg);
                        throw new InvalidOperationException(errorMsg);
                    }
                }

                // 设置回调函数
                _acceptCallback = OnAcceptCallback;
                _receiveCallback = OnReceiveCallback;
                _closeCallback = OnCloseCallback;
                
                IntPtr acceptPtr = Marshal.GetFunctionPointerForDelegate(_acceptCallback);
                IntPtr receivePtr = Marshal.GetFunctionPointerForDelegate(_receiveCallback);
                IntPtr closePtr = Marshal.GetFunctionPointerForDelegate(_closeCallback);
                
                // 某些版本需要contact参数
                // 3.9.12.45版本需要contact参数（传空字符串）
                // 4.1.0.34版本不需要contact参数（传null）
                string? contact = _weChatVersion == "3.9.12.45" ? "" : null;
                
                try
                {
                    int setCallbackResult = _dllWrapper.SetCallback(acceptPtr, receivePtr, closePtr, contact);
                    if (setCallbackResult != 0)
                    {
                        Logger.LogError($"回调函数设置失败，返回值: {setCallbackResult}");
                        return false;
                    }
                }
                catch (BadImageFormatException ex)
                {
                    string errorMsg = $"DLL架构不匹配！错误: {ex.Message}。应用程序是{appArchitecture}，请确保DLL也是{appArchitecture}。";
                    Logger.LogError(errorMsg);
                    throw new InvalidOperationException(errorMsg, ex);
                }

                Logger.LogInfo($"Hook管理器初始化成功，版本: {weChatVersion}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Hook管理器初始化失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 打开微信并Hook
        /// </summary>
        /// <param name="weChatExePath">微信可执行文件路径，如果为空则自动查找</param>
        /// <param name="autoStartWeChat">是否自动启动微信（如果微信未运行）。false表示不自动启动，仅检测已运行的微信</param>
        /// <returns>返回是否成功</returns>
        public bool OpenAndHook(string? weChatExePath = null, bool autoStartWeChat = false)
        {
            lock (_lockObject)
            {
                try
                {
                    if (_isHooked)
                    {
                        string msg = "微信已经Hook，无需重复操作";
                        Logger.LogWarning(msg);
                        return true;
                    }

                    // 检查DLL封装是否初始化
                    if (_dllWrapper == null)
                    {
                        string errorMsg = $"DLL封装未初始化，微信版本: {_weChatVersion ?? "未知"}";
                        Logger.LogError(errorMsg);
                        throw new InvalidOperationException(errorMsg);
                    }

                    Logger.LogInfo($"开始Hook微信，版本: {_weChatVersion}");

                    // 如果未提供路径，自动查找微信安装路径
                    if (string.IsNullOrEmpty(weChatExePath))
                    {
                        Logger.LogInfo("正在查找微信可执行文件...");
                        weChatExePath = FindWeChatExecutable();
                        if (string.IsNullOrEmpty(weChatExePath))
                        {
                            string errorMsg = "未找到微信可执行文件，请检查微信是否已安装";
                            Logger.LogError(errorMsg);
                            throw new FileNotFoundException(errorMsg);
                        }
                        Logger.LogInfo($"找到微信可执行文件: {weChatExePath}");
                    }
                    else
                    {
                        if (!File.Exists(weChatExePath))
                        {
                            string errorMsg = $"指定的微信可执行文件不存在: {weChatExePath}";
                            Logger.LogError(errorMsg);
                            throw new FileNotFoundException(errorMsg);
                        }
                        Logger.LogInfo($"使用指定的微信路径: {weChatExePath}");
                    }

                    // 步骤4：检测微信进程
                    OnProgressUpdate?.Invoke(this, (4, 15, "检测微信进程..."));
                    Logger.LogInfo("正在检查微信是否正在运行...");
                    Process? existingWeChatProcess = FindWeChatProcess();
                    if (existingWeChatProcess == null)
                    {
                        if (autoStartWeChat)
                        {
                            // 步骤5：启动微信（仅在允许自动启动时）
                            OnProgressUpdate?.Invoke(this, (5, 15, "启动微信..."));
                            Logger.LogInfo("微信未运行，正在启动微信...");
                            try
                            {
                                ProcessStartInfo startInfo = new ProcessStartInfo
                                {
                                    FileName = weChatExePath,
                                    UseShellExecute = true,
                                    WorkingDirectory = Path.GetDirectoryName(weChatExePath) ?? string.Empty
                                };
                                
                                Process? startedProcess = Process.Start(startInfo);
                                if (startedProcess == null)
                                {
                                    string errorMsg = "启动微信失败，无法创建进程";
                                    Logger.LogError(errorMsg);
                                    throw new InvalidOperationException(errorMsg);
                                }
                                
                                Logger.LogInfo("微信未运行，正在启动微信，等待微信启动...");
                                
                                // 等待微信启动（最多等待10秒）
                                int startWaitCount = 0;
                                int startMaxWaitCount = 100; // 10秒 = 100 * 100ms
                                while (startWaitCount < startMaxWaitCount)
                                {
                                    Thread.Sleep(100);
                                    existingWeChatProcess = FindWeChatProcess();
                                    if (existingWeChatProcess != null)
                                    {
                                        Logger.LogInfo($"微信已启动，进程ID: {existingWeChatProcess.Id}");
                                        break;
                                    }
                                    startWaitCount++;
                                }
                                
                                if (existingWeChatProcess == null)
                                {
                                    string errorMsg = "微信启动超时，未找到运行中的微信进程。可能原因：1. 微信启动失败 2. 进程名称不匹配 3. 等待时间不足";
                                    Logger.LogError(errorMsg);
                                    throw new InvalidOperationException(errorMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                string errorMsg = $"启动微信失败: {ex.Message}";
                                Logger.LogError(errorMsg, ex);
                                throw new InvalidOperationException(errorMsg, ex);
                            }
                        }
                        else
                        {
                            // 微信未运行，且不允许自动启动
                            string errorMsg = "微信进程未运行，请先手动启动微信，或点击"登录微信"按钮";
                            Logger.LogWarning(errorMsg);
                            OnProgressUpdate?.Invoke(this, (4, 15, "微信未运行，请手动启动"));
                            throw new InvalidOperationException(errorMsg);
                        }
                    }
                    else
                    {
                        Logger.LogInfo($"微信已在运行，进程ID: {existingWeChatProcess.Id}");
                    }

                    // 打开微信互斥锁
                    Logger.LogInfo("正在打开微信互斥锁...");
                    int result = _dllWrapper.OpenWeChatMutex(weChatExePath ?? string.Empty);
                    
                    Process? weChatProcess = null;
                    
                    if (result == 0)
                    {
                        // 返回0表示成功打开互斥锁，微信正在启动
                        Logger.LogInfo("微信互斥锁打开成功，等待微信启动...");
                        
                        // 等待微信启动
                        Logger.LogInfo("等待微信启动（3秒）...");
                        Thread.Sleep(3000);

                        // 查找微信进程
                        Logger.LogInfo("正在查找微信进程...");
                        weChatProcess = FindWeChatProcess();
                        if (weChatProcess == null)
                        {
                            // 如果找不到，尝试使用之前找到的进程
                            if (existingWeChatProcess != null)
                            {
                                Logger.LogWarning("通过互斥锁未找到进程，使用之前找到的进程");
                                weChatProcess = existingWeChatProcess;
                            }
                            else
                            {
                                string errorMsg = "未找到运行中的微信进程。可能原因：1. 微信启动失败 2. 进程名称不匹配 3. 等待时间不足";
                                Logger.LogError(errorMsg);
                                throw new InvalidOperationException(errorMsg);
                            }
                        }
                    }
                    else if (result > 0)
                    {
                        // 返回值 > 0 表示微信已运行，返回值是进程ID
                        // 注意：如果前面已经有"微信已启动，进程ID"的日志，这里不再重复输出
                        
                        // 尝试通过进程ID获取进程对象
                        try
                        {
                            weChatProcess = Process.GetProcessById(result);
                            // 注意：找到进程的日志将在后面统一输出，这里不再重复输出
                        }
                        catch (ArgumentException)
                        {
                            // 如果通过进程ID找不到，尝试通过进程名查找
                            Logger.LogWarning($"通过进程ID {result} 找不到进程，尝试通过进程名查找...");
                            weChatProcess = FindWeChatProcess();
                            if (weChatProcess == null)
                            {
                                // 如果找不到，尝试使用之前找到的进程
                                if (existingWeChatProcess != null)
                                {
                                    Logger.LogWarning("通过进程ID和进程名都未找到进程，使用之前找到的进程");
                                    weChatProcess = existingWeChatProcess;
                                }
                                else
                                {
                                    string errorMsg = $"未找到运行中的微信进程（进程ID: {result}）。可能原因：1. 进程已退出 2. 进程名称不匹配";
                                    Logger.LogError(errorMsg);
                                    throw new InvalidOperationException(errorMsg);
                                }
                            }
                        }
                    }
                    else
                    {
                        // 返回值 < 0 表示真正的错误
                        // 但如果我们已经找到了微信进程，可以继续处理
                        if (existingWeChatProcess != null)
                        {
                            Logger.LogWarning($"打开微信互斥锁返回错误码: {result}，但已找到微信进程，继续处理");
                            weChatProcess = existingWeChatProcess;
                        }
                        else
                        {
                            string errorMsg = $"打开微信失败，返回码: {result}。可能原因：1. 权限不足 2. DLL版本不匹配 3. 微信未安装";
                            Logger.LogError(errorMsg);
                            throw new InvalidOperationException(errorMsg);
                        }
                    }
                    
                    // 如果仍未找到进程，使用之前找到的进程
                    if (weChatProcess == null && existingWeChatProcess != null)
                    {
                        Logger.LogWarning("使用之前找到的微信进程");
                        weChatProcess = existingWeChatProcess;
                    }

                    // 确保找到了微信进程
                    if (weChatProcess == null)
                    {
                        string errorMsg = "未找到运行中的微信进程，无法继续Hook";
                        Logger.LogError(errorMsg);
                        throw new InvalidOperationException(errorMsg);
                    }

                    Logger.LogInfo($"找到微信进程，PID: {weChatProcess.Id}, 进程名: {weChatProcess.ProcessName}");

                    // 保存微信进程ID，用于后续检查进程是否还在运行
                    _weChatProcessId = weChatProcess.Id;

                    // ========== 等待微信完全启动（防止注入时机过早导致崩溃） ==========
                    Logger.LogInfo("等待微信完全启动（检查窗口可见性和主线程初始化）...");
                    
                    // 步骤6：等待微信窗口可见
                    OnProgressUpdate?.Invoke(this, (6, 15, "等待微信窗口可见..."));
                    bool isWindowVisible = false;
                    int windowWaitCount = 0;
                    int maxWindowWaitCount = 100; // 最多等待10秒

                    while (windowWaitCount < maxWindowWaitCount && !isWindowVisible)
                    {
                        try
                        {
                            // 刷新进程信息
                            weChatProcess.Refresh();
                            IntPtr mainWindowHandle = weChatProcess.MainWindowHandle;
                            
                            if (mainWindowHandle != IntPtr.Zero)
                            {
                                // 检查窗口是否可见
                                isWindowVisible = IsWindowVisible(mainWindowHandle);
                                
                                if (isWindowVisible)
                                {
                                    Logger.LogInfo("微信窗口已可见");
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 忽略异常，继续等待
                            Logger.LogWarning($"检查微信窗口状态时出错（继续等待）: {ex.Message}");
                        }
                        
                        Thread.Sleep(100);
                        windowWaitCount++;
                    }

                    if (!isWindowVisible)
                    {
                        Logger.LogWarning("微信窗口在10秒内未可见，但继续检查主线程初始化");
                    }

                    // 步骤7-12：检查主线程是否已完全初始化
                    bool isReady = CheckWeChatMainThreadReady(weChatProcess);

                    if (!isReady)
                    {
                        Logger.LogError("微信主线程初始化检查失败，但继续尝试注入");
                        // 即使检查失败，也等待一段时间后尝试注入
                        Thread.Sleep(3000);
                    }
                    // ========== 等待逻辑结束 ==========
                    
                    // 步骤13：注入DLL
                    OnProgressUpdate?.Invoke(this, (13, 15, "正在注入DLL..."));

                    // 注入到微信进程
                    Logger.LogInfo($"正在注入到微信进程 (PID: {weChatProcess.Id})...");
                    result = _dllWrapper.InjectWeChatProcess(weChatProcess.Id);
                    
                    // 根据原项目，InjectWeChatPid 返回 int，原项目不检查返回值
                    // 从日志看，返回进程ID后实际上连接成功了，说明返回值可能是进程ID或者成功标志
                    // 如果返回值 < 0，可能是错误码；如果返回值 >= 0，可能是进程ID或成功标志
                    if (result < 0)
                    {
                        string errorMsg = $"注入微信进程失败，返回码: {result}。可能原因：1. 权限不足（需要管理员权限） 2. DLL版本不匹配 3. 微信版本不支持";
                        Logger.LogError(errorMsg);
                        throw new InvalidOperationException(errorMsg);
                    }
                    
                    // 返回值 >= 0 表示成功，可能是进程ID或成功标志（0）
                    Logger.LogInfo($"注入微信进程成功，返回值: {result}");

                    // 注意：注入成功后，必须等待OnAcceptCallback被调用，才能确定真正的clientId
                    // OnAcceptCallback中的clientId才是DLL内部使用的正确ID
                    // 如果使用错误的clientId发送命令，可能导致命令发送失败或被风控
                    
                    // 步骤14：等待微信连接回调
                    OnProgressUpdate?.Invoke(this, (14, 15, "等待微信连接回调..."));
                    
                    // 等待OnAcceptCallback被调用，最多等待5秒（如果微信已登录，回调可能已经发送，需要更长时间）
                    int waitCount = 0;
                    int maxWaitCount = 50; // 5秒 = 50 * 100ms
                    while (_clientId == 0 && waitCount < maxWaitCount)
                    {
                        System.Threading.Thread.Sleep(100);
                        waitCount++;
                    }
                    
                    // 检查是否收到了OnAcceptCallback
                    if (_clientId == 0)
                    {
                        // 如果OnAcceptCallback没有被调用，检查InjectWeChatPid的返回值
                        // 某些版本的DLL可能直接返回clientId（进程ID）
                        if (result > 0 && result == weChatProcess.Id)
                        {
                            // 如果返回值等于进程ID，可能是DLL直接返回了clientId
                            // 或者微信在程序启动前就已经登录，OnAcceptCallback已经在程序启动前被调用过了
                            Logger.LogWarning($"OnAcceptCallback在5秒内未被调用，但InjectWeChatPid返回了进程ID: {result}");
                            Logger.LogWarning($"可能原因: 1. 微信在程序启动前就已经登录，回调已发送 2. DLL直接返回进程ID作为clientId");
                            
                            // 尝试使用进程ID作为clientId（某些版本的DLL可能直接返回进程ID）
                            // 但需要谨慎，因为之前成功时clientId=1，不是进程ID
                            // 先尝试使用进程ID，如果后续命令发送失败，再处理
                            Logger.LogWarning($"尝试使用进程ID作为clientId: {result}");
                            _clientId = result;
                            // 确保进程ID已保存（如果之前没有保存）
                            if (_weChatProcessId == 0)
                            {
                                _weChatProcessId = weChatProcess.Id;
                            }
                            _isHooked = true;
                            
                            Logger.LogInfo($"Hook成功（使用进程ID作为clientId），ClientId: {_clientId}, 微信版本: {_weChatVersion}");
                            Logger.LogWarning($"注意: 如果后续命令发送失败，可能需要等待OnAcceptCallback被调用");
                            OnHooked?.Invoke(this, _clientId);
                            
                            return true;
                        }
                        else
                        {
                            Logger.LogError($"OnAcceptCallback在5秒内未被调用，无法确定正确的clientId");
                            Logger.LogError($"可能原因: 1. DLL回调机制未正确设置 2. 微信版本不匹配 3. DLL版本问题 4. 微信未登录");
                            return false;
                        }
                    }
                    // 注意：OnAcceptCallback已被调用的信息已包含在"Hook成功"日志中，这里不再重复输出

                    _isHooked = true;

                    Logger.LogInfo($"Hook成功，ClientId: {_clientId}, 微信版本: {_weChatVersion}");
                    OnHooked?.Invoke(this, _clientId);

                    return true;
                }
                catch (FileNotFoundException ex)
                {
                    string errorMsg = $"文件未找到: {ex.Message}";
                    Logger.LogError($"{errorMsg}\n堆栈跟踪: {ex.StackTrace}", ex);
                    throw;
                }
                catch (InvalidOperationException ex)
                {
                    string errorMsg = $"操作无效: {ex.Message}";
                    Logger.LogError($"{errorMsg}\n堆栈跟踪: {ex.StackTrace}", ex);
                    throw;
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Hook微信失败: {ex.Message}";
                    string stackTrace = ex.StackTrace ?? "";
                    string innerException = ex.InnerException != null ? $"内部异常: {ex.InnerException.Message}" : "";
                    
                    Logger.LogError($"{errorMsg}\n堆栈跟踪: {stackTrace}\n{innerException}", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// 关闭Hook连接（安全撤回DLL注入）
        /// </summary>
        public void CloseHook()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isHooked)
                    {
                        Logger.LogInfo("Hook未连接，无需清理");
                        return;
                    }

                    Logger.LogInfo("========== 开始关闭Hook连接 ==========");
                    Logger.LogInfo($"当前ClientId: {_clientId}");
                    Logger.LogInfo($"Hook状态: {_isHooked}");
                    
                    // 1. 调用DLL的关闭方法（撤回DLL注入）
                    if (_dllWrapper != null)
                    {
                        Logger.LogInfo("正在调用DLL的CloseWeChat方法（撤回DLL注入）...");
                        bool result = _dllWrapper.CloseWeChat();
                        Logger.LogInfo($"DLL的CloseWeChat方法返回: {result}");
                        
                        if (!result)
                        {
                            Logger.LogWarning("DLL的CloseWeChat方法返回false，可能DLL注入未完全撤回");
                        }
                    }
                    else
                    {
                        Logger.LogWarning("DLL封装对象为空，无法调用CloseWeChat方法");
                    }
                    
                    // 2. 等待DLL注入完全清理（给系统时间释放文件句柄）
                    Logger.LogInfo("等待DLL注入资源释放（1秒）...");
                    System.Threading.Thread.Sleep(1000);
                    
                    // 3. 清理状态
                    _isHooked = false;
                    _clientId = 0;
                    _weChatProcessId = 0;
                    
                    Logger.LogInfo("Hook连接状态已清理");
                    Logger.LogInfo("========== Hook连接已关闭 ==========");
                    
                    // 4. 触发事件
                    OnUnhooked?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"关闭Hook连接失败: {ex.Message}", ex);
                    Logger.LogError($"异常类型: {ex.GetType().Name}");
                    Logger.LogError($"堆栈跟踪: {ex.StackTrace}");
                    
                    // 即使出错，也要清理状态
                    _isHooked = false;
                    _clientId = 0;
                    _weChatProcessId = 0;
                    OnUnhooked?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 发送命令到Hook的微信进程
        /// </summary>
        /// <param name="commandType">命令类型（命令ID）</param>
        /// <param name="data">命令数据（JSON对象）</param>
        /// <returns>返回是否成功</returns>
        public bool SendCommand(int commandType, object? data = null)
        {
            if (!_isHooked)
            {
                Logger.LogWarning("微信未Hook，无法发送命令");
                return false;
            }

            if (_dllWrapper == null)
            {
                Logger.LogError("DLL封装未初始化，无法发送命令");
                return false;
            }

            if (_clientId <= 0)
            {
                Logger.LogError($"ClientId无效: {_clientId}，无法发送命令");
                return false;
            }

            // 检查微信进程是否还在运行
            if (_weChatProcessId > 0)
            {
                try
                {
                    Process? weChatProcess = Process.GetProcessById(_weChatProcessId);
                    if (weChatProcess == null || weChatProcess.HasExited)
                    {
                        Logger.LogWarning($"微信进程已退出（PID: {_weChatProcessId}），无法发送命令");
                        _isHooked = false;
                        _clientId = 0;
                        _weChatProcessId = 0;
                        OnUnhooked?.Invoke(this, EventArgs.Empty);
                        return false;
                    }
                }
                catch (ArgumentException)
                {
                    // 进程不存在
                    Logger.LogWarning($"微信进程不存在（PID: {_weChatProcessId}），无法发送命令");
                    _isHooked = false;
                    _clientId = 0;
                    _weChatProcessId = 0;
                    OnUnhooked?.Invoke(this, EventArgs.Empty);
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"检查微信进程状态时出错: {ex.Message}，继续尝试发送命令");
                }
            }
            else
            {
                // 如果没有保存进程ID，尝试通过进程名查找
                Process? weChatProcess = FindWeChatProcess();
                if (weChatProcess == null)
                {
                    Logger.LogWarning("未找到运行中的微信进程，无法发送命令");
                    _isHooked = false;
                    _clientId = 0;
                    OnUnhooked?.Invoke(this, EventArgs.Empty);
                    return false;
                }
                // 更新进程ID
                _weChatProcessId = weChatProcess.Id;
            }

            try
            {
                var command = new
                {
                    type = commandType,
                    data = data
                };

                string jsonCommand = Newtonsoft.Json.JsonConvert.SerializeObject(command);
                Logger.LogInfo($"准备发送命令: 类型={commandType}, clientId={_clientId}, 微信进程ID={_weChatProcessId}, 命令内容={jsonCommand}");
                
                bool result = _dllWrapper.SendStringData(_clientId, jsonCommand);

                if (result)
                {
                    Logger.LogInfo($"发送命令成功，类型: {commandType}");
                }
                else
                {
                    Logger.LogError($"发送命令失败，类型: {commandType}, clientId: {_clientId}, Hook状态: {_isHooked}, 微信进程ID: {_weChatProcessId}");
                    Logger.LogError($"可能原因: 1. DLL的SendData方法返回false 2. 微信进程未响应 3. 命令格式不正确 4. 微信进程可能已退出");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"发送命令异常: {ex.Message}", ex);
                Logger.LogError($"异常类型: {ex.GetType().Name}, 堆栈跟踪: {ex.StackTrace}");
                return false;
            }
        }

        #region 回调函数

        /// <summary>
        /// 接受回调
        /// </summary>
        private void OnAcceptCallback(int clientId)
        {
            // 注意：连接成功的日志已在OnHooked事件处理中输出，这里不再重复输出
            _clientId = clientId;
            _isHooked = true;
            OnHooked?.Invoke(this, clientId);
        }

        /// <summary>
        /// 接收回调
        /// </summary>
        private void OnReceiveCallback(int clientId, IntPtr message, int length)
        {
            try
            {
                // 减少回调日志输出，避免日志过多（只在调试时输出）
                // Logger.LogInfo($"OnReceiveCallback: clientId={clientId}, length={length}");
                
                if (message == IntPtr.Zero || length <= 0)
                {
                    Logger.LogWarning($"OnReceiveCallback: message为空或length无效: message={message}, length={length}");
                    return;
                }

                byte[] buffer = new byte[length];
                Marshal.Copy(message, buffer, 0, length);
                string jsonMessage = Encoding.UTF8.GetString(buffer);

                // 注意：登录回调消息的日志已在WeChatConnectionManager中输出，这里不再重复输出
                
                if (OnMessageReceived != null)
                {
                    OnMessageReceived.Invoke(this, jsonMessage);
                }
                else
                {
                    Logger.LogWarning("OnMessageReceived事件没有订阅者！");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理接收回调失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 关闭回调
        /// </summary>
        private void OnCloseCallback(int clientId)
        {
            Logger.LogInfo($"微信连接已关闭，ClientId: {clientId}");
            _isHooked = false;
            _clientId = 0;
            _weChatProcessId = 0;
            OnUnhooked?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取DLL的架构（32位或64位）
        /// </summary>
        private string? GetDllArchitecture(string dllPath)
        {
            try
            {
                if (!File.Exists(dllPath))
                {
                    return null;
                }

                // 使用PE文件头读取架构信息
                using (FileStream fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        // 读取DOS头
                        fs.Seek(0, SeekOrigin.Begin);
                        ushort dosSignature = br.ReadUInt16();
                        if (dosSignature != 0x5A4D) // "MZ"
                        {
                            return null;
                        }

                        // 读取PE头偏移
                        fs.Seek(0x3C, SeekOrigin.Begin);
                        int peHeaderOffset = br.ReadInt32();

                        // 读取PE签名
                        fs.Seek(peHeaderOffset, SeekOrigin.Begin);
                        uint peSignature = br.ReadUInt32();
                        if (peSignature != 0x00004550) // "PE\0\0"
                        {
                            return null;
                        }

                        // 读取机器类型
                        ushort machineType = br.ReadUInt16();
                        
                        // 0x014C = IMAGE_FILE_MACHINE_I386 (32位)
                        // 0x8664 = IMAGE_FILE_MACHINE_AMD64 (64位)
                        if (machineType == 0x014C)
                        {
                            return "32位 (x86)";
                        }
                        else if (machineType == 0x8664)
                        {
                            return "64位 (x64)";
                        }
                        else
                        {
                            return $"未知架构 (0x{machineType:X4})";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"检查DLL架构失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 查找WxHelp.dll文件
        /// 如果当前版本目录没有，尝试从其他版本目录查找
        /// </summary>
        private string? FindWxHelpDll(string currentDllDirectory)
        {
            try
            {
                // 方法1: 在当前版本目录查找
                string dllPath = Path.Combine(currentDllDirectory, "WxHelp.dll");
                if (File.Exists(dllPath))
                {
                    return dllPath;
                }

                // 方法2: 在DLLs根目录下查找所有版本的WxHelp.dll
                string dllsBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DLLs");
                if (Directory.Exists(dllsBasePath))
                {
                    string[] subDirs = Directory.GetDirectories(dllsBasePath);
                    
                    // 优先查找相同主版本的目录
                    if (!string.IsNullOrEmpty(_weChatVersion))
                    {
                        string[] versionParts = _weChatVersion.Split('.');
                        if (versionParts.Length >= 3)
                        {
                            string majorVersion = $"{versionParts[0]}.{versionParts[1]}.{versionParts[2]}";
                            
                            foreach (string dir in subDirs)
                            {
                                string dirName = Path.GetFileName(dir);
                                // 优先选择相同主版本的目录
                                if (dirName.StartsWith(majorVersion))
                                {
                                    string testPath = Path.Combine(dir, "WxHelp.dll");
                                    if (File.Exists(testPath))
                                    {
                                        Logger.LogInfo($"从目录 {dirName} 找到WxHelp.dll");
                                        return testPath;
                                    }
                                }
                            }
                        }
                    }
                    
                    // 如果相同主版本没有，尝试其他任何目录
                    foreach (string dir in subDirs)
                    {
                        string testPath = Path.Combine(dir, "WxHelp.dll");
                        if (File.Exists(testPath))
                        {
                            Logger.LogInfo($"从目录 {Path.GetFileName(dir)} 找到WxHelp.dll");
                            return testPath;
                        }
                    }
                }

                // 方法3: 在程序目录下直接查找
                string appBasePath = AppDomain.CurrentDomain.BaseDirectory;
                string directPath = Path.Combine(appBasePath, "WxHelp.dll");
                if (File.Exists(directPath))
                {
                    return directPath;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"查找WxHelp.dll失败: {ex.Message}", ex);
            }

            return null;
        }

        /// <summary>
        /// 查找微信可执行文件
        /// </summary>
        private string? FindWeChatExecutable()
        {
            try
            {
                // 根据微信版本确定进程名和可执行文件名
                // 新版本（4.0.1.21, 4.0.3.22, 4.1.0.34）使用 Weixin，旧版本使用 WeChat
                bool isNewVersion = _weChatVersion == "4.0.1.21" || _weChatVersion == "4.0.3.22" || _weChatVersion == "4.1.0.34";
                string processName = isNewVersion ? "Weixin" : "WeChat";
                string exeName = isNewVersion ? "Weixin.exe" : "WeChat.exe";
                string registryPath = isNewVersion ? @"SOFTWARE\Tencent\Weixin" : @"SOFTWARE\Tencent\WeChat";

                // 方法1: 从注册表获取安装路径（优先使用 CurrentUser）
                string? installPath = null;
                using (Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        object? path = key.GetValue("InstallPath");
                        if (path != null)
                        {
                            installPath = path.ToString();
                        }
                    }
                }

                // 方法2: 如果 CurrentUser 没有，尝试 LocalMachine
                if (string.IsNullOrEmpty(installPath))
                {
                    using (Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryPath))
                    {
                        if (key != null)
                        {
                            object? path = key.GetValue("InstallPath");
                            if (path != null)
                            {
                                installPath = path.ToString();
                            }
                        }
                    }
                }

                // 方法3: 如果 LocalMachine 也没有，尝试 WOW6432Node
                if (string.IsNullOrEmpty(installPath))
                {
                    try
                    {
                        string[] pathParts = registryPath.Split('\\');
                        if (pathParts.Length > 0)
                        {
                            string lastPart = pathParts[pathParts.Length - 1];
                            string wow6432Path = $@"SOFTWARE\WOW6432Node\Tencent\{lastPart}";
                            using (Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(wow6432Path))
                            {
                                if (key != null)
                                {
                                    object? path = key.GetValue("InstallPath");
                                    if (path != null)
                                    {
                                        installPath = path.ToString();
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"从WOW6432Node注册表获取安装路径失败: {ex.Message}");
                    }
                }

                // 如果找到了安装路径，尝试在版本子目录下查找可执行文件
                if (!string.IsNullOrEmpty(installPath))
                {
                    // 新版本的可执行文件在版本子目录下
                    if (isNewVersion && !string.IsNullOrEmpty(_weChatVersion))
                    {
                        string versionExePath = Path.Combine(installPath, _weChatVersion, exeName);
                        if (File.Exists(versionExePath))
                        {
                            Logger.LogInfo($"找到微信可执行文件（版本目录）: {versionExePath}");
                            return versionExePath;
                        }
                    }

                    // 旧版本可能在根目录下
                    string rootExePath = Path.Combine(installPath, exeName);
                    if (File.Exists(rootExePath))
                    {
                        // 注意：找到文件的日志已在调用处输出，这里不再重复输出
                        return rootExePath;
                    }
                }

                // 方法4: 默认路径
                string[] defaultPaths = isNewVersion ? new[]
                {
                    Path.Combine(@"C:\Program Files\Tencent\Weixin", _weChatVersion ?? "", exeName),
                    Path.Combine(@"C:\Program Files (x86)\Tencent\Weixin", _weChatVersion ?? "", exeName),
                    @"C:\Program Files\Tencent\Weixin\Weixin.exe",
                    @"C:\Program Files (x86)\Tencent\Weixin\Weixin.exe"
                } : new[]
                {
                    @"C:\Program Files\Tencent\WeChat\WeChat.exe",
                    @"C:\Program Files (x86)\Tencent\WeChat\WeChat.exe"
                };

                foreach (string path in defaultPaths)
                {
                    if (File.Exists(path))
                    {
                        Logger.LogInfo($"找到微信可执行文件（默认路径）: {path}");
                        return path;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"查找微信可执行文件失败: {ex.Message}", ex);
            }

            Logger.LogWarning("未找到微信可执行文件");
            return null;
        }

        /// <summary>
        /// 查找微信进程
        /// </summary>
        private Process? FindWeChatProcess()
        {
            try
            {
                // 根据微信版本确定进程名
                // 新版本（4.0.1.21, 4.0.3.22, 4.1.0.34）使用 Weixin，旧版本使用 WeChat
                bool isNewVersion = _weChatVersion == "4.0.1.21" || _weChatVersion == "4.0.3.22" || _weChatVersion == "4.1.0.34";
                string processName = isNewVersion ? "Weixin" : "WeChat";
                
                // 先尝试根据版本查找
                Process[] processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    return processes[0];
                }
                
                // 如果根据版本找不到，尝试查找另一个进程名（兼容性处理）
                string alternativeProcessName = isNewVersion ? "WeChat" : "Weixin";
                processes = Process.GetProcessesByName(alternativeProcessName);
                if (processes.Length > 0)
                {
                    Logger.LogInfo($"根据版本未找到 {processName} 进程，但找到了 {alternativeProcessName} 进程，使用它");
                    return processes[0];
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"查找微信进程失败: {ex.Message}", ex);
            }

            return null;
        }

        /// <summary>
        /// 检查微信主线程是否已完全初始化
        /// </summary>
        /// <param name="process">微信进程</param>
        /// <returns>是否已完全初始化</returns>
        private bool CheckWeChatMainThreadReady(Process process)
        {
            try
            {
                int currentStep = 7;
                
                // 步骤7：检查进程状态
                OnProgressUpdate?.Invoke(this, (currentStep, 15, "检查进程状态..."));
                process.Refresh();
                if (process.HasExited)
                {
                    Logger.LogWarning("微信进程已退出");
                    return false;
                }
                Thread.Sleep(200);
                
                // 步骤8：检查进程运行时间
                currentStep = 8;
                OnProgressUpdate?.Invoke(this, (currentStep, 15, "检查进程运行时间..."));
                DateTime startTime = process.StartTime;
                TimeSpan runningTime = DateTime.Now - startTime;
                Logger.LogInfo($"微信进程已运行 {runningTime.TotalSeconds:F1} 秒");
                
                if (runningTime.TotalSeconds < 5)
                {
                    Logger.LogInfo($"进程运行时间较短（{runningTime.TotalSeconds:F1}秒），等待至5秒...");
                    int waitTime = (int)((5 - runningTime.TotalSeconds) * 1000);
                    Thread.Sleep(Math.Max(0, waitTime));
                }
                Thread.Sleep(200);
                
                // 步骤9：检查进程模块加载（可选，如果微信已运行则跳过）
                currentStep = 9;
                OnProgressUpdate?.Invoke(this, (currentStep, 15, "检查进程模块加载..."));
                Logger.LogInfo("========== 步骤9：开始检查进程模块加载 ==========");
                
                // 由于访问 process.Modules 可能导致程序崩溃（特别是架构不匹配时），
                // 且微信已经运行，模块检查不是必需的，直接跳过以避免程序崩溃
                try
                {
                    bool isApp64Bit = IntPtr.Size == 8;
                    Logger.LogInfo($"当前应用程序架构: {(isApp64Bit ? "64位" : "32位")}");
                    Logger.LogInfo("微信已运行，跳过模块检查以避免潜在的访问违规");
                    Logger.LogInfo("========== 步骤9：跳过模块检查（微信已运行） ==========");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"检查进程架构时出错: {ex.Message}，跳过模块检查");
                    Logger.LogInfo("========== 步骤9：跳过模块检查（架构检查失败） ==========");
                }
                
                Logger.LogInfo("步骤9完成后等待200ms...");
                Thread.Sleep(200);
                Logger.LogInfo("步骤9等待完成");
                
                // 步骤10：检查进程线程数
                currentStep = 10;
                OnProgressUpdate?.Invoke(this, (currentStep, 15, "检查进程线程数..."));
                Logger.LogInfo("========== 步骤10：开始检查进程线程数 ==========");
                try
                {
                    Logger.LogInfo("刷新进程信息...");
                    process.Refresh();
                    Logger.LogInfo("访问进程线程集合...");
                    int threadCount = process.Threads.Count;
                    Logger.LogInfo($"微信进程有 {threadCount} 个线程");
                    
                    if (threadCount < 5)
                    {
                        Logger.LogWarning($"线程数较少（{threadCount}），可能未完全初始化");
                        Logger.LogInfo("等待2秒...");
                        Thread.Sleep(2000);
                        Logger.LogInfo("等待完成");
                    }
                    Logger.LogInfo("========== 步骤10：进程线程数检查完成 ==========");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"检查进程线程数时出错: {ex.Message}");
                    Logger.LogError($"异常类型: {ex.GetType().Name}");
                    Logger.LogError($"堆栈跟踪: {ex.StackTrace}");
                    Logger.LogInfo("========== 步骤10：线程数检查失败，继续执行 ==========");
                }
                Logger.LogInfo("步骤10完成后等待200ms...");
                Thread.Sleep(200);
                Logger.LogInfo("步骤10等待完成");
                
                // 步骤11：检查窗口响应性
                currentStep = 11;
                OnProgressUpdate?.Invoke(this, (currentStep, 15, "检查窗口响应性..."));
                Logger.LogInfo("========== 步骤11：开始检查窗口响应性 ==========");
                try
                {
                    Logger.LogInfo("获取主窗口句柄...");
                    IntPtr mainWindowHandle = process.MainWindowHandle;
                    Logger.LogInfo($"主窗口句柄: {mainWindowHandle}");
                    
                    if (mainWindowHandle != IntPtr.Zero)
                    {
                        Logger.LogInfo("使用 SendMessage 测试窗口响应性...");
                        // 使用 SendMessage 测试窗口响应性
                        bool isResponding = IsWindowResponding(mainWindowHandle);
                        Logger.LogInfo($"首次响应性检查结果: {(isResponding ? "正常" : "异常")}");
                        
                        if (!isResponding)
                        {
                            Logger.LogWarning("窗口未响应，等待2秒后重试...");
                            Thread.Sleep(2000);
                            Logger.LogInfo("重试检查窗口响应性...");
                            isResponding = IsWindowResponding(mainWindowHandle);
                            Logger.LogInfo($"重试后响应性检查结果: {(isResponding ? "正常" : "异常")}");
                        }
                        Logger.LogInfo($"窗口响应性: {(isResponding ? "正常" : "异常")}");
                    }
                    else
                    {
                        Logger.LogWarning("主窗口句柄为空，无法检查响应性");
                    }
                    Logger.LogInfo("========== 步骤11：窗口响应性检查完成 ==========");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"检查窗口响应性时出错: {ex.Message}");
                    Logger.LogError($"异常类型: {ex.GetType().Name}");
                    Logger.LogError($"堆栈跟踪: {ex.StackTrace}");
                    Logger.LogInfo("========== 步骤11：窗口响应性检查失败，继续执行 ==========");
                }
                Logger.LogInfo("步骤11完成后等待200ms...");
                Thread.Sleep(200);
                Logger.LogInfo("步骤11等待完成");
                
                // 步骤12：最终检查
                currentStep = 12;
                OnProgressUpdate?.Invoke(this, (currentStep, 15, "完成初始化检查..."));
                Logger.LogInfo("========== 步骤12：开始最终检查 ==========");
                try
                {
                    Logger.LogInfo("刷新进程信息进行最终检查...");
                    process.Refresh();
                    Logger.LogInfo("检查进程是否已退出...");
                    
                    if (process.HasExited)
                    {
                        Logger.LogError("微信进程在检查过程中已退出");
                        Logger.LogInfo("========== 步骤12：检查失败（进程已退出） ==========");
                        return false;
                    }
                    
                    Logger.LogInfo("进程仍在运行");
                    Logger.LogInfo("========== 步骤12：最终检查完成 ==========");
                    Logger.LogInfo("========== 微信主线程初始化检查完成 ==========");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"最终检查时出错: {ex.Message}");
                    Logger.LogError($"异常类型: {ex.GetType().Name}");
                    Logger.LogError($"堆栈跟踪: {ex.StackTrace}");
                    Logger.LogInfo("========== 步骤12：最终检查失败 ==========");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"检查微信主线程初始化状态时出错: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 检查窗口是否响应
        /// </summary>
        private bool IsWindowResponding(IntPtr hWnd)
        {
            try
            {
                IntPtr result;
                IntPtr ret = SendMessageTimeout(hWnd, WM_NULL, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 2000, out result);
                return ret != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 生成随机DLL文件名（12位随机字符串+.dll）
        /// </summary>
        private string GenerateRandomDllName()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            Random random = new Random();
            char[] name = new char[12];
            for (int i = 0; i < 12; i++)
            {
                name[i] = chars[random.Next(chars.Length)];
            }
            return new string(name) + ".dll";
        }

        #endregion
    }
}
