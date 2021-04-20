using System.Runtime.InteropServices;

namespace SageBinaryData
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct AudioFileRuntime
    {
        public unsafe void* VTable;
        public AnsiString SubtitleStringName;
        public int NumberOfSamples;
        public int SampleRate;
        public unsafe void* HeaderData;
        public int HeaderDataSize;
        public int NumberOfChannels;
    }
}
