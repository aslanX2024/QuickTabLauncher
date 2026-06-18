using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using FormsTimer = System.Windows.Forms.Timer;

namespace QuickTabLauncher;

public sealed class LauncherForm : Form
{
    private const int PanelWidth = 296;
    private const int TabWidth = 16;
    private const int HiddenTriggerWidth = 8;
    private const int InitialLauncherHeight = 320;
    private const int PanelPadding = 16;
    private const int AppListTop = 110;
    private const int IconButtonSize = 50;
    private const int IconGap = 3;
    private const int IconColumns = 5;
    private const int NoteTopGap = 8;
    private const int NoteBoxHeight = 34;

    private readonly ConfigService _configService = new();
    private readonly FormsTimer _hideTimer;
    private readonly FormsTimer _animationTimer;
    private readonly FormsTimer _refreshTimer;
    private readonly FormsTimer _startupRevealTimer;
    private readonly FileSystemWatcher _shortcutsWatcher;
    private readonly FileSystemWatcher _configWatcher;
    private readonly ContextMenuStrip _contextMenu;
    private readonly RoundedPanel _surface;
    private readonly EdgeTab _tab;
    private readonly Panel _appList;
    private readonly TextBox _noteBox;
    private readonly TextBox _searchBox;
    private readonly Label _statusLabel;
    private readonly Button _pinButton;
    private IReadOnlyList<AppItem> _apps = [];
    private int _targetLeft;
    private bool _isPinned;
    private bool _isOpen;
    private bool _isContextMenuOpen;

    public LauncherForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.TransparentKey;
        ClientSize = new Size(PanelWidth + TabWidth, InitialLauncherHeight);
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "QuickTabLauncher";
        TopMost = true;
        TransparencyKey = Theme.TransparentKey;

