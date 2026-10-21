using Flatmate;
using Microsoft.EntityFrameworkCore;

namespace Flatmate.Demo;

public static class DemoData
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var tenants = services.GetRequiredService<IReadOnlyList<DemoTenant>>();
        var scopes = services.GetRequiredService<ITenantScopeFactory<DemoTenant>>();

        foreach (var tenant in tenants)
        {
            using var scope = scopes.CreateScope(tenant);
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            await db.Database.EnsureCreatedAsync();

            if (await db.Notes.AnyAsync())
            {
                continue;
            }

            db.Notes.Add(new Note
            {
                TenantId = tenant.Id,
                Text = tenant.Id == "acme" ? "Ship the Acme catalog" : "Contoso onboarding checklist"
            });
            await db.SaveChangesAsync();
        }
    }
}
