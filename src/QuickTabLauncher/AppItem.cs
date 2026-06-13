namespace QuickTabLauncher;

public sealed class AppItem
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? Icon { get; init; }
    public string? Group { get; init; }
    public bool FromShortcutFolder { get; init; }
}
