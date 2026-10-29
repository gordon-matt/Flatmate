namespace Flatmate.Internal;

/// <summary>
/// Holds a tenant pinned for this scope. A set value wins over the current HTTP request,
/// which is how a Blazor circuit and <see cref="ITenantScopeFactory{TTenant}"/> keep a tenant
/// after the request that created them has gone.
/// </summary>
internal sealed class TenantHolder<TTenant>
    where TTenant : class
{
    public TenantContext<TTenant>? Context { get; set; }
}