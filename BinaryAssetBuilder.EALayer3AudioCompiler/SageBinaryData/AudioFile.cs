using System;

namespace SageBinaryData
{
    public enum PCAudioCompressionSetting
    {
        NONE,
        XAS,
        EALAYER3
    }

    public class AudioFile
    {
        public string File;
        public int? PCSampleRate;
        public PCAudioCompressionSetting? PCCompression;
        public int PCQuality = 75;
        public bool? IsStreamedOnPC;
        public string SubtitleStringName;

        private void Marshal(string text, ref bool? objT)
        {
            objT = bool.Parse(text);
        }

        private void Marshal(Value value, ref bool? objT)
        {
            if (value is null)
            {
                return;
            }
            Marshal(value.GetText(), ref objT);
        }

        private void Marshal(string text, ref int? objT)
        {
            if (text.Length == 0)
            {
                return;
            }
            int result;
            if (text.Length == 10 && text[0] == '0' && text[1] == 'x')
            {
                result = int.Parse(text.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }
            else
            {
                result = int.Parse(text);
            }
            objT = result;
        }

        private void Marshal(Value value, ref int? objT)
        {
            if (value is null)
            {
                return;
            }
            Marshal(value.GetText(), ref objT);
        }

        private void Marshal(string text, ref int objT)
        {
            if (text.Length == 0)
            {
                return;
            }
            int result;
            if (text.Length == 10 && text[0] == '0' && text[1] == 'x')
            {
                result = int.Parse(text.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }
            else
            {
                result = int.Parse(text);
            }
            objT = result;
        }

        private void Marshal(Value value, ref int objT)
        {
            if (value is null)
            {
                return;
            }
            Marshal(value.GetText(), ref objT);
        }

        private void Marshal(string text, ref string objT)
        {
            objT = text;
        }

        private void Marshal(Value value, ref string objT)
        {
            if (value is null)
            {
                return;
            }
            Marshal(value.GetText(), ref objT);
        }

        private void Marshal(string text, ref PCAudioCompressionSetting? objT)
        {
            PCAudioCompressionSetting value;
            try
            {
                value = (PCAudioCompressionSetting)Enum.Parse(typeof(PCAudioCompressionSetting), text, false);
            }
            catch
            {
                return;
            }
            objT = value;
        }

        private void Marshal(Value value, ref PCAudioCompressionSetting? objT)
        {
            if (value is null)
            {
                return;
            }
            Marshal(value.GetText(), ref objT);
        }

        public void Marshal(Node node)
        {
            Marshal(node.GetAttributeValue(nameof(File), null), ref File);
            Marshal(node.GetAttributeValue(nameof(PCSampleRate), null), ref PCSampleRate);
            Marshal(node.GetAttributeValue(nameof(PCCompression), null), ref PCCompression);
            Marshal(node.GetAttributeValue(nameof(PCQuality), "75"), ref PCQuality);
            Marshal(node.GetAttributeValue(nameof(IsStreamedOnPC), null), ref IsStreamedOnPC);
            Marshal(node.GetAttributeValue(nameof(SubtitleStringName), null), ref SubtitleStringName);
        }
    }
}
