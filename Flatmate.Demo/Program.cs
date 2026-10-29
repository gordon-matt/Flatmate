using Flatmate;
using Flatmate.Demo;
using Flatmate.Demo.Components;
using Flatmate.Demo.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

string dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);

DemoTenant[] tenants =
[
    new("acme", "Acme", Path.Combine(dataDirectory, "acme.db")),
    new("contoso", "Contoso", Path.Combine(dataDirectory, "contoso.db"))
];

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<IReadOnlyList<DemoTenant>>(tenants);
builder.Services.AddFlatmate<DemoTenant>(options => options
    .IdentifyByPathSegment(rewritePath: true)
    .UseConnectionString(tenant => $"Data Source={tenant.DatabasePath}")
    .Resolve(
        (identifier, _) => ValueTask.FromResult(tenants.FirstOrDefault(tenant =>
            tenant.Id.Equals(identifier, StringComparison.OrdinalIgnoreCase))),
        tenant => [tenant.Id]));
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