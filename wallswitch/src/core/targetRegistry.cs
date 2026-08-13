using System.Collections.Generic;

static class TargetRegistry
{
    public static List<string> EnabledNames = new List<string>();

    public static void Load(List<IniParse.Section> sections)
    {
        EnabledNames = new List<string>();
        if (IsEnabled(sections, "Desktop")) EnabledNames.Add("Desktop");
        if (IsEnabled(sections, "Terminal"))
        {
            WinTerminal.Configure(IniParse.ReadValue(
                IniParse.Find(sections, "Terminal"), "SettingsPath", null));
            EnabledNames.Add("Terminal");
        }
        if (IsEnabled(sections, "Registry")) EnabledNames.Add("Registry");
    }

    public static void Apply(string targetName, string imagePath)
    {
        if (targetName == "Desktop") Desktop.Apply(imagePath);
        else if (targetName == "Terminal") WinTerminal.Apply(imagePath);
        else if (targetName == "Registry") RegKey.Apply(imagePath);
    }

    static bool IsEnabled(List<IniParse.Section> sections, string name)
    {
        IniParse.Section section = IniParse.Find(sections, name);
        return section != null && IniParse.ReadBool(section, "Enable", false);
    }
}
