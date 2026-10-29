using Flatmate;
using Flatmate.Demo;
using Flatmate.Demo.Components;
using Flatmate.Demo.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

DemoTenant[] tenants = DemoTenants.Load(builder.Configuration, builder.Environment.ContentRootPath);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<IReadOnlyList<DemoTenant>>(tenants);
builder.Services.AddFlatmate<DemoTenant>(options => options
    .IdentifyByHost(includePort: false)
    .UseConnectionString(tenant => tenant.ConnectionString)
    .Resolve(
        (identifier, _) => ValueTask.FromResult(tenants.FirstOrDefault(tenant =>
            tenant.Url.Equals(identifier, StringComparison.OrdinalIgnoreCase))),
        tenant => [tenant.Url]));
builder.Services.AddFlatmateCircuit<DemoTenant>();
builder.Services.AddDbContext<NotesDbContext>((services, options) =>
    options.UseSqlite(services.GetRequiredService<TenantConnectionString>().Value));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseFlatmate<DemoTenant>();
app.UseRouting();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await DemoData.SeedAsync(app.Services);
app.Run();