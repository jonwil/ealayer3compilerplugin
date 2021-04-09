using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct AnsiString
{
    public int Length;
    public unsafe sbyte* Target;
}
