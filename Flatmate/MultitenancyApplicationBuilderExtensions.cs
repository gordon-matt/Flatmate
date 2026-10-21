using Flatmate;
using Flatmate.Internal;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Adds Flatmate middleware to the request pipeline.</summary>
public static class FlatmateApplicationBuilderExtensions
{
    /// <summary>Resolves the current tenant and stores it on the request.</summary>
    public static IApplicationBuilder UseFlatmate<TTenant>(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<TenantResolutionMiddleware<TTenant>>();
    }

    /// <summary>
    /// Builds a pipeline branch the first time each tenant is seen.
    /// The branch runs before the rest of the root pipeline.
    /// Requests with no tenant continue on the root pipeline.
    /// </summary>
    /// <param name="keySelector">
    /// Cache key for the branch. Use a stable tenant id. The default is the tenant instance,
    /// so types without value equality build a new branch whenever the instance changes.
    /// </param>
    public static IApplicationBuilder UsePerTenant<TTenant>(
        this IApplicationBuilder app,
        Action<TenantPipelineBuilderContext<TTenant>, IApplicationBuilder> configuration,
        Func<TTenant, object>? keySelector = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        app.Use(next => new TenantPipelineMiddleware<TTenant>(next, app, configuration, keySelector).Invoke);
        return app;
    }

    /// <summary>Redirects requests that did not resolve a tenant.</summary>
    public static IApplicationBuilder UseUnresolvedTenantRedirect<TTenant>(
        this IApplicationBuilder app,
        string location,
        bool permanent = false)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        return app.UseMiddleware<TenantUnresolvedRedirectMiddleware<TTenant>>(location, permanent);
    }

    /// <summary>Ends the request with 404 when no tenant was resolved.</summary>
    public static IApplicationBuilder UseUnresolvedTenantNotFound<TTenant>(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<TenantNotFoundMiddleware<TTenant>>();
    }

    /// <summary>
    /// Redirects a resolved tenant to its canonical host, preserving the path and query.
    /// <paramref name="primaryHostname"/> may include a port, for example <c>localhost:5001</c>.
    /// </summary>
    public static IApplicationBuilder UsePrimaryHostnameRedirect<TTenant>(
        this IApplicationBuilder app,
        Func<TTenant, string?> primaryHostname,
        bool permanent = false)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(primaryHostname);

        return app.UseMiddleware<PrimaryHostnameRedirectMiddleware<TTenant>>(primaryHostname, permanent);
    }
}