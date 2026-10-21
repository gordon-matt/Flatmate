using Microsoft.AspNetCore.Http;

namespace Flatmate.Internal;

internal sealed class HttpContextCurrentTenant<TTenant> : ICurrentTenant<TTenant>
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public HttpContextCurrentTenant(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        this.httpContextAccessor = httpContextAccessor;
    }

    public TenantContext<TTenant>? Context => httpContextAccessor.HttpContext?.GetTenantContext<TTenant>();

    public TTenant? Tenant => Context is null ? default : Context.Tenant;
}