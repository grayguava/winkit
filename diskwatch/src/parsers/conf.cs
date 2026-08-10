using System.Collections.Generic;
using System.IO;

static class Conf
{
    // Non-empty, non-comment lines from a config file.
    // Lines starting with '#' or ';' are ignored.
    public static List<string> Lines(string path)
    {
        var result = new List<string>();
        if (!File.Exists(path)) return result;
        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
            result.Add(line);
        }
        return result;
    }

    // Split "key=value". Returns false when there is no '='.
    public static bool KeyValue(string line, out string key, out string value)
    {
        key = null;
        value = null;
        int eq = line.IndexOf('=');
        if (eq <= 0) return false;
        key = line.Substring(0, eq).Trim();
        value = line.Substring(eq + 1).Trim();
        return true;
    }

    // Read a single boolean key (case-insensitive). Missing key -> defaultValue.
    public static bool ReadBool(string path, string wantedKey, bool defaultValue)
    {
        foreach (string line in Lines(path))
        {
            string key, value;
            if (KeyValue(line, out key, out value)
                && key.Equals(wantedKey, System.StringComparison.OrdinalIgnoreCase))
                return value.Equals("true", System.StringComparison.OrdinalIgnoreCase);
        }
        return defaultValue;
    }

    // Read a single integer key (case-insensitive). Missing key or non-numeric -> defaultValue.
    public static int ReadInt(string path, string wantedKey, int defaultValue)
    {
        foreach (string line in Lines(path))
        {
            string key, value;
            if (KeyValue(line, out key, out value)
                && key.Equals(wantedKey, System.StringComparison.OrdinalIgnoreCase))
            {
                int n;
                if (int.TryParse(value, out n)) return n;
            }
        }
        return defaultValue;
    }
}
