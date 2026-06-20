using System.IO;

namespace QuickTabLauncher;

public static class AppPaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;
    public static string ConfigDirectory => Path.Combine(BaseDirectory, "config");
    public static string IconsDirectory => Path.Combine(BaseDirectory, "Icons");
    public static string NotesDirectory => Path.Combine(BaseDirectory, "Notes");
    public static string ShortcutsDirectory => Path.Combine(BaseDirectory, "Shortcuts");
    public static string AppsJson => Path.Combine(ConfigDirectory, "apps.json");
    public static string SettingsJson => Path.Combine(ConfigDirectory, "settings.json");
    public static string NotesInbox => Path.Combine(NotesDirectory, "Inbox.md");

    public static void EnsureLayout()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(IconsDirectory);
        Directory.CreateDirectory(NotesDirectory);
        Directory.CreateDirectory(ShortcutsDirectory);

        if (!File.Exists(AppsJson))
        {
            File.WriteAllText(AppsJson, DefaultAppsJson);
        }

        var shortcutReadme = Path.Combine(ShortcutsDirectory, "README.txt");
        if (!File.Exists(shortcutReadme))
        {
            File.WriteAllText(shortcutReadme,
                "Put .lnk, .url, .exe, .bat or .cmd shortcuts here. QuickTabLauncher will show them automatically.");
        }

        if (!File.Exists(NotesInbox))
        {
            File.WriteAllText(NotesInbox, "# QuickTab Notes\r\n\r\n");
        }
    }

    private const string DefaultAppsJson = """
[
  {
    "name": "Explorer",
    "path": "explorer.exe",
    "group": "System"
  },
  {
    "name": "Notepad",
    "path": "%WINDIR%\\notepad.exe",
    "group": "System"
  },
  {
    "name": "Calculator",
    "path": "calc.exe",
    "group": "System"
  },
  {
    "name": "Edge",
    "path": "msedge.exe",
    "group": "Web"
  }
]
""";
}
