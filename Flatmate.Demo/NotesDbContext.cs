using Flatmate;
using Microsoft.EntityFrameworkCore;

namespace Flatmate.Demo;

public sealed class NotesDbContext : DbContext
{
    private readonly ICurrentTenant<DemoTenant> currentTenant;

    public NotesDbContext(DbContextOptions<NotesDbContext> options, ICurrentTenant<DemoTenant> currentTenant)
        : base(options)
    {
        this.currentTenant = currentTenant;
    }

    public DbSet<Note> Notes => Set<Note>();

    public string TenantId => currentTenant.Tenant?.Id ?? "";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>().HasTenantQueryFilter(note => note.TenantId, () => TenantId);
    }
}
