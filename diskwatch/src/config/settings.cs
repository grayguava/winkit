using System.IO;

static class SettingsConfig
{
    public static bool WarnOnly = false;

    public static void Load(string path)
    {
        WarnOnly = Conf.ReadBool(path, "warnOnly", false);
    }
}
