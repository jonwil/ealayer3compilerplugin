using System;

namespace Native
{
    public static class NativeLibrary
    {
        public static IntPtr Load(string libraryPath)
        {
            return Kernel32.Load(libraryPath);
        }

        public static IntPtr GetExport(IntPtr handle, string name)
        {
            return Kernel32.GetExport(handle, name);
        }

        public static bool Close(IntPtr handle)
        {
            return Kernel32.Close(handle);
        }
    }
}
