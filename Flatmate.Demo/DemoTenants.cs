using Microsoft.Data.Sqlite;

namespace Flatmate.Demo;

public sealed class TenantSettings
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Url { get; set; }

    public string? ConnectionString { get; set; }
}

public sealed class DatabaseSettings
{
    public string? SharedConnectionString { get; set; }
}

public static class DemoTenants
{
    public static DemoTenant[] Load(IConfiguration configuration, string contentRoot)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        var settings = configuration.GetSection("Tenants").Get<List<TenantSettings>>();
        if (settings is null || settings.Count == 0)
        {
            throw new InvalidOperationException("Add at least one tenant under Tenants in appsettings.json.");
        }

        var database = configuration.GetSection("Database").Get<DatabaseSettings>() ?? new DatabaseSettings();
        string? shared = string.IsNullOrWhiteSpace(database.SharedConnectionString)
            ? null
            : NormalizeConnectionString(database.SharedConnectionString, contentRoot, "Database:SharedConnectionString");

        var pending = new List<(string Id, string Name, string ConnectionString, string Url)>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var setting in settings)
        {
            string id = setting.Id.Trim();
            string name = setting.Name.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException("Every tenant needs an Id.");
            }

            if (id.Contains('/') || id.Any(char.IsWhiteSpace))
            {
                throw new InvalidOperationException($"Tenant id '{id}' cannot contain whitespace or a slash.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"Tenant '{id}' needs a Name.");
            }

            if (!ids.Add(id))
            {
                throw new InvalidOperationException($"Tenant id '{id}' is listed more than once.");
            }

            string? url = NormalizeUrl(setting.Url, id);
            if (url is null)
            {
                throw new InvalidOperationException(
                    $"Tenant '{id}' needs a Url. The request host selects the tenant.");
            }

            if (!urls.Add(url))
            {
                throw new InvalidOperationException($"Url '{url}' is listed more than once.");
            }

            string connectionString;
            if (string.IsNullOrWhiteSpace(setting.ConnectionString))
            {
                if (shared is null)
                {
                    throw new InvalidOperationException(
                        $"Tenant '{id}' has no ConnectionString, and Database:SharedConnectionString is not set.");
                }

                connectionString = shared;
            }
            else
            {
                connectionString = NormalizeConnectionString(setting.ConnectionString, contentRoot, $"Tenant '{id}'");
            }

            pending.Add((id, name, connectionString, url));
        }

        foreach (var tenant in pending)
        {
            if (ids.Contains(tenant.Url) && !tenant.Id.Equals(tenant.Url, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Url '{tenant.Url}' matches another tenant id.");
            }
        }

        var tenants = new DemoTenant[pending.Count];
        for (int index = 0; index < pending.Count; index++)
        {
            var tenant = pending[index];
            bool shares = false;
            for (int other = 0; other < pending.Count; other++)
            {
                if (other != index
                    && pending[other].ConnectionString.Equals(tenant.ConnectionString, StringComparison.OrdinalIgnoreCase))
                {
                    shares = true;
                    break;
                }
            }

            string file = Path.GetFileName(new SqliteConnectionStringBuilder(tenant.ConnectionString).DataSource);
            tenants[index] = new DemoTenant(tenant.Id, tenant.Name, tenant.ConnectionString, file, shares, tenant.Url);
            EnsureDirectory(tenant.ConnectionString);
        }

        return tenants;
    }

    private static void EnsureDirectory(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string NormalizeConnectionString(string connectionString, string contentRoot, string owner)
    {
        SqliteConnectionStringBuilder builder;
        try
        {
            builder = new SqliteConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException($"{owner} has an invalid SQLite connection string.", exception);
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException($"{owner} needs a SQLite Data Source.");
        }

        if (!builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
            && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRoot, builder.DataSource));
        }

        return builder.ConnectionString;
    }

    private static string? NormalizeUrl(string? url, string tenantId)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        string value = url.Trim();
        if (value.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var absolute) || string.IsNullOrEmpty(absolute.Host))
            {
                throw new InvalidOperationException($"Tenant '{tenantId}' has a Url that is not a host name.");
            }

            return absolute.Host;
        }

        int slash = value.IndexOf('/');
        if (slash >= 0)
        {
            value = value[..slash];
        }

        int colon = value.LastIndexOf(':');
        if (colon > 0 && int.TryParse(value[(colon + 1)..], out _))
        {
            value = value[..colon];
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Tenant '{tenantId}' has a Url that is not a host name.");
        }

        return value;
    }
}