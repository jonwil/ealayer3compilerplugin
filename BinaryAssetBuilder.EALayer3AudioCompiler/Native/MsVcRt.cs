using System;
using System.Runtime.InteropServices;

namespace Native
{
    public static class MsVcRt
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr MemSetDelegate(IntPtr ptr, byte value, SizeT count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int MemICmpDelegate([In] IntPtr buf1, [In] IntPtr buf2, SizeT size);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr MemCpyDelegate(IntPtr dest, [In] IntPtr src, SizeT count);
        [UnmanagedFunctionPointer(CallingConvention.FastCall)]
        public delegate SizeT StrLenDelegate([In] IntPtr str);

        private const string _moduleName = "msvcrt.dll";

        private static readonly IntPtr _hModule;

        public static readonly MemSetDelegate MemSet;
        public static readonly MemICmpDelegate MemICmp;
        public static readonly MemCpyDelegate MemCpy;

        static MsVcRt()
        {
            _hModule = NativeLibrary.Load(_moduleName);
            MemSet = (MemSetDelegate)Marshal.GetDelegateForFunctionPointer(NativeLibrary.GetExport(_hModule, "memset"), typeof(MemSetDelegate));
            MemICmp = (MemICmpDelegate)Marshal.GetDelegateForFunctionPointer(NativeLibrary.GetExport(_hModule, "_memicmp"), typeof(MemICmpDelegate));
            MemCpy = (MemCpyDelegate)Marshal.GetDelegateForFunctionPointer(NativeLibrary.GetExport(_hModule, "memcpy"), typeof(MemCpyDelegate));
        }
    }
}
