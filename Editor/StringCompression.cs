using System.IO;
using System.IO.Compression;
using System.Text;

namespace Foxscore.EasyLogin
{
    public static class StringCompression
    {
        public static byte[] Compress(string text)
        {
            var buffer = Encoding.UTF8.GetBytes(text);
            using var memoryStream = new MemoryStream();
            using var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal);
            gzipStream.Write(buffer, 0, buffer.Length);
            return memoryStream.ToArray();
        }

        public static string Decompress(byte[] compressedData)
        {
            using var memoryStream = new MemoryStream(compressedData);
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            gzipStream.CopyTo(resultStream);
            return Encoding.UTF8.GetString(resultStream.ToArray());
        }
    }
}