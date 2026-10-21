using Flatmate;
using Flatmate.Internal;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers Flatmate tenant resolution.</summary>
public static class FlatmateServiceCollectionExtensions
{
    /// <summary>
    /// Resolves <typeparamref name="TTenant"/> with <typeparamref name="TResolver"/> on each request.
    /// A caching resolver also gets <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>.
    /// </summary>
    public static IServiceCollection AddFlatmate<TTenant, TResolver>(this IServiceCollection services)
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
    public static IServiceCollection AddFlatmate<TTenant>(
        this IServiceCollection services,
        Action<FlatmateBuilder<TTenant>> configure)
        where TTenant : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new FlatmateBuilder<TTenant>();
        configure(builder);

        if (builder.ResolveTenant is null)
        {
            throw new InvalidOperationException(
                $"Call {nameof(FlatmateBuilder<TTenant>.Resolve)} to tell Flatmate how to load a {typeof(TTenant).Name}.");
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

        if (builder.ConnectionStringSelector is { } connectionString)
        {
            services.AddTenantConnectionString(connectionString);
        }

        return services;
    }

    /// <summary>
    /// Registers <see cref="TenantConnectionString"/> from the current tenant.
    /// Resolve it from a request, a Blazor circuit, or <see cref="ITenantScopeFactory{TTenant}.CreateScope"/>.
    /// </summary>
    public static IServiceCollection AddTenantConnectionString<TTenant>(
        this IServiceCollection services,
        Func<TTenant, string?> connectionString)
        where TTenant : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionString);

        services.AddScoped(sp =>
        {
            var tenant = sp.GetRequiredService<ICurrentTenant<TTenant>>().Tenant
                ?? throw new TenantNotResolvedException(typeof(TTenant));

            var value = connectionString(tenant);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"The connection string selector returned no value for {typeof(TTenant).Name}.");
            }

            return new TenantConnectionString(value);
        });

        return services;
    }

    /// <summary>
    /// Copies the tenant from the circuit's first HTTP request into the circuit scope.
    /// Call this after <c>AddFlatmate</c>. Interactive components can then inject
    /// <see cref="ICurrentTenant{TTenant}"/> for the life of the circuit.
    /// </summary>
    public static IServiceCollection AddFlatmateCircuit<TTenant>(this IServiceCollection services)
        where TTenant : class
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<CircuitHandler, TenantCircuitHandler<TTenant>>();
        return services;
    }

    private static void AddCoreServices<TTenant>(IServiceCollection services)
        where TTenant : class
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<TenantHolder<TTenant>>();
        services.TryAddScoped<ICurrentTenant<TTenant>, CurrentTenant<TTenant>>();
        services.TryAddSingleton<ITenantScopeFactory<TTenant>, TenantScopeFactory<TTenant>>();
        services.TryAddScoped(typeof(TTenant), static sp =>
        {
            var current = sp.GetRequiredService<ICurrentTenant<TTenant>>();
            return current.Tenant ?? throw new TenantNotResolvedException(typeof(TTenant));
        });
    }
}
