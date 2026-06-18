using System.Drawing;
using System.Drawing.Drawing2D;
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

        bitmap = GetFolderBitmap(item);
        if (bitmap is not null)
        {
            return bitmap;
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

    private static Bitmap? GetFolderBitmap(AppItem item)
    {
        var path = ResolveFileSystemPath(item.Path);
        return Directory.Exists(path) ? CreateFolderBitmap(item.Name) : null;
    }

    private static string ResolveFileSystemPath(string? path)
    {
        var expandedPath = ShortcutResolver.ResolveIconPath(Expand(path));
        if (string.IsNullOrWhiteSpace(expandedPath) ||
            expandedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            expandedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            expandedPath.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        return Path.IsPathRooted(expandedPath)
            ? expandedPath
            : Path.Combine(AppPaths.BaseDirectory, expandedPath);
    }

    private static Bitmap CreateFolderBitmap(string seed)
    {
        var color = ColorFor(seed);
        var darker = Blend(color, Color.Black, 0.22f);
        var lighter = Blend(color, Color.White, 0.28f);
        var bitmap = new Bitmap(64, 64);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var shadow = new SolidBrush(Color.FromArgb(55, 0, 0, 0));
        graphics.FillEllipse(shadow, new Rectangle(10, 48, 45, 7));

        using var tabBrush = new SolidBrush(lighter);
        graphics.FillRoundedRectangle(tabBrush, new Rectangle(10, 14, 24, 14), 5);

        using var bodyBrush = new LinearGradientBrush(new Rectangle(8, 22, 48, 32), lighter, color, 90f);
        graphics.FillRoundedRectangle(bodyBrush, new Rectangle(8, 20, 48, 36), 8);

        using var accentBrush = new SolidBrush(Color.FromArgb(150, 255, 255, 255));
        graphics.FillRoundedRectangle(accentBrush, new Rectangle(15, 29, 34, 5), 3);

        using var border = new Pen(darker, 2f);
        graphics.DrawRoundedRectangle(border, new Rectangle(8, 20, 48, 36), 8);
        return bitmap;
    }

    private static Color ColorFor(string seed)
    {
        var palette = new[]
        {
            Color.FromArgb(77, 171, 247),
            Color.FromArgb(81, 207, 102),
            Color.FromArgb(255, 212, 59),
            Color.FromArgb(255, 146, 43),
            Color.FromArgb(240, 101, 149),
            Color.FromArgb(132, 94, 247),
            Color.FromArgb(34, 184, 207),
            Color.FromArgb(148, 216, 45)
        };

        var hash = NormalizeIconName(seed).Aggregate(17, (current, ch) => current * 31 + ch);
        return palette[(hash & int.MaxValue) % palette.Length];
    }

    private static Color Blend(Color color, Color target, float amount)
    {
        return Color.FromArgb(
            color.A,
            (int)(color.R + (target.R - color.R) * amount),
            (int)(color.G + (target.G - color.G) * amount),
            (int)(color.B + (target.B - color.B) * amount));
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
