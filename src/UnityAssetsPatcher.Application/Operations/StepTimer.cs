using System.Diagnostics;

namespace UnityAssetsPatcher.Application.Operations;

public sealed record TimingStep(string Name, TimeSpan Elapsed);

public sealed record TimingSnapshot(IReadOnlyList<TimingStep> Steps, TimeSpan Elapsed);

public sealed class StepTimer
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly List<TimingStep> _steps = [];

    public T Measure<T>(string name, Func<T> action)
    {
        var stepStopwatch = Stopwatch.StartNew();

        try
        {
            return action();
        }
        finally
        {
            stepStopwatch.Stop();

            _steps.Add(new TimingStep(name, stepStopwatch.Elapsed));
        }
    }

    public void Append(TimingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _steps.AddRange(snapshot.Steps);
    }

    public TimingSnapshot BuildSnapshot()
    {
        _stopwatch.Stop();

        return new TimingSnapshot(_steps.ToArray(), _stopwatch.Elapsed);
    }
}
