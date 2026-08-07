using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

static class Program {
    [STAThread]
    static void Main() {
        bool createdNew;
        using (var mutex = new Mutex(true, "Wallswitch", out createdNew)) {
            if (!createdNew) return;

            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Daemon.Init(exeDir);

            uint mods; uint vk;
            if (!LoadConfig(out mods, out vk)) {
                MessageBox.Show("Missing or invalid Hotkey= in .conf.", "wallswitch",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Application.EnableVisualStyles();
            Application.Run(new HotkeyForm(mods, vk));
        }
    }

    static bool LoadConfig(out uint mods, out uint vk) {
        mods = 0;
        vk = 0;
        string path = Path.Combine(Daemon.exeDir, ".conf");
        string[] lines;
        try {
            if (!File.Exists(path)) return false;
            lines = File.ReadAllLines(path);
        } catch {
            return false;
        }

        string hotkey = null;
        foreach (string raw in lines) {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Trim();
            string val = line.Substring(eq + 1).Trim();
            if (string.Equals(key, "AssetsDir", StringComparison.OrdinalIgnoreCase))
                Daemon.assetsDir = Path.IsPathRooted(val) ? val : Path.Combine(Daemon.exeDir, val);
            else if (string.Equals(key, "Hotkey", StringComparison.OrdinalIgnoreCase))
                hotkey = val;
        }
        return hotkey != null && Hotkey.Parse(hotkey, out mods, out vk);
    }
}