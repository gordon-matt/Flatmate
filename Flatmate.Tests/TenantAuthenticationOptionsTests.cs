using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Flatmate.Tests;

public class TenantAuthenticationOptionsTests
{
    [Fact]
    public void Get_names_the_cookie_for_the_current_tenant_and_reuses_it()
    {
        using ServiceProvider provider = BuildProvider();
        IOptionsMonitor<CookieAuthenticationOptions> monitor = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        IHttpContextAccessor accessor = provider.GetRequiredService<IHttpContextAccessor>();

        accessor.HttpContext = ContextFor("acme");
        CookieAuthenticationOptions acme = monitor.Get(CookieAuthenticationDefaults.AuthenticationScheme);
        CookieAuthenticationOptions acmeAgain = monitor.Get(CookieAuthenticationDefaults.AuthenticationScheme);

        accessor.HttpContext = ContextFor("contoso");
        CookieAuthenticationOptions contoso = monitor.Get(CookieAuthenticationDefaults.AuthenticationScheme);

        accessor.HttpContext = new DefaultHttpContext();
        CookieAuthenticationOptions none = monitor.Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Equal("acme.Auth", acme.Cookie.Name);
        Assert.Same(acme, acmeAgain);
        Assert.Equal("contoso.Auth", contoso.Cookie.Name);
        Assert.NotSame(acme, contoso);
        Assert.NotSame(acme.DataProtectionProvider, contoso.DataProtectionProvider);
        Assert.Equal(CookieAuthenticationDefaults.CookiePrefix + CookieAuthenticationDefaults.AuthenticationScheme, none.Cookie.Name);
    }

    [Fact]
    public void Get_rejects_an_empty_tenant_key()
    {
        using ServiceProvider provider = BuildProvider();
        IHttpContextAccessor accessor = provider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = ContextFor("  ");

        IOptionsMonitor<CookieAuthenticationOptions> monitor = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        Assert.Throws<InvalidOperationException>(() => monitor.Get(CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddAuthentication().AddCookie();
        services.AddTenantCookieAuthentication<TestTenant>(tenant => tenant.Id);
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext ContextFor(string id)
    {
        var context = new DefaultHttpContext();
        context.SetTenantContext(new TenantContext<TestTenant>(new TestTenant(id)));
        return context;
    }
}
