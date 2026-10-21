using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flatmate.Tests;

public class MultitenancyTests
{
    [Fact]
    public async Task Middleware_sets_the_tenant_and_continues_when_none_matches()
    {
        var (provider, app) = CreateApp(tenants => tenants["acme.example"] = new TestTenant("acme"));

        var matched = await SendAsync(provider, app, request => request.Host = new HostString("Acme.Example"));
        var missed = await SendAsync(provider, app, request => request.Host = new HostString("missing.example"));

        Assert.Equal("acme", matched.GetTenant<TestTenant>()?.Id);
        Assert.Null(missed.GetTenant<TestTenant>());
        Assert.Equal(StatusCodes.Status204NoContent, missed.Response.StatusCode);
    }

    [Fact]
    public async Task Cache_reuses_one_resolution_for_every_identifier()
    {
        var (provider, app) = CreateApp(
            tenants => tenants["acme.example"] = new TestTenant("acme"),
            identifiers: tenant => [tenant.Id + ".example", "www." + tenant.Id + ".example"]);

        var first = await SendAsync(provider, app, request => request.Host = new HostString("acme.example"));
        var alias = await SendAsync(provider, app, request => request.Host = new HostString("WWW.ACME.EXAMPLE"));

        Assert.Equal(1, provider.GetRequiredService<ResolveCounter>().Count);
        Assert.Equal(first.GetTenantContext<TestTenant>()!.Id, alias.GetTenantContext<TestTenant>()!.Id);
    }

    [Fact]
    public async Task Evict_drops_aliases_and_disposes_context_properties_once()
    {
        await using var provider = BuildCatalogProvider();
        var app = BuildPipeline(provider, pipeline: null);
        var state = provider.GetRequiredService<CatalogState>();

        await SendAsync(provider, app, request => request.Host = new HostString("apple.example"));
        provider.GetRequiredService<ITenantCache<TestTenant>>().Evict("APPLE.EXAMPLE");
        await WaitUntil(() => state.FirstResource!.DisposeCount > 0);
        await SendAsync(provider, app, request => request.Host = new HostString("pear.example"));

        Assert.Equal(2, state.StoreCalls);
        Assert.Equal(1, state.FirstResource!.DisposeCount);
        Assert.Equal(0, state.FirstTenant!.DisposeCount);
    }

