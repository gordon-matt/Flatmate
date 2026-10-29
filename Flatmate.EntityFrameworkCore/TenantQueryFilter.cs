namespace Microsoft.EntityFrameworkCore;

/// <summary>The query filter added by <see cref="TenantQueryFilterExtensions"/>.</summary>
public static class TenantQueryFilter
{
    /// <summary>
    /// Filter name. Pass it to <c>IgnoreQueryFilters</c> to include rows from every tenant:
    /// <c>orders.IgnoreQueryFilters([TenantQueryFilter.Name])</c>.
    /// </summary>
    public const string Name = "Flatmate.Tenant";
}