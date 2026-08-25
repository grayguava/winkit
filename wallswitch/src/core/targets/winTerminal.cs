using System;
using System.IO;
using System.Text;
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

        byte[] raw = File.ReadAllBytes(settingsPath);
        bool bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
        string text = new UTF8Encoding(false).GetString(raw);
        if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);

        string escaped = imagePath
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("$", "$$");
        string pattern = "(\"backgroundImage\"\\s*:\\s*\")[^\"]*(\")";
        string replaced = Regex.Replace(text, pattern, "$1" + escaped + "$2",
            RegexOptions.IgnoreCase);
        if (replaced == text)
        {
            FailLog.Append("terminal: no \"backgroundImage\" match in " + settingsPath + " - nothing written");
            return;
        }

        string tmp = settingsPath + ".wallswitch.tmp";
        File.WriteAllText(tmp, replaced, new UTF8Encoding(bom));
        File.Copy(settingsPath, settingsPath + ".bak", true);
        File.Replace(tmp, settingsPath, null);
    }
}
