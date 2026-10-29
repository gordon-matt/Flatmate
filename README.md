<img src="https://github.com/gordon-matt/Flatmate/blob/master/_Misc/Logo.png" alt="Logo" width="350" />

Multi-tenancy for ASP.NET Core. Resolve a tenant for each request, inject it, and branch the pipeline per tenant.

`Flatmate.Demo` is a Blazor Server app. Tenants are the `Tenants` array in `appsettings.json`. `Url` is the host (`acme.localhost`, any port). `Id` is the key stored on each row. The path is a page on that host, so `http://fabrikam.localhost:5218/acme` stays on Fabrikam and is not the Acme site. Tenants that omit `ConnectionString` use `Database:SharedConnectionString`, so small tenants can share one database. A tenant that sets `ConnectionString` uses that database on its own, and two tenants with the same connection string share that one. An EF Core query filter separates rows in a shared database, and the Blazor circuit keeps the tenant. Run the project and open `http://acme.localhost:5218/`, `http://contoso.localhost:5218/`, or `http://fabrikam.localhost:5218/`. Acme and Contoso share `shared.db`. Fabrikam uses `fabrikam.db`.

The sample `Url` values use `.localhost`, which the browser resolves to this machine. To test other hosts, map them to loopback and set each tenant's `Url` to that host. On Windows, edit `C:\Windows\System32\drivers\etc\hosts` as administrator. On macOS or Linux, edit `/etc/hosts`.

```
127.0.0.1 acme.mysite.local contoso.mysite.local fabrikam.local
::1 acme.mysite.local contoso.mysite.local fabrikam.local
```

```json
{ "Id": "acme", "Name": "Acme", "Url": "acme.mysite.local" },
{ "Id": "contoso", "Name": "Contoso", "Url": "contoso.mysite.local" },
{ "Id": "fabrikam", "Name": "Fabrikam", "Url": "fabrikam.local", "ConnectionString": "Data Source=App_Data/fabrikam.db" }
```

Restart the app and open `http://acme.mysite.local:5218/`. The port is the one in `Properties/launchSettings.json`. On Windows, run `ipconfig /flushdns` when a name still points somewhere else. Names that end in `.local` are also used by mDNS, so give the hosts file a moment before treating a failure as a bad entry.

## Install

```xml
<ProjectReference Include="..\Flatmate\Flatmate.csproj" />
<ProjectReference Include="..\Flatmate.EntityFrameworkCore\Flatmate.EntityFrameworkCore.csproj" />
```

The Entity Framework project is only needed for the query-filter helpers.

## Resolve a tenant

Strategies run in order. The first one that returns a value is the identifier passed to `Resolve`. Register authentication before `UseFlatmate` when a strategy reads claims.

```csharp
builder.Services.AddFlatmate<AppTenant>(tenants => tenants
    .IdentifyByHeader("X-Tenant")
    .IdentifyByHost()
    .UseConnectionString(tenant => tenant.ConnectionString)
    .Resolve(async (identifier, services, cancellationToken) =>
    {
        var db = services.GetRequiredService<CatalogDbContext>();
        return await db.Tenants.FirstOrDefaultAsync(t => t.Host == identifier, cancellationToken);
    },
    tenant => tenant.Hosts));

builder.Services.AddFlatmateCircuit<AppTenant>();
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseSqlServer(services.GetRequiredService<TenantConnectionString>().Value));

var app = builder.Build();
app.UseFlatmate<AppTenant>();
```

`Resolve` receives the request scope. Return every hostname from the second callback so an alias hits the same cached tenant. The default cache keeps a resolution for one hour of inactivity. `CacheFor` and `DisableCaching` change that.

`UseConnectionString` registers `TenantConnectionString` for the current scope. `ITenantScopeFactory<AppTenant>.CreateScope(tenant)` opens a scope with that tenant already selected, which is how startup code and Blazor components create a `DbContext` without an HTTP request. Do not pool contexts that take the connection string from the scope.

`AddFlatmateCircuit` copies the tenant onto the Blazor circuit. Give the document a `<base href>` that includes the tenant prefix, so the circuit connects on a URL the tenant middleware can see.

