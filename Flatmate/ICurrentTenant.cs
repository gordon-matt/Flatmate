namespace Flatmate;

/// <summary>
/// The tenant for the current request scope. The accessor itself is never null;
/// <see cref="Tenant"/> is null when the request did not resolve one.
/// </summary>
/// <typeparam name="TTenant">The application's tenant type.</typeparam>
public interface ICurrentTenant<TTenant>
{
    /// <summary>The resolution for this request, or null.</summary>
    TenantContext<TTenant>? Context { get; }

    /// <summary>The tenant for this request, or null.</summary>
    TTenant? Tenant { get; }
}