using System.Collections.Generic;
using System.IO;

public class SmartAttrDef
{
    public int Id;
    public string Name;
}

static class SmartAttrConfig
{
    public static List<SmartAttrDef> Load(string path)
    {
        var attrs = new List<SmartAttrDef>();
        if (!File.Exists(path)) return attrs;
        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
            int eq = line.IndexOf('=');
            string idStr = eq >= 0 ? line.Substring(0, eq).Trim() : line.Trim();
            string name = eq >= 0 ? line.Substring(eq + 1).Trim() : idStr;
            int id;
            if (int.TryParse(idStr, out id))
                attrs.Add(new SmartAttrDef { Id = id, Name = name });
        }
        return attrs;
    }
}
