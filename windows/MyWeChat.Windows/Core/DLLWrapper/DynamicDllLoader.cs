using System;
using System.Runtime.InteropServices;

namespace MyWeChat.Windows.Core.DLLWrapper
{
    /// <summary>
    /// 动态DLL加载器
    /// 使用LoadLibrary和GetProcAddress动态加载DLL函数
    /// </summary>
    public class DynamicDllLoader : IDisposable
    {
        private IntPtr _hModule = IntPtr.Zero;
        private readonly string _dllPath;
        private bool _disposed = false;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        public DynamicDllLoader(string dllPath)
        {
            _dllPath = dllPath ?? throw new ArgumentNullException(nameof(dllPath));
            _hModule = LoadLibrary(dllPath);
            if (_hModule == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new DllNotFoundException($"无法加载DLL: {dllPath}, 错误码: {errorCode}");
            }
        }

        /// <summary>
        /// 获取DLL中的函数地址并转换为委托
        /// </summary>
        public TDelegate GetFunction<TDelegate>(string functionName) where TDelegate : Delegate
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DynamicDllLoader));

            IntPtr procAddress = GetProcAddress(_hModule, functionName);
            if (procAddress == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new EntryPointNotFoundException($"无法找到函数: {functionName}, 错误码: {errorCode}");
            }

            return Marshal.GetDelegateForFunctionPointer<TDelegate>(procAddress);
        }

        public bool IsLoaded => _hModule != IntPtr.Zero && !_disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_hModule != IntPtr.Zero)
                {
                    FreeLibrary(_hModule);
                    _hModule = IntPtr.Zero;
                }
                _disposed = true;
            }
        }
    }
}

