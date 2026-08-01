using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

public static partial class MasterStateManager
{
    public static MasterState Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            string json = File.ReadAllText(path);
            var jss = new JavaScriptSerializer();
            var d = jss.Deserialize<Dictionary<string, object>>(json);
            return MapMaster(d);
        }
        catch { return null; }
    }

    public static void Save(string path, MasterState state)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, ToJson(state), new System.Text.UTF8Encoding(false));
    }

    public static List<string> Diff(MasterState prev, MasterState curr)
    {
        var changes = new List<string>();
        if (prev == null) return changes;
        if (prev.Drives == null || curr.Drives == null) return changes;

        foreach (var kv in curr.Drives)
        {
            DriveState pd;
            if (!prev.Drives.TryGetValue(kv.Key, out pd))
            {
                changes.Add(kv.Key + ": new drive");
                continue;
            }
            DriveState cd = kv.Value;
            if (pd.Dirty != cd.Dirty)
                changes.Add(kv.Key + ": dirty " + pd.Dirty + " \u2192 " + cd.Dirty);
            if (pd.Filesystem != cd.Filesystem)
                changes.Add(kv.Key + ": filesystem " + pd.Filesystem + " \u2192 " + cd.Filesystem);
            if (pd.BadSectorsKb != cd.BadSectorsKb)
                changes.Add(kv.Key + ": bad sectors " + pd.BadSectorsKb + " \u2192 " + cd.BadSectorsKb);
        }

        if (prev.Smart != null && curr.Smart != null)
        {
            foreach (var kv in curr.Smart)
            {
                SmartState ps;
                if (!prev.Smart.TryGetValue(kv.Key, out ps))
                {
                    changes.Add(kv.Key + ": new smart device");
                    continue;
                }
                SmartState cs = kv.Value;
                if (ps.Health != cs.Health)
                    changes.Add(kv.Key + ": health " + ps.Health + " \u2192 " + cs.Health);
                if (ps.Endurance != cs.Endurance)
                    changes.Add(kv.Key + ": endurance " + ps.Endurance + "% \u2192 " + cs.Endurance + "%");
                if (cs.ImportantAttrs != null)
                {
                    foreach (var akv in cs.ImportantAttrs)
                    {
                        long pv;
                        if (ps.ImportantAttrs != null && ps.ImportantAttrs.TryGetValue(akv.Key, out pv) && pv != akv.Value)
                            changes.Add(kv.Key + ": " + akv.Key + " " + pv + " \u2192 " + akv.Value);
                    }
                }
                if (cs.ExtraAttrs != null)
                {
                    foreach (var akv in cs.ExtraAttrs)
                    {
                        long pv;
                        if (ps.ExtraAttrs != null && ps.ExtraAttrs.TryGetValue(akv.Key, out pv) && pv != akv.Value)
                            changes.Add(kv.Key + ": extra " + akv.Key + " " + pv + " \u2192 " + akv.Value);
                    }
                }
            }
        }

        if (prev.LastRepair != curr.LastRepair)
            changes.Add("repair events changed");

        return changes;
    }

    public static string EncodeJson(string s)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('"');
        foreach (char c in s)
        {
            if (c == '"') sb.Append("\\\"");
            else if (c == '\\') sb.Append("\\\\");
            else sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    class RawResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
    }

    static RawResult LoadRaw(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            var jss = new JavaScriptSerializer();
            return jss.Deserialize<RawResult>(json);
        }
        catch { return null; }
    }

    static MasterState MapMaster(Dictionary<string, object> d)
    {
        var s = new MasterState();
        if (d == null) return s;
        s.Timestamp = d.ContainsKey("timestamp") ? (string)d["timestamp"] : null;
        if (d.ContainsKey("drives"))
        {
            var dd = d["drives"] as Dictionary<string, object>;
            if (dd != null)
            {
                s.Drives = new Dictionary<string, DriveState>();
                foreach (var kv in dd)
                    s.Drives[kv.Key] = MapDrive(kv.Value as Dictionary<string, object>);
            }
        }
        if (d.ContainsKey("smart"))
        {
            var sd = d["smart"] as Dictionary<string, object>;
            if (sd != null)
            {
                s.Smart = new Dictionary<string, SmartState>();
                foreach (var kv in sd)
                    s.Smart[kv.Key] = MapSmart(kv.Value as Dictionary<string, object>);
            }
        }
        s.LastRepair = d.ContainsKey("lastRepair") ? (string)d["lastRepair"] : null;
        return s;
    }

    static DriveState MapDrive(Dictionary<string, object> d)
    {
        var ds = new DriveState();
        if (d == null) return ds;
        if (d.ContainsKey("dirty")) ds.Dirty = d["dirty"] as bool?;
        ds.Filesystem = d.ContainsKey("filesystem") ? (string)d["filesystem"] : null;
        if (d.ContainsKey("badSectorsKb")) ds.BadSectorsKb = Convert.ToInt64(d["badSectorsKb"]);
        return ds;
    }

    static SmartState MapSmart(Dictionary<string, object> d)
    {
        var ss = new SmartState();
        if (d == null) return ss;
        ss.Model = d.ContainsKey("model") ? (string)d["model"] : null;
        ss.Serial = d.ContainsKey("serial") ? (string)d["serial"] : null;
        ss.Firmware = d.ContainsKey("firmware") ? (string)d["firmware"] : null;
        ss.Health = d.ContainsKey("health") ? (string)d["health"] : null;
        if (d.ContainsKey("endurance")) ss.Endurance = Convert.ToInt32(d["endurance"]);
        if (d.ContainsKey("important"))
        {
            var ad = d["important"] as Dictionary<string, object>;
            if (ad != null)
            {
                ss.ImportantAttrs = new Dictionary<string, long>();
                foreach (var kv in ad)
                    ss.ImportantAttrs[kv.Key] = Convert.ToInt64(kv.Value);
            }
        }
        if (d.ContainsKey("extras"))
        {
            var ad = d["extras"] as Dictionary<string, object>;
            if (ad != null)
            {
                ss.ExtraAttrs = new Dictionary<string, long>();
                foreach (var kv in ad)
                    ss.ExtraAttrs[kv.Key] = Convert.ToInt64(kv.Value);
            }
        }
        return ss;
    }

    static string ToJson(MasterState s)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\r\n");
        Field(sb, "timestamp", s.Timestamp, 1); sb.Append(",\r\n");
        sb.Append("  \"drives\": {\r\n");
        bool first = true;
        if (s.Drives != null)
        {
            foreach (var kv in s.Drives)
            {
                if (!first) sb.Append(",\r\n");
                first = false;
                sb.Append("    \"" + kv.Key + "\": {\r\n");
                Field(sb, "dirty", kv.Value.Dirty, 3); sb.Append(",\r\n");
                Field(sb, "filesystem", kv.Value.Filesystem, 3); sb.Append(",\r\n");
                Field(sb, "badSectorsKb", kv.Value.BadSectorsKb, 3); sb.Append("\r\n");
                sb.Append("    }");
            }
        }
        sb.Append("\r\n  },\r\n");
        sb.Append("  \"smart\": {\r\n");
        first = true;
        if (s.Smart != null)
        {
            foreach (var kv in s.Smart)
            {
                if (!first) sb.Append(",\r\n");
                first = false;
                sb.Append("    \"" + kv.Key + "\": {\r\n");
                Field(sb, "model", kv.Value.Model, 3); sb.Append(",\r\n");
                Field(sb, "serial", kv.Value.Serial, 3); sb.Append(",\r\n");
                Field(sb, "firmware", kv.Value.Firmware, 3); sb.Append(",\r\n");
                Field(sb, "health", kv.Value.Health, 3); sb.Append(",\r\n");
                Field(sb, "endurance", kv.Value.Endurance, 3); sb.Append(",\r\n");

                sb.Append("      \"important\": {\r\n");
                bool afirst = true;
                if (kv.Value.ImportantAttrs != null)
                {
                    foreach (var akv in kv.Value.ImportantAttrs)
                    {
                        if (!afirst) sb.Append(",\r\n");
                        afirst = false;
                        sb.Append("        \"" + akv.Key + "\": " + akv.Value);
                    }
                }
                sb.Append("\r\n      },\r\n");

                sb.Append("      \"extras\": {\r\n");
                afirst = true;
                if (kv.Value.ExtraAttrs != null)
                {
                    foreach (var akv in kv.Value.ExtraAttrs)
                    {
                        if (!afirst) sb.Append(",\r\n");
                        afirst = false;
                        sb.Append("        \"" + akv.Key + "\": " + akv.Value);
                    }
                }
                sb.Append("\r\n      }\r\n");
                sb.Append("    }");
            }
        }
        sb.Append("\r\n  },\r\n");
        Field(sb, "lastRepair", s.LastRepair, 1); sb.Append("\r\n");
        sb.Append("}\r\n");
        return sb.ToString();
    }

    static void Field(System.Text.StringBuilder sb, string name, object val, int indent)
    {
        string pad = new string(' ', indent * 2);
        sb.Append(pad + "\"" + name + "\": ");
        if (val == null) sb.Append("null");
        else if (val is bool) sb.Append(((bool)val) ? "true" : "false");
        else if (val is long || val is int) sb.Append(val.ToString());
        else
        {
            sb.Append('"');
            foreach (char c in val.ToString())
            {
                if (c == '"') sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\t') sb.Append("\\t");
                else sb.Append(c);
            }
            sb.Append('"');
        }
    }
}