    [Fact]
    public async Task Expired_resolution_disposes_properties_and_leaves_the_tenant()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2026-10-21T00:00:00Z"));
        await using var provider = BuildCatalogProvider(services =>
        {
            services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions
            {
                Clock = clock,
                ExpirationScanFrequency = TimeSpan.FromSeconds(1)
            }));
            services.Configure<MemoryCacheTenantResolverOptions>(options =>
            {
                options.SlidingExpiration = null;
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            });
        });

        var app = BuildPipeline(provider, pipeline: null);
        var state = provider.GetRequiredService<CatalogState>();

        await SendAsync(provider, app, request => request.Host = new HostString("apple.example"));
        clock.UtcNow += TimeSpan.FromMinutes(6);
        await SendAsync(provider, app, request => request.Host = new HostString("pear.example"));
        await WaitUntil(() => state.FirstResource!.DisposeCount > 0);

        Assert.Equal(2, state.StoreCalls);
        Assert.Equal(1, state.FirstResource!.DisposeCount);
        Assert.Equal(0, state.FirstTenant!.DisposeCount);
    }

    [Fact]
    public async Task Header_is_used_when_present_and_host_is_used_when_it_is_missing()
    {
        var (provider, app) = CreateApp(
            tenants =>
            {
                tenants["acme"] = new TestTenant("from-header");
                tenants["acme.example"] = new TestTenant("from-host");
            },
            configure: builder => builder.IdentifyByHeader("X-Tenant").IdentifyByHost());

        var header = await SendAsync(provider, app, request =>
        {
            request.Host = new HostString("acme.example");
            request.Headers["X-Tenant"] = "acme";
        });
        var host = await SendAsync(provider, app, request => request.Host = new HostString("acme.example"));

        Assert.Equal("from-header", header.GetTenant<TestTenant>()?.Id);
        Assert.Equal("from-host", host.GetTenant<TestTenant>()?.Id);
    }

    [Fact]
    public async Task Claim_and_path_strategies_read_the_request()
    {
        var (provider, app) = CreateApp(
            tenants =>
            {
                tenants["northwind"] = new TestTenant("from-claim");
                tenants["contoso"] = new TestTenant("from-path");
            },
            configure: builder => builder.IdentifyByClaim("tenant").IdentifyByPathSegment());

        var claim = await SendAsync(provider, app, request =>
        {
            request.Path = "/contoso/home";
            request.HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim("tenant", "northwind")], authenticationType: "test"));
        });
        var path = await SendAsync(provider, app, request => request.Path = "/contoso/home");

        Assert.Equal("from-claim", claim.GetTenant<TestTenant>()?.Id);
        Assert.Equal("from-path", path.GetTenant<TestTenant>()?.Id);
    }

    [Fact]
    public async Task Per_tenant_branch_runs_before_the_root_pipeline_and_is_built_once()
    {
        int builds = 0;
        var (provider, app) = CreateApp(
            tenants => tenants["acme.example"] = new TestTenant("acme"),
            pipeline: application =>
            {
                application.UsePerTenant<TestTenant>((tenant, branch) =>
                {
                    builds++;
                    branch.Use(async (context, next) =>
                    {
                        context.Items["branch"] = tenant.Tenant.Id;
                        await next(context);
                    });
                },
                tenant => tenant.Id);

                application.Run(context =>
                {
                    context.Items["root"] = true;
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return Task.CompletedTask;
                });
            });

        var first = await SendAsync(provider, app, request => request.Host = new HostString("acme.example"));
        var second = await SendAsync(provider, app, request => request.Host = new HostString("acme.example"));
        var missing = await SendAsync(provider, app, request => request.Host = new HostString("missing.example"));

        Assert.Equal("acme", first.Items["branch"]);
        Assert.Equal(true, first.Items["root"]);
        Assert.Equal(1, builds);
        Assert.Equal("acme", second.Items["branch"]);
        Assert.False(missing.Items.ContainsKey("branch"));
        Assert.Equal(true, missing.Items["root"]);
    }

    [Fact]
    public async Task Unresolved_redirect_and_not_found_end_the_request()
    {
        int redirectNext = 0;
        var (redirectProvider, redirectApp) = CreateApp(
            tenants => tenants["acme.example"] = new TestTenant("acme"),
            pipeline: application => application
                .UseUnresolvedTenantRedirect<TestTenant>("/signup", permanent: true)
                .Run(_ =>
                {
                    redirectNext++;
                    return Task.CompletedTask;
                }));

        int notFoundNext = 0;
        var (notFoundProvider, notFoundApp) = CreateApp(
            tenants => tenants["acme.example"] = new TestTenant("acme"),
            pipeline: application => application
                .UseUnresolvedTenantNotFound<TestTenant>()
                .Run(_ =>
                {
                    notFoundNext++;
                    return Task.CompletedTask;
                }));

        var redirect = await SendAsync(redirectProvider, redirectApp, request => request.Host = new HostString("missing.example"));
        var notFound = await SendAsync(notFoundProvider, notFoundApp, request => request.Host = new HostString("missing.example"));

        Assert.Equal(StatusCodes.Status301MovedPermanently, redirect.Response.StatusCode);
        Assert.Equal("/signup", redirect.Response.Headers.Location.ToString());
        Assert.Equal(0, redirectNext);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.Response.StatusCode);
        Assert.Equal(0, notFoundNext);

        var found = await SendAsync(notFoundProvider, notFoundApp, request => request.Host = new HostString("acme.example"));
        Assert.Equal(StatusCodes.Status200OK, found.Response.StatusCode);
        Assert.Equal(1, notFoundNext);
    }

    [Fact]
    public async Task Primary_hostname_redirect_preserves_path_query_and_port()
    {
        var (provider, app) = CreateApp(
            tenants =>
            {
                tenants["alias.example:5001"] = new TestTenant("acme");
                tenants["acme.example:5000"] = new TestTenant("acme");
            },
            pipeline: application =>
            {
                application.UsePrimaryHostnameRedirect<TestTenant>(_ => "acme.example:5000");
                application.Run(context =>
                {
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return Task.CompletedTask;
                });
            });

        var redirected = await SendAsync(provider, app, request =>
        {
            request.Host = new HostString("alias.example", 5001);
            request.Path = "/docs";
            request.QueryString = new QueryString("?x=1");
        });
        var canonical = await SendAsync(provider, app, request =>
        {
            request.Host = new HostString("acme.example", 5000);
            request.Path = "/docs";
        });

        Assert.Equal(StatusCodes.Status302Found, redirected.Response.StatusCode);
        Assert.Equal("https://acme.example:5000/docs?x=1", redirected.Response.Headers.Location.ToString());
        Assert.Equal(StatusCodes.Status204NoContent, canonical.Response.StatusCode);
    }

    [Fact]
    public async Task Primary_hostname_without_a_port_drops_the_request_port()
    {
        var (provider, app) = CreateApp(
            tenants => tenants["alias.example:5001"] = new TestTenant("acme"),
            pipeline: application => application.UsePrimaryHostnameRedirect<TestTenant>(_ => "acme.example", permanent: true));

        var redirected = await SendAsync(provider, app, request => request.Host = new HostString("alias.example", 5001));

        Assert.Equal(StatusCodes.Status301MovedPermanently, redirected.Response.StatusCode);
        Assert.Equal("https://acme.example/", redirected.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Current_tenant_is_optional_and_the_tenant_service_is_required()
    {
        var (provider, app) = CreateApp(tenants => tenants["acme.example"] = new TestTenant("acme"));
        var matched = await SendAsync(provider, app, request => request.Host = new HostString("acme.example"));
        var missing = await SendAsync(provider, app, request => request.Host = new HostString("missing.example"));

        using var matchedScope = provider.CreateScope();
        using var missingScope = provider.CreateScope();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();

        accessor.HttpContext = matched;
        Assert.Equal("acme", matchedScope.ServiceProvider.GetRequiredService<ICurrentTenant<TestTenant>>().Tenant?.Id);
        Assert.Equal("acme", matchedScope.ServiceProvider.GetRequiredService<TestTenant>().Id);

        accessor.HttpContext = missing;
        Assert.Null(missingScope.ServiceProvider.GetRequiredService<ICurrentTenant<TestTenant>>().Tenant);
        var exception = Assert.Throws<TenantNotResolvedException>(
            missingScope.ServiceProvider.GetRequiredService<TestTenant>);
        Assert.Equal(typeof(TestTenant), exception.TenantType);
    }

    [Fact]
    public async Task Resolve_receives_the_request_scope()
    {
        IServiceProvider? seen = null;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ScopeMarker>();
        services.AddFlatmate<TestTenant>(builder => builder.Resolve(async (identifier, scope, _) =>
        {
            seen = scope;
            await Task.Yield();
            scope.GetRequiredService<ScopeMarker>();
            return identifier == "acme.example" ? new TestTenant("acme") : null;
        }));

        await using var provider = services.BuildServiceProvider();
        var app = BuildPipeline(provider, pipeline: null);
        await SendAsync(provider, app, request => request.Host = new HostString("acme.example"));

        Assert.NotNull(seen);
        Assert.NotSame(provider, seen);
    }

    [Fact]
    public async Task Custom_resolver_still_caches_by_alias()
    {
        await using var provider = BuildCatalogProvider();
        var app = BuildPipeline(provider, pipeline: null);

        var first = await SendAsync(provider, app, request => request.Host = new HostString("pear.example"));
        var alias = await SendAsync(provider, app, request => request.Host = new HostString("APPLE.EXAMPLE"));

        Assert.Equal("fruit", first.GetTenant<TestTenant>()?.Id);
        Assert.Equal(first.GetTenantContext<TestTenant>()!.Id, alias.GetTenantContext<TestTenant>()!.Id);
        Assert.Equal(1, provider.GetRequiredService<CatalogState>().StoreCalls);
    }

    [Fact]
    public void Builder_requires_a_resolve_callback()
    {
        var services = new ServiceCollection();
        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddFlatmate<TestTenant>(_ => { }));

        Assert.Contains("Resolve", exception.Message);
    }

    [Fact]
    public async Task Path_segment_moves_the_tenant_prefix_onto_the_path_base()
    {
        var (provider, app) = CreateApp(
            tenants => tenants["acme"] = new TestTenant("acme"),
            configure: builder => builder.IdentifyByPathSegment(rewritePath: true));

        var matched = await SendAsync(provider, app, request =>
        {
            request.PathBase = "/app";
            request.Path = "/acme/orders/";
            request.QueryString = new QueryString("?x=1");
        });
        var root = await SendAsync(provider, app, request => request.Path = "/acme");
        var missing = await SendAsync(provider, app, request => request.Path = "/missing/orders");

        Assert.Equal("acme", matched.GetTenant<TestTenant>()?.Id);
        Assert.Equal("/app/acme", matched.Request.PathBase.Value);
        Assert.Equal("/orders/", matched.Request.Path.Value);
        Assert.Equal("?x=1", matched.Request.QueryString.Value);
        Assert.Equal("/acme", root.Request.PathBase.Value);
        Assert.Equal("/", root.Request.Path.Value);
        Assert.Equal("/missing/orders", missing.Request.Path.Value);
        Assert.Equal(string.Empty, missing.Request.PathBase.Value);
    }

    [Fact]
    public async Task Path_rewrite_can_remove_a_later_segment_without_changing_the_path_base()
    {
        var (provider, app) = CreateApp(
            tenants => tenants["acme"] = new TestTenant("acme"),
            configure: builder => builder.IdentifyByPathSegment(1, rewritePath: true));

        var context = await SendAsync(provider, app, request => request.Path = "/api/acme/orders");

        Assert.Equal("acme", context.GetTenant<TestTenant>()?.Id);
        Assert.Equal(string.Empty, context.Request.PathBase.Value);
        Assert.Equal("/api/orders", context.Request.Path.Value);
    }

    [Fact]
    public async Task Path_segment_leaves_the_path_alone_when_rewrite_is_off()
    {
        var (provider, app) = CreateApp(
            tenants => tenants["acme"] = new TestTenant("acme"),
            configure: builder => builder.IdentifyByPathSegment());

        var context = await SendAsync(provider, app, request => request.Path = "/acme/orders");

        Assert.Equal("acme", context.GetTenant<TestTenant>()?.Id);
        Assert.Equal("/acme/orders", context.Request.Path.Value);
    }

    private static (ServiceProvider Provider, RequestDelegate App) CreateApp(
        Action<Dictionary<string, TestTenant>> seed,
        Action<FlatmateBuilder<TestTenant>>? configure = null,
        Func<TestTenant, IEnumerable<string>>? identifiers = null,
        Action<IApplicationBuilder>? pipeline = null)
    {
        var tenants = new Dictionary<string, TestTenant>(StringComparer.OrdinalIgnoreCase);
        seed(tenants);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ResolveCounter>();
        services.AddFlatmate<TestTenant>(builder =>
        {
            if (configure is null)
            {
                builder.IdentifyByHost();
            }
            else
            {
                configure(builder);
            }

            builder.Resolve((identifier, scope, _) =>
            {
                scope.GetRequiredService<ResolveCounter>().Count++;
                tenants.TryGetValue(identifier, out var tenant);
                return ValueTask.FromResult(tenant);
            },
            identifiers);
        });

        var provider = services.BuildServiceProvider();
        return (provider, BuildPipeline(provider, pipeline));
    }

    private static ServiceProvider BuildCatalogProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<CatalogState>();
        configure?.Invoke(services);
        services.AddFlatmate<TestTenant, CatalogResolver>();
        return services.BuildServiceProvider();
    }

    private static RequestDelegate BuildPipeline(IServiceProvider provider, Action<IApplicationBuilder>? pipeline)
    {
        var app = new ApplicationBuilder(provider);
        app.UseFlatmate<TestTenant>();
        if (pipeline is null)
        {
            app.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });
        }
        else
        {
            pipeline(app);
        }

        return app.Build();
    }

    private static async Task<DefaultHttpContext> SendAsync(
        IServiceProvider provider,
        RequestDelegate pipeline,
        Action<HttpRequest> configure)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = provider.CreateScope().ServiceProvider
        };
        context.Request.Scheme = "https";
        configure(context.Request);
        await pipeline(context);
        return context;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    private sealed class ResolveCounter
    {
        public int Count { get; set; }
    }

    private sealed class ScopeMarker;

    private sealed class ManualClock(DateTimeOffset utcNow) : Microsoft.Extensions.Internal.ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }
}

