using Microsoft.Win32;

namespace QuickTabLauncher;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuickTabLauncher";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(ValueName) is string value && value.Contains(ExecutablePath(), StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{ExecutablePath()}\"");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    private static string ExecutablePath()
    {
        return Environment.ProcessPath ?? "";
    }
}
