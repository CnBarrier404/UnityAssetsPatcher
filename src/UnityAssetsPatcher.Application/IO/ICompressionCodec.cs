namespace UnityAssetsPatcher.Application.IO;

public interface ICompressionCodec
{
    public void Compress(Stream source, Stream destination);

    public void Decompress(Stream source, Stream destination);
}
