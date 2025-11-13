using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using MyWeChat.Windows.Core.Connection;
using MyWeChat.Windows.Services.WebSocket;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Services
{
    /// <summary>
    /// 公众号消息同步服务
    /// 负责从微信获取公众号推送消息并实时同步到服务器
    /// </summary>
    public class OfficialAccountSyncService
    {
        private readonly WeChatConnectionManager _connectionManager;
        private readonly WebSocketService _webSocketService;
        private Func<string>? _getWeChatIdFunc;

        /// <summary>
        /// 构造函数
        /// </summary>
        public OfficialAccountSyncService(WeChatConnectionManager connectionManager, WebSocketService webSocketService, Func<string>? getWeChatIdFunc = null)
        {
            _connectionManager = connectionManager;
            _webSocketService = webSocketService;
            _getWeChatIdFunc = getWeChatIdFunc;
        }

        /// <summary>
        /// 处理公众号消息回调数据
        /// </summary>
        /// <param name="jsonData">JSON格式的公众号消息数据</param>
        public void ProcessOfficialAccountCallback(string jsonData)
        {
            try
            {
                Logger.LogInfo($"收到公众号消息回调，数据: {jsonData}");

                // 解析JSON数据
                var messageObj = JsonConvert.DeserializeObject<dynamic>(jsonData);
                if (messageObj == null)
                {
                    Logger.LogWarning("公众号消息数据为空");
                    return;
                }

                // 获取当前登录的微信ID
                string weChatId = _getWeChatIdFunc?.Invoke() ?? _connectionManager?.ClientId.ToString() ?? "";
                if (string.IsNullOrEmpty(weChatId))
                {
                    Logger.LogWarning("无法获取微信ID，跳过公众号消息同步");
                    return;
                }

                // 获取raw_msg（XML格式）
                string rawMsg = messageObj.raw_msg?.ToString() ?? "";
                if (string.IsNullOrEmpty(rawMsg))
                {
                    Logger.LogWarning("公众号消息raw_msg为空");
                    return;
                }

                // 解析XML格式的raw_msg
                var officialAccountData = ParseRawMsg(rawMsg);
                if (officialAccountData == null)
                {
                    Logger.LogWarning("解析公众号消息XML失败");
                    return;
                }

                // 添加消息基本信息
                officialAccountData["from_wxid"] = messageObj.from_wxid?.ToString() ?? "";
                officialAccountData["msgid"] = messageObj.msgid?.ToString() ?? "";
                officialAccountData["wechat_id"] = weChatId;

                // 实时同步到服务器
                SyncToServer(officialAccountData);
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理公众号消息回调失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 解析XML格式的raw_msg
        /// </summary>
        private Dictionary<string, object>? ParseRawMsg(string rawMsg)
        {
            try
            {
                // 解析XML
                XDocument doc = XDocument.Parse(rawMsg);
                XElement? appmsg = doc.Descendants("appmsg").FirstOrDefault();
                if (appmsg == null)
                {
                    Logger.LogWarning("未找到appmsg节点");
                    return null;
                }

                // 检查type是否为5（公众号消息）
                string type = appmsg.Element("type")?.Value ?? "";
                if (type != "5")
                {
                    Logger.LogInfo($"消息type不是5，跳过处理。type: {type}");
                    return null;
                }

                // 获取mmreader节点
                XElement? mmreader = appmsg.Element("mmreader");
                if (mmreader == null)
                {
                    Logger.LogWarning("未找到mmreader节点");
                    return null;
                }

                // 获取category节点
                XElement? category = mmreader.Element("category");
                if (category == null)
                {
                    Logger.LogWarning("未找到category节点");
                    return null;
                }

                // 获取公众号名称
                string accountName = category.Element("name")?.Value ?? "";

                // 获取publisher节点（公众号信息）
                XElement? publisher = mmreader.Element("publisher");
                string publisherUsername = publisher?.Element("username")?.Value ?? "";
                string publisherNickname = publisher?.Element("nickname")?.Value ?? accountName;

                // 获取文章列表
                var articles = new List<Dictionary<string, object>>();
                var items = category.Elements("item");
                
                foreach (var item in items)
                {
                    var article = new Dictionary<string, object>
                    {
                        ["title"] = item.Element("title")?.Value ?? item.Element("title_v2")?.Value ?? "",
                        ["url"] = item.Element("url")?.Value ?? "",
                        ["cover"] = item.Element("cover")?.Value ?? item.Element("cover_1_1")?.Value ?? item.Element("cover_235_1")?.Value ?? "",
                        ["summary"] = item.Element("summary")?.Value ?? item.Element("digest")?.Value ?? "",
                        ["pub_time"] = item.Element("pub_time")?.Value ?? "0"
                    };
                    articles.Add(article);
                }

                // 获取template_header（模板消息头部信息）
                var templateHeader = new Dictionary<string, object>();
                XElement? templateHeaderNode = mmreader.Element("template_header");
                if (templateHeaderNode != null)
                {
                    templateHeader["title"] = templateHeaderNode.Element("title")?.Value ?? "";
                    templateHeader["title_color"] = templateHeaderNode.Element("title_color")?.Value ?? "";
                    templateHeader["pub_time"] = templateHeaderNode.Element("pub_time")?.Value ?? "0";
                    templateHeader["first_data"] = templateHeaderNode.Element("first_data")?.Value ?? "";
                    templateHeader["first_color"] = templateHeaderNode.Element("first_color")?.Value ?? "";
                }

                // 获取template_detail（模板消息详细内容）
                var templateDetail = new Dictionary<string, object>();
                XElement? templateDetailNode = mmreader.Element("template_detail");
                if (templateDetailNode != null)
                {
                    // 获取line_content（行内容，包含key-value对）
                    var lineContent = new List<Dictionary<string, object>>();
                    XElement? lineContentNode = templateDetailNode.Element("line_content");
                    if (lineContentNode != null)
                    {
                        var lines = lineContentNode.Element("lines");
                        if (lines != null)
                        {
                            foreach (var line in lines.Elements("line"))
                            {
                                var lineData = new Dictionary<string, object>();
                                
                                // 获取key
                                XElement? keyNode = line.Element("key");
                                if (keyNode != null)
                                {
                                    lineData["key"] = keyNode.Element("word")?.Value ?? "";
                                    lineData["key_color"] = keyNode.Element("color")?.Value ?? "";
                                }
                                
                                // 获取value
                                XElement? valueNode = line.Element("value");
                                if (valueNode != null)
                                {
                                    lineData["value"] = valueNode.Element("word")?.Value ?? "";
                                    lineData["value_color"] = valueNode.Element("color")?.Value ?? "";
                                }
                                
                                if (lineData.Count > 0)
                                {
                                    lineContent.Add(lineData);
                                }
                            }
                        }
                    }
                    
                    // 获取flat_content（扁平化内容，用于兼容）
                    var flatContent = new List<Dictionary<string, object>>();
                    XElement? flatContentNode = templateDetailNode.Element("flat_content");
                    if (flatContentNode != null)
                    {
                        var flatLines = flatContentNode.Element("lines");
                        if (flatLines != null)
                        {
                            foreach (var line in flatLines.Elements("line"))
                            {
                                var lineData = new Dictionary<string, object>();
                                
                                XElement? keyNode = line.Element("key");
                                if (keyNode != null)
                                {
                                    lineData["key"] = keyNode.Element("word")?.Value ?? "";
                                    lineData["key_color"] = keyNode.Element("color")?.Value ?? "";
                                }
                                
                                XElement? valueNode = line.Element("value");
                                if (valueNode != null)
                                {
                                    lineData["value"] = valueNode.Element("word")?.Value ?? "";
                                    lineData["value_color"] = valueNode.Element("color")?.Value ?? "";
                                }
                                
                                if (lineData.Count > 0)
                                {
                                    flatContent.Add(lineData);
                                }
                            }
                        }
                    }
                    
                    // 获取opitems（操作项，如"查看详情"按钮）
                    var opItems = new List<Dictionary<string, object>>();
                    XElement? opItemsNode = templateDetailNode.Element("opitems");
                    if (opItemsNode != null)
                    {
                        foreach (var opItem in opItemsNode.Elements("opitem"))
                        {
                            var opItemData = new Dictionary<string, object>
                            {
                                ["word"] = opItem.Element("word")?.Value ?? "",
                                ["url"] = opItem.Element("url")?.Value ?? "",
                                ["icon"] = opItem.Element("icon")?.Value ?? "",
                                ["color"] = opItem.Element("color")?.Value ?? ""
                            };
                            opItems.Add(opItemData);
                        }
                    }
                    
                    templateDetail["line_content"] = lineContent;
                    templateDetail["flat_content"] = flatContent;
                    templateDetail["opitems"] = opItems;
                    templateDetail["template_show_type"] = templateDetailNode.Element("template_show_type")?.Value ?? "";
                }

                // 获取title和des（标题和描述）
                string title = appmsg.Element("title")?.Value ?? "";
                string des = appmsg.Element("des")?.Value ?? "";

                // 构建返回数据
                var result = new Dictionary<string, object>
                {
                    ["account_name"] = accountName,
                    ["publisher_username"] = publisherUsername,
                    ["publisher_nickname"] = publisherNickname,
                    ["title"] = title,
                    ["description"] = des,
                    ["articles"] = articles,
                    ["template_header"] = templateHeader,
                    ["template_detail"] = templateDetail,
                    ["receive_time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                Logger.LogInfo($"解析公众号消息成功，公众号: {accountName}, 文章数: {articles.Count}, 模板行数: {(templateDetail.ContainsKey("line_content") ? ((List<Dictionary<string, object>>)templateDetail["line_content"]).Count : 0)}");
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"解析公众号消息XML失败: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// 同步公众号消息到服务器
        /// </summary>
        private void SyncToServer(Dictionary<string, object> officialAccountData)
        {
            try
            {
                Logger.LogInfo($"开始同步公众号消息到服务器: {officialAccountData["account_name"]}");

                // 通过WebSocket发送到服务器
                var syncData = new
                {
                    type = "sync_official_account",
                    data = officialAccountData
                };

                _ = _webSocketService.SendMessageAsync(syncData);

                Logger.LogInfo("公众号消息同步完成");
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步公众号消息到服务器失败: {ex.Message}", ex);
            }
        }
    }
}

