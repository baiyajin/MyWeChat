using System;
using System.IO;
using System.Text;
using System.Windows;
using WinForms = System.Windows.Forms;
using Newtonsoft.Json;
using MyWeChat.Windows.Utils;
using MyWeChat.Windows.Services;

namespace MyWeChat.Windows
{
    /// <summary>
    /// 应用程序入口点
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // WPF应用自动感知DPI，无需手动设置
            // 高DPI设置已通过项目属性ApplicationHighDpiMode配置
            base.OnStartup(e);
            
            // 添加全局异常处理
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            
            // 检查登录状态
            string? wxid = LoadLoginState();
            
            if (string.IsNullOrEmpty(wxid))
            {
                // 未登录，显示登录窗口
                var loginWindow = new LoginWindow();
                loginWindow.Show();
            }
            else
            {
                // 已登录，显示主窗口
                var mainWindow = new MainWindow(wxid);
                mainWindow.Show();
            }
        }

        /// <summary>
        /// 处理未捕获的异常（非UI线程）
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception? ex = e.ExceptionObject as Exception;
                if (ex != null)
                {
                    Logger.LogError($"========== 未捕获的异常（非UI线程） ==========");
                    Logger.LogError($"异常类型: {ex.GetType().Name}");
                    Logger.LogError($"错误消息: {ex.Message}");
                    Logger.LogError($"堆栈跟踪: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Logger.LogError($"内部异常: {ex.InnerException.Message}");
                    }
                    Logger.LogError($"是否终止: {e.IsTerminating}");
                }
            }
            catch
            {
                // 如果日志记录也失败，至少尝试写入事件日志
            }
        }

        /// <summary>
        /// 处理未捕获的异常（UI线程）
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                Logger.LogError($"========== 未捕获的异常（UI线程） ==========");
                Logger.LogError($"异常类型: {e.Exception.GetType().Name}");
                Logger.LogError($"错误消息: {e.Exception.Message}");
                Logger.LogError($"堆栈跟踪: {e.Exception.StackTrace}");
                if (e.Exception.InnerException != null)
                {
                    Logger.LogError($"内部异常: {e.Exception.InnerException.Message}");
                }
                
                // 标记为已处理，防止程序崩溃
                e.Handled = true;
                
                // 显示错误消息
                MessageBox.Show(
                    $"发生未处理的异常:\n\n{e.Exception.Message}\n\n程序将继续运行，但可能不稳定。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            catch
            {
                // 如果错误处理也失败，至少标记为已处理
                e.Handled = true;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 应用退出时，释放全局微信初始化服务
            try
            {
                WeChatInitializationService.Instance.Dispose();
                Logger.LogInfo("全局微信初始化服务已释放");
            }
            catch (Exception ex)
            {
                Logger.LogError($"释放全局微信初始化服务失败: {ex.Message}", ex);
            }
            
            base.OnExit(e);
        }

        /// <summary>
        /// 加载登录状态
        /// </summary>
        private string? LoadLoginState()
        {
            try
            {
                string stateFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login_state.json");
                if (File.Exists(stateFilePath))
                {
                    string json = File.ReadAllText(stateFilePath, Encoding.UTF8);
                    var state = JsonConvert.DeserializeObject<dynamic>(json);
                    return state?.wxid?.ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"加载登录状态失败: {ex.Message}", ex);
            }
            return null;
        }
    }
}

