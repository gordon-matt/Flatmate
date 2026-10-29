using Microsoft.Extensions.DependencyInjection;

namespace Flatmate.Internal;

internal sealed class TenantScopeFactory<TTenant>(IServiceScopeFactory scopes) : ITenantScopeFactory<TTenant>
    where TTenant : class
{
    public IServiceScope CreateScope(TTenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantHolder<TTenant>>().Context = new TenantContext<TTenant>(tenant);
        return scope;
    }
}