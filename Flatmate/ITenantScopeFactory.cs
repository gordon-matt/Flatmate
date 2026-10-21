using Microsoft.Extensions.DependencyInjection;

namespace Flatmate;

/// <summary>
/// Opens a dependency-injection scope in which <typeparamref name="TTenant"/> is already selected.
/// Use it from startup, background work, and Blazor, where there is no current HTTP request.
/// </summary>
/// <typeparam name="TTenant">The application's tenant type.</typeparam>
public interface ITenantScopeFactory<TTenant>
    where TTenant : class
{
    /// <summary>
    /// Opens a scope pinned to <paramref name="tenant"/>.
    /// <see cref="ICurrentTenant{TTenant}"/> and <typeparamref name="TTenant"/> resolve to that tenant.
    /// <see cref="TenantConnectionString"/> does too, when a selector is registered.
    /// </summary>
    IServiceScope CreateScope(TTenant tenant);
}
