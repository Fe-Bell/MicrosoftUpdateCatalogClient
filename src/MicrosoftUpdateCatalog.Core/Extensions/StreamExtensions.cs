using MicrosoftUpdateCatalog.Core.Progress;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MicrosoftUpdateCatalog.Core.Extensions
{
    public static class StreamExtensions
    {
        static StreamExtensions()
        {

        }

        public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize = 0x2000, ByteCountProgress progress = null, CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            if (!destination.CanWrite)
                throw new Exception("Destination stream cannot be written to.");

            if (progress != null)
                progress.TotalSize = source.Length;

            byte[] buffer = new byte[bufferSize];
            int count;
            while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                progress?.Report(count);
            }
        }
    }
}
