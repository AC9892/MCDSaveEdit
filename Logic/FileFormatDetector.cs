using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MCDSaveEdit.Logic
{
    public enum DetectedFileFormat
    {
        Empty,
        JsonText,
        PlainText,
        Binary
    }

    public static class FileFormatDetector
    {
        private const int ProbeLength = 4096;

        public static DetectedFileFormat Detect(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("The stream is not readable.", nameof(stream));
            if (stream.CanSeek && stream.Length == 0) return DetectedFileFormat.Empty;

            var originalPosition = stream.CanSeek ? stream.Position : 0;
            var buffer = new byte[ProbeLength];
            int bytesRead;
            try
            {
                if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);
                bytesRead = stream.Read(buffer, 0, buffer.Length);
            }
            finally
            {
                if (stream.CanSeek) stream.Seek(originalPosition, SeekOrigin.Begin);
            }

            if (bytesRead == 0) return DetectedFileFormat.Empty;
            var sample = Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimStart('\uFEFF', ' ', '\t', '\r', '\n', '\0');
            if (sample.StartsWith("{") || sample.StartsWith("[")) return DetectedFileFormat.JsonText;

            var printable = buffer.Take(bytesRead).Count(value => value == 9 || value == 10 || value == 13 || value >= 32 && value < 127);
            return printable >= bytesRead * 0.9 ? DetectedFileFormat.PlainText : DetectedFileFormat.Binary;
        }
    }
}
