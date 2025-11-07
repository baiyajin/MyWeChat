using System;
using SalesChampion.Windows.Utils;

namespace SalesChampion.Windows.Core.DLLWrapper
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
        /// <returns>返回DLL封装实例</returns>
        public static WeChatHelperWrapperBase Create(string version)
        {
            try
            {
                Logger.LogInfo($"创建DLL封装实例，版本: {version}");
                
                switch (version)
                {
                    case "3.9.12.45":
                        Logger.LogInfo("使用3.9.12.45版本的DLL封装");
                        return new WeChatHelperWrapper_3_9_12_45();
                    
                    case "4.1.0.34":
                        Logger.LogInfo("使用4.1.0.34版本的DLL封装");
                        return new WeChatHelperWrapper_4_1_0_34();
                    
                    // 可以添加其他版本的实现
                    default:
                        Logger.LogError($"不支持的微信版本: {version}");
                        Logger.LogError($"支持的版本: 3.9.12.45, 4.1.0.34");
                        return null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"创建DLL封装实例失败: {ex.Message}", ex);
                return null;
            }
        }
    }
}

