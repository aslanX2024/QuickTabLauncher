using System.Drawing;
using System.Runtime.InteropServices;

namespace QuickTabLauncher;

public static class IconProvider
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint FileAttributeNormal = 0x00000080;

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
            using var image = Image.FromFile(expandedPath);
            return new Bitmap(image);
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
