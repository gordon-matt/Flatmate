using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Flatmate;

/// <summary>
/// Builds authentication options per tenant. The framework calls
/// <see cref="IConfigureOptions{TOptions}.Configure"/> once, so a cookie name set there
/// sticks to whichever tenant was current at startup. Handlers call <see cref="Get"/>
/// on each request, which is the point where the tenant is known.
/// </summary>
public sealed class TenantAuthenticationOptionsMonitor<TTenant, TOptions>(
    IOptionsFactory<TOptions> factory,
    IHttpContextAccessor httpContextAccessor,
    IServiceProvider services,
    Func<TTenant, string> tenantKey,
    Action<TOptions, TTenant, IServiceProvider> configure) : IOptionsMonitor<TOptions>
    where TTenant : class
    where TOptions : class
{
    private readonly ConcurrentDictionary<string, TOptions> cache = new(StringComparer.Ordinal);
    private readonly IOptionsFactory<TOptions> optionsFactory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly IHttpContextAccessor contextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    private readonly IServiceProvider serviceProvider = services ?? throw new ArgumentNullException(nameof(services));
    private readonly Func<TTenant, string> keySelector = tenantKey ?? throw new ArgumentNullException(nameof(tenantKey));
    private readonly Action<TOptions, TTenant, IServiceProvider> configureOptions = configure ?? throw new ArgumentNullException(nameof(configure));

    /// <inheritdoc />
    public TOptions CurrentValue => Get(Options.DefaultName);

    /// <summary>
    /// Returns options for the scheme, adjusted for the tenant on the current request.
    /// A request with no tenant gets the unadjusted options. <see cref="TenantContext{TTenant}.Id"/>
    /// is a new value on every resolution, so the scheme name is combined with the stable key
    /// from the tenant selector instead.
    /// </summary>
    public TOptions Get(string? name)
    {
        string scheme = name ?? Options.DefaultName;
        TTenant? tenant = contextAccessor.HttpContext?.GetTenant<TTenant>();
        if (tenant is null)
        {
            return cache.GetOrAdd(scheme, _ => optionsFactory.Create(scheme));
        }

        string key = keySelector(tenant);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                $"The tenant key for {typeof(TTenant).Name} was empty. Return a stable id, such as the tenant's primary key.");
        }

        return cache.GetOrAdd(scheme + "\n" + key, _ =>
        {
            TOptions options = optionsFactory.Create(scheme);
            configureOptions(options, tenant, serviceProvider);
            return options;
        });
    }

    /// <summary>
    /// No listener. A new tenant is picked up on the next <see cref="Get"/>, not by an options change token.
    /// </summary>
    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
