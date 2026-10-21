namespace Flatmate;

/// <summary>
/// The tenant resolved for the current request, plus any per-resolution state.
/// </summary>
/// <typeparam name="TTenant">The application's tenant type.</typeparam>
public sealed class TenantContext<TTenant> : IDisposable
{
    private int disposed;

    /// <summary>
    /// Creates a context for a resolved tenant.
    /// </summary>
    /// <param name="tenant">The tenant. This object is not disposed with the context.</param>
    /// <param name="identifiers">
    /// Additional cache keys for this tenant, such as every hostname it answers.
    /// The identifier that matched the current request is added automatically when missing.
    /// </param>
    public TenantContext(TTenant tenant, IEnumerable<string>? identifiers = null)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        Tenant = tenant;
        Identifiers = identifiers?
            .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
    }

    /// <summary>A new id for this resolution. It is not stable across requests.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("n");

    /// <summary>The resolved tenant.</summary>
    public TTenant Tenant { get; }

    /// <summary>Hostnames, headers, or other keys that should hit this same context in the cache.</summary>
    public IReadOnlyList<string> Identifiers { get; }

    /// <summary>
    /// Per-resolution state. <see cref="IDisposable"/> values stored here are disposed with the context.
    /// The tenant itself is never disposed.
    /// </summary>
    public Dictionary<string, object?> Properties { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Disposes <see cref="IDisposable"/> entries in <see cref="Properties"/>.
    /// Safe to call more than once. Does not dispose <see cref="Tenant"/>.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        foreach (object? value in Properties.Values)
        {
            if (value is IDisposable disposable && !ReferenceEquals(value, Tenant))
            {
                try
                {
                    disposable.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }
}