using System.Configuration;

namespace MyWeChat.Windows.Utils
{
    /// <summary>
    /// 配置帮助类
    /// 读取配置文件中的设置
    /// </summary>
    public static class ConfigHelper
    {
        /// <summary>
        /// 获取服务器URL
        /// </summary>
        public static string GetServerUrl()
        {
            return ConfigurationManager.AppSettings["ServerUrl"] ?? "http://localhost:8000";
        }

        /// <summary>
        /// 获取WebSocket URL
        /// </summary>
        public static string GetWebSocketUrl()
        {
            return ConfigurationManager.AppSettings["WebSocketUrl"] ?? "ws://localhost:8000/ws";
        }

        /// <summary>
        /// 获取回调端口
        /// </summary>
        public static int GetCallbackPort()
        {
            string portStr = ConfigurationManager.AppSettings["CallbackPort"] ?? "6060";
            if (int.TryParse(portStr, out int port))
            {
                return port;
            }
            return 6060;
        }

        /// <summary>
        /// 是否启用开发模式（开发模式下关闭时不撤回DLL注入）
        /// 自动检测：Debug 模式下自动启用，Release 模式下自动禁用
        /// </summary>
        public static bool IsDevelopmentMode()
        {
#if DEBUG
            // Debug 模式下自动启用开发模式
            return true;
#else
            // Release 模式下自动禁用开发模式
            return false;
#endif
        }
    }
}

