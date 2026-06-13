using System.IO;
using System.Text.Json;

namespace QuickTabLauncher;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly HashSet<string> ShortcutExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".lnk", ".url", ".exe", ".bat", ".cmd"
    };

    public IReadOnlyList<AppItem> Load()
    {
        AppPaths.EnsureLayout();

        var items = new List<AppItem>();
        items.AddRange(LoadFromJson());
        items.AddRange(LoadFromShortcutFolder());

        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.Path))
            .GroupBy(item => ShortcutResolver.IdentityFor(item.Path, item.Arguments), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => !item.FromShortcutFolder)
                .ThenByDescending(item => !string.Equals(item.Group, "Shortcuts", StringComparison.OrdinalIgnoreCase))
                .First())
            .OrderBy(item => item.Group ?? "zzz", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static IEnumerable<AppItem> LoadFromJson()
    {
        try
        {
            var json = File.ReadAllText(AppPaths.AppsJson);
            return JsonSerializer.Deserialize<List<AppItem>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<AppItem> LoadFromShortcutFolder()
    {
        if (!Directory.Exists(AppPaths.ShortcutsDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(AppPaths.ShortcutsDirectory, "*", SearchOption.AllDirectories)
            .Where(path => ShortcutExtensions.Contains(Path.GetExtension(path)))
            .Select(path => new AppItem
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Path = path,
                Group = GroupFromShortcutPath(path),
                FromShortcutFolder = true
            });
    }

    private static string GroupFromShortcutPath(string path)
    {
        var relativeDirectory = Path.GetRelativePath(AppPaths.ShortcutsDirectory, Path.GetDirectoryName(path) ?? "");
        if (string.IsNullOrWhiteSpace(relativeDirectory) || relativeDirectory == ".")
        {
            return "Shortcuts";
        }

        return relativeDirectory.Replace(Path.DirectorySeparatorChar.ToString(), " / ");
    }
}
