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
    /// 联系人同步服务
    /// 负责从微信获取好友列表并同步到服务器
    /// </summary>
    public class ContactSyncService
    {
        private readonly WeChatConnectionManager _connectionManager;
        private readonly WebSocketService _webSocketService;
        private readonly Func<string>? _getWeChatIdFunc;
        private readonly Dictionary<int, List<ContactInfo>> _pendingContacts = new Dictionary<int, List<ContactInfo>>();

        // 命令ID定义
        private const int CMD_GET_FRIEND_LIST = 11126;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ContactSyncService(WeChatConnectionManager connectionManager, WebSocketService webSocketService, Func<string>? getWeChatIdFunc = null)
        {
            _connectionManager = connectionManager;
            _webSocketService = webSocketService;
            _getWeChatIdFunc = getWeChatIdFunc;
        }

        /// <summary>
        /// 同步好友列表
        /// </summary>
        /// <returns>返回是否成功</returns>
        public bool SyncContacts()
        {
            if (!_connectionManager.IsConnected)
            {
                Logger.LogWarning("微信未连接，无法同步好友列表");
                return false;
            }

            try
            {
                Logger.LogInfo("开始同步好友列表");
                
                // 发送获取好友列表命令
                bool result = _connectionManager.SendCommand(CMD_GET_FRIEND_LIST);
                
                if (result)
                {
                    Logger.LogInfo("已发送获取好友列表命令，等待回调数据");
                }
                else
                {
                    Logger.LogError("发送获取好友列表命令失败");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步好友列表失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理好友列表回调数据
        /// </summary>
        /// <param name="jsonData">JSON格式的好友列表数据</param>
        public void ProcessContactsCallback(string jsonData)
        {
            try
            {
                Logger.LogInfo($"收到好友列表回调数据: {jsonData}");

                // 解析JSON数据
                var friendList = JsonConvert.DeserializeObject<List<dynamic>>(jsonData);
                if (friendList == null || friendList.Count == 0)
                {
                    Logger.LogWarning("好友列表数据为空");
                    return;
                }

                // 转换为ContactInfo模型
                List<ContactInfo> contacts = new List<ContactInfo>();
                // 优先使用真正的wxid，如果没有则使用ClientId（进程ID）作为fallback
                string? realWxid = _getWeChatIdFunc?.Invoke();
                string weChatId = !string.IsNullOrEmpty(realWxid) ? realWxid : _connectionManager.ClientId.ToString();
                if (!string.IsNullOrEmpty(realWxid))
                {
                    Logger.LogInfo($"使用真正的wxid进行同步: {weChatId}");
                }
                else
                {
                    Logger.LogWarning("未获取到真正的wxid，使用ClientId（进程ID）作为fallback");
                }

                foreach (var friend in friendList)
                {
                    try
                    {
                        // 过滤过长的ID
                        string wxid = friend.wxid?.ToString() ?? "";
                        if (wxid.Length > 50)
                        {
                            Logger.LogWarning($"好友ID过长，已过滤: {wxid}");
                            continue;
                        }

                        var contact = new ContactInfo
                        {
                            Avatar = friend.avatar?.ToString() ?? "",
                            City = friend.city?.ToString() ?? "",
                            Country = friend.country?.ToString() ?? "",
                            LabelIds = friend.labelid_list?.ToString() ?? "",
                            NickName = friend.nickname?.ToString() ?? "",
                            Province = friend.province?.ToString() ?? "",
                            Sex = friend.sex != null ? (int)friend.sex : 0,
                            Remark = friend.remark?.ToString() ?? "",
                            FriendId = wxid,
                            FriendNo = friend.account?.ToString() ?? wxid,
                            WeChatId = weChatId,
                            IsNewFriend = "1" // 首次同步标记为新好友
                        };
                        
                        // 设置ID（用于App端显示）
                        contact.Id = $"{weChatId}_{wxid}";

                        contacts.Add(contact);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"解析好友信息失败: {ex.Message}");
                    }
                }

                if (contacts.Count > 0)
                {
                    // 同步到服务器
                    SyncToServer(contacts);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理好友列表回调失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 同步好友列表到服务器
        /// </summary>
        private void SyncToServer(List<ContactInfo> contacts)
        {
            try
            {
                Logger.LogInfo($"开始同步好友列表到服务器，数量: {contacts.Count}");

                // 分批同步（每批1000条）
                int batchSize = 1000;
                for (int i = 0; i < contacts.Count; i += batchSize)
                {
                    var batch = contacts.Skip(i).Take(batchSize).ToList();
                    
                // 通过WebSocket发送到服务器
                // 将ContactInfo转换为App端期望的格式（小写下划线命名）
                var contactData = batch.Select(c => new
                {
                    id = c.Id ?? $"{c.WeChatId}_{c.FriendId}",
                    we_chat_id = c.WeChatId,
                    friend_id = c.FriendId,
                    nick_name = c.NickName,
                    remark = c.Remark,
                    avatar = c.Avatar,
                    city = c.City,
                    province = c.Province,
                    country = c.Country,
                    sex = c.Sex,
                    label_ids = c.LabelIds,
                    friend_no = c.FriendNo,
                    is_new_friend = c.IsNewFriend == "1" ? "1" : "0"
                }).ToList();
                
                var syncData = new
                {
                    type = "sync_contacts",
                    data = contactData
                };

                _ = _webSocketService.SendMessageAsync(syncData);

                    Logger.LogInfo($"已同步好友列表批次 {i / batchSize + 1}，数量: {batch.Count}");
                }

                Logger.LogInfo("好友列表同步完成");
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步好友列表到服务器失败: {ex.Message}", ex);
            }
        }
    }
}