internal sealed record TestTenant(string Id) : IDisposable
{
    public int DisposeCount { get; private set; }

    public void Dispose() => DisposeCount++;
}

internal sealed class CatalogState
{
    public int StoreCalls { get; set; }

    public TestTenant? FirstTenant { get; set; }

    public TrackingDisposable? FirstResource { get; set; }
}

internal sealed class TrackingDisposable : IDisposable
{
    public int DisposeCount { get; private set; }

    public void Dispose() => DisposeCount++;
}

internal sealed class CatalogResolver : MemoryCacheTenantResolver<TestTenant>
{
    private readonly CatalogState state;

    public CatalogResolver(
        IMemoryCache cache,
        ILoggerFactory loggerFactory,
        IOptions<MemoryCacheTenantResolverOptions> options,
        CatalogState state)
        : base(cache, loggerFactory, options)
    {
        this.state = state;
    }

    protected override string? GetContextIdentifier(HttpContext context) => context.Request.Host.Value;

    protected override IEnumerable<string>? GetTenantIdentifiers(TenantContext<TestTenant> context) => context.Identifiers;

    protected override Task<TenantContext<TestTenant>?> ResolveFromStoreAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        state.StoreCalls++;
        var tenant = new TestTenant("fruit");
        var tenantContext = new TenantContext<TestTenant>(tenant, ["apple.example", "pear.example"]);
        var resource = new TrackingDisposable();
        tenantContext.Properties["resource"] = resource;

        state.FirstTenant ??= tenant;
        state.FirstResource ??= resource;

        return Task.FromResult<TenantContext<TestTenant>?>(tenantContext);
    }
}