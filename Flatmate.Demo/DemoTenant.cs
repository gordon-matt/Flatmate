namespace Flatmate.Demo;

public sealed record DemoTenant(
    string Id,
    string Name,
    string ConnectionString,
    string DatabaseFile,
    bool SharesDatabase,
    string Url)
{
    public string DatabaseLabel => SharesDatabase
        ? $"Shared database {DatabaseFile}"
        : $"Database {DatabaseFile}";
}