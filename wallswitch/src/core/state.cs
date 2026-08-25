using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class PoolState
{
    public string FilePath;
    public string PoolDir;
    public List<string> Queue = new List<string>();
    public List<string> Shown = new List<string>();

    public static PoolState Load(string path, string poolDir)
    {
        var s = new PoolState();
        s.FilePath = path;
        s.PoolDir = poolDir;
        if (!File.Exists(path)) return s;
        try
        {
            if (new FileInfo(path).Length > 8 * 1024 * 1024) return s;
            string section = "";
            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line == "queue:") { section = "queue"; continue; }
                if (line == "shown:") { section = "shown"; continue; }
                if (IsUnsafeEntry(line)) continue;
                if (section == "queue") s.Queue.Add(line);
                else if (section == "shown") s.Shown.Add(line);
            }
        }
        catch { }
        return s;
    }

    static bool IsUnsafeEntry(string line)
    {
        if (Path.IsPathRooted(line)) return true;
        if (line == "." || line == "..") return true;
        if (line.StartsWith(@"..\") || line.StartsWith("../")) return true;
        if (line.Contains(@"\..\") || line.Contains("/../")) return true;
        return false;
    }

    public void Save()
    {
        var sb = new StringBuilder();
        sb.AppendLine("queue:");
        foreach (var f in Queue) sb.AppendLine(f);
        sb.AppendLine();
        sb.AppendLine("shown:");
        foreach (var f in Shown) sb.AppendLine(f);
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
        if (File.Exists(FilePath))
            File.Replace(tmp, FilePath, null);
        else
            File.Move(tmp, FilePath);
    }

    public string Next()
    {
        while (Queue.Count > 0)
        {
            string chosen = Queue[0];
            Queue.RemoveAt(0);
            string full = Path.Combine(PoolDir, chosen);
            if (File.Exists(full))
            {
                Shown.Add(chosen);
                Save();
                return full;
            }
        }
        return null;
    }

    public string NextFromPool(List<string> extensions)
    {
        string img = Next();
        if (img != null) return img;

        Shown = new List<string>();
        Queue = ScanPool(extensions);
        if (Queue.Count == 0) return null;
        return Next();
    }

    List<string> ScanPool(List<string> extensions)
    {
        var result = new List<string>();
        foreach (string ext in extensions)
            foreach (string f in Directory.GetFiles(PoolDir, ext, SearchOption.TopDirectoryOnly))
                result.Add(MakeRelative(f));
        Shuffle(result);
        return result;
    }

    string MakeRelative(string full)
    {
        string prefix = PoolDir;
        if (!prefix.EndsWith("\\") && !prefix.EndsWith("/")) prefix += "\\";
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? full.Substring(prefix.Length)
            : full;
    }

    static void Shuffle(List<string> list)
    {
        var rng = new Random(Guid.NewGuid().GetHashCode());
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }
}
