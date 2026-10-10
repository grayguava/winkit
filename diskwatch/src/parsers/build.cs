using System;
using System.Collections.Generic;
using System.IO;

public static partial class MasterStateManager
{
    public static MasterState Build(string runDir, List<SmartAttrDef> smartAttrs)
    {
        var state = new MasterState
        {
            Timestamp = DateTime.Now.ToString("o"),
            Smart = new Dictionary<string, SmartState>()
        };

        if (Directory.Exists(runDir))
        {
            foreach (string file in Directory.GetFiles(runDir, "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                var result = LoadRaw(file);
                if (result == null) continue;

                if (name.StartsWith("smartctl_"))
                {
                    string label = name.Substring(name.IndexOf('_') + 1);
                    state.Smart[label] = ParseSmart(result.Output, smartAttrs);
                }
            }
        }

        return state;
    }
}
