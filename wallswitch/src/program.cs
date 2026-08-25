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
        try
        {
            Run();
        }
        catch (Exception ex)
        {
            FailLog.Append(ex.ToString());
        }
    }

    static void Run()
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

            IntPtr unused = form.Handle;

            foreach (string failure in form.RegistrationFailures.ToArray())
                FailLog.Append(failure);
            if (form.RegistrationFailures.Count > 0)
                MessageBox.Show(string.Join("\n", form.RegistrationFailures.ToArray()), "wallswitch",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

            Application.EnableVisualStyles();
            Application.Run(form);
        }
    }
}
