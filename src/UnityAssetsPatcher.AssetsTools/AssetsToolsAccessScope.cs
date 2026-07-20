using System.Runtime.ExceptionServices;
using UnityAssetsPatcher.Application.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsAccessScope : IAssetsAccessScope
{
    public IAssetsFileReader Reader
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _reader ??= _readerFactory();

            return _reader;
        }
    }

    public IAssetsFileWriter Writer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _writer ??= _writerFactory();

            return _writer;
        }
    }

    private readonly Func<IAssetsFileReader> _readerFactory;
    private readonly Func<IAssetsFileWriter> _writerFactory;
    private IAssetsFileReader? _reader;
    private IAssetsFileWriter? _writer;
    private bool _disposed;

    public AssetsToolsAccessScope(
        Func<IAssetsFileReader> readerFactory,
        Func<IAssetsFileWriter> writerFactory)
    {
        ArgumentNullException.ThrowIfNull(readerFactory);
        ArgumentNullException.ThrowIfNull(writerFactory);

        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
    }

    public void CloseReadSessions()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IAssetsFileReader? reader = _reader;
        _reader = null;
        reader?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ExceptionDispatchInfo? firstException = null;
        IAssetsFileReader? reader = _reader;
        IAssetsFileWriter? writer = _writer;
        _reader = null;
        _writer = null;

        try
        {
            reader?.Dispose();
        }
        catch (Exception exception)
        {
            firstException = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            writer?.Dispose();
        }
        catch (Exception exception)
        {
            firstException ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstException?.Throw();
    }
}
