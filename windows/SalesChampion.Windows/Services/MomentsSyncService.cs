using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SalesChampion.Windows.Core.Connection;
using SalesChampion.Windows.Models;
using SalesChampion.Windows.Services.WebSocket;
using SalesChampion.Windows.Utils;

namespace SalesChampion.Windows.Services
{
    /// <summary>
    /// 朋友圈同步服务
    /// 负责从微信获取朋友圈内容并同步到服务器
    /// </summary>
    public class MomentsSyncService
    {
        private readonly WeChatConnectionManager _connectionManager;
        private readonly WebSocketService _webSocketService;
        private Func<string>? _getWeChatIdFunc; // 获取真正的wxid的函数

        // 命令ID定义
        private const int CMD_GET_MOMENTS_LIST = 11241;

        /// <summary>
        /// 构造函数
        /// </summary>
        public MomentsSyncService(WeChatConnectionManager connectionManager, WebSocketService webSocketService, Func<string>? getWeChatIdFunc = null)
        {
            _connectionManager = connectionManager;
            _webSocketService = webSocketService;
            _getWeChatIdFunc = getWeChatIdFunc;
        }

        /// <summary>
        /// 同步朋友圈列表
        /// </summary>
        /// <param name="maxId">最大ID，用于分页获取</param>
        /// <returns>返回是否成功</returns>
        public bool SyncMoments(string maxId = "0")
        {
            if (!_connectionManager.IsConnected)
            {
                Logger.LogWarning("微信未连接，无法同步朋友圈");
                return false;
            }

            try
            {
                Logger.LogInfo($"开始同步朋友圈，MaxId: {maxId}");

                var data = new { max_id = maxId };
                bool result = _connectionManager.SendCommand(CMD_GET_MOMENTS_LIST, data);

                if (result)
                {
                    Logger.LogInfo("已发送获取朋友圈命令，等待回调数据");
                }
                else
                {
                    Logger.LogError("发送获取朋友圈命令失败");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步朋友圈失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理朋友圈回调数据
        /// </summary>
        /// <param name="jsonData">JSON格式的朋友圈数据</param>
        public void ProcessMomentsCallback(string jsonData)
        {
            try
            {
                Logger.LogInfo($"收到朋友圈回调数据: {jsonData}");

                // 解析JSON数据
                var momentsList = JsonConvert.DeserializeObject<List<dynamic>>(jsonData);
                if (momentsList == null || momentsList.Count == 0)
                {
                    Logger.LogWarning("朋友圈数据为空");
                    return;
                }

                // 转换为MomentsInfo模型
                List<MomentsInfo> moments = new List<MomentsInfo>();
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

                foreach (var moment in momentsList)
                {
                    try
                    {
                        var momentsInfo = new MomentsInfo
                        {
                            MomentId = moment.object_id?.ToString() ?? "",
                            NickName = moment.nickname?.ToString() ?? "",
                            FriendId = moment.wxid?.ToString() ?? "",
                            Moments = moment.content?.ToString() ?? "",
                            WeChatId = weChatId,
                            ReleaseTime = moment.create_time?.ToString() ?? "",
                            Type = moment.type != null ? (int)moment.type : 0,
                            JsonText = JsonConvert.SerializeObject(moment)
                        };

                        moments.Add(momentsInfo);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"解析朋友圈信息失败: {ex.Message}");
                    }
                }

                if (moments.Count > 0)
                {
                    // 同步到服务器
                    SyncToServer(moments);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理朋友圈回调失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 同步朋友圈到服务器
        /// </summary>
        private void SyncToServer(List<MomentsInfo> moments)
        {
            try
            {
                Logger.LogInfo($"开始同步朋友圈到服务器，数量: {moments.Count}");

                // 通过WebSocket发送到服务器
                var syncData = new
                {
                    type = "sync_moments",
                    data = moments
                };

                _ = _webSocketService.SendMessageAsync(syncData);

                Logger.LogInfo("朋友圈同步完成");
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步朋友圈到服务器失败: {ex.Message}", ex);
            }
        }
    }
}

