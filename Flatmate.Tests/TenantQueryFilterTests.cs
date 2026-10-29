using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flatmate.Tests;

public class TenantQueryFilterTests
{
    [Fact]
    public async Task Query_filter_returns_only_the_current_tenants_rows()
    {
        const string connectionString = "Data Source=flatmate-filter;Mode=Memory;Cache=Shared";
        await using var keepAlive = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        await keepAlive.OpenAsync();

        var services = new ServiceCollection();
        services.AddFlatmate<FilterTenant>(builder => builder
            .Resolve((identifier, _) => ValueTask.FromResult<FilterTenant?>(new FilterTenant(identifier))));
        services.AddDbContext<FilterDbContext>(options => options.UseSqlite(connectionString));

        await using var provider = services.BuildServiceProvider();
        var scopes = provider.GetRequiredService<ITenantScopeFactory<FilterTenant>>();

        using (var seed = scopes.CreateScope(new FilterTenant("acme")))
        {
            var db = seed.ServiceProvider.GetRequiredService<FilterDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Orders.AddRange(
                new Order { TenantId = "acme", Name = "Acme order" },
                new Order { TenantId = "contoso", Name = "Contoso order" });
            db.Audits.Add(new Audit { TenantId = "acme", Name = "Acme audit" });
            db.Logs.Add(new Log { Name = "shared" });
            await db.SaveChangesAsync();
        }

        using var acme = scopes.CreateScope(new FilterTenant("acme"));
        using var contoso = scopes.CreateScope(new FilterTenant("contoso"));
        var acmeDb = acme.ServiceProvider.GetRequiredService<FilterDbContext>();
        var contosoDb = contoso.ServiceProvider.GetRequiredService<FilterDbContext>();

        Assert.Equal("Acme order", Assert.Single(await acmeDb.Orders.Select(order => order.Name).ToListAsync()));
        Assert.Equal("Acme audit", Assert.Single(await acmeDb.Audits.Select(audit => audit.Name).ToListAsync()));
        Assert.Equal("Contoso order", Assert.Single(await contosoDb.Orders.Select(order => order.Name).ToListAsync()));
        Assert.Empty(await contosoDb.Audits.ToListAsync());
        Assert.Equal("shared", Assert.Single(await contosoDb.Logs.Select(log => log.Name).ToListAsync()));

        var unfiltered = await acmeDb.Orders
            .IgnoreQueryFilters([TenantQueryFilter.Name])
            .OrderBy(order => order.Name)
            .Select(order => order.Name)
            .ToListAsync();
        Assert.Equal(["Acme order", "Contoso order"], unfiltered);

        var filter = Assert.Single(acmeDb.Model.FindEntityType(typeof(Order))!.GetDeclaredQueryFilters());
        Assert.Equal(TenantQueryFilter.Name, filter.Key);
        Assert.False(filter.IsAnonymous);
    }

    [Fact]
    public void Query_filter_rejects_a_value_that_is_not_a_context_member()
    {
        var options = new DbContextOptionsBuilder<ConstantFilterDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var db = new ConstantFilterDbContext(options);

        var exception = Assert.Throws<ArgumentException>(() => _ = db.Model);
        Assert.Contains("() => TenantId", exception.Message);
    }
}

public sealed record FilterTenant(string Id);

public sealed class Order
{
    public int Id { get; set; }

    public string TenantId { get; set; } = "";

    public string Name { get; set; } = "";
}

public sealed class Audit
{
    public int Id { get; set; }

    public string TenantId { get; set; } = "";

    public string Name { get; set; } = "";
}

public sealed class Log
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
}

public sealed class FilterDbContext : DbContext
{
    private readonly ICurrentTenant<FilterTenant> currentTenant;

    public FilterDbContext(DbContextOptions<FilterDbContext> options, ICurrentTenant<FilterTenant> currentTenant)
        : base(options)
    {
        this.currentTenant = currentTenant;
    }

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Audit> Audits => Set<Audit>();

    public DbSet<Log> Logs => Set<Log>();

    public string TenantId => currentTenant.Tenant?.Id ?? string.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().HasTenantQueryFilter(order => order.TenantId, () => TenantId);
        modelBuilder.Entity<Audit>();
        modelBuilder.Entity<Log>();
        modelBuilder.ApplyTenantQueryFilter<string>(() => TenantId);
    }
}

public sealed class ConstantFilterDbContext(DbContextOptions<ConstantFilterDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Order>().HasTenantQueryFilter(order => order.TenantId, () => "acme");
}