using System;
using System.Collections.Generic;
using System.IO;

static class LogConf
{
    static Dictionary<string, Dictionary<string, string>> sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    public static void Load(string path)
    {
        if (!File.Exists(path)) return;

        Dictionary<string, string> current = null;
        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                string name = line.Substring(1, line.Length - 2).Trim();
                if (!sections.TryGetValue(name, out current))
                {
                    current = new Dictionary<string, string>();
                    sections[name] = current;
                }
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0 || current == null) continue;
            current[line.Substring(0, eq).Trim().ToLowerInvariant()] = line.Substring(eq + 1).Trim();
        }
    }

    public static bool Enabled(string tool)
    {
        return ReadBool(tool, "log", !tool.Equals("read", StringComparison.OrdinalIgnoreCase));
    }

    public static int Count(string tool)
    {
        return ReadInt(tool, "logcount", 10);
    }

    public static string GeneralString(string key, string defaultValue)
    {
        return ReadValue("General", key.ToLowerInvariant()) ?? defaultValue;
    }

    static bool ReadBool(string tool, string key, bool defaultValue)
    {
        string v = ReadValue(tool, key);
        if (v == null) return defaultValue;
        return v.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    static int ReadInt(string tool, string key, int defaultValue)
    {
        string v = ReadValue(tool, key);
        if (v == null) return defaultValue;
        int n;
        if (!int.TryParse(v, out n) || n < 0) return defaultValue;
        return n;
    }

    static string ReadValue(string tool, string key)
    {
        Dictionary<string, string> section;
        if (!sections.TryGetValue(tool, out section)) return null;
        string value;
        if (!section.TryGetValue(key, out value)) return null;
        return value;
    }
}
