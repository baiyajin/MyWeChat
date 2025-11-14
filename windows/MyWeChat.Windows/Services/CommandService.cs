using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using MyWeChat.Windows.Core.Connection;
using MyWeChat.Windows.Models;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Services
{
    /// <summary>
    /// 命令处理服务
    /// 负责接收App端命令并执行相应的操作
    /// </summary>
    public class CommandService
    {
        private readonly WeChatConnectionManager _connectionManager;
        private ContactSyncService? _contactSyncService;
        private MomentsSyncService? _momentsSyncService;
        private TagSyncService? _tagSyncService;
        private string? _serverUrl; // 服务器地址，用于返回命令结果

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
        /// 设置服务器地址（用于返回命令结果）
        /// </summary>
        public void SetServerUrl(string serverUrl)
        {
            _serverUrl = serverUrl?.TrimEnd('/');
        }

        /// <summary>
        /// 设置同步服务（用于处理同步命令）
        /// </summary>
        public void SetSyncServices(ContactSyncService? contactSyncService, MomentsSyncService? momentsSyncService, TagSyncService? tagSyncService)
        {
            _contactSyncService = contactSyncService;
            _momentsSyncService = momentsSyncService;
            _tagSyncService = tagSyncService;
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
                        result = HandleMomentsLike(command, commandData);
                        break;
                    case "moments_comment":
                        result = HandleMomentsComment(command, commandData);
                        break;
                    case "send_moments":
                        result = HandleSendMoments(command, commandData);
                        break;
                    case "sync_contacts":
                        result = HandleSyncContacts(command);
                        break;
                    case "sync_moments":
                        result = HandleSyncMoments(command, commandData);
                        break;
                    case "sync_tags":
                        result = HandleSyncTags(command);
                        break;
                    case "get_logs":
                        // get_logs命令需要异步处理，立即返回true，结果通过API返回
                        _ = Task.Run(async () => await HandleGetLogsAsync(command));
                        result = true;
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
        private bool HandleMomentsLike(CommandInfo command, dynamic? commandData)
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
        private bool HandleMomentsComment(CommandInfo command, dynamic? commandData)
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
        private bool HandleSendMoments(CommandInfo command, dynamic? commandData)
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

        /// <summary>
        /// 处理同步好友列表命令
        /// </summary>
        private bool HandleSyncContacts(CommandInfo command)
        {
            try
            {
                Logger.LogInfo("收到同步好友列表命令");
                
                if (_contactSyncService == null)
                {
                    Logger.LogError("好友同步服务未初始化");
                    return false;
                }

                bool result = _contactSyncService.SyncContacts();
                
                if (result)
                {
                    Logger.LogInfo("好友列表同步命令已发送");
                }
                else
                {
                    Logger.LogError("好友列表同步命令发送失败");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理同步好友列表命令失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理同步朋友圈命令
        /// </summary>
        private bool HandleSyncMoments(CommandInfo command, dynamic? commandData)
        {
            try
            {
                Logger.LogInfo("收到同步朋友圈命令");
                
                if (_momentsSyncService == null)
                {
                    Logger.LogError("朋友圈同步服务未初始化");
                    return false;
                }

                string maxId = "0";
                if (commandData != null)
                {
                    string? maxIdStr = commandData.max_id?.ToString();
                    if (!string.IsNullOrEmpty(maxIdStr))
                    {
                        maxId = maxIdStr;
                    }
                }

                bool result = _momentsSyncService.SyncMoments(maxId);
                
                if (result)
                {
                    Logger.LogInfo($"朋友圈同步命令已发送，MaxId: {maxId}");
                }
                else
                {
                    Logger.LogError("朋友圈同步命令发送失败");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理同步朋友圈命令失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理同步标签列表命令
        /// </summary>
        private bool HandleSyncTags(CommandInfo command)
        {
            try
            {
                Logger.LogInfo("收到同步标签列表命令");
                
                if (_tagSyncService == null)
                {
                    Logger.LogError("标签同步服务未初始化");
                    return false;
                }

                bool result = _tagSyncService.SyncTags();
                
                if (result)
                {
                    Logger.LogInfo("标签列表同步命令已发送");
                }
                else
                {
                    Logger.LogError("标签列表同步命令发送失败");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理同步标签列表命令失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理获取日志命令
        /// </summary>
        private async Task HandleGetLogsAsync(CommandInfo command)
        {
            try
            {
                Logger.LogInfo("收到获取日志命令");

                // 获取日志目录
                string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(logDirectory))
                {
                    await ReturnCommandResultAsync(command.CommandId, "error", "日志目录不存在");
                    return;
                }

                // 获取最新的日志文件（按日期排序）
                var logFiles = Directory.GetFiles(logDirectory, "log_*.txt")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (logFiles.Count == 0)
                {
                    await ReturnCommandResultAsync(command.CommandId, "error", "没有找到日志文件");
                    return;
                }

                // 读取最新的日志文件（保持加密状态）
                var latestLogFile = logFiles[0];
                string encryptedLogContent = await File.ReadAllTextAsync(latestLogFile.FullName);

                // 获取机器特征（用于服务端生成密钥）
                string machineId = KeyDerivationService.GetMachineId();

                // 构建返回结果
                var result = new
                {
                    log_file_name = latestLogFile.Name,
                    encrypted_log_content = encryptedLogContent,
                    machine_id = machineId
                };

                string resultJson = JsonConvert.SerializeObject(result);
                await ReturnCommandResultAsync(command.CommandId, "completed", resultJson);
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理获取日志命令失败: {ex.Message}", ex);
                await ReturnCommandResultAsync(command.CommandId, "error", $"获取日志失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 返回命令执行结果到服务器
        /// </summary>
        private async Task ReturnCommandResultAsync(string commandId, string status, string result)
        {
            if (string.IsNullOrEmpty(_serverUrl) || string.IsNullOrEmpty(commandId))
            {
                Logger.LogWarning("服务器地址或命令ID为空，无法返回命令结果");
                return;
            }

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    string url = $"{_serverUrl}/api/commands/{commandId}/result";
                    var requestBody = new
                    {
                        status = status,
                        result = result
                    };

                    string json = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await httpClient.PostAsync(url, content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        Logger.LogInfo($"命令结果已返回: {commandId}, 状态: {status}");
                    }
                    else
                    {
                        Logger.LogError($"返回命令结果失败: {response.StatusCode}, 命令ID: {commandId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"返回命令结果异常: {ex.Message}", ex);
            }
        }
    }
}

