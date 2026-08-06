using System.IO;

static class SettingsConfig
{
    public static bool WarnOnly = false;

    public static void Load(string path)
    {
        if (!File.Exists(path)) return;
        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Trim();
            string val = line.Substring(eq + 1).Trim();
            if (key.Equals("warnOnly", System.StringComparison.OrdinalIgnoreCase))
                WarnOnly = val.Equals("true", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
