using System.Drawing;
using System.Runtime.InteropServices;

namespace QuickTabLauncher;

public static class IconProvider
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint FileAttributeNormal = 0x00000080;

    public static Bitmap? GetBitmapFor(AppItem item)
    {
        var bitmap = GetBitmap(item.Icon);
        if (bitmap is not null)
        {
            return bitmap;
        }

        var namedIconPath = FindIconByName(item.Name);
        if (namedIconPath is not null)
        {
            bitmap = GetBitmap(namedIconPath);
            if (bitmap is not null)
            {
                return bitmap;
            }
        }

        return GetBitmap(item.Path);
    }

    public static Bitmap? GetBitmap(string? path)
    {
        var expandedPath = ShortcutResolver.ResolveIconPath(Expand(path));
        if (string.IsNullOrWhiteSpace(expandedPath))
        {
            return null;
        }

        if (!Path.IsPathRooted(expandedPath))
        {
            expandedPath = Path.Combine(AppPaths.BaseDirectory, expandedPath);
        }

        if (IsImageFile(expandedPath) && File.Exists(expandedPath))
        {
            try
            {
                using var image = Image.FromFile(expandedPath);
                return new Bitmap(image);
            }
            catch
            {
                return null;
            }
        }

        var flags = ShgfiIcon | ShgfiLargeIcon;
        if (!File.Exists(expandedPath) && !Directory.Exists(expandedPath))
        {
            flags |= ShgfiUseFileAttributes;
        }

        var info = new ShFileInfo();
        var result = SHGetFileInfo(expandedPath, FileAttributeNormal, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), flags);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using var icon = (Icon)Icon.FromHandle(info.hIcon).Clone();
            return icon.ToBitmap();
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static string Expand(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? "" : Environment.ExpandEnvironmentVariables(path);
    }

    private static bool IsImageFile(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".ico";
    }

    private static string? FindIconByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !Directory.Exists(AppPaths.IconsDirectory))
        {
            return null;
        }

        var normalizedName = NormalizeIconName(name);
        if (normalizedName.Length == 0)
        {
            return null;
        }

        var candidates = Directory.EnumerateFiles(AppPaths.IconsDirectory)
            .Where(IsImageFile)
            .Select(path => new IconCandidate(path, NormalizeIconName(Path.GetFileNameWithoutExtension(path))))
            .Where(candidate => candidate.NormalizedName.Length > 0)
            .ToList();

        var exact = candidates.FirstOrDefault(candidate => candidate.NormalizedName == normalizedName);
        if (exact.Path is not null)
        {
            return exact.Path;
        }

        return candidates
            .Where(candidate =>
                normalizedName.Contains(candidate.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
                candidate.NormalizedName.Contains(normalizedName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.NormalizedName.Length)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }

    private static string NormalizeIconName(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private readonly record struct IconCandidate(string Path, string NormalizedName);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}
