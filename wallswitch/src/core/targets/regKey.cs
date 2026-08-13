using Microsoft.Win32;

static class RegKey
{
    public static void Apply(string imagePath)
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
        {
            if (key == null) return;
            key.SetValue("Wallpaper", imagePath);
            key.SetValue("WallpaperStyle", "10");
            key.SetValue("TileWallpaper", "0");
        }
    }
}
