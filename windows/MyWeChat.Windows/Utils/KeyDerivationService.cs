using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace MyWeChat.Windows.Utils
{
    /// <summary>
    /// 密钥派生服务（PBKDF2）
    /// 用于从机器特征生成加密密钥
    /// </summary>
    public static class KeyDerivationService
    {
        private static byte[]? _cachedKey;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取或生成加密密钥（32字节，256位）
        /// </summary>
        public static byte[] GetEncryptionKey()
        {
            if (_cachedKey != null)
            {
                return _cachedKey;
            }

            lock (_lock)
            {
                if (_cachedKey != null)
                {
                    return _cachedKey;
                }

                try
                {
                    // 获取机器特征
                    string machineId = GetMachineId();
                    
                    // 使用 PBKDF2 派生密钥
                    // 参数：密码（机器ID）、盐（固定值）、迭代次数（100000）、输出长度（32字节）
                    byte[] salt = Encoding.UTF8.GetBytes("MyWeChat_Encryption_Salt_2024");
                    using (var pbkdf2 = new Rfc2898DeriveBytes(machineId, salt, 100000, HashAlgorithmName.SHA256))
                    {
                        _cachedKey = pbkdf2.GetBytes(32); // 256位密钥
                    }

                    return _cachedKey;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"生成加密密钥失败: {ex.Message}", ex);
                    // 回退到默认密钥（不安全，但至少能工作）
                    _cachedKey = Encoding.UTF8.GetBytes("MyWeChat_Default_Key_32Bytes!!").Take(32).ToArray();
                    return _cachedKey;
                }
            }
        }

        /// <summary>
        /// 获取机器唯一标识（MAC地址 + CPU ID）
        /// </summary>
        private static string GetMachineId()
        {
            try
            {
                StringBuilder machineId = new StringBuilder();

                // 获取第一个网络适配器的MAC地址
                try
                {
                    NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                    var firstInterface = interfaces.FirstOrDefault(ni => 
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        ni.OperationalStatus == OperationalStatus.Up);
                    
                    if (firstInterface != null)
                    {
                        byte[] macBytes = firstInterface.GetPhysicalAddress().GetAddressBytes();
                        machineId.Append(BitConverter.ToString(macBytes).Replace("-", ""));
                    }
                }
                catch
                {
                    // 如果获取MAC地址失败，使用默认值
                    machineId.Append("DefaultMAC");
                }

                // 获取CPU ID
                try
                {
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            string? processorId = obj["ProcessorId"]?.ToString();
                            if (!string.IsNullOrEmpty(processorId))
                            {
                                machineId.Append(processorId);
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // 如果获取CPU ID失败，使用默认值
                    machineId.Append("DefaultCPU");
                }

                // 如果都失败了，使用机器名
                if (machineId.Length == 0)
                {
                    machineId.Append(Environment.MachineName);
                }

                return machineId.ToString();
            }
            catch (Exception ex)
            {
                Logger.LogError($"获取机器ID失败: {ex.Message}", ex);
                // 回退到机器名
                return Environment.MachineName;
            }
        }

        /// <summary>
        /// 清除缓存的密钥（用于测试或密钥轮换）
        /// </summary>
        public static void ClearCache()
        {
            lock (_lock)
            {
                if (_cachedKey != null)
                {
                    Array.Clear(_cachedKey, 0, _cachedKey.Length);
                    _cachedKey = null;
                }
            }
        }
    }
}

