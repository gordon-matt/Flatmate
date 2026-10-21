namespace Flatmate;

/// <summary>
/// Cache policy for <see cref="MemoryCacheTenantResolver{TTenant}"/>.
/// One options object is shared by every tenant type in the application.
/// </summary>
public sealed class MemoryCacheTenantResolverOptions
{
    /// <summary>When false, every request resolves the tenant from the store.</summary>
    public bool CachingEnabled { get; set; } = true;

    /// <summary>
    /// When true, expiring or removing any cached identifier for a tenant drops the other identifiers
    /// that were stored with it. Default is true.
    /// </summary>
    public bool EvictAllEntriesOnExpiry { get; set; } = true;

    /// <summary>
    /// When true, the <see cref="TenantContext{TTenant}"/> is disposed when its cache entry is evicted.
    /// Disposable values in <see cref="TenantContext{TTenant}.Properties"/> are disposed.
    /// The tenant object itself is not disposed. Default is true.
    /// MemoryCache runs the eviction callback on the thread pool, so disposal can finish after
    /// <see cref="ITenantCache{TTenant}.Evict"/> returns.
    /// </summary>
    public bool DisposeOnEviction { get; set; } = true;

    /// <summary>Sliding lifetime of a cached resolution. Default is one hour. Null disables sliding expiration.</summary>
    public TimeSpan? SlidingExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Absolute lifetime of a cached resolution. Null disables absolute expiration.</summary>
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
}