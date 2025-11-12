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
    public partial class WindowCloseOverlay : UserControl
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
            
            // 居中显示弹窗
            CenterBorder(ConfirmDialogBorder);
        }

        /// <summary>
        /// 显示关闭进度圆环
        /// </summary>
        public void ShowProgress()
        {
            ConfirmDialogBorder.Visibility = Visibility.Collapsed;
            ProgressBorder.Visibility = Visibility.Visible;
            OverlayCanvas.Visibility = Visibility.Visible;
            OverlayCanvas.IsHitTestVisible = false; // 进度显示时不可交互
            OverlayCanvas.IsEnabled = false;
            
            // 居中显示进度
            CenterBorder(ProgressBorder);
            
            // 重置进度
            UpdateProgress(0, "准备关闭...");
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
                ProgressArc.Point = new Point(endX, endY);
                ProgressArc.IsLargeArc = isLargeArc;
                ProgressArc.Size = new Size(radius, radius);
            }
            catch (Exception ex)
            {
                Logger.LogError($"更新关闭进度圆环失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 居中显示Border
        /// </summary>
        private void CenterBorder(Border border)
        {
            if (border == null) return;

            // 等待布局更新
            OverlayCanvas.UpdateLayout();
            
            // 获取UserControl的实际大小（从父容器）
            double canvasWidth = this.ActualWidth > 0 ? this.ActualWidth : OverlayCanvas.ActualWidth;
            double canvasHeight = this.ActualHeight > 0 ? this.ActualHeight : OverlayCanvas.ActualHeight;
            
            // 如果还是0，尝试从父窗口获取
            if (canvasWidth == 0 || canvasHeight == 0)
            {
                var parent = this.Parent as FrameworkElement;
                if (parent != null)
                {
                    canvasWidth = parent.ActualWidth;
                    canvasHeight = parent.ActualHeight;
                }
            }
            
            double borderWidth = border.Width;
            double borderHeight = border.Height;

            if (canvasWidth > 0 && canvasHeight > 0)
            {
                Canvas.SetLeft(border, (canvasWidth - borderWidth) / 2);
                Canvas.SetTop(border, (canvasHeight - borderHeight) / 2);
            }
        }

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
    }
}

