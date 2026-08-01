using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static partial class MasterStateManager
{
    static SmartState ParseSmart(string output, List<SmartAttrDef> smartAttrs)
    {
        var ss = new SmartState
        {
            Endurance = -1,
            ImportantAttrs = new Dictionary<string, long>(),
            ExtraAttrs = new Dictionary<string, long>()
        };
        if (output == null) return ss;

        string o = output;
        var m = Regex.Match(o, @"Device Model:\s+(.+)");
        if (m.Success) ss.Model = m.Groups[1].Value.Trim();
        m = Regex.Match(o, @"Serial Number:\s+(.+)");
        if (m.Success) ss.Serial = m.Groups[1].Value.Trim();
        m = Regex.Match(o, @"Firmware Version:\s+(.+)");
        if (m.Success) ss.Firmware = m.Groups[1].Value.Trim();
        m = Regex.Match(o, @"SMART overall-health self-assessment test result:\s+(\w+)");
        if (m.Success) ss.Health = m.Groups[1].Value;
        m = Regex.Match(o, @"(\d+)\s+---\s+Percentage Used Endurance Indicator");
        if (m.Success)
        {
            int used = int.Parse(m.Groups[1].Value);
            ss.Endurance = 100 - used;
        }
        int importantLimit = smartAttrs.Count < 5 ? smartAttrs.Count : 5;
        for (int i = 0; i < smartAttrs.Count; i++)
        {
            var attr = smartAttrs[i];
            m = Regex.Match(o,
                @"^\s*" + attr.Id + @"\s+\S[\S ]*?\S\s+\S+\s+\d+\s+\d+\s+\S+\s+\S\s+(\d+)",
                RegexOptions.Multiline);
            if (m.Success)
            {
                long val = long.Parse(m.Groups[1].Value);
                if (i < importantLimit)
                    ss.ImportantAttrs[attr.Name] = val;
                else
                    ss.ExtraAttrs[attr.Name] = val;
            }
        }
        return ss;
    }
}
