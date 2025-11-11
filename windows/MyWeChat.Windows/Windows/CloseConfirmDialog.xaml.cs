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

