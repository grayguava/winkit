using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        bool createdNew;
        using (var mutex = new Mutex(true, "Wallswitch", out createdNew))
        {
            if (!createdNew) return;

            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Daemon.Init(exeDir);

            var readyPools = Daemon.LoadAndValidate();
            if (readyPools.Count == 0)
            {
                string msg = "No usable pools in .pools (missing dir, no images, or bad hotkey).";
                if (Daemon.Warnings.Count > 0)
                    msg += "\n\n" + string.Join("\n", Daemon.Warnings.ToArray());
                MessageBox.Show(msg, "wallswitch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (Daemon.Warnings.Count > 0)
                MessageBox.Show(string.Join("\n", Daemon.Warnings.ToArray()), "wallswitch",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

            var form = new HotkeyForm();
            Daemon.RegisterHotkeys(form, readyPools);

            Application.EnableVisualStyles();
            Application.Run(form);
        }
    }
}