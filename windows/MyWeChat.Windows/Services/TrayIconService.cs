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

            Icon appIcon = GetApplicationIcon();
            _notifyIcon = new NotifyIcon
            {
                Icon = appIcon,
                Text = "微信",
                Visible = true
            };
            
            // 确保图标立即显示
            _notifyIcon.Visible = true;
            Logger.LogInfo($"托盘图标已初始化，图标大小: {appIcon.Size}");

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
            }
        }

        /// <summary>
        /// 获取应用程序图标（使用默认系统图标，反检测措施）
        /// </summary>
        private Icon GetApplicationIcon()
        {
            // 直接使用系统默认图标（反检测措施，不显示自定义图标）
            return SystemIcons.Application;
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

