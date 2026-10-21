# Flatmate

Multi-tenancy for ASP.NET Core. Resolve a tenant for each request, inject it, and branch the pipeline per tenant.

## Install

```xml
<ProjectReference Include="..\Flatmate\src\Flatmate\Flatmate.csproj" />
```

## Resolve from a store

Strategies run in order. The first one that returns a value is the identifier passed to `Resolve`. Register authentication before `UseMultitenancy` when a strategy reads claims.

```csharp
builder.Services.AddMultitenancy<AppTenant>(tenants => tenants
    .IdentifyByHeader("X-Tenant")
    .IdentifyByHost()
    .Resolve(async (identifier, services, cancellationToken) =>
    {
        var db = services.GetRequiredService<AppDbContext>();
        return await db.Tenants.FirstOrDefaultAsync(t => t.Host == identifier, cancellationToken);
    },
    tenant => tenant.Hosts));

var app = builder.Build();
app.UseMultitenancy<AppTenant>();
app.UsePerTenant<AppTenant>((tenant, branch) =>
{
    branch.UseMiddleware<TenantSpecificMiddleware>(tenant.Tenant.Id);
},
tenant => tenant.Id);
```

`Resolve` receives the request scope, so a scoped `DbContext` works. Return every hostname (or other identifier) from the second callback so an alias hits the same cached tenant. The default cache keeps a resolution for one hour of inactivity. Call `CacheFor` or `DisableCaching` to change that.

Inject `ICurrentTenant<AppTenant>` when a request might not have a tenant. Inject `AppTenant` directly when the rest of the pipeline requires one; that registration throws `TenantNotResolvedException` until a tenant has been resolved.

```csharp
app.UseUnresolvedTenantNotFound<AppTenant>();
// or
app.UseUnresolvedTenantRedirect<AppTenant>("/signup", permanent: true);
app.UsePrimaryHostnameRedirect<AppTenant>(tenant => tenant.PrimaryHost);
```

After a tenant's identifiers change, remove the stale entry:

```csharp
services.GetRequiredService<ITenantCache<AppTenant>>().Evict(oldHost);
```

## Custom resolver

Subclass `MemoryCacheTenantResolver<TTenant>` when resolution needs more than the built-in strategies. The constructor is `(IMemoryCache cache, ILoggerFactory loggerFactory, IOptions<MemoryCacheTenantResolverOptions>? options = null)`, so an existing `: base(cache, loggerFactory)` call still compiles. Override `GetContextIdentifier`, `GetTenantIdentifiers`, and `ResolveFromStoreAsync`.

```csharp
builder.Services.AddMultitenancy<AppTenant, AppTenantResolver>();
```

Cache keys are trimmed and compared case-insensitively. Disposable objects stored in `TenantContext<T>.Properties` are disposed when the cache entry is evicted. The tenant object itself is not disposed.

`UsePerTenant` keeps one branch per key. Pass a stable id. The default key is the tenant instance, so a type without value equality builds a new branch whenever the instance changes. Requests that did not resolve a tenant continue down the root pipeline.

Cache expiration settings are application-wide: every tenant type shares one `MemoryCacheTenantResolverOptions`.

## Acknowledgements

The request pipeline shape was adapted from [SaasKit](https://github.com/saaskit/saaskit) by Ben Foster, used under the MIT license.
