using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace MyWeChat.Windows.Core.Hook
{
    /// <summary>
    /// 微信版本检测器
    /// 自动检测已安装的微信版本，并返回对应的版本号
    /// </summary>
    public class WeChatVersionDetector
    {
        /// <summary>
        /// 支持的微信版本列表
        /// </summary>
        private static readonly string[] SupportedVersions = new[]
        {
            "4.1.0.34",
            "4.0.3.22",
            "4.0.1.21",
            "3.9.12.45",
            "3.9.12.15",
            "3.9.10.19",
            "3.9.7.28",
            "3.9.5.80"
        };

        /// <summary>
        /// 检测微信版本
        /// </summary>
        /// <returns>返回微信版本号，如果未找到返回null</returns>
        public static string? DetectWeChatVersion()
        {
            try
            {
                // 方法1: 从注册表获取微信版本（最可靠）
                string? version = GetVersionFromRegistry();
                if (!string.IsNullOrEmpty(version))
                {
                    Utils.Logger.LogInfo($"从注册表获取微信版本: {version}");
                    string? normalized = NormalizeVersion(version);
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        return normalized;
                    }
                }

                // 方法2: 从微信安装目录获取版本
                string? weChatPath = GetWeChatInstallPath();
                if (!string.IsNullOrEmpty(weChatPath))
                {
                    Utils.Logger.LogInfo($"找到微信安装路径: {weChatPath}");
                    
                    // 方法2.1: 从安装路径的子目录中检测版本（优先）
                    string? versionFromPath = GetVersionFromInstallPath(weChatPath);
                    if (!string.IsNullOrEmpty(versionFromPath))
                    {
                        Utils.Logger.LogInfo($"从安装路径子目录获取微信版本: {versionFromPath}");
                        string? normalized = NormalizeVersion(versionFromPath);
                        if (!string.IsNullOrEmpty(normalized))
                        {
                            return normalized;
                        }
                    }
                    
                    // 方法2.2: 从WeChat.exe文件版本信息获取
                    string exePath = Path.Combine(weChatPath, "WeChat.exe");
                    if (File.Exists(exePath))
                    {
                        try
                        {
                            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                            string? fileVersion = versionInfo.FileVersion;
                            Utils.Logger.LogInfo($"从WeChat.exe获取文件版本: {fileVersion}");
                            string? normalized = NormalizeVersion(fileVersion);
                            if (!string.IsNullOrEmpty(normalized))
                            {
                                return normalized;
                            }
                        }
                        catch (Exception ex)
                        {
                            Utils.Logger.LogWarning($"从WeChat.exe获取版本失败: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Utils.Logger.LogWarning("未找到微信安装路径");
                }

                // 方法3: 从运行中的进程获取版本（可能失败，因为架构不匹配）
                try
                {
                    version = GetVersionFromRunningProcess();
                    if (!string.IsNullOrEmpty(version))
                    {
                        Utils.Logger.LogInfo($"从进程获取微信版本: {version}");
                        string? normalized = NormalizeVersion(version);
                        if (!string.IsNullOrEmpty(normalized))
                        {
                            return normalized;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 忽略进程获取失败（可能是架构不匹配）
                    Utils.Logger.LogWarning($"从进程获取版本失败（可能架构不匹配）: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.LogError($"检测微信版本失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 从注册表获取微信版本
        /// 注意：64位应用访问注册表时，SOFTWARE\...访问64位视图，SOFTWARE\WOW6432Node\...访问32位视图
        /// 微信可能注册在CurrentUser或LocalMachine下
        /// </summary>
        private static string? GetVersionFromRegistry()
        {
            try
            {
                bool is64Bit = IntPtr.Size == 8;
                Utils.Logger.LogInfo($"应用程序架构: {(is64Bit ? "64位" : "32位")}");
                
                // 尝试多个注册表位置
                // 1. CurrentUser (用户级注册表)
                // 2. LocalMachine (系统级注册表)
                RegistryKey[] registryRoots = new[] { Registry.CurrentUser, Registry.LocalMachine };
                
                // 尝试多个路径
                string[] registryPaths = new[]
                {
                    @"Software\Tencent\Weixin",      // 旧版微信路径
                    @"Software\Tencent\WeChat",      // 新版微信路径
                    @"SOFTWARE\Tencent\WeChat",      // 大写路径
                    @"SOFTWARE\WOW6432Node\Tencent\WeChat",  // 32位视图
                    @"SOFTWARE\Tencent\WeChat"       // 64位视图
                };

                foreach (RegistryKey root in registryRoots)
                {
                    foreach (string registryPath in registryPaths)
                    {
                        try
                        {
                            string fullPath = $"{root.Name}\\{registryPath}";
                            Utils.Logger.LogInfo($"尝试访问注册表: {fullPath}");
                            
                            using (RegistryKey? key = root.OpenSubKey(registryPath))
                            {
                                if (key != null)
                                {
                                    Utils.Logger.LogInfo($"注册表键存在: {fullPath}");
                                    
                                    // 获取所有值名称（用于调试）
                                    string[] valueNames = key.GetValueNames();
                                    Utils.Logger.LogInfo($"注册表值数量: {valueNames.Length}");
                                    foreach (string valueName in valueNames)
                                    {
                                        object? value = key.GetValue(valueName);
                                        Utils.Logger.LogInfo($"  - {valueName} = {value}");
                                    }
                                    
                                    // 尝试获取Version值
                                    object? versionValue = key.GetValue("Version");
                                    if (versionValue != null)
                                    {
                                        string? version = versionValue?.ToString();
                                        Utils.Logger.LogInfo($"从注册表获取到版本: {version} (路径: {fullPath})");
                                        return version;
                                    }
                                    
                                    // 如果没有Version值，尝试从InstallPath获取版本
                                    object? installPathValue = key.GetValue("InstallPath");
                                    if (installPathValue != null)
                                    {
                                        string? installPath = installPathValue?.ToString();
                                        Utils.Logger.LogInfo($"找到安装路径: {installPath}");
                                        
                                        // 从安装路径的子目录中检测版本
                                        string? versionFromPath = GetVersionFromInstallPath(installPath);
                                        if (!string.IsNullOrEmpty(versionFromPath))
                                        {
                                            Utils.Logger.LogInfo($"从安装路径获取到版本: {versionFromPath}");
                                            return versionFromPath;
                                        }
                                    }
                                }
                                else
                                {
                                    Utils.Logger.LogInfo($"注册表键不存在: {fullPath}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Utils.Logger.LogWarning($"访问注册表路径失败: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.LogError($"从注册表读取微信版本失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 从安装路径的子目录中检测版本号
        /// 微信安装目录下通常有版本号命名的子目录，如 [4.1.0.34] 或 4.1.0.34
        /// </summary>
        private static string? GetVersionFromInstallPath(string? installPath)
        {
            try
            {
                if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                {
                    return null;
                }

                Utils.Logger.LogInfo($"检查安装路径下的子目录: {installPath}");
                string[] subDirs = Directory.GetDirectories(installPath);
                
                foreach (string subDir in subDirs)
                {
                    string dirName = Path.GetFileName(subDir);
                    Utils.Logger.LogInfo($"  检查子目录: {dirName}");
                    
                    // 方法1: 检查 [版本号] 格式（如 [4.1.0.34]）
                    if (dirName.StartsWith("[") && dirName.EndsWith("]"))
                    {
                        string version = dirName.Substring(1, dirName.Length - 2);
                        if (IsValidVersion(version))
                        {
                            Utils.Logger.LogInfo($"  找到版本目录（方括号格式）: {version}");
                            return version;
                        }
                    }
                    
                    // 方法2: 检查直接版本号格式（如 4.1.0.34）
                    if (IsValidVersion(dirName))
                    {
                        Utils.Logger.LogInfo($"  找到版本目录（直接格式）: {dirName}");
                        return dirName;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.LogWarning($"从安装路径检测版本失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 检查字符串是否为有效的版本号格式
        /// </summary>
        private static bool IsValidVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                return false;
            }

            // 检查是否符合版本号格式：x.x.x.x
            string[] parts = version.Split('.');
            if (parts.Length >= 3)
            {
                foreach (string part in parts)
                {
                    if (!int.TryParse(part, out _))
                    {
                        return false;
                    }
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取微信安装路径
        /// 注意：64位应用访问注册表时，SOFTWARE\...访问64位视图，SOFTWARE\WOW6432Node\...访问32位视图
        /// 微信可能注册在CurrentUser或LocalMachine下
        /// </summary>
        private static string? GetWeChatInstallPath()
        {
            try
            {
                // 尝试多个注册表位置
                RegistryKey[] registryRoots = new[] { Registry.CurrentUser, Registry.LocalMachine };
                
                // 尝试多个路径
                string[] registryPaths = new[]
                {
                    @"Software\Tencent\Weixin",      // 旧版微信路径
                    @"Software\Tencent\WeChat",      // 新版微信路径
                    @"SOFTWARE\Tencent\WeChat",      // 大写路径
                    @"SOFTWARE\WOW6432Node\Tencent\WeChat",  // 32位视图
                    @"SOFTWARE\Tencent\WeChat"       // 64位视图
                };

                foreach (RegistryKey root in registryRoots)
                {
                    foreach (string registryPath in registryPaths)
                    {
                        try
                        {
                            using (RegistryKey? key = root.OpenSubKey(registryPath))
                            {
                                if (key != null)
                                {
                                    object? installPath = key.GetValue("InstallPath");
                                    if (installPath != null)
                                    {
                                        string? path = installPath?.ToString();
                                        Utils.Logger.LogInfo($"从注册表获取到安装路径: {path} (路径: {root.Name}\\{registryPath})");
                                        return path;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Utils.Logger.LogWarning($"访问注册表路径失败: {ex.Message}");
                        }
                    }
                }
                
                // 如果注册表找不到，尝试默认路径
                Utils.Logger.LogInfo("注册表中未找到安装路径，尝试默认路径...");
                string[] defaultPaths = new[]
                {
                    @"C:\Program Files\Tencent\WeChat",
                    @"C:\Program Files (x86)\Tencent\WeChat",
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Tencent\WeChat",
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Tencent\WeChat"
                };

                foreach (string path in defaultPaths)
                {
                    if (Directory.Exists(path))
                    {
                        string exePath = Path.Combine(path, "WeChat.exe");
                        if (File.Exists(exePath))
                        {
                            Utils.Logger.LogInfo($"从默认路径找到微信: {path}");
                            return path;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.LogError($"从注册表读取微信安装路径失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 从运行中的进程获取版本
        /// </summary>
        private static string? GetVersionFromRunningProcess()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("WeChat");
                if (processes.Length > 0)
                {
                    // 尝试从进程的可执行文件路径获取版本
                    // 注意：如果架构不匹配（32位应用访问64位进程），MainModule会失败
                    try
                    {
                        string? exePath = processes[0].MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                            return versionInfo.FileVersion;
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // 架构不匹配，无法访问进程模块
                        // 尝试从进程的StartInfo获取路径
                        try
                        {
                            string? exePath = processes[0].StartInfo?.FileName;
                            if (string.IsNullOrEmpty(exePath))
                            {
                                // 如果StartInfo没有，尝试从进程名推断路径
                                exePath = Path.Combine(GetWeChatInstallPath() ?? "", "WeChat.exe");
                            }
                            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                            {
                                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                                return versionInfo.FileVersion;
                            }
                        }
                        catch
                        {
                            // 忽略所有错误
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 不记录错误，因为这是可选的检测方法
                Utils.Logger.LogWarning($"从进程获取微信版本失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 标准化版本号，匹配支持的版本
        /// 如果找不到精确匹配，会自动选择最接近的版本
        /// </summary>
        private static string? NormalizeVersion(string? version)
        {
            if (string.IsNullOrEmpty(version))
            {
                return null;
            }

            // 方法1: 精确匹配
            foreach (string supportedVersion in SupportedVersions)
            {
                if (version == supportedVersion || (version != null && version.StartsWith(supportedVersion + ".")))
                {
                    return supportedVersion;
                }
            }

            // 方法2: 提取前三个数字进行匹配（如3.9.12.55匹配到3.9.12.45）
            if (string.IsNullOrEmpty(version))
            {
                return null;
            }
            string[] parts = version.Split('.');
            if (parts.Length >= 3)
            {
                string normalized = $"{parts[0]}.{parts[1]}.{parts[2]}";
                string? bestMatch = null;
                
                foreach (string supportedVersion in SupportedVersions)
                {
                    if (supportedVersion.StartsWith(normalized))
                    {
                        // 选择最接近的版本（版本号最大的）
                        if (bestMatch == null || String.Compare(supportedVersion, bestMatch) > 0)
                        {
                            bestMatch = supportedVersion;
                        }
                    }
                }
                
                if (bestMatch != null)
                {
                    return bestMatch;
                }
            }

            // 方法3: 如果还是无法匹配，尝试查找最接近的版本
            return FindClosestVersion(version);
        }

        /// <summary>
        /// 查找最接近的版本
        /// 例如：3.9.12.55 -> 3.9.12.45
        /// </summary>
        private static string? FindClosestVersion(string? version)
        {
            if (string.IsNullOrEmpty(version))
            {
                return null;
            }

            string[] parts = version.Split('.');
            if (parts.Length < 3)
            {
                return null;
            }

            int major = int.TryParse(parts[0], out int m) ? m : 0;
            int minor = int.TryParse(parts[1], out int n) ? n : 0;
            int patch = int.TryParse(parts[2], out int p) ? p : 0;

            string? bestMatch = null;
            int bestScore = int.MaxValue;

            foreach (string supportedVersion in SupportedVersions)
            {
                string[] svParts = supportedVersion.Split('.');
                if (svParts.Length < 3)
                {
                    continue;
                }

                int svMajor = int.TryParse(svParts[0], out int svm) ? svm : 0;
                int svMinor = int.TryParse(svParts[1], out int svn) ? svn : 0;
                int svPatch = int.TryParse(svParts[2], out int svp) ? svp : 0;

                // 计算版本差异分数（越小越好）
                int score = Math.Abs(svMajor - major) * 10000 + 
                           Math.Abs(svMinor - minor) * 100 + 
                           Math.Abs(svPatch - patch);

                // 只考虑主版本号匹配或接近的版本
                if (svMajor == major && (svMinor == minor || Math.Abs(svMinor - minor) <= 1))
                {
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestMatch = supportedVersion;
                    }
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// 检查版本是否支持
        /// 如果找不到精确匹配，会尝试查找最接近的版本
        /// </summary>
        public static bool IsVersionSupported(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                return false;
            }

            // 精确匹配
            foreach (string supportedVersion in SupportedVersions)
            {
                if (version == supportedVersion)
                {
                    return true;
                }
            }

            // 尝试查找最接近的版本
            string? closestVersion = FindClosestVersion(version);
            return !string.IsNullOrEmpty(closestVersion);
        }

        /// <summary>
        /// 获取版本对应的DLL目录路径
        /// 如果找不到精确匹配，会自动选择最接近的版本
        /// </summary>
        public static string? GetDllDirectoryPath(string? version)
        {
            if (string.IsNullOrEmpty(version))
            {
                return null;
            }

            // 先尝试精确匹配
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DLLs");
            string exactPath = Path.Combine(basePath, version);
            if (Directory.Exists(exactPath))
            {
                return exactPath;
            }

            // 如果精确匹配不存在，查找最接近的版本
            string? normalizedVersion = NormalizeVersion(version);
            if (!string.IsNullOrEmpty(normalizedVersion))
            {
                string normalizedPath = Path.Combine(basePath, normalizedVersion);
                if (Directory.Exists(normalizedPath))
                {
                    Utils.Logger.LogInfo($"微信版本 {version} 未找到精确匹配，使用最接近的版本 {normalizedVersion}");
                    return normalizedPath;
                }
            }

            return null;
        }
    }
}

