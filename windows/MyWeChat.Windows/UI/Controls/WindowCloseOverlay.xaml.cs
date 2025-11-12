using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.UI.Controls
{
    /// <summary>
    /// 窗口关闭遮罩控件
    /// 统一处理窗口关闭时的UI显示（关闭确认弹窗和关闭进度圆环）
    /// </summary>
    public partial class WindowCloseOverlay : System.Windows.Controls.UserControl
    {
        /// <summary>
        /// 关闭操作结果
        /// </summary>
        public enum CloseAction
        {
            MinimizeToTray,
            Close,
            Cancel
        }

        /// <summary>
        /// 用户选择的结果
        /// </summary>
        public CloseAction SelectedAction { get; private set; } = CloseAction.Cancel;

        /// <summary>
        /// 操作选择事件
        /// </summary>
        public event EventHandler<CloseAction>? ActionSelected;

        public WindowCloseOverlay()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 显示关闭确认弹窗
        /// </summary>
        public void ShowConfirmDialog()
        {
            SelectedAction = CloseAction.Cancel;
            ConfirmDialogBorder.Visibility = Visibility.Visible;
            ProgressBorder.Visibility = Visibility.Collapsed;
            OverlayCanvas.Visibility = Visibility.Visible;
            OverlayCanvas.IsHitTestVisible = true;
            OverlayCanvas.IsEnabled = true;
            
            // Grid会自动居中，不需要手动计算位置
            // 强制更新布局确保立即显示
            this.UpdateLayout();
            OverlayCanvas.UpdateLayout();
        }

        /// <summary>
        /// 显示关闭进度圆环
        /// </summary>
        public void ShowProgress()
        {
            try
            {
                ConfirmDialogBorder.Visibility = Visibility.Collapsed;
                ProgressBorder.Visibility = Visibility.Visible;
                OverlayCanvas.Visibility = Visibility.Visible;
                OverlayCanvas.IsHitTestVisible = false; // 进度显示时不可交互
                OverlayCanvas.IsEnabled = false;
                
                // Grid会自动居中，不需要手动计算位置
                // 强制更新布局确保立即显示
                this.UpdateLayout();
                OverlayCanvas.UpdateLayout();
                
                // 重置进度
                UpdateProgress(0, "准备关闭...");
                
                // 强制渲染
                this.InvalidateVisual();
                OverlayCanvas.InvalidateVisual();
            }
            catch (Exception ex)
            {
                Logger.LogError($"显示进度圆环失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 隐藏遮罩
        /// </summary>
        public void HideOverlay()
        {
            OverlayCanvas.Visibility = Visibility.Collapsed;
            OverlayCanvas.IsHitTestVisible = false;
            OverlayCanvas.IsEnabled = false;
        }

        /// <summary>
        /// 更新关闭进度
        /// </summary>
        /// <param name="progress">进度值（0-100）</param>
        /// <param name="status">状态文本</param>
        public void UpdateProgress(int progress, string status)
        {
            UpdateProgressRing(progress);
            if (StatusText != null)
            {
                StatusText.Text = status;
            }
            if (ProgressText != null)
            {
                ProgressText.Text = $"{progress}%";
            }
        }

        /// <summary>
        /// 更新关闭进度圆环
        /// </summary>
        /// <param name="progress">进度值（0-100）</param>
        private void UpdateProgressRing(int progress)
        {
            try
            {
                if (ProgressArc == null) return;

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
                ProgressArc.Point = new System.Windows.Point(endX, endY);
                ProgressArc.IsLargeArc = isLargeArc;
                ProgressArc.Size = new System.Windows.Size(radius, radius);
            }
            catch (Exception ex)
            {
                Logger.LogError($"更新关闭进度圆环失败: {ex.Message}", ex);
            }
        }

        // 注意：CenterBorder方法已移除，现在使用Grid自动居中

        /// <summary>
        /// 最小化到托盘按钮点击
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = CloseAction.MinimizeToTray;
            ActionSelected?.Invoke(this, CloseAction.MinimizeToTray);
        }

        /// <summary>
        /// 关闭程序按钮点击
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = CloseAction.Close;
            ActionSelected?.Invoke(this, CloseAction.Close);
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = CloseAction.Cancel;
            ActionSelected?.Invoke(this, CloseAction.Cancel);
        }

        /// <summary>
        /// 显示启动进度圆环
        /// </summary>
        public void ShowStartupProgress()
        {
            try
            {
                ConfirmDialogBorder.Visibility = Visibility.Collapsed;
                ProgressBorder.Visibility = Visibility.Collapsed;
                StartupProgressBorder.Visibility = Visibility.Visible;
                OverlayCanvas.Visibility = Visibility.Visible;
                OverlayCanvas.IsHitTestVisible = false; // 进度显示时不可交互
                OverlayCanvas.IsEnabled = false;
                
                this.UpdateLayout();
                OverlayCanvas.UpdateLayout();
                
                // 重置进度
                UpdateStartupProgress(0, 15, "准备启动...");
                
                this.InvalidateVisual();
                OverlayCanvas.InvalidateVisual();
            }
            catch (Exception ex)
            {
                Logger.LogError($"显示启动进度圆环失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 更新启动进度
        /// </summary>
        /// <param name="step">当前步骤（从1开始）</param>
        /// <param name="totalSteps">总步骤数</param>
        /// <param name="status">状态文本</param>
        public void UpdateStartupProgress(int step, int totalSteps, string status)
        {
            // 计算进度百分比
            int progress = totalSteps > 0 ? (int)((step / (double)totalSteps) * 100) : 0;
            UpdateStartupProgressRing(progress);
            
            if (StartupStatusText != null)
            {
                StartupStatusText.Text = status;
            }
            if (StartupProgressText != null)
            {
                StartupProgressText.Text = $"{progress}%";
            }
            if (StartupStepText != null)
            {
                StartupStepText.Text = $"步骤 {step}/{totalSteps}";
            }
        }

        /// <summary>
        /// 更新启动进度圆环
        /// </summary>
        /// <param name="progress">进度值（0-100）</param>
        private void UpdateStartupProgressRing(int progress)
        {
            try
            {
                if (StartupProgressArc == null) return;

                progress = Math.Max(0, Math.Min(100, progress));

                double angle = (progress / 100.0) * 360.0;
                double angleRad = (angle - 90) * Math.PI / 180.0; // 转换为弧度，-90度使起点在顶部

                double centerX = 60;
                double centerY = 60;
                double radius = 50;

                double endX = centerX + radius * Math.Cos(angleRad);
                double endY = centerY + radius * Math.Sin(angleRad);

                bool isLargeArc = progress > 50;

                StartupProgressArc.Point = new System.Windows.Point(endX, endY);
                StartupProgressArc.IsLargeArc = isLargeArc;
                StartupProgressArc.Size = new System.Windows.Size(radius, radius);
            }
            catch (Exception ex)
            {
                Logger.LogError($"更新启动进度圆环失败: {ex.Message}", ex);
            }
        }
    }
}

