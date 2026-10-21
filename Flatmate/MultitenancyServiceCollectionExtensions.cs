using Flatmate;
using Flatmate.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers Flatmate tenant resolution.</summary>
public static class MultitenancyServiceCollectionExtensions
{
    /// <summary>
    /// Resolves <typeparamref name="TTenant"/> with <typeparamref name="TResolver"/> on each request.
    /// A caching resolver also gets <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>.
    /// </summary>
    public static IServiceCollection AddMultitenancy<TTenant, TResolver>(this IServiceCollection services)
        where TTenant : class
        where TResolver : class, ITenantResolver<TTenant>
    {
        ArgumentNullException.ThrowIfNull(services);

        AddCoreServices<TTenant>(services);
        services.AddScoped<ITenantResolver<TTenant>, TResolver>();

        if (typeof(MemoryCacheTenantResolver<TTenant>).IsAssignableFrom(typeof(TResolver)))
        {
            services.AddMemoryCache();
            services.AddScoped<ITenantCache<TTenant>>(sp => (ITenantCache<TTenant>)sp.GetRequiredService<ITenantResolver<TTenant>>());
        }

        return services;
    }

    /// <summary>
    /// Resolves <typeparamref name="TTenant"/> from the strategies and lookup configured on
    /// <paramref name="configure"/>. The host is used when no strategy is added.
    /// </summary>
    public static IServiceCollection AddMultitenancy<TTenant>(
        this IServiceCollection services,
        Action<MultitenancyBuilder<TTenant>> configure)
        where TTenant : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new MultitenancyBuilder<TTenant>();
        configure(builder);

        if (builder.ResolveTenant is null)
        {
            throw new InvalidOperationException(
                $"Call {nameof(MultitenancyBuilder<TTenant>.Resolve)} to tell Flatmate how to load a {typeof(TTenant).Name}.");
        }

        if (builder.Strategies.Count == 0)
        {
            builder.IdentifyByHost();
        }

        services.Configure<MemoryCacheTenantResolverOptions>(options =>
        {
            options.CachingEnabled = builder.CachingEnabled;
            options.SlidingExpiration = builder.SlidingExpiration;
            options.AbsoluteExpirationRelativeToNow = builder.AbsoluteExpirationRelativeToNow;
        });

        services.AddSingleton(new StrategyTenantResolverOptions<TTenant>
        {
            Strategies = builder.Strategies.ToArray(),
            Resolve = builder.ResolveTenant,
            Identifiers = builder.IdentifierSelector
        });

        AddCoreServices<TTenant>(services);
        services.AddMemoryCache();
        services.AddScoped<StrategyTenantResolver<TTenant>>();
        services.AddScoped<ITenantResolver<TTenant>>(sp => sp.GetRequiredService<StrategyTenantResolver<TTenant>>());
        services.AddScoped<ITenantCache<TTenant>>(sp => sp.GetRequiredService<StrategyTenantResolver<TTenant>>());

        return services;
    }

    private static void AddCoreServices<TTenant>(IServiceCollection services)
        where TTenant : class
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentTenant<TTenant>, HttpContextCurrentTenant<TTenant>>();
        services.TryAddScoped(typeof(TTenant), sp =>
        {
            var current = sp.GetRequiredService<ICurrentTenant<TTenant>>();
            return current.Tenant ?? throw new TenantNotResolvedException(typeof(TTenant));
        });
    }
}