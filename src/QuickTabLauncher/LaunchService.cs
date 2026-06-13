using System.Diagnostics;
using System.IO;

namespace QuickTabLauncher;

public static class LaunchService
{
    public static void Launch(AppItem item, bool runAsAdmin = false)
    {
        var path = Environment.ExpandEnvironmentVariables(item.Path);
        var arguments = item.Arguments ?? "";
        var workingDirectory = string.IsNullOrWhiteSpace(item.WorkingDirectory)
            ? GetWorkingDirectory(path)
            : Environment.ExpandEnvironmentVariables(item.WorkingDirectory);

        if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase) &&
            ShortcutResolver.TryResolveShortcut(path, out var shortcut))
        {
            path = shortcut.TargetPath;
            arguments = string.IsNullOrWhiteSpace(arguments) ? shortcut.Arguments : arguments;
            workingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? shortcut.WorkingDirectory : workingDirectory;
        }

        if (Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase) &&
            ShortcutResolver.TryReadUrl(path, out var url))
        {
            path = url;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments,
            UseShellExecute = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        if (runAsAdmin)
        {
            startInfo.Verb = "runas";
        }

        Process.Start(startInfo);
    }

    private static string? GetWorkingDirectory(string path)
    {
        if (File.Exists(path))
        {
            return Path.GetDirectoryName(path);
        }

        return null;
    }

}
