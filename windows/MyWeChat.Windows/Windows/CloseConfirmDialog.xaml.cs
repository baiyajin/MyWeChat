using System.Windows;

namespace MyWeChat.Windows.Windows
{
    /// <summary>
    /// 关闭确认对话框（微信风格）
    /// </summary>
    public partial class CloseConfirmDialog : Window
    {
        /// <summary>
        /// 对话框结果
        /// </summary>
        public enum CloseDialogResult
        {
            MinimizeToTray,
            Close,
            Cancel
        }

        /// <summary>
        /// 用户选择的结果
        /// </summary>
        public CloseDialogResult Result { get; private set; } = CloseDialogResult.Cancel;

        public CloseConfirmDialog()
        {
            InitializeComponent();
        }
        
        /// <summary>
        /// 窗口加载完成事件 - 设置窗口大小为父窗口大小，并居中显示
        /// </summary>
        private void CloseConfirmDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置窗口大小为父窗口大小，以便遮罩层覆盖整个父窗口
            if (Owner != null)
            {
                this.Width = Owner.ActualWidth;
                this.Height = Owner.ActualHeight;
                // 设置窗口位置，确保在父窗口内（与父窗口完全重叠）
                this.Left = Owner.Left;
                this.Top = Owner.Top;
                // 确保窗口在最上层
                this.Topmost = true;
            }
        }
        
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // 在窗口初始化后再次确保位置正确
            if (Owner != null)
            {
                this.Left = Owner.Left;
                this.Top = Owner.Top;
                this.Width = Owner.ActualWidth;
                this.Height = Owner.ActualHeight;
            }
        }

        /// <summary>
        /// 最小化到托盘按钮点击
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            Result = CloseDialogResult.MinimizeToTray;
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 关闭程序按钮点击
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = CloseDialogResult.Close;
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = CloseDialogResult.Cancel;
            DialogResult = false;
            Close();
        }
    }
}

