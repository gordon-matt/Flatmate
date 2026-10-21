using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Flatmate;

/// <summary>
/// Resolves tenants and remembers them in <see cref="IMemoryCache"/>.
/// Subclass this when resolution needs more than the built-in strategies.
/// </summary>
/// <typeparam name="TTenant">The application's tenant type.</typeparam>
public abstract class MemoryCacheTenantResolver<TTenant> : ITenantResolver<TTenant>, ITenantCache<TTenant>
{
    private readonly IMemoryCache cache;
    private readonly ILogger logger;
    private readonly MemoryCacheTenantResolverOptions options;

    /// <summary>Creates a caching resolver.</summary>
    protected MemoryCacheTenantResolver(
        IMemoryCache cache,
        ILoggerFactory loggerFactory,
        IOptions<MemoryCacheTenantResolverOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.cache = cache;
        logger = loggerFactory.CreateLogger(GetType());
        this.options = options?.Value ?? new MemoryCacheTenantResolverOptions();
    }

    /// <inheritdoc />
    public async ValueTask<TenantContext<TTenant>?> ResolveAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!options.CachingEnabled)
        {
            return await ResolveFromStoreAsync(context, cancellationToken);
        }

        string? cacheKey = Normalize(GetContextIdentifier(context));
        if (cacheKey is null)
        {
            return await ResolveFromStoreAsync(context, cancellationToken);
        }

        if (cache.TryGetValue(cacheKey, out TenantContext<TTenant>? cached))
        {
            logger.LogDebug("Retrieved tenant {TenantContextId} from cache key {CacheKey}.", cached?.Id, cacheKey);
            return cached;
        }

        logger.LogDebug("Cache miss for {CacheKey}. Resolving tenant.", cacheKey);
        var tenantContext = await ResolveFromStoreAsync(context, cancellationToken);
        if (tenantContext is null)
        {
            return null;
        }

        string[] identifiers = GetCacheKeys(cacheKey, tenantContext);
        int evicted = 0;
        var cancellation = options.EvictAllEntriesOnExpiry
            ? new CancellationTokenSource()
            : null;

        foreach (string identifier in identifiers)
        {
            var entryOptions = CreateCacheEntryOptions();
            if (cancellation is not null)
            {
                entryOptions.AddExpirationToken(new CancellationChangeToken(cancellation.Token));
            }

            entryOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                if (reason is EvictionReason.None)
                {
                    return;
                }

                if (options.DisposeOnEviction)
                {
                    logger.LogDebug("Disposing tenant context {TenantContextId} evicted from {CacheKey}.", tenantContext.Id, key);
                    tenantContext.Dispose();
                }

                if (cancellation is not null && Interlocked.Exchange(ref evicted, 1) == 0)
                {
                    // Cancel drops the sibling identifiers. The source stays referenced by those
                    // callbacks until they finish, so it is left for the garbage collector.
                    try
                    {
                        cancellation.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            });

            cache.Set(identifier, tenantContext, entryOptions);
        }

        logger.LogDebug(
            "Cached tenant {TenantContextId} under {CacheKeys}.",
            tenantContext.Id,
            identifiers);

        return tenantContext;
    }

    /// <inheritdoc />
    public void Evict(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        cache.Remove(identifier.Trim().ToLowerInvariant());
    }

    /// <summary>The cache key for this request, before normalization. Return null to skip the cache.</summary>
    protected abstract string? GetContextIdentifier(HttpContext context);

    /// <summary>Every key that should return this context. Return null to cache only the current request key.</summary>
    protected abstract IEnumerable<string>? GetTenantIdentifiers(TenantContext<TTenant> context);

    /// <summary>Loads the tenant from the application's store.</summary>
    protected abstract Task<TenantContext<TTenant>?> ResolveFromStoreAsync(HttpContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Builds the cache entry options. Called once per identifier, so return a new instance each time.
    /// The default uses <see cref="MemoryCacheTenantResolverOptions"/>.
    /// </summary>
    protected virtual MemoryCacheEntryOptions CreateCacheEntryOptions()
    {
        var entry = new MemoryCacheEntryOptions();

        if (options.SlidingExpiration is { } sliding)
        {
            entry.SetSlidingExpiration(sliding);
        }

        if (options.AbsoluteExpirationRelativeToNow is { } absolute)
        {
            entry.SetAbsoluteExpiration(absolute);
        }

        return entry;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private string[] GetCacheKeys(string requestKey, TenantContext<TTenant> tenantContext)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            requestKey
        };

        var configured = GetTenantIdentifiers(tenantContext) ?? tenantContext.Identifiers;
        foreach (string identifier in configured)
        {
            string? normalized = Normalize(identifier);
            if (normalized is not null)
            {
                keys.Add(normalized);
            }
        }

        return [.. keys];
    }
}