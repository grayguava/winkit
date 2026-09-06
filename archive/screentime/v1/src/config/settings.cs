namespace screentime.config
{
    static class Settings
    {
        public static bool ElevatedMode = false;
        public static int Days = 7;

        public static void Load(string path)
        {
            ElevatedMode = Conf.ReadBool(path, "elevatedMode", false);
            Days = Conf.ReadInt(path, "days", 7);
            if (Days < 1) Days = 1;
            if (Days > 365) Days = 365;
        }
    }
}
