using System.Diagnostics;

namespace UnityAssetsPatcher.Application.Modules;

public sealed class WorkflowTiming
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();

    private TimeSpan _readPackage;
    private TimeSpan _prepareSources;
    private TimeSpan _findGameFiles;
    private TimeSpan _analyzeChanges;
    private TimeSpan? _applyPatches;
    private TimeSpan? _copyFiles;

    public T MeasureReadPackage<T>(Func<T> action)
    {
        return Measure(action, elapsed => _readPackage = elapsed);
    }

    public T MeasurePrepareSources<T>(Func<T> action)
    {
        return Measure(action, elapsed => _prepareSources = elapsed);
    }

    public T MeasureFindGameFiles<T>(Func<T> action)
    {
        return Measure(action, elapsed => _findGameFiles = elapsed);
    }

    public T MeasureAnalyzeChanges<T>(Func<T> action)
    {
        return Measure(action, elapsed => _analyzeChanges = elapsed);
    }

    public T MeasureApplyPatches<T>(Func<T> action)
    {
        return Measure(action, elapsed => _applyPatches = elapsed);
    }

    public T MeasureCopyFiles<T>(Func<T> action)
    {
        return Measure(action, elapsed => _copyFiles = elapsed);
    }

    public WorkflowTimingSnapshot Build()
    {
        return new WorkflowTimingSnapshot(
            _readPackage,
            _prepareSources,
            _findGameFiles,
            _analyzeChanges,
            _applyPatches,
            _copyFiles,
            _elapsed.Elapsed);
    }

    private static T Measure<T>(Func<T> action, Action<TimeSpan> setElapsed)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            return action();
        }
        finally
        {
            stopwatch.Stop();
            setElapsed(stopwatch.Elapsed);
        }
    }
}

public sealed record WorkflowTimingSnapshot(
    TimeSpan ReadPackage,
    TimeSpan PrepareSources,
    TimeSpan FindGameFiles,
    TimeSpan AnalyzeChanges,
    TimeSpan? ApplyPatches,
    TimeSpan? CopyFiles,
    TimeSpan Elapsed);
