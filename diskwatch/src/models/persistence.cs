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
        foreach (var kv in prev.Drives)
            if (!curr.Drives.ContainsKey(kv.Key))
                changes.Add(kv.Key + ": drive disappeared");

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
            foreach (var kv in prev.Smart)
                if (!curr.Smart.ContainsKey(kv.Key))
                    changes.Add(kv.Key + ": smart device disappeared");
        }

        return changes;
    }

    static string EscapeJsonString(string s)
    {
        if (s == null) return "null";
        var sb = new System.Text.StringBuilder();
        sb.Append('"');
        foreach (char c in s)
        {
            if (c == '"') sb.Append("\\\"");
            else if (c == '\\') sb.Append("\\\\");
            else if (c == '\r') sb.Append("\\r");
            else if (c == '\n') sb.Append("\\n");
            else if (c == '\t') sb.Append("\\t");
            else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
            else sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    static long AsLong(object v, long fallback)
    {
        if (v is long) return (long)v;
        if (v is int) return (int)v;
        if (v is double)
        {
            try { return Convert.ToInt64((double)v); } catch { }
        }
        if (v is decimal)
        {
            try { return Convert.ToInt64((decimal)v); } catch { }
        }
        if (v is string)
        {
            long r;
            if (long.TryParse((string)v, out r)) return r;
        }
        return fallback;
    }

    static int AsInt(object v, int fallback)
    {
        if (v is int) return (int)v;
        if (v is long) return (int)(long)v;
        if (v is double)
        {
            try { return Convert.ToInt32((double)v); } catch { }
        }
        if (v is decimal)
        {
            try { return Convert.ToInt32((decimal)v); } catch { }
        }
        if (v is string)
        {
            int r;
            if (int.TryParse((string)v, out r)) return r;
        }
        return fallback;
    }

    static string AsString(Dictionary<string, object> d, string key)
    {
        object v;
        if (d.TryGetValue(key, out v) && v is string) return (string)v;
        return null;
    }

    public static string EncodeJson(string s)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('"');
        foreach (char c in s)
        {
            if (c == '"') sb.Append("\\\"");
            else if (c == '\\') sb.Append("\\\\");
            else if (c == '\r') sb.Append("\\r");
            else if (c == '\n') sb.Append("\\n");
            else if (c == '\t') sb.Append("\\t");
            else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
            else sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    class RawResult
    {
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
        s.Timestamp = AsString(d, "timestamp");
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
        return s;
    }

    static DriveState MapDrive(Dictionary<string, object> d)
    {
        var ds = new DriveState();
        if (d == null) return ds;
        if (d.ContainsKey("dirty")) ds.Dirty = d["dirty"] as bool?;
        ds.Filesystem = AsString(d, "filesystem");
        if (d.ContainsKey("badSectorsKb")) ds.BadSectorsKb = AsLong(d["badSectorsKb"], 0);
        return ds;
    }

    static SmartState MapSmart(Dictionary<string, object> d)
    {
        var ss = new SmartState();
        if (d == null) return ss;
        ss.Model = AsString(d, "model");
        ss.Serial = AsString(d, "serial");
        ss.Firmware = AsString(d, "firmware");
        ss.Health = AsString(d, "health");
        if (d.ContainsKey("endurance")) ss.Endurance = AsInt(d["endurance"], -1);
        if (d.ContainsKey("important"))
        {
            var ad = d["important"] as Dictionary<string, object>;
            if (ad != null)
            {
                ss.ImportantAttrs = new Dictionary<string, long>();
                foreach (var kv in ad)
                {
                    long v = AsLong(kv.Value, long.MinValue);
                    if (v != long.MinValue)
                        ss.ImportantAttrs[kv.Key] = v;
                }
            }
        }
        if (d.ContainsKey("extras"))
        {
            var ad = d["extras"] as Dictionary<string, object>;
            if (ad != null)
            {
                ss.ExtraAttrs = new Dictionary<string, long>();
                foreach (var kv in ad)
                {
                    long v = AsLong(kv.Value, long.MinValue);
                    if (v != long.MinValue)
                        ss.ExtraAttrs[kv.Key] = v;
                }
            }
        }
        return ss;
    }

    static string ToJson(MasterState s)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\r\n");
        sb.Append("  \"timestamp\": " + EscapeJsonString(s.Timestamp) + ",\r\n");
        sb.Append("  \"drives\": {\r\n");
        bool first = true;
        if (s.Drives != null)
        {
            foreach (var kv in s.Drives)
            {
                if (!first) sb.Append(",\r\n");
                first = false;
                sb.Append("    " + EscapeJsonString(kv.Key) + ": {\r\n");
                sb.Append("      \"dirty\": ");
                if (kv.Value.Dirty == null) sb.Append("null");
                else sb.Append(((bool)kv.Value.Dirty) ? "true" : "false");
                sb.Append(",\r\n");
                sb.Append("      \"filesystem\": " + EscapeJsonString(kv.Value.Filesystem) + ",\r\n");
                sb.Append("      \"badSectorsKb\": " + kv.Value.BadSectorsKb + "\r\n");
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
                sb.Append("    " + EscapeJsonString(kv.Key) + ": {\r\n");
                sb.Append("      \"model\": " + EscapeJsonString(kv.Value.Model) + ",\r\n");
                sb.Append("      \"serial\": " + EscapeJsonString(kv.Value.Serial) + ",\r\n");
                sb.Append("      \"firmware\": " + EscapeJsonString(kv.Value.Firmware) + ",\r\n");
                sb.Append("      \"health\": " + EscapeJsonString(kv.Value.Health) + ",\r\n");
                sb.Append("      \"endurance\": " + kv.Value.Endurance + ",\r\n");

                sb.Append("      \"important\": {\r\n");
                bool afirst = true;
                if (kv.Value.ImportantAttrs != null)
                {
                    foreach (var akv in kv.Value.ImportantAttrs)
                    {
                        if (!afirst) sb.Append(",\r\n");
                        afirst = false;
                        sb.Append("        " + EscapeJsonString(akv.Key) + ": " + akv.Value);
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
                        sb.Append("        " + EscapeJsonString(akv.Key) + ": " + akv.Value);
                    }
                }
                sb.Append("\r\n      }\r\n");
                sb.Append("    }");
            }
        }
        sb.Append("\r\n  }\r\n");
        sb.Append("}\r\n");
        return sb.ToString();
    }
}
