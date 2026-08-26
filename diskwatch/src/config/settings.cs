using System;

static class SettingsConfig
{
    public static bool WarnOnly = false;
    public static int LogRetention = 5;
    public static int CommandTimeoutMinutes = 60;

    public static void Load(string path)
    {
        WarnOnly = Conf.ReadBool(path, "warnOnly", false);
        LogRetention = Math.Max(1, Conf.ReadInt(path, "logRetention", 5));
        CommandTimeoutMinutes = Math.Max(1, Conf.ReadInt(path, "commandTimeoutMinutes", 60));
    }
}
