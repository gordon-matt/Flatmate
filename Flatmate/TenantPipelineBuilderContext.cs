namespace Flatmate;

/// <summary>
/// Passed to a per-tenant pipeline configuration so the branch can read the tenant it was built for.
/// </summary>
/// <typeparam name="TTenant">The application's tenant type.</typeparam>
public sealed record TenantPipelineBuilderContext<TTenant>
{
    /// <summary>Creates a builder context for a resolved tenant.</summary>
    public TenantPipelineBuilderContext(TenantContext<TTenant> tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        TenantContext = tenantContext;
    }

    /// <summary>The resolution this pipeline was built for.</summary>
    public TenantContext<TTenant> TenantContext { get; }

    /// <summary>The tenant this pipeline was built for.</summary>
    public TTenant Tenant => TenantContext.Tenant;
}
