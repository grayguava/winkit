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
            Drives = new Dictionary<string, DriveState>(),
            Smart = new Dictionary<string, SmartState>()
        };

        if (Directory.Exists(runDir))
        {
            foreach (string file in Directory.GetFiles(runDir, "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                var result = LoadRaw(file);
                if (result == null) continue;

                if (name.StartsWith("fsutil_") || name.StartsWith("chkdsk_"))
                {
                    string letter = name.Substring(name.IndexOf('_') + 1);
                    if (string.IsNullOrEmpty(letter)) continue;
                    if (name.StartsWith("fsutil_"))
                        GetOrCreateDrive(state, letter).Dirty = ParseDirty(result.Output);
                    else
                        ParseChkdsk(result.Output, GetOrCreateDrive(state, letter));
                }
                else if (name.StartsWith("smartctl_"))
                {
                    string label = name.Substring(name.IndexOf('_') + 1);
                    state.Smart[label] = ParseSmart(result.Output, smartAttrs);
                }
            }
        }

        return state;
    }

    static DriveState GetOrCreateDrive(MasterState state, string letter)
    {
        DriveState ds;
        if (!state.Drives.TryGetValue(letter, out ds))
        {
            ds = new DriveState { BadSectorsKb = -1 };
            state.Drives[letter] = ds;
        }
        return ds;
    }
}
