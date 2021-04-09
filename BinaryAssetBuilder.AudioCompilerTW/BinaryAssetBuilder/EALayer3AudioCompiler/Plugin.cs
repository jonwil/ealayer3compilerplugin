using BinaryAssetBuilder.Core;
using Relo;
using SageBinaryData;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace BinaryAssetBuilder.EALayer3AudioCompiler
{
    public class Plugin : IAssetBuilderPlugin, IDisposable
    {
        private unsafe struct SINSTANCE { };
        private unsafe struct SINFO { };

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int SIMEX_id(string filename, long offset);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int SIMEX_open(string filename, long fileoffset, int filetype, SINSTANCE** instance);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int SIMEX_create(string filename, uint filetype, SINSTANCE** instance);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int SIMEX_info(SINSTANCE* instance, SINFO** info, int element);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int SIMEX_read(SINSTANCE* instance, SINFO* info, int element);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern string SIMEX_getsamplerepname(int codec);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void SIMEX_resample(SINFO* info, int sample_rate);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void SIMEX_init();

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void SIMEX_shutdown();

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void SIMEX_setplayloc(SINFO* info, int playloc);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void SIMEX_setcodec(SINFO* info, int codec);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void SIMEX_setvbrquality(SINFO* info, int quality);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int SIMEX_getsamplerate(SINFO* info);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int SIMEX_getchannelconfig(SINFO* info);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int SIMEX_getnumsamples(SINFO* info);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int SIMEX_write(SINSTANCE* instance, SINFO* info, int element);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int SIMEX_close(SINSTANCE* inst);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int SIMEX_wclose(SINSTANCE* instance);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int SIMEX_freesinfo(SINFO* info);

        [DllImport("audio.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern string SIMEX_getlasterr();

        private unsafe class AutoSINSTANCECloser : IDisposable
        {
            SINSTANCE* instance;
            public AutoSINSTANCECloser(SINSTANCE* i)
            {
                instance = i;
            }
            public void Dispose()
            {
                if (instance != null)
                {
                    SIMEX_close(instance);
                }
            }
        }

        private unsafe class AutoSINSTANCEWCloser : IDisposable
        {
            SINSTANCE* instance;
            public AutoSINSTANCEWCloser(SINSTANCE* i)
            {
                instance = i;
            }
            public void Dispose()
            {
                if (instance != null)
                {
                    SIMEX_wclose(instance);
                }
            }
        }

        private unsafe class AutoSINFOFreer : IDisposable
        {
            SINFO* info;
            public AutoSINFOFreer(SINFO* i)
            {
                info = i;
            }
            public void Dispose()
            {
                if (info != null)
                {
                    SIMEX_freesinfo(info);
                }
            }
        }

        private static readonly string _tempFilenameSuffix = "BinaryAssetBuilder.AudioCompiler.tempfile";
        private static readonly uint _realAudioPluginVersion = 1010;
        private static readonly uint _effectiveAudioPluginVersion = _realAudioPluginVersion + 1000000;
        private static readonly uint _effectiveAudioPluginVersion360 = _effectiveAudioPluginVersion + 2000000;

        private readonly Tracer _tracer = Tracer.GetTracer(nameof(EALayer3AudioCompiler), "Provides Audio processing functionality");
        private uint _hashCode = 0u;
        private TargetPlatform _platform = TargetPlatform.Win32;

        private unsafe void FinalizeTracker(Tracker tracker, AssetBuffer buffer)
        {
            Relo.Chunk chunk = new Relo.Chunk();
            tracker.MakeRelocatable(chunk);
            buffer.InstanceData = chunk.InstanceBuffer;
            if (chunk.RelocationBuffer.Length > 0)
            {
                buffer.RelocationData = chunk.RelocationBuffer;
            }
            else
            {
                buffer.RelocationData = new byte[0];
            }
            if (chunk.ImportsBuffer.Length > 0)
            {
                buffer.ImportsData = chunk.ImportsBuffer;
            }
            else
            {
                buffer.ImportsData = new byte[0];
            }
        }

        private unsafe AssetBuffer ProcessAudioFileInstance(InstanceDeclaration declaration)
        {
            AssetBuffer result = new AssetBuffer();
            bool isBigEndian = _platform != TargetPlatform.Win32;
            Node node = new Node(declaration.Node.CreateNavigator(), declaration.Document.NamespaceManager);
            AudioFileRuntime* audioFileRuntime;
            using (Tracker tracker = new Tracker((void**)&audioFileRuntime, (uint)sizeof(AudioFileRuntime), isBigEndian))
            {
                AudioFile audioFile = new AudioFile();
                audioFile.Marshal(node);
                string file = declaration.XmlNode.Attributes["File"].Value;
                bool isSound = Path.GetDirectoryName(file).ToLower().EndsWith("\\sounds");
                int codec;
                PCAudioCompressionSetting compression = audioFile.PCCompression ?? (isSound ? PCAudioCompressionSetting.XAS : PCAudioCompressionSetting.NONE);
                switch (compression)
                {
                    case PCAudioCompressionSetting.NONE: // would become '1'
                        codec = 1;
                        break;
                    case PCAudioCompressionSetting.XAS: // would become '29', xbox XMA would be '28'
                        codec = 29;
                        break;
                    case PCAudioCompressionSetting.EALAYER3: // would become '31', '30' in TW (-ealayer3_int)
                        codec = 30;
                        break;
                    default:
                        throw new BinaryAssetBuilderException(ErrorCode.InternalError, "Internal error: xml compiler returned bad PC audio compression type of {0}", compression);
                }
                bool isStreamed = audioFile.IsStreamedOnPC ?? !isSound;
                if (audioFile.SubtitleStringName is null)
                {
                    audioFile.SubtitleStringName = $"DIALOGEVENT:{Path.GetFileNameWithoutExtension(file)}SubTitle";
                }
                Marshaler.Marshal(audioFile.SubtitleStringName, &audioFileRuntime->SubtitleStringName, tracker);
                int type = SIMEX_id(file, 0);
                if (type < 0)
                {
                    Console.WriteLine("Warning: Unable to identify format of \"{0}\"; cannot process. (Error: {1})", file, SIMEX_getlasterr());
                    return null;
                }
                if (type != 1)
                {
                    Console.WriteLine("Warning: Input files must be WAVE format. Cannot process \"{0}\"", file);
                    return null;
                }
                SINSTANCE* instance = null;
                int count = SIMEX_open(file, 0, type, &instance);
                using (AutoSINSTANCECloser closer = new AutoSINSTANCECloser(instance))
                {
                    if (count <= 0 || instance == null)
                    {
                        Console.WriteLine("Warning: Could not begin audio processing of \"{0}\": {1}.", file, SIMEX_getlasterr());
                        return null;
                    }
                    string tempFile = declaration.CustomDataPath + _tempFilenameSuffix;
                    using (AutoCleanUpTempFiles tempFiles = new AutoCleanUpTempFiles(tempFile))
                    {
                        SINSTANCE* instance2 = null;
                        if (SIMEX_create(tempFile, 39, &instance2) == 0)
                        {
                            throw new BinaryAssetBuilderException(ErrorCode.InternalError, "Internal error preparing audio output file \"{0}\" (SIMEX_create(): {1}).", tempFile, SIMEX_getlasterr());
                        }
                        using (AutoSINSTANCEWCloser closer2 = new AutoSINSTANCEWCloser(instance2))
                        {
                            for (int i = 0; i < count; i++)
                            {
                                SINFO* info = null;
                                if (SIMEX_info(instance, &info, i) == 0)
                                {
                                    throw new BinaryAssetBuilderException(ErrorCode.InternalError, "Internal error reading element {0} of \"{1}\" (SIMEX_info(): {2}).", file, SIMEX_getlasterr());
                                }
                                using (AutoSINFOFreer freer = new AutoSINFOFreer(info))
                                {
                                    if (info != null)
                                    {
                                        if (SIMEX_read(instance, info, i) == 0)
                                        {
                                            throw new BinaryAssetBuilderException(ErrorCode.InternalError, "Internal error reading element {0} of \"{1}\" (SIMEX_read(): {2}).", file, SIMEX_getlasterr());
                                        }
                                        if (isStreamed)
                                        {
                                            SIMEX_setplayloc(info, 4096);
                                            _tracer.TraceNote("Setting play location to streamed");
                                        }
                                        else
                                        {
                                            SIMEX_setplayloc(info, 2048);
                                            _tracer.TraceNote("Setting play location to RAM");
                                        }
                                        SIMEX_setcodec(info, codec);
                                        _tracer.TraceNote("Setting compression type to {0}. ", SIMEX_getsamplerepname(codec));
                                    }
                                    if (codec == 30 || codec == 38)
                                    {
                                        int quality = audioFile.PCQuality;
                                        if (quality < 0 || quality > 100)
                                        {
                                            throw new BinaryAssetBuilderException(ErrorCode.InternalError, "Audio file {0}: Quality parameter must be between 0 and 100", file);
                                        }
                                        SIMEX_setvbrquality(info, quality);
                                        _tracer.TraceNote("Setting compression quality to {0}", quality);
                                    }
                                    if (audioFile.PCSampleRate.HasValue)
                                    {
                                        int oldrate = SIMEX_getsamplerate(info);
                                        int newrate = audioFile.PCSampleRate.Value;
                                        if (oldrate != newrate)
                                        {
                                            if (newrate < 400 || newrate > 96000)
                                                throw new BinaryAssetBuilderException(ErrorCode.InternalError, "Audio file {0}: Sample rate must be between 400 and 96000", file);
                                            _tracer.TraceNote("Resampling from {0} to {1} ", oldrate, newrate);
                                            SIMEX_resample(info, newrate);
                                            int rate = SIMEX_getsamplerate(info);
                                            if (newrate != rate)
                                            {
                                                Console.WriteLine("Warning: Downsampling of audio file {0} not completely sucessful. Wanted final sample of {1}Hz but got {2}Hz", file, newrate, rate);
                                            }
                                        }
                                    }
                                    audioFileRuntime->NumberOfChannels = SIMEX_getchannelconfig(info);
                                    audioFileRuntime->NumberOfSamples = SIMEX_getnumsamples(info);
                                    audioFileRuntime->SampleRate = SIMEX_getsamplerate(info);
                                    if (audioFileRuntime->NumberOfChannels != 1 && audioFileRuntime->NumberOfChannels != 2 && audioFileRuntime->NumberOfChannels != 4 && audioFileRuntime->NumberOfChannels != 6)
                                    {
                                        Console.WriteLine("Warning: Audio file {0} has {1} channels. The only supported channel counts are 1, 2, 4, and 6; sample will probably use only the first channel in the engine", file, audioFileRuntime->NumberOfChannels);
                                    }
                                    if (SIMEX_write(instance2, info, i) == 0)
                                    {
                                        throw new BinaryAssetBuilderException(ErrorCode.InternalError, "Internal error writing element {0} of \"{1}\" (SIMEX_write(): {2}).", i, tempFile, SIMEX_getlasterr());
                                    }
                                }
                            }
                        }
                    }
                    string snr = tempFile + ".snr";
                    byte[] buffer;
                    using (Stream stream = File.Open(snr, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        buffer = new byte[stream.Length];
                        stream.Read(buffer, 0, buffer.Length);
                    }
                    if (isStreamed)
                    {
                        string sns = tempFile + ".sns";
                        if (File.Exists(declaration.CustomDataPath))
                        {
                            File.Delete(declaration.CustomDataPath);
                        }
                        _tracer.TraceNote("Creating output file {0}\n", declaration.CustomDataPath);
                        File.Move(sns, declaration.CustomDataPath);
                        audioFileRuntime->HeaderDataSize = buffer.Length;
                        fixed (byte* pBuffer = &buffer[0])
                        {
                            using (Tracker.Context context = tracker.Push(&audioFileRuntime->HeaderData, 1u, (uint)buffer.Length))
                            {
                                Native.MsVcRt.MemCpy(new IntPtr(audioFileRuntime->HeaderData), new IntPtr(pBuffer), new Native.SizeT(buffer.Length));
                            }
                        }
                    }
                    else
                    {
                        if (File.Exists(declaration.CustomDataPath))
                        {
                            File.Delete(declaration.CustomDataPath);
                        }
                        _tracer.TraceNote("Creating output file {0}\n", declaration.CustomDataPath);
                        File.Move(snr, declaration.CustomDataPath);
                    }
                    FinalizeTracker(tracker, result);
                }
            }
            return result;
        }

        public void Initialize(object configObject, TargetPlatform platform)
        {
            SIMEX_init();
            _hashCode = HashProvider.GetTypeHash(GetType());
            _platform = platform;
        }

        public uint GetAllTypesHash()
        {
            return 0xEB19D975u;
        }

        public ExtendedTypeInformation GetExtendedTypeInformation(uint typeId)
        {
            ExtendedTypeInformation result = new ExtendedTypeInformation
            {
                TypeId = typeId
            };
            switch (typeId)
            {
                case 0x166B084Du:
                    result.HasCustomData = true;
                    result.TypeHash = 0x46410F77u;
                    result.TypeName = nameof(AudioFile);
                    switch (_platform)
                    {
                        case TargetPlatform.Win32:
                            result.ProcessingHash = _effectiveAudioPluginVersion ^ 0x83398E45u;
                            break;
                        case TargetPlatform.Xbox360:
                            result.ProcessingHash = _effectiveAudioPluginVersion360 ^ 0x83398E45u;
                            break;
                        default:
                            throw new BinaryAssetBuilderException(ErrorCode.InternalError, "Unknown platform {0}", _platform);
                    }
                    break;
            }
            return result;
        }

        public AssetBuffer ProcessInstance(InstanceDeclaration instance)
        {
            switch (instance.Handle.TypeId)
            {
                case 0x166B084Du:
                    return ProcessAudioFileInstance(instance);
                default:
                    _tracer.TraceWarning("Couldn't process {0}. No matching handler found.", instance);
                    return null;
            }
        }
        public void Dispose()
        {
            SIMEX_shutdown();
        }
    }
}
