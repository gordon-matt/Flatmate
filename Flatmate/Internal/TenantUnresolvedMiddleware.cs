using Microsoft.AspNetCore.Http;

namespace Flatmate.Internal;

internal sealed class TenantUnresolvedRedirectMiddleware<TTenant>(
    RequestDelegate next,
    string redirectLocation,
    bool permanentRedirect)
{
    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.GetTenantContext<TTenant>() is null)
        {
            context.Response.StatusCode = permanentRedirect
                ? StatusCodes.Status301MovedPermanently
                : StatusCodes.Status302Found;
            context.Response.Headers.Location = redirectLocation;
            return;
        }

        await next(context);
    }
}

internal sealed class TenantNotFoundMiddleware<TTenant>(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.GetTenantContext<TTenant>() is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    }
}