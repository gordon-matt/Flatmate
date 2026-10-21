using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;

namespace Flatmate.Internal;

internal sealed class TenantCircuitHandler<TTenant>(
    TenantHolder<TTenant> holder,
    IHttpContextAccessor httpContextAccessor) : CircuitHandler
    where TTenant : class
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Capture();
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Capture();
        return Task.CompletedTask;
    }

    private void Capture()
    {
        if (holder.Context is not null)
        {
            return;
        }

        var current = httpContextAccessor.HttpContext?.GetTenantContext<TTenant>();
        if (current is null)
        {
            return;
        }

        // Copy the tenant into a context owned by the circuit. The request's context can be
        // disposed when its cache entry expires, and the circuit outlives that request.
        holder.Context = new TenantContext<TTenant>(current.Tenant, current.Identifiers);
    }
}
