using System.Globalization;
using System.IO;
using System.Text;

namespace QuickTabLauncher;

public static class NoteService
{
    public static void SaveQuickNote(string text)
    {
        var note = text.Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        AppPaths.EnsureLayout();

        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var entry = new StringBuilder()
            .Append("## ")
            .Append(stamp)
            .AppendLine(" — QuickTab")
            .AppendLine(note)
            .AppendLine()
            .ToString();

        File.AppendAllText(AppPaths.NotesInbox, entry, Encoding.UTF8);
    }
}
