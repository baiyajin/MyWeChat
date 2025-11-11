using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MyWeChat.Windows.Services.WebSocket;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Services
{
    /// <summary>
    /// 窗口关闭处理器
    /// 封装窗口关闭时的资源清理逻辑
    /// </summary>
    public class WindowCloseHandler
    {
        private readonly Window _window;
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

            /// <summary>
            /// 更新进度显示的回调（可选，如果为null则不显示进度）
            /// </summary>
            public Action<int, string>? UpdateProgressCallback { get; set; }

            /// <summary>
            /// 显示/隐藏进度遮罩的回调（可选）
            /// </summary>
            public Action<bool>? ShowProgressOverlayCallback { get; set; }
        }

        private readonly CleanupConfig _config;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="window">要处理的窗口</param>
        /// <param name="config">资源清理配置</param>
        public WindowCloseHandler(Window window, CleanupConfig config)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _dispatcher = window.Dispatcher;
            _config = config ?? throw new ArgumentNullException(nameof(config));
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
            _isClosing = true;

            // 显示进度遮罩（如果提供了回调）
            if (_config.ShowProgressOverlayCallback != null)
            {
                _dispatcher.Invoke(() =>
                {
                    _config.ShowProgressOverlayCallback(true);
                });
            }

            // 更新初始进度
            UpdateProgress(0, "准备关闭...");

            // 异步执行资源清理
            Task.Run(async () =>
            {
                try
                {
                    Logger.LogInfo("========== 开始清理资源 ==========");

                    // 0. 停止所有定时器
                    UpdateProgress(5, "正在停止所有定时器...");
                    Logger.LogInfo("正在停止所有定时器...");
                    try
                    {
                        if (_config.StopAllTimersCallback != null)
                        {
                            _dispatcher.Invoke(() => _config.StopAllTimersCallback());
                        }
                        Logger.LogInfo("所有定时器已停止");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"停止定时器时出错: {ex.Message}");
                    }

                    // 1. 取消事件订阅（防止内存泄漏）
                    UpdateProgress(15, "正在取消事件订阅...");
                    Logger.LogInfo("正在取消事件订阅...");
                    try
                    {
                        if (_config.UnsubscribeEventsCallback != null)
                        {
                            _dispatcher.Invoke(() => _config.UnsubscribeEventsCallback());
                        }
                        Logger.LogInfo("事件订阅已取消");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"取消事件订阅时出错: {ex.Message}");
                    }

                    // 2. 断开WebSocket连接
                    UpdateProgress(30, "正在断开WebSocket连接...");
                    if (_config.WebSocketService != null)
                    {
                        Logger.LogInfo("正在断开WebSocket连接...");
                        try
                        {
                            await _config.WebSocketService.DisconnectAsync().ConfigureAwait(false);
                            Logger.LogInfo("WebSocket连接已断开");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"断开WebSocket连接时出错: {ex.Message}");
                        }
                    }

                    // 3. 关闭Hook连接（撤回DLL注入）
                    UpdateProgress(50, "正在关闭Hook连接（撤回DLL注入）...");
                    if (_config.WeChatManager != null)
                    {
                        Logger.LogInfo("正在关闭Hook连接（撤回DLL注入）...");
                        try
                        {
                            _dispatcher.Invoke(() => _config.WeChatManager.Disconnect());
                            Logger.LogInfo("Hook连接已关闭");

                            // 等待DLL注入完全清理（给系统时间释放文件句柄）
                            UpdateProgress(60, "等待DLL注入资源释放（2秒）...");
                            Logger.LogInfo("等待DLL注入资源释放（2秒）...");
                            await Task.Delay(2000).ConfigureAwait(false);
                            Logger.LogInfo("DLL注入资源已释放");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"关闭Hook连接时出错: {ex.Message}", ex);
                        }
                    }

                    // 4. 清理同步服务（释放服务资源）
                    UpdateProgress(75, "正在清理同步服务...");
                    Logger.LogInfo("正在清理同步服务...");
                    try
                    {
                        if (_config.CleanupSyncServicesCallback != null)
                        {
                            _dispatcher.Invoke(() => _config.CleanupSyncServicesCallback());
                        }
                        Logger.LogInfo("同步服务已清理");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"清理同步服务时出错: {ex.Message}");
                    }

                    // 5. 清空账号列表（释放集合资源）
                    UpdateProgress(85, "正在清空账号列表...");
                    Logger.LogInfo("正在清空账号列表...");
                    try
                    {
                        if (_config.ClearAccountListCallback != null)
                        {
                            _dispatcher.Invoke(() => _config.ClearAccountListCallback());
                        }
                        Logger.LogInfo("账号列表已清空");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"清空账号列表时出错: {ex.Message}");
                    }

                    // 6. 等待后台任务完成（给正在运行的任务时间完成）
                    UpdateProgress(90, "等待后台任务完成（1秒）...");
                    Logger.LogInfo("等待后台任务完成（1秒）...");
                    try
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                        Logger.LogInfo("后台任务等待完成");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"等待后台任务时出错: {ex.Message}");
                    }

                    UpdateProgress(100, "资源清理完成，正在关闭窗口...");
                    Logger.LogInfo("========== 资源清理完成 ==========");

                    // 等待一小段时间让用户看到完成状态
                    await Task.Delay(300).ConfigureAwait(false);

                    // 关闭窗口
                    _dispatcher.Invoke(() =>
                    {
                        if (_config.ShowProgressOverlayCallback != null)
                        {
                            _config.ShowProgressOverlayCallback(false);
                        }
                        _window.Close();
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError($"关闭窗口时出错: {ex.Message}", ex);
                    UpdateProgress(100, $"关闭时出错: {ex.Message}");

                    // 即使出错也关闭窗口
                    await Task.Delay(1000).ConfigureAwait(false);
                    _dispatcher.Invoke(() =>
                    {
                        if (_config.ShowProgressOverlayCallback != null)
                        {
                            _config.ShowProgressOverlayCallback(false);
                        }
                        _window.Close();
                    });
                }
            });
        }

        /// <summary>
        /// 更新进度显示
        /// </summary>
        private void UpdateProgress(int progress, string status)
        {
            if (_config.UpdateProgressCallback != null)
            {
                _dispatcher.Invoke(() =>
                {
                    _config.UpdateProgressCallback(progress, status);
                });
            }
        }
    }
}

