using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Infrastructure.IO;

public sealed class BrotliCompression : ICompressionCodec
{
    private readonly ILogger<BrotliCompression> _logger;

    public BrotliCompression(ILogger<BrotliCompression> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public void Compress(Stream source, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        IOLog.Compressing(_logger);

        long startTimestamp = Stopwatch.GetTimestamp();

        using (BrotliStream brotli = new(destination, CompressionLevel.Fastest, true))
        {
            source.CopyTo(brotli);
        }

        double elapsedMilliseconds = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        IOLog.Compressed(_logger, elapsedMilliseconds);
    }

    public void Decompress(Stream source, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        IOLog.Decompressing(_logger);

        long startTimestamp = Stopwatch.GetTimestamp();

        using (BrotliStream brotli = new(source, CompressionMode.Decompress, true))
        {
            brotli.CopyTo(destination);
        }

        double elapsedMilliseconds = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        IOLog.Decompressed(_logger, elapsedMilliseconds);
    }
}
