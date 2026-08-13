using System;
using System.IO;
using System.Text.RegularExpressions;

static class WinTerminal
{
    static string settingsPath;

    public static void Configure(string path)
    {
        settingsPath = path;
    }

    public static void Apply(string imagePath)
    {
        if (string.IsNullOrEmpty(settingsPath)) return;
        if (!File.Exists(settingsPath)) return;

        string text = File.ReadAllText(settingsPath);
        string escaped = imagePath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string pattern = "(\"backgroundImage\"\\s*:\\s*\")[^\"]*(\")";
        string replaced = Regex.Replace(text, pattern, "$1" + escaped + "$2",
            RegexOptions.IgnoreCase);
        if (replaced == text) return;
        File.WriteAllText(settingsPath, replaced);
    }
}
