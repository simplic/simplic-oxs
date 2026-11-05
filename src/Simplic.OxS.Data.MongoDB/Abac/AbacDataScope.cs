namespace Simplic.OxS.Data.MongoDB.Abac;

/// <summary>
/// Async-flow scoped switch to suppress the repository ABAC read filter for trusted system
/// code paths that have no user subject (e.g. migrations, background jobs, bus consumers that
/// enforce ABAC themselves). Never use it on request paths that return data to a caller.
/// <code>
/// using (AbacDataScope.SuppressReadFilter())
/// {
///     var all = await repository.GetAllAsync();
/// }
/// </code>
/// </summary>
public static class AbacDataScope
{
    private static readonly AsyncLocal<int> SuppressionDepth = new();

    /// <summary>Gets whether the ABAC read filter is currently suppressed for this async flow.</summary>
    public static bool IsReadFilterSuppressed => SuppressionDepth.Value > 0;

    /// <summary>Suppresses the ABAC read filter until the returned handle is disposed.</summary>
    public static IDisposable SuppressReadFilter()
    {
        SuppressionDepth.Value++;
        return new Handle();
    }

    private sealed class Handle : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            SuppressionDepth.Value--;
        }
    }
}
