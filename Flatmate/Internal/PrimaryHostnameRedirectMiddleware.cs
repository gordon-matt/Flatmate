using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace Flatmate.Internal;

internal sealed class PrimaryHostnameRedirectMiddleware<TTenant>(
    RequestDelegate next,
    Func<TTenant, string?> primaryHostnameAccessor,
    bool permanentRedirect)
{
    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tenantContext = context.GetTenantContext<TTenant>();
        if (tenantContext is not null)
        {
            string? primaryHostname = primaryHostnameAccessor(tenantContext.Tenant);
            string? requestHost = context.Request.Host.Value;
            if (!string.IsNullOrWhiteSpace(primaryHostname)
                && !string.Equals(requestHost, primaryHostname, StringComparison.OrdinalIgnoreCase))
            {
                Redirect(context, primaryHostname);
                return;
            }
        }

        await next(context);
    }

    private void Redirect(HttpContext context, string primaryHostname)
    {
        var destination = new UriBuilder(context.Request.GetEncodedUrl());
        var host = HostString.FromUriComponent(primaryHostname);
        destination.Host = host.Host;
        destination.Port = host.Port ?? -1;

        context.Response.StatusCode = permanentRedirect
            ? StatusCodes.Status301MovedPermanently
            : StatusCodes.Status302Found;
        context.Response.Headers.Location = destination.Uri.AbsoluteUri;
    }
}