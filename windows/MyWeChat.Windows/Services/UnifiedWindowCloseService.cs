using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MyWeChat.Windows.Core.Connection;
using MyWeChat.Windows.Services.WebSocket;
using MyWeChat.Windows.UI.Controls;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Services
{
    /// <summary>
    /// 统一窗口关闭服务
    /// 封装所有窗口的关闭逻辑，提供统一的关闭体验
    /// </summary>
    public class UnifiedWindowCloseService
    {
        private readonly Window _window;
        private readonly WindowCloseOverlay _overlay;
        private readonly CleanupConfig _cleanupConfig;
        private readonly Dispatcher _dispatcher;
        private bool _isClosing = false;

        /// <summary>
        /// 资源清理配置
        /// </summary>
        public class CleanupConfig
        {
            /// <summary>
            /// 微信管理器
            /// </summary>
            public WeChatManager? WeChatManager { get; set; }

            /// <summary>
            /// WebSocket服务
            /// </summary>
            public WebSocketService? WebSocketService { get; set; }

            /// <summary>
            /// 停止所有定时器的回调
            /// </summary>
            public Action? StopAllTimersCallback { get; set; }

            /// <summary>
            /// 取消事件订阅的回调
            /// </summary>
            public Action? UnsubscribeEventsCallback { get; set; }

            /// <summary>
            /// 清理同步服务的回调
            /// </summary>
            public Action? CleanupSyncServicesCallback { get; set; }

            /// <summary>
            /// 清空账号列表的回调
            /// </summary>
            public Action? ClearAccountListCallback { get; set; }
        }

        /// <summary>
        /// 最小化到托盘的回调（可选）
        /// </summary>
        public Action? MinimizeToTrayCallback { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="window">要处理的窗口</param>
        /// <param name="overlay">关闭遮罩控件</param>
        /// <param name="cleanupConfig">资源清理配置</param>
        public UnifiedWindowCloseService(Window window, WindowCloseOverlay overlay, CleanupConfig cleanupConfig)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _cleanupConfig = cleanupConfig ?? throw new ArgumentNullException(nameof(cleanupConfig));
            _dispatcher = window.Dispatcher;

            // 订阅遮罩控件的事件
            _overlay.ActionSelected += OnOverlayActionSelected;
        }

        /// <summary>
        /// 处理遮罩控件的操作选择
        /// </summary>
        private void OnOverlayActionSelected(object? sender, WindowCloseOverlay.CloseAction action)
        {
            switch (action)
            {
                case WindowCloseOverlay.CloseAction.MinimizeToTray:
                    HandleMinimizeToTray();
                    break;
                case WindowCloseOverlay.CloseAction.Close:
                    HandleClose();
                    break;
                case WindowCloseOverlay.CloseAction.Cancel:
                    HandleCancel();
                    break;
            }
        }

        /// <summary>
        /// 处理窗口关闭事件
        /// </summary>
        /// <param name="e">取消事件参数</param>
        public void HandleClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 如果已经在关闭中，直接允许关闭
            if (_isClosing)
            {
                return;
            }

            // 阻止窗口立即关闭
            e.Cancel = true;

            // 显示关闭确认弹窗
            _overlay.ShowConfirmDialog();
        }

        /// <summary>
        /// 处理最小化到托盘
        /// </summary>
        private void HandleMinimizeToTray()
        {
            // 在UI线程上执行
            _dispatcher.InvokeAsync(() =>
            {
                // 隐藏遮罩
                _overlay.HideOverlay();

                // 执行最小化回调
                if (MinimizeToTrayCallback != null)
                {
                    MinimizeToTrayCallback();
                }
                else
                {
                    // 如果没有提供回调，直接最小化并隐藏
                    _window.WindowState = WindowState.Minimized;
                    _window.Hide();
                }
            }, DispatcherPriority.Normal);
        }

        /// <summary>
        /// 处理关闭程序
        /// </summary>
        private void HandleClose()
        {
            _isClosing = true;

            // 在UI线程上切换遮罩内容为进度圆环（使用Invoke确保同步执行）
            _dispatcher.Invoke(() =>
            {
                try
                {
                    // 确保overlay控件已经加载
                    if (_overlay != null)
                    {
                        _overlay.ShowProgress();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"显示进度圆环失败: {ex.Message}", ex);
                }
            }, DispatcherPriority.Render); // 使用Render优先级，确保立即渲染

            // 异步执行资源清理
            Task.Run(async () =>
            {
                try
                {
                    Logger.LogInfo("========== 开始清理资源 ==========");

                    // 0. 停止所有定时器
                    UpdateProgress(5, "正在停止所有定时器...");
                    try
                    {
                        if (_cleanupConfig.StopAllTimersCallback != null)
                        {
                            await _dispatcher.InvokeAsync(() => _cleanupConfig.StopAllTimersCallback());
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"停止定时器时出错: {ex.Message}");
                    }

                    // 1. 取消事件订阅（防止内存泄漏）
                    UpdateProgress(15, "正在取消事件订阅...");
                    try
                    {
                        if (_cleanupConfig.UnsubscribeEventsCallback != null)
                        {
                            await _dispatcher.InvokeAsync(() => _cleanupConfig.UnsubscribeEventsCallback());
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"取消事件订阅时出错: {ex.Message}");
                    }

                    // 2. 断开WebSocket连接
                    UpdateProgress(30, "正在断开WebSocket连接...");
                    if (_cleanupConfig.WebSocketService != null)
                    {
                        try
                        {
                            await _cleanupConfig.WebSocketService.DisconnectAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"断开WebSocket连接时出错: {ex.Message}");
                        }
                    }

                    // 3. 关闭微信连接
                    UpdateProgress(50, "正在关闭微信连接...");
                    if (_cleanupConfig.WeChatManager != null)
                    {
                        try
                        {
                            await _dispatcher.InvokeAsync(() => _cleanupConfig.WeChatManager.Disconnect());

                            // 等待资源释放（给系统时间释放文件句柄）
                            UpdateProgress(60, "等待资源释放（2秒）...");
                            await Task.Delay(2000).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"关闭微信连接时出错: {ex.Message}", ex);
                        }
                    }

                    // 4. 清理同步服务（释放服务资源）
                    UpdateProgress(75, "正在清理同步服务...");
                    try
                    {
                        if (_cleanupConfig.CleanupSyncServicesCallback != null)
                        {
                            await _dispatcher.InvokeAsync(() => _cleanupConfig.CleanupSyncServicesCallback());
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"清理同步服务时出错: {ex.Message}");
                    }

                    // 5. 清空账号列表（释放集合资源）
                    UpdateProgress(85, "正在清空账号列表...");
                    try
                    {
                        if (_cleanupConfig.ClearAccountListCallback != null)
                        {
                            await _dispatcher.InvokeAsync(() => _cleanupConfig.ClearAccountListCallback());
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"清空账号列表时出错: {ex.Message}");
                    }

                    // 6. 等待后台任务完成（给正在运行的任务时间完成）
                    UpdateProgress(90, "等待后台任务完成（1秒）...");
                    try
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"等待后台任务时出错: {ex.Message}");
                    }

                    UpdateProgress(100, "资源清理完成，正在关闭窗口...");
                    Logger.LogInfo("========== 资源清理完成 ==========");

                    // 等待一小段时间让用户看到完成状态
                    await Task.Delay(300).ConfigureAwait(false);

                    // 关闭窗口并确保应用完全退出
                    await _dispatcher.InvokeAsync(() =>
                    {
                        _overlay.HideOverlay();
                        _window.Close();
                        
                        // 确保应用完全退出（防止停留在后台进程）
                        System.Windows.Application.Current.Shutdown();
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError($"关闭窗口时出错: {ex.Message}", ex);
                    UpdateProgress(100, $"关闭时出错: {ex.Message}");

                    // 即使出错也关闭窗口并确保应用完全退出
                    await Task.Delay(1000).ConfigureAwait(false);
                    await _dispatcher.InvokeAsync(() =>
                    {
                        _overlay.HideOverlay();
                        _window.Close();
                        
                        // 确保应用完全退出（防止停留在后台进程）
                        System.Windows.Application.Current.Shutdown();
                    });
                }
            });
        }

        /// <summary>
        /// 处理取消操作
        /// </summary>
        private void HandleCancel()
        {
            // 隐藏遮罩
            _overlay.HideOverlay();
        }

        /// <summary>
        /// 更新进度显示
        /// </summary>
        private void UpdateProgress(int progress, string status)
        {
            _dispatcher.InvokeAsync(new Action(() =>
            {
                _overlay.UpdateProgress(progress, status);
            }), DispatcherPriority.Normal);
        }
    }
}

