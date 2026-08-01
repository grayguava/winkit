using System;
using System.Text.RegularExpressions;

public static partial class MasterStateManager
{
    static bool? ParseDirty(string output)
    {
        if (output == null) return null;
        if (output.IndexOf("NOT Dirty", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (output.IndexOf("is set", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return null;
    }

    static void ParseChkdsk(string output, DriveState ds)
    {
        if (output == null) return;
        string o = output;
        if (o.IndexOf("Access Denied", StringComparison.OrdinalIgnoreCase) >= 0)
            ds.Filesystem = "access_denied";
        else if (o.IndexOf("found no problems", StringComparison.OrdinalIgnoreCase) >= 0
                 || o.IndexOf("No further action", StringComparison.OrdinalIgnoreCase) >= 0)
            ds.Filesystem = "clean";
        else if (o.IndexOf("found problems", StringComparison.OrdinalIgnoreCase) >= 0
                 || o.IndexOf("problems found", StringComparison.OrdinalIgnoreCase) >= 0)
            ds.Filesystem = "issues";
        else
            ds.Filesystem = "unknown";

        var m = Regex.Match(o, @"(\d+)\s+KB in bad sectors");
        long b;
        if (m.Success && long.TryParse(m.Groups[1].Value, out b))
            ds.BadSectorsKb = b;
        else
            ds.BadSectorsKb = -1;
    }
}
