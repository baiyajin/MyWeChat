using System;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Core.DLLWrapper
{
    /// <summary>
    /// 微信帮助DLL封装工厂类
    /// 根据微信版本创建对应的DLL封装实例
    /// </summary>
    public static class WeChatHelperWrapperFactory
    {
        /// <summary>
        /// 创建DLL封装实例
        /// </summary>
        /// <param name="version">微信版本号</param>
        /// <param name="randomDllPath">随机文件名的DLL路径（用于动态加载）</param>
        /// <returns>返回DLL封装实例</returns>
        public static WeChatHelperWrapperBase? Create(string? version, string? randomDllPath = null)
        {
            try
            {
                // 注意：DLL封装实例创建的详细信息已包含在Hook管理器初始化日志中，这里不再重复输出
                
                WeChatHelperWrapperBase? instance = null;
                
                switch (version)
                {
                    case "3.9.12.45":
                        instance = new WeChatHelperWrapper_3_9_12_45();
                        break;
                    
                    case "4.1.0.34":
                        instance = new WeChatHelperWrapper_4_1_0_34();
                        break;
                    
                    // 可以添加其他版本的实现
                    default:
                        Logger.LogError($"不支持的微信版本: {version}");
                        Logger.LogError($"支持的版本: 3.9.12.45, 4.1.0.34");
                        return null;
                }

                // 如果提供了随机DLL路径，初始化动态加载器
                if (instance != null && !string.IsNullOrEmpty(randomDllPath))
                {
                    instance.InitializeDynamicLoader(randomDllPath);
                }

                return instance;
            }
            catch (Exception ex)
            {
                Logger.LogError($"创建DLL封装实例失败: {ex.Message}", ex);
                return null;
            }
        }
    }
}

