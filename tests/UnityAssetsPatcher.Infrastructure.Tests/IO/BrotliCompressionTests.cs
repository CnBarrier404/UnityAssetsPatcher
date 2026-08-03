using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Infrastructure.IO;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.IO;

public sealed class BrotliCompressionTests
{
    [Fact]
    public void Compress_WhenContentIsDecompressed_RestoresContent()
    {
        byte[] expected = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("UnityAssetsPatcher\n", 128)));
        using MemoryStream source = new(expected);
        using MemoryStream compressed = new();
        var codec = new BrotliCompression(NullLogger<BrotliCompression>.Instance);

        codec.Compress(source, compressed);
        compressed.Position = 0;
        using MemoryStream restored = new();
        codec.Decompress(compressed, restored);

        Assert.Equal(expected, restored.ToArray());
    }
}
