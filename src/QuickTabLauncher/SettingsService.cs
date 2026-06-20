using System.Text.Json;

namespace QuickTabLauncher;

public sealed class LauncherSettings
{
    public int? PanelTop { get; set; }
}

public static class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static LauncherSettings Load()
    {
        AppPaths.EnsureLayout();

        try
        {
            if (!File.Exists(AppPaths.SettingsJson))
            {
                return new LauncherSettings();
            }

            var json = File.ReadAllText(AppPaths.SettingsJson);
            return JsonSerializer.Deserialize<LauncherSettings>(json, JsonOptions) ?? new LauncherSettings();
        }
        catch
        {
            return new LauncherSettings();
        }
    }

    public static void Save(LauncherSettings settings)
    {
        AppPaths.EnsureLayout();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(AppPaths.SettingsJson, json);
    }
}
