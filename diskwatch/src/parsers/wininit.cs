using System;
using System.Text.RegularExpressions;

public static partial class MasterStateManager
{
    static string ParseWininit(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;
        var m = Regex.Match(output, @"(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2})");
        return m.Success ? m.Groups[1].Value : "found";
    }
}
