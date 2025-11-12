using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MyWeChat.Windows.Core.DLLWrapper
{
    /// <summary>
    /// 微信帮助DLL封装类 - 版本3.9.12.45
    /// 封装WxHelp.dll的调用接口（使用动态加载）
    /// </summary>
    public class WeChatHelperWrapper_3_9_12_45 : WeChatHelperWrapperBase
    {
        // 函数委托定义（使用StdCall调用约定）
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate int SetCBDelegate(IntPtr cba, IntPtr cbr, IntPtr cbc, string contact);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate int OpenWechatMutexTwoDelegate(string exePath);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int InjectWeChatPidDelegate(int pid);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool SendHpSocketDataDelegate(int clientId, IntPtr msg);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool ContentUseUtf8Delegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool CloseWeChatDelegate();

        // 委托实例
        private SetCBDelegate? _setCB;
        private OpenWechatMutexTwoDelegate? _openWechatMutexTwo;
        private InjectWeChatPidDelegate? _injectWeChatPid;
        private SendHpSocketDataDelegate? _sendHpSocketData;
        private ContentUseUtf8Delegate? _contentUseUtf8;
        private CloseWeChatDelegate? _closeWeChat;

        // Windows API: 设置DLL搜索路径（保留，因为这是Windows API，不是WxHelp.dll的函数）
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// 构造函数
        /// </summary>
        public WeChatHelperWrapper_3_9_12_45() : base("3.9.12.45")
        {
        }

        /// <summary>
        /// 初始化动态DLL加载器
        /// </summary>
        public override void InitializeDynamicLoader(string dllPath)
        {
            base.InitializeDynamicLoader(dllPath);
            
            if (_dllLoader == null)
                throw new InvalidOperationException("DLL加载器未初始化");

            // 加载所有函数
            _setCB = _dllLoader.GetFunction<SetCBDelegate>("SetCB");
            _openWechatMutexTwo = _dllLoader.GetFunction<OpenWechatMutexTwoDelegate>("openWechatMutexTwo");
            _injectWeChatPid = _dllLoader.GetFunction<InjectWeChatPidDelegate>("InjectWeChatPid");
            _sendHpSocketData = _dllLoader.GetFunction<SendHpSocketDataDelegate>("sendHpSocketData");
            _contentUseUtf8 = _dllLoader.GetFunction<ContentUseUtf8Delegate>("ContentUseUtf8");
            _closeWeChat = _dllLoader.GetFunction<CloseWeChatDelegate>("closeWeChat");
        }

        #region 重写基类方法

        public override int SetCallback(IntPtr acceptPtr, IntPtr receivePtr, IntPtr closePtr, string? contact = null)
        {
            if (_setCB == null)
                throw new InvalidOperationException("DLL函数未初始化，请先调用InitializeDynamicLoader");
            return _setCB(acceptPtr, receivePtr, closePtr, contact ?? "");
        }

        public override int OpenWeChatMutex(string exePath)
        {
            if (_openWechatMutexTwo == null)
                throw new InvalidOperationException("DLL函数未初始化，请先调用InitializeDynamicLoader");
            return _openWechatMutexTwo(exePath);
        }

        public override int InjectWeChatProcess(int pid)
        {
            if (_injectWeChatPid == null)
                throw new InvalidOperationException("DLL函数未初始化，请先调用InitializeDynamicLoader");
            return _injectWeChatPid(pid);
        }

        public override bool SendData(int clientId, IntPtr msgPtr)
        {
            if (_sendHpSocketData == null)
                throw new InvalidOperationException("DLL函数未初始化，请先调用InitializeDynamicLoader");
            return _sendHpSocketData(clientId, msgPtr);
        }

        public override bool IsContentUtf8()
        {
            if (_contentUseUtf8 == null)
                throw new InvalidOperationException("DLL函数未初始化，请先调用InitializeDynamicLoader");
            return _contentUseUtf8();
        }

        public override bool CloseWeChat()
        {
            if (_closeWeChat == null)
                throw new InvalidOperationException("DLL函数未初始化，请先调用InitializeDynamicLoader");
            return _closeWeChat();
        }

        #endregion
    }
}

