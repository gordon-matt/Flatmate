using Microsoft.AspNetCore.Http;

namespace Flatmate;

/// <summary>
/// Resolves the tenant for an HTTP request.
/// </summary>
/// <typeparam name="TTenant">The application's tenant type.</typeparam>
public interface ITenantResolver<TTenant>
{
    /// <summary>
    /// Resolves the tenant for <paramref name="context"/>, or returns null when the request has no tenant.
    /// </summary>
    ValueTask<TenantContext<TTenant>?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default);
}