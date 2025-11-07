using System.Configuration;

namespace SalesChampion.Windows.Utils
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
    }
}

