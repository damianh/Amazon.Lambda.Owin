namespace AwsLambdaOwin
{
    using System.IO;
    using System.Text;
    using Microsoft.IO;

    internal static class MemoryStreamManagerExtensions
    {
        internal static MemoryStream GetStream(this RecyclableMemoryStreamManager memoryStreamManager, string s)
        {
            var memoryStream = memoryStreamManager.GetStream();
            var bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
            memoryStream.Write(bytes, 0, bytes.Length);
            memoryStream.Position = 0;
            return memoryStream;
        }
    }
}