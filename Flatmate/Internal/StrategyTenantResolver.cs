using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flatmate.Internal;

internal sealed class StrategyTenantResolverOptions<TTenant>
    where TTenant : class
{
    public required IReadOnlyList<ITenantResolutionStrategy> Strategies { get; init; }

    public required Func<string, IServiceProvider, CancellationToken, ValueTask<TTenant?>> Resolve { get; init; }

    public Func<TTenant, IEnumerable<string>>? Identifiers { get; init; }
}

internal sealed class StrategyTenantResolver<TTenant> : MemoryCacheTenantResolver<TTenant>
    where TTenant : class
{
    private readonly IReadOnlyList<ITenantResolutionStrategy> strategies;
    private readonly Func<string, IServiceProvider, CancellationToken, ValueTask<TTenant?>> resolve;
    private readonly Func<TTenant, IEnumerable<string>>? identifiers;
    private readonly IServiceProvider services;

    public StrategyTenantResolver(
        IMemoryCache cache,
        ILoggerFactory loggerFactory,
        IOptions<MemoryCacheTenantResolverOptions> options,
        StrategyTenantResolverOptions<TTenant> resolverOptions,
        IServiceProvider services)
        : base(cache, loggerFactory, options)
    {
        ArgumentNullException.ThrowIfNull(resolverOptions);
        ArgumentNullException.ThrowIfNull(services);

        strategies = resolverOptions.Strategies;
        resolve = resolverOptions.Resolve;
        identifiers = resolverOptions.Identifiers;
        this.services = services;
    }

    protected override string? GetContextIdentifier(HttpContext context)
    {
        foreach (var strategy in strategies)
        {
            string? identifier = strategy.GetIdentifier(context);
            if (!string.IsNullOrWhiteSpace(identifier))
            {
                return identifier;
            }
        }

        return null;
    }

    protected override IEnumerable<string>? GetTenantIdentifiers(TenantContext<TTenant> context)
        => context.Identifiers;

    protected override async Task<TenantContext<TTenant>?> ResolveFromStoreAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        string? identifier = GetContextIdentifier(context);
        if (identifier is null)
        {
            return null;
        }

        var tenant = await resolve(identifier, services, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var keys = identifiers?.Invoke(tenant) ?? [identifier];
        return new TenantContext<TTenant>(tenant, keys);
    }
}