using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MyWeChat.Windows.UI.ViewModels
{
    /// <summary>
    /// 主窗口视图模型
    /// 使用MVVM模式管理UI状态
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private bool _isWeChatConnected;
        private string _weChatVersion = string.Empty;
        private bool _isAppConnected;

        /// <summary>
        /// 微信连接状态
        /// </summary>
        public bool IsWeChatConnected
        {
            get => _isWeChatConnected;
            set
            {
                if (_isWeChatConnected != value)
                {
                    _isWeChatConnected = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 微信版本号
        /// </summary>
        public string WeChatVersion
        {
            get => _weChatVersion;
            set
            {
                if (_weChatVersion != value)
                {
                    _weChatVersion = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// App连接状态
        /// </summary>
        public bool IsAppConnected
        {
            get => _isAppConnected;
            set
            {
                if (_isAppConnected != value)
                {
                    _isAppConnected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

