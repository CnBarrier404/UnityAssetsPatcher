using System.IO.Compression;
using UnityAssetsPatcher.Abstractions.IO;

namespace UnityAssetsPatcher.Infrastructure.IO;

public sealed class BrotliCompression : ICompressionCodec
{
    public void Compress(Stream source, Stream destination)
    {
        using BrotliStream brotli = new(destination, CompressionLevel.Fastest, leaveOpen: true);
        source.CopyTo(brotli);
    }

    public void Decompress(Stream source, Stream destination)
    {
        using BrotliStream brotli = new(source, CompressionMode.Decompress, leaveOpen: true);
        brotli.CopyTo(destination);
    }
}
