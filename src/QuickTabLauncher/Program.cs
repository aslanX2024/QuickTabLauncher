using System.Windows.Forms;

namespace QuickTabLauncher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        AppPaths.EnsureLayout();

        using var form = new LauncherForm();
        using var tray = new TrayController(form);

        Application.Run(form);
    }
}
