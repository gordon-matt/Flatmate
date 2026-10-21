using Flatmate;

namespace Microsoft.AspNetCore.Http;

/// <summary>Reads and writes the tenant resolved for a request.</summary>
public static class FlatmateHttpContextExtensions
{
    /// <summary>Stores the resolution for the rest of the request.</summary>
    public static void SetTenantContext<TTenant>(this HttpContext context, TenantContext<TTenant> tenantContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);
        context.Items[typeof(TenantContext<TTenant>)] = tenantContext;
    }

    /// <summary>The resolution stored by tenant middleware, or null.</summary>
    public static TenantContext<TTenant>? GetTenantContext<TTenant>(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(typeof(TenantContext<TTenant>), out object? value)
            ? value as TenantContext<TTenant>
            : null;
    }

    /// <summary>The tenant stored by tenant middleware, or default when the request has none.</summary>
    public static TTenant? GetTenant<TTenant>(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tenantContext = context.GetTenantContext<TTenant>();
        return tenantContext is null ? default : tenantContext.Tenant;
    }
}