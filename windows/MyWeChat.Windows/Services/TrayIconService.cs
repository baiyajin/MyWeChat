using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Reflection;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Services
{
    /// <summary>
    /// 系统托盘图标服务
    /// </summary>
    public class TrayIconService : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private Window? _window;
        private bool _isDisposed = false;

        /// <summary>
        /// 初始化系统托盘
        /// </summary>
        /// <param name="window">要管理的窗口</param>
        public void Initialize(Window window)
        {
            if (_notifyIcon != null)
            {
                return; // 已经初始化
            }

            _window = window ?? throw new ArgumentNullException(nameof(window));

            Icon? appIcon = GetApplicationIcon();
            if (appIcon != null)
            {
                _notifyIcon = new NotifyIcon
                {
                    Icon = appIcon,
                    Text = "微信",
                    Visible = true
                };
                
                // 确保图标立即显示
                _notifyIcon.Visible = true;
                Logger.LogInfo($"托盘图标已初始化，图标大小: {appIcon.Size}");
            }
            else
            {
                Logger.LogWarning("无法加载托盘图标，使用默认图标");
                _notifyIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Application,
                    Text = "微信",
                    Visible = true
                };
                _notifyIcon.Visible = true;
            }

            // 创建上下文菜单
            var contextMenu = new ContextMenuStrip();
            
            var showMenuItem = new ToolStripMenuItem("显示窗口");
            showMenuItem.Click += (s, e) => ShowWindow();
            contextMenu.Items.Add(showMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitMenuItem = new ToolStripMenuItem("退出");
            exitMenuItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // 双击托盘图标显示窗口
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();

            // 窗口状态变化时更新托盘图标
            _window.StateChanged += (s, e) =>
            {
                if (_window.WindowState == WindowState.Minimized)
                {
                    _window.Hide();
                }
            };
        }

        /// <summary>
        /// 显示窗口
        /// </summary>
        public void ShowWindow()
        {
            if (_window == null) return;

            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
            _window.Focus();
        }

        /// <summary>
        /// 退出应用程序
        /// </summary>
        private void ExitApplication()
        {
            if (_window != null)
            {
                // 直接关闭窗口，触发正常的关闭流程
                _window.Close();
                
                // 确保应用完全退出（防止停留在后台进程）
                System.Windows.Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// 获取应用程序图标
        /// </summary>
        private Icon? GetApplicationIcon()
        {
            try
            {
                // 优先从 Resources 文件夹加载 logo.ico
                string logoIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.ico");
                if (File.Exists(logoIconPath))
                {
                    FileStream fs = new FileStream(logoIconPath, FileMode.Open, FileAccess.Read);
                    Icon icon = new Icon(fs);
                    fs.Close();
                    return icon;
                }
                
                // 尝试从程序集资源加载
                try
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    Stream? stream = assembly.GetManifestResourceStream("MyWeChat.Windows.Resources.logo.ico");
                    if (stream != null)
                    {
                        Icon icon = new Icon(stream);
                        stream.Close();
                        return icon;
                    }
                }
                catch
                {
                    // 如果资源加载失败，继续尝试其他方式
                }
                
                // 尝试加载 favicon.ico 作为后备
                string faviconIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "favicon.ico");
                if (File.Exists(faviconIconPath))
                {
                    FileStream fs = new FileStream(faviconIconPath, FileMode.Open, FileAccess.Read);
                    Icon icon = new Icon(fs);
                    fs.Close();
                    return icon;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"加载托盘图标失败: {ex.Message}", ex);
            }
            
            // 如果所有方式都失败，返回 null（调用者会使用默认图标）
            return null;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            _isDisposed = true;
        }
    }
}

