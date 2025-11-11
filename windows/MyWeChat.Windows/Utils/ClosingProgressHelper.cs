using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Utils
{
    /// <summary>
    /// 关闭进度遮罩辅助类
    /// 统一处理窗口关闭时的进度显示逻辑
    /// </summary>
    public class ClosingProgressHelper
    {
        private readonly Canvas _overlayCanvas;
        private readonly Border? _overlayBorder;
        private readonly ArcSegment? _progressArc;
        private readonly TextBlock? _progressText;
        private readonly TextBlock? _statusText;
        private readonly string _windowName;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="overlayCanvas">遮罩Canvas</param>
        /// <param name="overlayBorder">遮罩边框（可选，用于居中定位）</param>
        /// <param name="progressArc">进度圆环ArcSegment（可选）</param>
        /// <param name="progressText">进度文本TextBlock（可选）</param>
        /// <param name="statusText">状态文本TextBlock（可选）</param>
        /// <param name="windowName">窗口名称（用于日志）</param>
        public ClosingProgressHelper(
            Canvas overlayCanvas,
            Border? overlayBorder = null,
            ArcSegment? progressArc = null,
            TextBlock? progressText = null,
            TextBlock? statusText = null,
            string windowName = "窗口")
        {
            _overlayCanvas = overlayCanvas ?? throw new ArgumentNullException(nameof(overlayCanvas));
            _overlayBorder = overlayBorder;
            _progressArc = progressArc;
            _progressText = progressText;
            _statusText = statusText;
            _windowName = windowName;
        }

        /// <summary>
        /// 显示/隐藏进度遮罩
        /// </summary>
        public void ShowProgressOverlay(bool show)
        {
            if (show)
            {
                // 确保Canvas可见并启用
                _overlayCanvas.Visibility = Visibility.Visible;
                _overlayCanvas.IsHitTestVisible = true;
                _overlayCanvas.IsEnabled = true;

                // 更新进度显示
                UpdateClosingProgressRing(0);
                if (_statusText != null)
                {
                    _statusText.Text = "准备关闭...";
                }
                if (_progressText != null)
                {
                    _progressText.Text = "0%";
                }

                // 居中显示遮罩内容
                if (_overlayBorder != null)
                {
                    // 强制更新布局
                    _overlayCanvas.UpdateLayout();
                    double canvasWidth = _overlayCanvas.ActualWidth;
                    double canvasHeight = _overlayCanvas.ActualHeight;
                    double borderWidth = 400;
                    double borderHeight = 280;

                    Canvas.SetLeft(_overlayBorder, (canvasWidth - borderWidth) / 2);
                    Canvas.SetTop(_overlayBorder, (canvasHeight - borderHeight) / 2);
                }

                Logger.LogInfo($"关闭进度遮罩已显示（{_windowName}）");
            }
            else
            {
                _overlayCanvas.Visibility = Visibility.Collapsed;
                _overlayCanvas.IsHitTestVisible = false;
                _overlayCanvas.IsEnabled = false;
                Logger.LogInfo($"关闭进度遮罩已隐藏（{_windowName}）");
            }
        }

        /// <summary>
        /// 更新关闭进度
        /// </summary>
        /// <param name="progress">进度值（0-100）</param>
        /// <param name="status">状态文本</param>
        public void UpdateClosingProgress(int progress, string status)
        {
            UpdateClosingProgressRing(progress);
            if (_statusText != null)
            {
                _statusText.Text = status;
            }
            if (_progressText != null)
            {
                _progressText.Text = $"{progress}%";
            }
        }

        /// <summary>
        /// 更新关闭进度圆环
        /// </summary>
        /// <param name="progress">进度值（0-100）</param>
        public void UpdateClosingProgressRing(int progress)
        {
            try
            {
                if (_progressArc == null) return;

                // 确保进度在0-100范围内
                progress = Math.Max(0, Math.Min(100, progress));

                // 计算角度（0度在顶部，顺时针）
                double angle = (progress / 100.0) * 360.0;
                double angleRad = (angle - 90) * Math.PI / 180.0; // 转换为弧度，-90度使起点在顶部

                // 圆环中心 (60, 60)，半径 50
                double centerX = 60;
                double centerY = 60;
                double radius = 50;

                // 计算终点坐标
                double endX = centerX + radius * Math.Cos(angleRad);
                double endY = centerY + radius * Math.Sin(angleRad);

                // 判断是否需要大弧（超过180度）
                bool isLargeArc = progress > 50;

                // 更新ArcSegment
                _progressArc.Point = new System.Windows.Point(endX, endY);
                _progressArc.IsLargeArc = isLargeArc;
                _progressArc.Size = new System.Windows.Size(radius, radius);
            }
            catch (Exception ex)
            {
                Logger.LogError($"更新关闭进度圆环失败（{_windowName}）: {ex.Message}", ex);
            }
        }
    }
}

