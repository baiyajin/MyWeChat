using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MyWeChat.Windows.Core.DLLWrapper
{
    /// <summary>
    /// 微信帮助DLL封装类 - 版本3.9.12.45
    /// 封装WxHelp.dll的调用接口
    /// </summary>
    public class WeChatHelperWrapper_3_9_12_45 : WeChatHelperWrapperBase
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public WeChatHelperWrapper_3_9_12_45() : base("3.9.12.45")
        {
        }

        #region DLL导入声明

        // 使用相对路径，通过SetDllDirectory或PATH环境变量指定搜索路径
        // 注意：原项目未指定CallingConvention，使用默认的Winapi（StdCall）
        [DllImport("WxHelp.dll")]
        public static extern int SetCB(IntPtr cba, IntPtr cbr, IntPtr cbc, string contact);

        [DllImport("WxHelp.dll")]
        public static extern int openWechatMutexTwo(string exePath);

        [DllImport("WxHelp.dll")]
        public static extern int InjectWeChatPid(int pid);

        [DllImport("WxHelp.dll")]
        public static extern bool sendHpSocketData(int clientId, IntPtr msg);

        [DllImport("WxHelp.dll")]
        public static extern bool ContentUseUtf8();

        [DllImport("WxHelp.dll")]
        public static extern bool closeWeChat();

        // Windows API: 设置DLL搜索路径
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);

        #endregion

        #region 重写基类方法

        public override int SetCallback(IntPtr acceptPtr, IntPtr receivePtr, IntPtr closePtr, string? contact = null)
        {
            return SetCB(acceptPtr, receivePtr, closePtr, contact ?? "");
        }

        public override int OpenWeChatMutex(string exePath)
        {
            return openWechatMutexTwo(exePath);
        }

        public override int InjectWeChatProcess(int pid)
        {
            return InjectWeChatPid(pid);
        }

        public override bool SendData(int clientId, IntPtr msgPtr)
        {
            return sendHpSocketData(clientId, msgPtr);
        }

        public override bool IsContentUtf8()
        {
            return ContentUseUtf8();
        }

        public override bool CloseWeChat()
        {
            return closeWeChat();
        }

        #endregion
    }
}

