using System;
using System.Diagnostics;
using System.IO;

namespace uniapp
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // 获取启动器所在目录
                string launcherDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // 主程序exe路径（与启动器同目录）
                string mainExePath = Path.Combine(launcherDir, "MyWeChat.Windows.exe");
                
                if (!File.Exists(mainExePath))
                {
                    Console.WriteLine($"错误: 找不到主程序: {mainExePath}");
                    Console.WriteLine("按任意键退出...");
                    Console.ReadKey();
                    return;
                }
                
                // 启动主程序
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = mainExePath,
                    WorkingDirectory = launcherDir,
                    UseShellExecute = true,
                    Verb = "runas" // 请求管理员权限
                };
                
                // 传递命令行参数
                if (args.Length > 0)
                {
                    startInfo.Arguments = string.Join(" ", args);
                }
                
                Process.Start(startInfo);
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

