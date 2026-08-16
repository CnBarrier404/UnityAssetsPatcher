using System.Runtime.ExceptionServices;

namespace UnityAssetsPatcher.Infrastructure.AssetsTools;

internal static class ResourceCleanup
{
    internal const string CleanupExceptionsDataKey = "UnityAssetsPatcher.CleanupExceptions";

    public static IReadOnlyList<Exception> RunAll(IEnumerable<Action> cleanups)
    {
        ArgumentNullException.ThrowIfNull(cleanups);

        var exceptions = new List<Exception>();

        foreach (Action cleanup in cleanups)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }

        return exceptions;
    }

    public static void ThrowOrAttach(Exception? primaryException, IReadOnlyList<Exception> cleanupExceptions)
    {
        ArgumentNullException.ThrowIfNull(cleanupExceptions);

        if (primaryException is not null)
        {
            if (cleanupExceptions.Count > 0)
            {
                Attach(primaryException, cleanupExceptions);
            }

            ExceptionDispatchInfo.Capture(primaryException).Throw();

            return;
        }

        switch (cleanupExceptions.Count)
        {
            case 0:
                return;
            case 1:
                ExceptionDispatchInfo.Capture(cleanupExceptions[0]).Throw();
                return;
            default:
                throw new AggregateException(cleanupExceptions);
        }
    }

    public static void Attach(
        Exception primaryException,
        IReadOnlyList<Exception> cleanupExceptions)
    {
        ArgumentNullException.ThrowIfNull(primaryException);
        ArgumentNullException.ThrowIfNull(cleanupExceptions);

        if (cleanupExceptions.Count == 0)
        {
            return;
        }

        var allCleanupExceptions = new List<Exception>();

        switch (primaryException.Data[CleanupExceptionsDataKey])
        {
            case AggregateException previous:
                allCleanupExceptions.AddRange(previous.InnerExceptions);
                break;
            case Exception previousException:
                allCleanupExceptions.Add(previousException);
                break;
        }

        allCleanupExceptions.AddRange(cleanupExceptions);
        primaryException.Data[CleanupExceptionsDataKey] = new AggregateException(allCleanupExceptions);
    }
}
