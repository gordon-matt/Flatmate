using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Flatmate.Internal;

internal sealed class TenantResolutionMiddleware<TTenant>(
    RequestDelegate next,
    ILogger<TenantResolutionMiddleware<TTenant>> logger)
{
    public async Task Invoke(HttpContext context, ITenantResolver<TTenant> tenantResolver)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantResolver);

        logger.LogDebug("Resolving {TenantType} with {ResolverType}.", typeof(TTenant).Name, tenantResolver.GetType().Name);

        var tenantContext = await tenantResolver.ResolveAsync(context, context.RequestAborted);
        if (tenantContext is null)
        {
            logger.LogDebug("No {TenantType} was resolved.", typeof(TTenant).Name);
        }
        else
        {
            logger.LogDebug("Resolved {TenantType} as tenant context {TenantContextId}.", typeof(TTenant).Name, tenantContext.Id);
            context.SetTenantContext(tenantContext);
            TenantPath.Rewrite(context);
        }

        await next(context);
    }
}