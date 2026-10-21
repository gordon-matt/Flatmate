namespace Flatmate;

/// <summary>
/// Configures identifier strategies and the store lookup used by <c>AddMultitenancy</c>.
/// </summary>
/// <typeparam name="TTenant">The application's tenant type.</typeparam>
public sealed class MultitenancyBuilder<TTenant>
    where TTenant : class
{
    internal List<ITenantResolutionStrategy> Strategies { get; } = [];

    internal Func<string, IServiceProvider, CancellationToken, ValueTask<TTenant?>>? ResolveTenant { get; private set; }

    internal Func<TTenant, IEnumerable<string>>? IdentifierSelector { get; private set; }

    internal bool CachingEnabled { get; private set; } = true;

    internal TimeSpan? SlidingExpiration { get; private set; } = TimeSpan.FromHours(1);

    internal TimeSpan? AbsoluteExpirationRelativeToNow { get; private set; }

    /// <summary>Uses the request host. The port is included unless <paramref name="includePort"/> is false.</summary>
    public MultitenancyBuilder<TTenant> IdentifyByHost(bool includePort = true)
        => IdentifyBy(new HostResolutionStrategy(includePort));

    /// <summary>Uses a request header when it has a value.</summary>
    public MultitenancyBuilder<TTenant> IdentifyByHeader(string headerName)
        => IdentifyBy(new HeaderResolutionStrategy(headerName));

    /// <summary>Uses a claim. Authentication has to run before tenant resolution.</summary>
    public MultitenancyBuilder<TTenant> IdentifyByClaim(string claimType)
        => IdentifyBy(new ClaimResolutionStrategy(claimType));

    /// <summary>Uses a path segment. Index 0 is the first segment.</summary>
    public MultitenancyBuilder<TTenant> IdentifyByPathSegment(int segmentIndex = 0)
        => IdentifyBy(new PathSegmentResolutionStrategy(segmentIndex));

    /// <summary>Adds a custom strategy. Strategies run in registration order and the first value wins.</summary>
    public MultitenancyBuilder<TTenant> IdentifyBy(ITenantResolutionStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        Strategies.Add(strategy);
        return this;
    }

    /// <summary>
    /// Loads a tenant for an identifier. Return null when nothing matches.
    /// <paramref name="identifiers"/> supplies every cache key for that tenant, such as all of its hostnames.
    /// </summary>
    public MultitenancyBuilder<TTenant> Resolve(
        Func<string, IServiceProvider, CancellationToken, ValueTask<TTenant?>> resolve,
        Func<TTenant, IEnumerable<string>>? identifiers = null)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        ResolveTenant = resolve;
        IdentifierSelector = identifiers;
        return this;
    }

    /// <summary>Loads a tenant for an identifier without using services.</summary>
    public MultitenancyBuilder<TTenant> Resolve(
        Func<string, CancellationToken, ValueTask<TTenant?>> resolve,
        Func<TTenant, IEnumerable<string>>? identifiers = null)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        return Resolve((identifier, _, cancellationToken) => resolve(identifier, cancellationToken), identifiers);
    }

    /// <summary>
    /// Caches resolutions. Passing only one lifetime replaces the default one-hour sliding expiration.
    /// </summary>
    public MultitenancyBuilder<TTenant> CacheFor(TimeSpan duration, bool sliding = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        CachingEnabled = true;

        if (sliding)
        {
            SlidingExpiration = duration;
            AbsoluteExpirationRelativeToNow = null;
        }
        else
        {
            AbsoluteExpirationRelativeToNow = duration;
            SlidingExpiration = null;
        }

        return this;
    }

    /// <summary>Resolves the tenant from the store on every request.</summary>
    public MultitenancyBuilder<TTenant> DisableCaching()
    {
        CachingEnabled = false;
        return this;
    }
}