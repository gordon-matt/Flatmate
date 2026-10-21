namespace Flatmate;

/// <summary>
/// Removes cached tenant resolutions. Inject this after a tenant's hostnames or other identifiers change.
/// </summary>
/// <typeparam name="TTenant">The application's tenant type.</typeparam>
public interface ITenantCache<TTenant>
{
    /// <summary>
    /// Drops the cached resolution stored under <paramref name="identifier"/> and, when configured,
    /// the other identifiers that were cached with it.
    /// </summary>
    void Evict(string identifier);
}