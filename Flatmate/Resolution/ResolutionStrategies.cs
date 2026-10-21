using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Flatmate;

/// <summary>Reads the request host, with or without the port.</summary>
public sealed class HostResolutionStrategy(bool includePort = true) : ITenantResolutionStrategy
{
    /// <inheritdoc />
    public string? GetIdentifier(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var host = context.Request.Host;
        if (!host.HasValue)
        {
            return null;
        }

        string value = includePort ? host.Value : host.Host;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// <summary>Reads a tenant identifier from a request header.</summary>
public sealed class HeaderResolutionStrategy(string headerName) : ITenantResolutionStrategy
{
    /// <inheritdoc />
    public string? GetIdentifier(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);

        if (!context.Request.Headers.TryGetValue(headerName, out var values))
        {
            return null;
        }

        string value = values.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// <summary>
/// Reads a tenant identifier from a claim on <see cref="HttpContext.User"/>.
/// Authentication must run before tenant resolution for this strategy to see a user.
/// </summary>
public sealed class ClaimResolutionStrategy(string claimType) : ITenantResolutionStrategy
{
    /// <inheritdoc />
    public string? GetIdentifier(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);

        string? value = context.User.FindFirstValue(claimType);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// <summary>
/// Reads a tenant identifier from a path segment. Segment 0 is the first segment after the leading slash.
/// When <paramref name="rewritePath"/> is true, a resolved tenant is removed from the path before the rest of the pipeline runs.
/// Segment 0 is moved onto <see cref="HttpRequest.PathBase"/> so generated links keep the prefix. Any later segment is removed in place.
/// </summary>
public sealed class PathSegmentResolutionStrategy(int segmentIndex = 0, bool rewritePath = false) : ITenantResolutionStrategy
{
    /// <inheritdoc />
    public string? GetIdentifier(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(segmentIndex);

        string? path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segmentIndex >= segments.Length)
        {
            return null;
        }

        if (rewritePath)
        {
            Internal.TenantPath.Mark(context, segmentIndex);
        }

        return segments[segmentIndex];
    }
}