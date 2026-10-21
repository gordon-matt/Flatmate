using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Flatmate.Internal;

internal sealed class TenantPipelineMiddleware<TTenant>
{
    private readonly RequestDelegate next;
    private readonly IApplicationBuilder rootApp;
    private readonly Action<TenantPipelineBuilderContext<TTenant>, IApplicationBuilder> configuration;
    private readonly Func<TTenant, object> keySelector;
    private readonly ConcurrentDictionary<object, RequestDelegate> pipelines = new();

    public TenantPipelineMiddleware(
        RequestDelegate next,
        IApplicationBuilder rootApp,
        Action<TenantPipelineBuilderContext<TTenant>, IApplicationBuilder> configuration,
        Func<TTenant, object>? keySelector)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(rootApp);
        ArgumentNullException.ThrowIfNull(configuration);

        this.next = next;
        this.rootApp = rootApp;
        this.configuration = configuration;
        this.keySelector = keySelector ?? (tenant => tenant!);
    }

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tenantContext = context.GetTenantContext<TTenant>();
        if (tenantContext is null)
        {
            await next(context);
            return;
        }

        object key = keySelector(tenantContext.Tenant)
            ?? throw new InvalidOperationException("The per-tenant pipeline key cannot be null.");

        var pipeline = pipelines.GetOrAdd(key, _ => BuildTenantPipeline(tenantContext));
        await pipeline(context);
    }

    private RequestDelegate BuildTenantPipeline(TenantContext<TTenant> tenantContext)
    {
        var branch = rootApp.New();
        configuration(new TenantPipelineBuilderContext<TTenant>(tenantContext), branch);
        branch.Run(next);
        return branch.Build();
    }
}