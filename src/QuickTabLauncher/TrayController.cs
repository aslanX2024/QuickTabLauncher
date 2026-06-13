using System.Diagnostics;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace QuickTabLauncher;

public sealed class TrayController : IDisposable
{
    private readonly LauncherForm _window;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;

    public TrayController(LauncherForm window)
    {
        _window = window;
        _menu = BuildMenu();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "QuickTabLauncher",
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => _window.ShowPanel();
        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Right)
            {
                ShowTrayMenu();
            }
        };
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }

    private void ShowTrayMenu()
    {
        SetForegroundWindow(_window.Handle);
        _menu.Show(Forms.Cursor.Position);
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        menu.Items.Add("Paneli aç", null, (_, _) => _window.ShowPanel());
        menu.Items.Add("Yenile", null, (_, _) => _window.ReloadApps());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("apps.json düzenle", null, (_, _) => OpenPath(AppPaths.AppsJson));
        menu.Items.Add("Shortcuts klasörü", null, (_, _) => OpenPath(AppPaths.ShortcutsDirectory));
        menu.Items.Add("Notları aç", null, (_, _) => OpenPath(AppPaths.NotesInbox));

        var startupItem = new Forms.ToolStripMenuItem("Windows ile başlat")
        {
            Checked = StartupService.IsEnabled(),
            CheckOnClick = true
        };
        startupItem.CheckedChanged += (_, _) => StartupService.SetEnabled(startupItem.Checked);
        menu.Items.Add(startupItem);

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) =>
        {
            _notifyIcon.Visible = false;
            Forms.Application.Exit();
        });

        return menu;
    }

    private static void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
