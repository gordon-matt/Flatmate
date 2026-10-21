namespace Flatmate;

/// <summary>
/// Thrown when a service requires <typeparamref name="TTenant"/> and the current request has no tenant.
/// Inject <see cref="ICurrentTenant{TTenant}"/> instead when the tenant is optional.
/// </summary>
public sealed class TenantNotResolvedException : InvalidOperationException
{
    /// <summary>Creates an exception for the given tenant type.</summary>
    public TenantNotResolvedException(Type tenantType)
        : base($"No {tenantType.Name} was resolved for the current request. " +
               "Run tenant resolution before services that require a tenant, or inject ICurrentTenant<TTenant> when the tenant is optional.")
    {
        ArgumentNullException.ThrowIfNull(tenantType);
        TenantType = tenantType;
    }

    /// <summary>The tenant type that was requested.</summary>
    public Type TenantType { get; }
}