Inject `ICurrentTenant<AppTenant>` when a request might not have a tenant. Inject `AppTenant` when one is required; that registration throws `TenantNotResolvedException` until a tenant has been resolved.

```csharp
app.UseUnresolvedTenantNotFound<AppTenant>();
app.UseUnresolvedTenantRedirect<AppTenant>("/signup", permanent: true);
app.UsePrimaryHostnameRedirect<AppTenant>(tenant => tenant.PrimaryHost);
```

`IdentifyByPathSegment(rewritePath: true)` removes the tenant segment before routing. Segment 0 is appended to `Request.PathBase`. A later index is removed and the path base stays as it was. Put `UseFlatmate` before `UseRouting`, otherwise endpoint matching still sees the original path.

After a tenant's identifiers change:

```csharp
services.GetRequiredService<ITenantCache<AppTenant>>().Evict(oldHost);
```

## Query filters

Reference `Flatmate.EntityFrameworkCore`. The expression passed as the current key has to be a property on the context. EF Core reads it for each query.

```csharp
public string TenantId => currentTenant.Tenant?.Id ?? "";

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>().HasTenantQueryFilter(order => order.TenantId, () => TenantId);

    // Or, after every entity is configured, for a shared TenantId column:
    modelBuilder.ApplyTenantQueryFilter<string>(() => TenantId);
}
```

A query can skip that filter without turning the others off

```csharp
var everything = db.Orders.IgnoreQueryFilters([TenantQueryFilter.Name]);
```

## Custom resolver

Subclass `MemoryCacheTenantResolver<TTenant>` when resolution needs more than the built-in strategies. The constructor is `(IMemoryCache cache, ILoggerFactory loggerFactory, IOptions<MemoryCacheTenantResolverOptions>? options = null)`. Override `GetContextIdentifier`, `GetTenantIdentifiers`, and `ResolveFromStoreAsync`.

```csharp
builder.Services.AddFlatmate<AppTenant, AppTenantResolver>();
builder.Services.AddTenantConnectionString<AppTenant>(tenant => tenant.ConnectionString);
```

Cache keys are trimmed and compared case-insensitively. Disposable objects in `TenantContext<T>.Properties` are disposed when the cache entry is evicted. The tenant object itself is not. Eviction runs on the thread pool, so it can finish after `Evict` returns.

`UsePerTenant` keeps one branch per key. Pass a stable id. Requests with no tenant continue down the root pipeline.

Cache expiration settings are application-wide: every tenant type shares one `MemoryCacheTenantResolverOptions`.

## Acknowledgements

The request pipeline shape was adapted from [SaasKit](https://github.com/saaskit/saaskit) by Ben Foster, used under the MIT license.

## Support

If you find this project useful, consider supporting its development.

<p align="center">
  <a href="https://buymeacoffee.com/vnmatt">
    <img src="https://github.com/gordon-matt/MyResume/blob/gh-pages/assets/images/BuyMeACoffee_Logo.png"
         alt="Buy Me a Coffee"
         height="40">
  </a>&nbsp;&nbsp;&nbsp;
  <a href="https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=gordon_matt%40live%2ecom&lc=AU&currency_code=AUD&bn=PP%2dDonationsBF%3abtn_donateCC_LG%2egif%3aNonHosted">
    <img src="https://github.com/gordon-matt/MyResume/blob/gh-pages/assets/images/PayPal_Logo.png"
         alt="Donate with PayPal"
         height="40">
  </a>
</p>

<p align="center">
  Prefer using your phone? Scan the QR code:
</p>

<p align="center">
  <a href="https://buymeacoffee.com/vnmatt">
    <img src="https://github.com/gordon-matt/MyResume/blob/gh-pages/assets/images/BuyMeACoffee_QR.png"
         alt="Buy Me a Coffee QR Code"
         width="180">
  </a>
</p>

<img src="https://komarev.com/ghpvc/?username=gordon-matt&label=GitHub%20Hits%20Since%202025-06-01%3A%20&color=ff0000&style=flat" alt="gordon-matt" />