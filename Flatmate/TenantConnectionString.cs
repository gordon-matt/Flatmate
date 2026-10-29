namespace Flatmate;

/// <summary>The connection string for the tenant pinned to the current scope.</summary>
public sealed record TenantConnectionString
{
    /// <summary>Creates a connection string value.</summary>
    public TenantConnectionString(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The connection string.</summary>
    public string Value { get; }
}