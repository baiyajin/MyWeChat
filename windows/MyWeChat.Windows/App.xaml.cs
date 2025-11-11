using System;
using System.IO;
using System.Text;
using System.Windows;
using WinForms = System.Windows.Forms;
using Newtonsoft.Json;
using MyWeChat.Windows.Utils;

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

