using Microsoft.AspNetCore.Http;

namespace Flatmate.Internal;

internal sealed class CurrentTenant<TTenant>(
    TenantHolder<TTenant> holder,
    IHttpContextAccessor httpContextAccessor) : ICurrentTenant<TTenant>
    where TTenant : class
{
    public TenantContext<TTenant>? Context =>
        holder.Context ?? httpContextAccessor.HttpContext?.GetTenantContext<TTenant>();

    public TTenant? Tenant => Context is null ? default : Context.Tenant;
}
