using System.Collections.Generic;
using System.IO;

static class IniParse
{
    public static List<Section> Parse(string path)
    {
        var result = new List<Section>();
        if (!File.Exists(path)) return result;

        Section current = null;
        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                string name = line.Substring(1, line.Length - 2).Trim();
                current = Find(result, name);
                if (current == null)
                {
                    current = new Section(name);
                    result.Add(current);
                }
                continue;
            }

            if (current == null) continue;
            string key, value;
            if (KeyValue(line, out key, out value))
                current.Values[key] = value;
        }
        return result;
    }

    public static Section Find(List<Section> sections, string name)
    {
        foreach (Section s in sections)
            if (string.Equals(s.Name, name, System.StringComparison.OrdinalIgnoreCase))
                return s;
        return null;
    }

    public static string ReadValue(Section section, string key, string defaultValue)
    {
        if (section == null) return defaultValue;
        foreach (var kv in section.Values)
            if (string.Equals(kv.Key, key, System.StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        return defaultValue;
    }

    public static bool ReadBool(Section section, string key, bool defaultValue)
    {
        string v = ReadValue(section, key, null);
        if (v == null) return defaultValue;
        return v.Equals("true", System.StringComparison.OrdinalIgnoreCase);
    }

    static bool KeyValue(string line, out string key, out string value)
    {
        key = null;
        value = null;
        int eq = line.IndexOf('=');
        if (eq <= 0) return false;
        key = line.Substring(0, eq).Trim();
        value = line.Substring(eq + 1).Trim();
        return true;
    }

    public class Section
    {
        public string Name;
        public Dictionary<string, string> Values = new Dictionary<string, string>();

        public Section(string name)
        {
            Name = name;
        }
    }
}

static class TargetsConfig
{
    public static List<IniParse.Section> Load(string path)
    {
        return IniParse.Parse(path);
    }
}

static class PoolsConfig
{
    public static List<PoolConfig> Load(string path)
    {
        var result = new List<PoolConfig>();
        foreach (var section in IniParse.Parse(path))
        {
            if (section.Name.Length == 0) continue;
            result.Add(new PoolConfig
            {
                Name = section.Name,
                Dir = IniParse.ReadValue(section, "PoolDir", ""),
                Key = IniParse.ReadValue(section, "PoolKey", ""),
                Mode = IniParse.ReadValue(section, "Mode", "random")
            });
        }
        return result;
    }
}

class PoolConfig
{
    public string Name;
    public string Dir;
    public string Key;
    public string Mode;

    public bool IsSync
    {
        get { return string.Equals(Mode, "sync", System.StringComparison.OrdinalIgnoreCase); }
    }
}
