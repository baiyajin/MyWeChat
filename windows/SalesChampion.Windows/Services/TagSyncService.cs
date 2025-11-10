using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using MyWeChat.Windows.Core.Connection;
using MyWeChat.Windows.Models;
using MyWeChat.Windows.Services.WebSocket;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Services
{
    /// <summary>
    /// 标签同步服务
    /// 负责从微信获取标签列表并同步到服务器
    /// </summary>
    public class TagSyncService
    {
        private readonly WeChatConnectionManager _connectionManager;
        private readonly WebSocketService _webSocketService;
        private Func<string>? _getWeChatIdFunc; // 获取真正的wxid的函数

        // 命令ID定义
        private const int CMD_GET_TAG_LIST = 11238;

        /// <summary>
        /// 构造函数
        /// </summary>
        public TagSyncService(WeChatConnectionManager connectionManager, WebSocketService webSocketService, Func<string>? getWeChatIdFunc = null)
        {
            _connectionManager = connectionManager;
            _webSocketService = webSocketService;
            _getWeChatIdFunc = getWeChatIdFunc;
        }

        /// <summary>
        /// 同步标签列表
        /// </summary>
        /// <returns>返回是否成功</returns>
        public bool SyncTags()
        {
            if (!_connectionManager.IsConnected)
            {
                Logger.LogWarning("微信未连接，无法同步标签");
                return false;
            }

            try
            {
                Logger.LogInfo("开始同步标签列表");

                bool result = _connectionManager.SendCommand(CMD_GET_TAG_LIST);

                if (result)
                {
                    Logger.LogInfo("已发送获取标签列表命令，等待回调数据");
                }
                else
                {
                    Logger.LogError("发送获取标签列表命令失败");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步标签列表失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理标签列表回调数据
        /// </summary>
        /// <param name="jsonData">JSON格式的标签列表数据</param>
        public void ProcessTagsCallback(string jsonData)
        {
            try
            {
                Logger.LogInfo($"收到标签列表回调数据: {jsonData}");

                // 解析JSON数据
                var tagList = JsonConvert.DeserializeObject<List<dynamic>>(jsonData);
                if (tagList == null || tagList.Count == 0)
                {
                    Logger.LogWarning("标签列表数据为空");
                    return;
                }

                // 转换为TagInfo模型
                List<TagInfo> tags = new List<TagInfo>();
                // 优先使用真正的wxid，如果没有则使用ClientId（进程ID）作为fallback
                string weChatId = _getWeChatIdFunc?.Invoke() ?? _connectionManager.ClientId.ToString();
                if (string.IsNullOrEmpty(weChatId))
                {
                    weChatId = _connectionManager.ClientId.ToString();
                    Logger.LogWarning("未获取到真正的wxid，使用ClientId（进程ID）作为fallback");
                }
                else
                {
                    Logger.LogInfo($"使用真正的wxid进行同步: {weChatId}");
                }

                foreach (var tag in tagList)
                {
                    try
                    {
                        var tagInfo = new TagInfo
                        {
                            TagId = tag.tag_id?.ToString() ?? "",
                            TagName = tag.tag_name?.ToString() ?? "",
                            WeChatId = weChatId
                        };

                        tags.Add(tagInfo);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"解析标签信息失败: {ex.Message}");
                    }
                }

                if (tags.Count > 0)
                {
                    // 同步到服务器
                    SyncToServer(tags);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理标签列表回调失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 同步标签到服务器
        /// </summary>
        private void SyncToServer(List<TagInfo> tags)
        {
            try
            {
                Logger.LogInfo($"开始同步标签到服务器，数量: {tags.Count}");

                // 通过WebSocket发送到服务器
                var syncData = new
                {
                    type = "sync_tags",
                    data = tags
                };

                _ = _webSocketService.SendMessageAsync(syncData);

                Logger.LogInfo("标签同步完成");
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步标签到服务器失败: {ex.Message}", ex);
            }
        }
    }
}

