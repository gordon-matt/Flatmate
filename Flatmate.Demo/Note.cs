namespace Flatmate.Demo;

public sealed class Note
{
    public int Id { get; set; }

    public string TenantId { get; set; } = "";

    public string Text { get; set; } = "";
}
