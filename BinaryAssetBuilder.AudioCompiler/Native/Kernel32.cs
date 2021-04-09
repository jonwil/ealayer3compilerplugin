using System;
using System.Runtime.InteropServices;

namespace Native
{
    internal static partial class Kernel32
    {
        private const string _moduleName = "kernel32.dll";

        [DllImport(_moduleName, EntryPoint = "LoadLibraryA")] internal static extern IntPtr Load(string lpLibFileName);
        [DllImport(_moduleName, EntryPoint = "GetProcAddress")] internal static extern IntPtr GetExport(IntPtr hModule, string lpProcName);
        [DllImport(_moduleName, EntryPoint = "FreeLibrary")] internal static extern bool Close(IntPtr hLibModule);
    }
}
