static class SettingsConfig
{
    public static bool WarnOnly = false;
    public static int LogRetention = 5;

    public static void Load(string path)
    {
        WarnOnly = Conf.ReadBool(path, "warnOnly", false);
        LogRetention = Conf.ReadInt(path, "logRetention", 5);
    }
}
