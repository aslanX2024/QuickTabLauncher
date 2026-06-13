using System.Runtime.InteropServices;
using System.Text;

namespace QuickTabLauncher;

public sealed class ShortcutTarget
{
    public string TargetPath { get; init; } = "";
    public string Arguments { get; init; } = "";
    public string WorkingDirectory { get; init; } = "";
}

public static class ShortcutResolver
{
    public static string ResolveIconPath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        return Path.GetExtension(expanded).Equals(".lnk", StringComparison.OrdinalIgnoreCase) &&
               TryResolveShortcut(expanded, out var shortcut) &&
               !string.IsNullOrWhiteSpace(shortcut.TargetPath)
            ? shortcut.TargetPath
            : expanded;
    }

    public static string IdentityFor(string path, string? arguments = null)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        var expandedArguments = Environment.ExpandEnvironmentVariables(arguments ?? "");

        if (Path.GetExtension(expanded).Equals(".lnk", StringComparison.OrdinalIgnoreCase) &&
            TryResolveShortcut(expanded, out var shortcut) &&
            !string.IsNullOrWhiteSpace(shortcut.TargetPath))
        {
            return Normalize(shortcut.TargetPath, shortcut.Arguments);
        }

        if (Path.GetExtension(expanded).Equals(".url", StringComparison.OrdinalIgnoreCase) &&
            TryReadUrl(expanded, out var url))
        {
            return Normalize(url);
        }

        return Normalize(expanded, expandedArguments);
    }

    public static bool TryReadUrl(string path, out string url)
    {
        url = "";

        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                {
                    url = line[4..].Trim();
                    return !string.IsNullOrWhiteSpace(url);
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static bool TryResolveShortcut(string shortcutPath, out ShortcutTarget target)
    {
        target = new ShortcutTarget();

        try
        {
            var link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(shortcutPath, 0);

            var targetPath = new StringBuilder(512);
            var arguments = new StringBuilder(512);
            var workingDirectory = new StringBuilder(512);

            link.GetPath(targetPath, targetPath.Capacity, IntPtr.Zero, 0);
            link.GetArguments(arguments, arguments.Capacity);
            link.GetWorkingDirectory(workingDirectory, workingDirectory.Capacity);

            target = new ShortcutTarget
            {
                TargetPath = targetPath.ToString(),
                Arguments = arguments.ToString(),
                WorkingDirectory = workingDirectory.ToString()
            };

            return !string.IsNullOrWhiteSpace(target.TargetPath);
        }
        catch
        {
            return false;
        }
    }

    private static string Normalize(string value, string? arguments = null)
    {
        var normalizedValue = value.Trim().TrimEnd('\\').ToUpperInvariant();
        var normalizedArguments = string.IsNullOrWhiteSpace(arguments) ? "" : "|" + arguments.Trim().ToUpperInvariant();
        return normalizedValue + normalizedArguments;
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink;

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
