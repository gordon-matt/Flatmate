using Flatmate;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flatmate.Tests;

public class TenantScopeTests
{
    [Fact]
    public async Task Scope_pins_the_tenant_and_its_connection_string()
    {
        var directory = Directory.CreateTempSubdirectory("flatmate-");
        try
        {
            var services = new ServiceCollection();
            services.AddFlatmate<ScopeTenant>(builder => builder
                .UseConnectionString(tenant => $"Data Source={Path.Combine(directory.FullName, tenant.Id + ".db")}")
                .Resolve((identifier, _) => ValueTask.FromResult<ScopeTenant?>(new ScopeTenant(identifier))));
            services.AddDbContext<ScopeDbContext>((sp, options) =>
                options.UseSqlite(sp.GetRequiredService<TenantConnectionString>().Value));

            await using var provider = services.BuildServiceProvider();
            var scopes = provider.GetRequiredService<ITenantScopeFactory<ScopeTenant>>();

            using (var acme = scopes.CreateScope(new ScopeTenant("acme")))
            {
                var db = acme.ServiceProvider.GetRequiredService<ScopeDbContext>();
                await db.Database.EnsureCreatedAsync();
                db.Rows.Add(new ScopeRow { Name = "from-acme" });
                await db.SaveChangesAsync();
            }

            using (var contoso = scopes.CreateScope(new ScopeTenant("contoso")))
            {
                var db = contoso.ServiceProvider.GetRequiredService<ScopeDbContext>();
                await db.Database.EnsureCreatedAsync();
                db.Rows.Add(new ScopeRow { Name = "from-contoso" });
                await db.SaveChangesAsync();
            }

            using var check = scopes.CreateScope(new ScopeTenant("acme"));
            var rows = await check.ServiceProvider.GetRequiredService<ScopeDbContext>().Rows.Select(row => row.Name).ToListAsync();

            Assert.Equal(["from-acme"], rows);
            Assert.Equal(
                $"Data Source={Path.Combine(directory.FullName, "acme.db")}",
                check.ServiceProvider.GetRequiredService<TenantConnectionString>().Value);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Connection_string_requires_a_tenant()
    {
        var services = new ServiceCollection();
        services.AddFlatmate<ScopeTenant>(builder => builder
            .UseConnectionString(tenant => tenant.Id)
            .Resolve((identifier, _) => ValueTask.FromResult<ScopeTenant?>(new ScopeTenant(identifier))));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Throws<TenantNotResolvedException>(
            () => scope.ServiceProvider.GetRequiredService<TenantConnectionString>());
    }

    [Fact]
    public async Task Circuit_keeps_the_tenant_after_the_request_is_gone()
    {
        var services = new ServiceCollection();
        services.AddFlatmate<ScopeTenant>(builder => builder
            .Resolve((identifier, _) => ValueTask.FromResult<ScopeTenant?>(new ScopeTenant(identifier))));
        services.AddFlatmateCircuit<ScopeTenant>();

        await using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.SetTenantContext(new TenantContext<ScopeTenant>(new ScopeTenant("acme"), ["acme.example"]));
        accessor.HttpContext = httpContext;

        using var circuitScope = provider.CreateScope();
        var handler = Assert.Single(circuitScope.ServiceProvider.GetServices<CircuitHandler>());
        await handler.OnCircuitOpenedAsync(null!, CancellationToken.None);
        accessor.HttpContext = null;
        await handler.OnConnectionUpAsync(null!, CancellationToken.None);

        var current = circuitScope.ServiceProvider.GetRequiredService<ICurrentTenant<ScopeTenant>>();
        Assert.Equal("acme", current.Tenant?.Id);
        Assert.Equal("acme.example", Assert.Single(current.Context!.Identifiers));

        using var otherScope = provider.CreateScope();
        Assert.Null(otherScope.ServiceProvider.GetRequiredService<ICurrentTenant<ScopeTenant>>().Tenant);
    }
}

public sealed record ScopeTenant(string Id);

public sealed class ScopeRow
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
}

public sealed class ScopeDbContext(DbContextOptions<ScopeDbContext> options) : DbContext(options)
{
    public DbSet<ScopeRow> Rows => Set<ScopeRow>();
}