        _hideTimer = new FormsTimer { Interval = 420 };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!_isPinned && !ClientRectangle.Contains(PointToClient(Cursor.Position)))
            {
                HidePanel();
            }
        };

        _animationTimer = new FormsTimer { Interval = 12 };
        _animationTimer.Tick += (_, _) => AnimateStep();

        _refreshTimer = new FormsTimer { Interval = 250 };
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            ReloadApps();
        };

        _startupRevealTimer = new FormsTimer { Interval = 1400 };
        _startupRevealTimer.Tick += (_, _) =>
        {
            _startupRevealTimer.Stop();
            if (!_isPinned && !ClientRectangle.Contains(PointToClient(Cursor.Position)))
            {
                HidePanel();
            }
        };

        _contextMenu = BuildContextMenu();
        _contextMenu.Opening += (_, _) =>
        {
            _isContextMenuOpen = true;
            _hideTimer.Stop();
            ShowPanel();
        };
        _contextMenu.Closed += (_, _) =>
        {
            _isContextMenuOpen = false;
            ScheduleHide();
        };
        ContextMenuStrip = _contextMenu;

        _surface = new RoundedPanel
        {
            Bounds = new Rectangle(0, 0, PanelWidth, InitialLauncherHeight),
            BackColor = Theme.Panel,
            BorderColor = Theme.Border,
            Radius = 18
        };
        _surface.ContextMenuStrip = _contextMenu;
        Controls.Add(_surface);

        var title = new Label
        {
            AutoSize = false,
            Bounds = new Rectangle(16, 13, 190, 24),
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Theme.Text,
            Text = "QuickTab"
        };
        _surface.Controls.Add(title);

        var subtitle = new Label
        {
            AutoSize = false,
            Bounds = new Rectangle(17, 38, 180, 18),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Theme.MutedText,
            Text = "ikon launcher"
        };
        _surface.Controls.Add(subtitle);

        _pinButton = new Button
        {
            Bounds = new Rectangle(248, 14, 30, 30),
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Theme.Text,
            Text = "P"
        };
        _pinButton.FlatAppearance.BorderColor = Theme.Border;
        _pinButton.FlatAppearance.MouseDownBackColor = Theme.Pressed;
        _pinButton.FlatAppearance.MouseOverBackColor = Theme.Hover;
        _pinButton.BackColor = Theme.Button;
        _pinButton.Click += (_, _) => TogglePin();
        _surface.Controls.Add(_pinButton);
        ToolTipService.SetToolTip(_pinButton, "Paneli sabitle");

        _searchBox = new TextBox
        {
            Bounds = new Rectangle(16, 68, 262, 28),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Input,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 9.5f)
        };
        _searchBox.TextChanged += (_, _) => RenderApps();
        _searchBox.KeyDown += SearchBoxOnKeyDown;
        _surface.Controls.Add(_searchBox);

        _appList = new Panel
        {
            Bounds = new Rectangle(16, AppListTop, 262, 180),
            AutoScroll = false,
            BackColor = Theme.Panel
        };
        _surface.Controls.Add(_appList);

        _noteBox = new TextBox
        {
            Bounds = new Rectangle(16, 266, 262, NoteBoxHeight),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Input,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 9.5f),
            PlaceholderText = "Hızlı not... (Enter kaydet)"
        };
        _noteBox.KeyDown += NoteBoxOnKeyDown;
        _surface.Controls.Add(_noteBox);

        _statusLabel = new Label
        {
            AutoSize = false,
            Bounds = new Rectangle(18, 306, 256, 20),
            Font = new Font("Segoe UI", 8f),
            ForeColor = Theme.MutedText,
            Text = ""
        };
        _surface.Controls.Add(_statusLabel);

        _tab = new EdgeTab
        {
            Bounds = new Rectangle(PanelWidth, 104, TabWidth, 112),
            Cursor = Cursors.Hand
        };
        _tab.ContextMenuStrip = _contextMenu;
        _tab.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_isOpen)
                {
                    HidePanel();
                }
                else
                {
                    ShowPanel();
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                _contextMenu.Show(_tab, e.Location);
            }
        };
        Controls.Add(_tab);

        MouseLeave += (_, _) => ScheduleHide();
        _surface.MouseLeave += (_, _) => ScheduleHide();
        _tab.MouseEnter += (_, _) => ShowPanel();
        _tab.MouseLeave += (_, _) => ScheduleHide();
        Deactivate += (_, _) => ScheduleHide();

        Load += (_, _) =>
        {
            ReloadApps();
            SetInitialPosition();
            HidePanel(immediate: true);
            BeginInvoke(() =>
            {
                ShowPanel();
                _startupRevealTimer.Start();
            });
        };

        _shortcutsWatcher = CreateWatcher(AppPaths.ShortcutsDirectory, "*.*", includeSubdirectories: true);
        _configWatcher = CreateWatcher(AppPaths.ConfigDirectory, "apps.json", includeSubdirectories: false);
    }

    public void ReloadApps()
    {
        _apps = _configService.Load();
        RenderApps();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hideTimer.Dispose();
            _animationTimer.Dispose();
            _refreshTimer.Dispose();
            _startupRevealTimer.Dispose();
            _shortcutsWatcher.Dispose();
            _configWatcher.Dispose();
            _contextMenu.Dispose();
        }

        base.Dispose(disposing);
    }

    public void ShowPanel()
    {
        _hideTimer.Stop();
        _isOpen = true;
        AnimateTo(0);
    }

    private void HidePanel(bool immediate = false)
    {
        _isOpen = false;
        _searchBox.Parent?.Focus();
        var hiddenLeft = HiddenLeft;
        if (immediate)
        {
            Left = hiddenLeft;
            _targetLeft = hiddenLeft;
        }
        else
        {
            AnimateTo(hiddenLeft);
        }
    }

    private void RenderApps()
    {
        _appList.SuspendLayout();
        _appList.Controls.Clear();

        var y = 0;
        var filtered = FilterApps().ToList();
        foreach (var group in filtered.GroupBy(item => item.Group ?? "Apps"))
        {
            var groupLabel = new Label
            {
                Bounds = new Rectangle(2, y + 2, 246, 18),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Theme.Accent,
                Text = group.Key
            };
            _appList.Controls.Add(groupLabel);
            y += 24;

            var x = 0;
            var column = 0;
            foreach (var item in group)
            {
                var button = new AppButton(item)
                {
                    Bounds = new Rectangle(x, y, IconButtonSize, IconButtonSize)
                };
                button.Activated += (_, _) => TryLaunch(item);
                ToolTipService.SetToolTip(button, TooltipFor(item));
                _appList.Controls.Add(button);

                column++;
                if (column >= IconColumns)
                {
                    column = 0;
                    x = 0;
                    y += IconButtonSize + IconGap;
                }
                else
                {
                    x += IconButtonSize + IconGap;
                }
            }

            if (column != 0)
            {
                y += IconButtonSize + IconGap;
            }

            y += 4;
        }

        if (filtered.Count == 0)
        {
            _statusLabel.ForeColor = Theme.MutedText;
            _statusLabel.Text = "Sonuç yok. apps.json veya Shortcuts klasörüne ekle.";
        }
        else if (!_statusLabel.Text.StartsWith("Not kaydedildi.", StringComparison.Ordinal))
        {
            _statusLabel.Text = "";
        }

        ResizeToContent(Math.Max(y, filtered.Count == 0 ? 42 : 0));

        _appList.ResumeLayout();
    }

    private IEnumerable<AppItem> FilterApps()
    {
        var query = _searchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return _apps;
        }

        return _apps.Where(item =>
            item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            (item.Group?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false));
    }

    private void TryLaunch(AppItem item)
    {
        try
        {
            var runAsAdmin = (ModifierKeys & Keys.Shift) == Keys.Shift;
            LaunchService.Launch(item, runAsAdmin);
            HidePanel();
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = Theme.Error;
            _statusLabel.Text = $"Açılamadı: {ex.Message}";
        }
    }

    private void SearchBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            _searchBox.Clear();
            HidePanel();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            var first = FilterApps().FirstOrDefault();
            if (first is not null)
            {
                TryLaunch(first);
                e.Handled = true;
            }
        }
    }

    private void NoteBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;
        e.Handled = true;

        var note = _noteBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        try
        {
            NoteService.SaveQuickNote(note);
            _noteBox.Clear();
            _statusLabel.ForeColor = Theme.Accent;
            _statusLabel.Text = "Not kaydedildi.";
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = Theme.Error;
            _statusLabel.Text = $"Not kaydedilemedi: {ex.Message}";
        }
    }

    private void TogglePin()
    {
        _isPinned = !_isPinned;
        _pinButton.Text = _isPinned ? "U" : "P";
        ToolTipService.SetToolTip(_pinButton, _isPinned ? "Sabitlemeyi kaldır" : "Paneli sabitle");
        if (!_isPinned)
        {
            ScheduleHide();
        }
    }

    private void SetInitialPosition()
    {
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);
        Top = Math.Max(workArea.Top + 24, workArea.Top + (workArea.Height - Height) / 2);
    }

    private void ResizeToContent(int appContentHeight)
    {
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);
        var statusHeight = string.IsNullOrWhiteSpace(_statusLabel.Text) ? 0 : 24;
        var noteTop = AppListTop + appContentHeight + NoteTopGap;
        var wantedHeight = noteTop + NoteBoxHeight + statusHeight + PanelPadding;
        var maxHeight = Math.Max(InitialLauncherHeight, workArea.Height - 48);
        var newHeight = Math.Clamp(wantedHeight, 190, maxHeight);

        if (Height != newHeight)
        {
            var wasHidden = Left < -10;
            ClientSize = new Size(PanelWidth + TabWidth, newHeight);
            if (wasHidden)
            {
                Left = HiddenLeft;
                _targetLeft = Left;
            }
        }

        _surface.Bounds = new Rectangle(0, 0, PanelWidth, newHeight);
        _appList.Bounds = new Rectangle(PanelPadding, AppListTop, 262, Math.Max(40, appContentHeight));
        _noteBox.Bounds = new Rectangle(PanelPadding, noteTop, 262, NoteBoxHeight);
        _statusLabel.Bounds = new Rectangle(18, noteTop + NoteBoxHeight + 4, 256, 20);
        _tab.Bounds = new Rectangle(PanelWidth, Math.Max(40, (newHeight - _tab.Height) / 2), TabWidth, 112);
        SetInitialPosition();
    }

    private int HiddenLeft => -(PanelWidth + TabWidth - HiddenTriggerWidth);

    private void ScheduleHide()
    {
        if (_isPinned || _isContextMenuOpen)
        {
            return;
        }

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Yenile", null, (_, _) => ReloadApps());
        menu.Items.Add("apps.json düzenle", null, (_, _) => OpenPath(AppPaths.AppsJson));
        menu.Items.Add("Shortcuts klasörü", null, (_, _) => OpenPath(AppPaths.ShortcutsDirectory));
        menu.Items.Add("Notları aç", null, (_, _) => OpenPath(AppPaths.NotesInbox));
        menu.Items.Add(new ToolStripSeparator());

        var startupItem = new ToolStripMenuItem("Windows ile başlat")
        {
            Checked = StartupService.IsEnabled(),
            CheckOnClick = true
        };
        startupItem.CheckedChanged += (_, _) => StartupService.SetEnabled(startupItem.Checked);
        menu.Items.Add(startupItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => Application.Exit());
        return menu;
    }

    private FileSystemWatcher CreateWatcher(string path, string filter, bool includeSubdirectories)
    {
        var watcher = new FileSystemWatcher(path, filter)
        {
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        watcher.Created += (_, _) => QueueRefresh();
        watcher.Deleted += (_, _) => QueueRefresh();
        watcher.Renamed += (_, _) => QueueRefresh();
        watcher.Changed += (_, _) => QueueRefresh();
        return watcher;
    }

    private void QueueRefresh()
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(() =>
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        });
    }

    private static void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static string TooltipFor(AppItem item)
    {
        var group = string.IsNullOrWhiteSpace(item.Group) ? "Apps" : item.Group;
        return $"{item.Name}\n{group}\n{item.Path}";
    }

    private void AnimateTo(int targetLeft)
    {
        _targetLeft = targetLeft;
        _animationTimer.Start();
    }

    private void AnimateStep()
    {
        var delta = _targetLeft - Left;
        if (Math.Abs(delta) <= 2)
        {
            Left = _targetLeft;
            _animationTimer.Stop();
            return;
        }

        Left += Math.Sign(delta) * Math.Max(2, Math.Abs(delta) / 4);
    }
}

