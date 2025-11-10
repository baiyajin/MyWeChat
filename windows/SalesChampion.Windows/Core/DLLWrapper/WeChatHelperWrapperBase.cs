using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MyWeChat.Windows.Core.DLLWrapper
{
    /// <summary>
    /// 微信帮助DLL封装基类
    /// 定义统一的接口，各版本实现具体调用
    /// </summary>
    public abstract class WeChatHelperWrapperBase
    {
        protected readonly string _version;

        // 回调函数委托定义
        // 注意：必须使用UnmanagedFunctionPointer属性，明确指定调用约定为StdCall
        // 这样可以确保与DLL的调用约定匹配，避免栈溢出错误
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void AcceptCallback(int clientId);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void ReceiveCallback(int clientId, IntPtr message, int length);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void CloseCallback(int clientId);

        /// <summary>
        /// 构造函数
        /// </summary>
        protected WeChatHelperWrapperBase(string version)
        {
            _version = version;
        }

        /// <summary>
        /// 设置回调函数
        /// </summary>
        public abstract int SetCallback(IntPtr acceptPtr, IntPtr receivePtr, IntPtr closePtr, string? contact = null);

        /// <summary>
        /// 打开微信互斥锁
        /// </summary>
        public abstract int OpenWeChatMutex(string exePath);

        /// <summary>
        /// 注入到微信进程
        /// </summary>
        public abstract int InjectWeChatProcess(int pid);

        /// <summary>
        /// 发送数据
        /// </summary>
        public abstract bool SendData(int clientId, IntPtr msgPtr);

        /// <summary>
        /// 检查是否使用UTF-8
        /// </summary>
        public abstract bool IsContentUtf8();

        /// <summary>
        /// 关闭微信连接
        /// </summary>
        public abstract bool CloseWeChat();

        /// <summary>
        /// 发送字符串数据（封装方法）
        /// </summary>
        public bool SendStringData(int clientId, string message)
        {
            IntPtr msgPtr = ConvertToUtf8(message);
            try
            {
                return SendData(clientId, msgPtr);
            }
            finally
            {
                // 释放内存
                System.Runtime.InteropServices.Marshal.FreeHGlobal(msgPtr);
            }
        }

        /// <summary>
        /// 将字符串转换为UTF-8编码的IntPtr
        /// </summary>
        protected IntPtr ConvertToUtf8(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return IntPtr.Zero;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(str);
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(bytes.Length + 1);
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, ptr, bytes.Length);
            System.Runtime.InteropServices.Marshal.WriteByte(ptr, bytes.Length, 0);
            return ptr;
        }
    }
}

