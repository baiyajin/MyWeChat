using System;
using Newtonsoft.Json;
using SalesChampion.Windows.Core.Connection;
using SalesChampion.Windows.Models;
using SalesChampion.Windows.Utils;

namespace SalesChampion.Windows.Services
{
    /// <summary>
    /// 命令处理服务
    /// 负责接收App端命令并执行相应的操作
    /// </summary>
    public class CommandService
    {
        private readonly WeChatConnectionManager _connectionManager;

        // 命令ID定义
        private const int CMD_SEND_TEXT = 11132;
        private const int CMD_SEND_IMAGE = 11136;
        private const int CMD_MOMENTS_LIKE = 11243;
        private const int CMD_MOMENTS_COMMENT = 11242;
        private const int CMD_SEND_MOMENTS = 11244;

        /// <summary>
        /// 构造函数
        /// </summary>
        public CommandService(WeChatConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        /// <summary>
        /// 处理命令
        /// </summary>
        /// <param name="commandJson">JSON格式的命令数据</param>
        /// <returns>返回执行结果</returns>
        public bool ProcessCommand(string commandJson)
        {
            try
            {
                Logger.LogInfo($"收到命令: {commandJson}");

                var command = JsonConvert.DeserializeObject<CommandInfo>(commandJson);
                if (command == null)
                {
                    Logger.LogError("命令解析失败");
                    return false;
                }

                // 解析命令数据
                dynamic? commandData = null;
                if (!string.IsNullOrEmpty(command.CommandData))
                {
                    try
                    {
                        commandData = JsonConvert.DeserializeObject(command.CommandData) ?? null;
                    }
                    catch
                    {
                        commandData = new { };
                    }
                }

                bool result = false;

                switch (command.CommandType.ToLower())
                {
                    case "send_message":
                        result = HandleSendMessage(commandData, command.TargetWeChatId ?? "");
                        break;
                    case "send_file":
                        result = HandleSendFile(commandData, command.TargetWeChatId ?? "");
                        break;
                    case "moments_like":
                        result = HandleMomentsLike(command);
                        break;
                    case "moments_comment":
                        result = HandleMomentsComment(command);
                        break;
                    case "send_moments":
                        result = HandleSendMoments(command);
                        break;
                    default:
                        Logger.LogWarning($"未知的命令类型: {command.CommandType}");
                        return false;
                }

                if (result)
                {
                    Logger.LogInfo($"命令执行成功: {command.CommandType}");
                }
                else
                {
                    Logger.LogError($"命令执行失败: {command.CommandType}");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理命令失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理发送消息命令
        /// </summary>
        private bool HandleSendMessage(dynamic commandData, string targetWeChatId)
        {
            try
            {
                string toWeChatId = commandData?.to_wechat_id?.ToString() ?? targetWeChatId;
                string content = commandData?.content?.ToString() ?? "";

                if (string.IsNullOrEmpty(toWeChatId) || string.IsNullOrEmpty(content))
                {
                    Logger.LogError("发送消息命令参数不完整");
                    return false;
                }

                var cmdData = new
                {
                    to_wxid = toWeChatId,
                    content = content
                };

                return _connectionManager.SendCommand(CMD_SEND_TEXT, cmdData);
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理发送消息命令失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理发送文件命令
        /// </summary>
        private bool HandleSendFile(dynamic commandData, string targetWeChatId)
        {
            try
            {
                string toWeChatId = commandData?.to_wechat_id?.ToString() ?? targetWeChatId;
                string filePath = commandData?.file_path?.ToString() ?? "";

                if (string.IsNullOrEmpty(toWeChatId) || string.IsNullOrEmpty(filePath))
                {
                    Logger.LogError("发送文件命令参数不完整");
                    return false;
                }

                var cmdData = new
                {
                    to_wxid = toWeChatId,
                    file = filePath
                };

                return _connectionManager.SendCommand(CMD_SEND_IMAGE, cmdData);
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理发送文件命令失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理朋友圈点赞命令
        /// </summary>
        private bool HandleMomentsLike(dynamic commandData)
        {
            try
            {
                string momentId = commandData?.moment_id?.ToString() ?? "";

                if (string.IsNullOrEmpty(momentId))
                {
                    Logger.LogError("朋友圈点赞命令参数不完整");
                    return false;
                }

                var cmdData = new
                {
                    object_id = momentId
                };

                return _connectionManager.SendCommand(CMD_MOMENTS_LIKE, cmdData);
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理朋友圈点赞命令失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理朋友圈评论命令
        /// </summary>
        private bool HandleMomentsComment(dynamic commandData)
        {
            try
            {
                string momentId = commandData?.moment_id?.ToString() ?? "";
                string content = commandData?.content?.ToString() ?? "";

                if (string.IsNullOrEmpty(momentId) || string.IsNullOrEmpty(content))
                {
                    Logger.LogError("朋友圈评论命令参数不完整");
                    return false;
                }

                var cmdData = new
                {
                    object_id = momentId,
                    content = content
                };

                return _connectionManager.SendCommand(CMD_MOMENTS_COMMENT, cmdData);
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理朋友圈评论命令失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理发布朋友圈命令
        /// </summary>
        private bool HandleSendMoments(dynamic commandData)
        {
            try
            {
                string content = commandData?.content?.ToString() ?? "";
                var items = commandData?.items;

                if (string.IsNullOrEmpty(content) && (items == null))
                {
                    Logger.LogError("发布朋友圈命令参数不完整");
                    return false;
                }

                // 构建朋友圈数据
                var cmdData = new
                {
                    object_desc = content,
                    items = items
                };

                return _connectionManager.SendCommand(CMD_SEND_MOMENTS, cmdData);
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理发布朋友圈命令失败: {ex.Message}", ex);
                return false;
            }
        }
    }
}

