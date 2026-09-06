using System.Runtime.ExceptionServices;

namespace UnityAssetsPatcher.Infrastructure.AssetsTools;

internal static class ResourceCleanup
{
    // Kept for callers that inspect the former diagnostic format. Failures now propagate together.
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

    public static void ThrowIfFailed(Exception? primaryException, IReadOnlyList<Exception> cleanupExceptions)
    {
        ArgumentNullException.ThrowIfNull(cleanupExceptions);

        if (primaryException is not null)
        {
            if (cleanupExceptions.Count > 0)
            {
                throw new AggregateException([primaryException, .. cleanupExceptions]);
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
}
