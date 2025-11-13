using System;
using System.IO;
using Newtonsoft.Json;

namespace MyWeChat.Windows.Utils
{
    /// <summary>
    /// Hook状态管理器
    /// 用于在开发模式下保存和恢复Hook状态，避免频繁注入/撤回DLL
    /// </summary>
    public static class HookStateManager
    {
        private static readonly string StateFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "hook_state.json");

        /// <summary>
        /// Hook状态数据
        /// </summary>
        public class HookState
        {
            public int ClientId { get; set; }
            public int WeChatProcessId { get; set; }
            public string? WeChatVersion { get; set; }
            public bool IsHooked { get; set; }
            public DateTime SaveTime { get; set; }
        }

        /// <summary>
        /// 保存Hook状态
        /// </summary>
        public static void SaveState(int clientId, int weChatProcessId, string? weChatVersion, bool isHooked)
        {
            try
            {
                var state = new HookState
                {
                    ClientId = clientId,
                    WeChatProcessId = weChatProcessId,
                    WeChatVersion = weChatVersion,
                    IsHooked = isHooked,
                    SaveTime = DateTime.Now
                };

                string json = JsonConvert.SerializeObject(state, Formatting.Indented);
                File.WriteAllText(StateFilePath, json);
                Logger.LogInfo($"Hook状态已保存: clientId={clientId}, processId={weChatProcessId}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"保存Hook状态失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 读取Hook状态
        /// </summary>
        public static HookState? LoadState()
        {
            try
            {
                if (!File.Exists(StateFilePath))
                {
                    return null;
                }

                string json = File.ReadAllText(StateFilePath);
                var state = JsonConvert.DeserializeObject<HookState>(json);
                
                if (state != null)
                {
                    // 检查状态是否过期（超过1小时认为过期）
                    TimeSpan age = DateTime.Now - state.SaveTime;
                    if (age.TotalHours > 1)
                    {
                        Logger.LogInfo($"Hook状态已过期（保存时间: {state.SaveTime}），将忽略");
                        DeleteState();
                        return null;
                    }

                    Logger.LogInfo($"Hook状态已读取: clientId={state.ClientId}, processId={state.WeChatProcessId}, 保存时间: {state.SaveTime}");
                }

                return state;
            }
            catch (Exception ex)
            {
                Logger.LogError($"读取Hook状态失败: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// 删除Hook状态
        /// </summary>
        public static void DeleteState()
        {
            try
            {
                if (File.Exists(StateFilePath))
                {
                    File.Delete(StateFilePath);
                    Logger.LogInfo("Hook状态已删除");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"删除Hook状态失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 验证状态是否有效（检查微信进程是否还在运行）
        /// </summary>
        public static bool ValidateState(HookState state)
        {
            try
            {
                if (state == null || state.WeChatProcessId <= 0)
                {
                    return false;
                }

                // 检查微信进程是否还在运行
                var process = System.Diagnostics.Process.GetProcessById(state.WeChatProcessId);
                if (process == null || process.HasExited)
                {
                    Logger.LogInfo($"微信进程已退出（PID: {state.WeChatProcessId}），状态无效");
                    return false;
                }

                // 检查进程名是否是微信
                string processName = process.ProcessName.ToLower();
                if (!processName.Contains("wechat"))
                {
                    Logger.LogInfo($"进程名不匹配（期望微信，实际: {processName}），状态无效");
                    return false;
                }

                Logger.LogInfo($"Hook状态验证通过: processId={state.WeChatProcessId}");
                return true;
            }
            catch (ArgumentException)
            {
                // 进程不存在
                Logger.LogInfo($"微信进程不存在（PID: {state.WeChatProcessId}），状态无效");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"验证Hook状态时出错: {ex.Message}，假设状态无效");
                return false;
            }
        }
    }
}

