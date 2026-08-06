using System.Collections.Generic;

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
        foreach (string line in Conf.Lines(path))
        {
            string key, value;
            if (!Conf.KeyValue(line, out key, out value))
            {
                key = line;
                value = line;
            }
            int id;
            if (int.TryParse(key, out id))
                attrs.Add(new SmartAttrDef { Id = id, Name = value });
        }
        return attrs;
    }
}
