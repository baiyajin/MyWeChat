using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace uniapp
{
    class Program
    {
        /// <summary>
        /// 从文件加载进程名称列表
        /// </summary>
        private static List<string> LoadProcessNamesFromFile(string filePath)
        {
            List<string> processNames = new List<string>();
            
            try
            {
                if (!File.Exists(filePath))
                {
                    return processNames;
                }
                
                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine))
                    {
                        // 处理进程名称：移除或替换非法字符
                        string sanitizedName = SanitizeProcessName(trimmedLine);
                        if (!string.IsNullOrEmpty(sanitizedName))
                        {
                            processNames.Add(sanitizedName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取进程名称文件失败: {ex.Message}");
            }
            
            return processNames;
        }
        
        /// <summary>
        /// 清理进程名称，使其符合Windows文件名规范
        /// </summary>
        private static string SanitizeProcessName(string processName)
        {
            if (string.IsNullOrEmpty(processName))
            {
                return string.Empty;
            }
            
            // 替换空格为下划线
            string sanitized = processName.Replace(" ", "_");
            
            // 移除Windows文件名非法字符: < > : " / \ | ? *
            char[] invalidChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
            foreach (char invalidChar in invalidChars)
            {
                sanitized = sanitized.Replace(invalidChar.ToString(), "");
            }
            
            // 移除控制字符
            sanitized = new string(sanitized.Where(c => !char.IsControl(c)).ToArray());
            
            // 确保不为空且长度合理
            if (string.IsNullOrEmpty(sanitized) || sanitized.Length > 200)
            {
                return string.Empty;
            }
            
            return sanitized;
        }
        
        /// <summary>
        /// 生成随机EXE文件名（从文件读取或生成随机字符串）
        /// </summary>
        private static string GetRandomExeName(string launcherDir)
        {
            // 尝试从文件读取进程名称
            string processNamesFile = System.IO.Path.Combine(launcherDir, "process_names.txt");
            List<string> processNames = LoadProcessNamesFromFile(processNamesFile);
            
            if (processNames.Count > 0)
            {
                // 从文件中的进程名称随机选择一个
                Random random = new Random();
                int index = random.Next(processNames.Count);
                string selectedName = processNames[index];
                return selectedName + ".exe";
            }
            
            // 如果文件读取失败或为空，回退到生成随机字符串
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            Random fallbackRandom = new Random();
            char[] name = new char[12];
            for (int i = 0; i < 12; i++)
            {
                name[i] = chars[fallbackRandom.Next(chars.Length)];
            }
            return new string(name) + ".exe";
        }

        static void Main(string[] args)
        {
            try
            {
                // 获取启动器所在目录
                string launcherDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // 主程序exe路径（编译后的固定名称）
                string sourceExePath = System.IO.Path.Combine(launcherDir, "app.exe");
                
                if (!File.Exists(sourceExePath))
                {
                    Console.WriteLine($"错误: 找不到主程序: {sourceExePath}");
                    Console.WriteLine("按任意键退出...");
                    Console.ReadKey();
                    return;
                }
                
                // 生成随机文件名并复制主程序（反检测措施）
                string randomExeName = GetRandomExeName(launcherDir);
                string randomExePath = System.IO.Path.Combine(launcherDir, randomExeName);
                
                try
                {
                    // 如果随机名称的文件已存在，先删除（可能是上次启动留下的）
                    if (File.Exists(randomExePath))
                    {
                        File.Delete(randomExePath);
                    }
                    
                    // 复制主程序为随机名称
                    File.Copy(sourceExePath, randomExePath, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"复制主程序为随机文件名失败: {ex.Message}，将使用原始文件名");
                    randomExePath = sourceExePath; // 回退到原始路径
                }
                
                // 启动主程序（使用随机名称）
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = randomExePath,
                    WorkingDirectory = launcherDir,
                    UseShellExecute = true,
                    Verb = "runas" // 请求管理员权限
                };
                
                // 传递命令行参数
                if (args.Length > 0)
                {
                    startInfo.Arguments = string.Join(" ", args);
                }
                
                Process? process = Process.Start(startInfo);
                
                // 如果启动成功且使用了随机名称，不删除临时文件（保持进程名称为随机名称）
                // 注意：删除临时文件会导致进程名称变回原始名称，影响反检测效果
                // 临时文件将在进程退出后由系统自动清理，或由用户手动清理
            }
            catch (Exception ex)
            {
                Console.WriteLine($"启动失败: {ex.Message}");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
        }
    }
}