internal sealed class AppButton : Control
{
    private readonly AppItem _item;
    private readonly Image? _icon;
    private bool _hover;
    private bool _pressed;

    public event EventHandler? Activated;

    public AppButton(AppItem item)
    {
        _item = item;
        _icon = IconProvider.GetBitmap(item.Icon ?? item.Path);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        ForeColor = Theme.Text;
        SetStyle(ControlStyles.Selectable, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        var shouldActivate = _pressed && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location);
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);

        if (shouldActivate)
        {
            Activated?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var backColor = _pressed ? Theme.Pressed : _hover ? Theme.Hover : Theme.Panel;
        using var back = new SolidBrush(backColor);
        using var iconBack = new SolidBrush(Theme.Button);

        e.Graphics.FillRoundedRectangle(back, new Rectangle(0, 0, Width - 1, Height - 1), 12);
        e.Graphics.FillRoundedRectangle(iconBack, new Rectangle(4, 4, Width - 8, Height - 8), 12);

        if (_icon is not null)
        {
            var iconSize = 30;
            var offset = (Width - iconSize) / 2;
            e.Graphics.DrawImage(_icon, new Rectangle(offset, offset, iconSize, iconSize));
        }
        else
        {
            TextRenderer.DrawText(e.Graphics, Initial(), new Font("Segoe UI", 11f, FontStyle.Bold),
                new Rectangle(4, 4, Width - 8, Height - 8), Theme.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

    }

    private string Initial()
    {
        return string.IsNullOrWhiteSpace(_item.Name) ? "?" : _item.Name[..1].ToUpperInvariant();
    }
}

internal sealed class EdgeTab : Control
{
    public EdgeTab()
    {
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var back = new SolidBrush(Theme.Panel);
        using var border = new Pen(Theme.Border);
        using var accent = new SolidBrush(Theme.Accent);
        using var muted = new SolidBrush(Color.FromArgb(120, 133, 144, 160));

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        e.Graphics.FillRoundedRectangle(back, rect, 10);
        e.Graphics.DrawRoundedRectangle(border, rect, 10);

        var centerX = Width / 2 - 2;
        e.Graphics.FillRoundedRectangle(muted, new Rectangle(centerX, 24, 4, 18), 3);
        e.Graphics.FillRoundedRectangle(accent, new Rectangle(centerX, 47, 4, 18), 3);
        e.Graphics.FillRoundedRectangle(muted, new Rectangle(centerX, 70, 4, 18), 3);
    }
}

internal sealed class RoundedPanel : Panel
{
    public Color BorderColor { get; set; }
    public int Radius { get; set; } = 18;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(BorderColor);
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        e.Graphics.DrawRoundedRectangle(border, bounds, Radius);
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = RoundedPath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using var path = RoundedPath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal static class Theme
{
    public static readonly Color TransparentKey = Color.FromArgb(1, 2, 3);
    public static readonly Color Panel = Color.FromArgb(26, 31, 39);
    public static readonly Color Button = Color.FromArgb(38, 48, 59);
    public static readonly Color Hover = Color.FromArgb(43, 53, 66);
    public static readonly Color Pressed = Color.FromArgb(51, 64, 80);
    public static readonly Color Input = Color.FromArgb(37, 45, 55);
    public static readonly Color Border = Color.FromArgb(64, 78, 96);
    public static readonly Color Text = Color.FromArgb(244, 247, 251);
    public static readonly Color MutedText = Color.FromArgb(141, 152, 168);
    public static readonly Color Accent = Color.FromArgb(125, 227, 196);
    public static readonly Color Error = Color.FromArgb(255, 126, 126);
}

internal static class ToolTipService
{
    private static readonly ToolTip ToolTip = new();

    public static void SetToolTip(Control control, string text)
    {
        ToolTip.SetToolTip(control, text);
    }
}
