using Microsoft.AspNetCore.Http;

namespace Flatmate;

/// <summary>
/// Reads a tenant identifier from an HTTP request. The first strategy that returns a value wins.
/// </summary>
public interface ITenantResolutionStrategy
{
    /// <summary>The identifier for this request, or null when this strategy does not apply.</summary>
    string? GetIdentifier(HttpContext context);
}