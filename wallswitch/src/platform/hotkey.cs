using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class HotkeyForm : Form
{
    const uint WM_HOTKEY = 0x0312;
    const uint MOD_NOREPEAT = 0x4000;

    readonly List<Registration> registrations = new List<Registration>();

    public readonly List<string> RegistrationFailures = new List<string>();

    public HotkeyForm()
    {
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new System.Drawing.Point(-10000, -10000);
        ClientSize = new System.Drawing.Size(1, 1);
    }

    public void AddHotkey(int id, uint mods, uint vk)
    {
        registrations.Add(new Registration { Id = id, Mods = mods, Vk = vk });
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        foreach (var reg in registrations)
        {
            bool ok = RegisterHotKey(Handle, reg.Id, reg.Mods | MOD_NOREPEAT, reg.Vk);
            if (!ok)
                RegistrationFailures.Add("Hotkey id=" + reg.Id + " could not be registered (may be in use).");
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        foreach (var reg in registrations)
            UnregisterHotKey(Handle, reg.Id);
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            try
            {
                Daemon.HandleHotkey((int)m.WParam);
            }
            catch (Exception ex)
            {
                FailLog.Append(ex.ToString());
            }
        }
        base.WndProc(ref m);
    }

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    class Registration
    {
        public int Id;
        public uint Mods;
        public uint Vk;
    }
}

static class Hotkey
{
    const uint MOD_ALT = 0x0001;
    const uint MOD_CONTROL = 0x0002;
    const uint MOD_SHIFT = 0x0004;
    const uint MOD_WIN = 0x0008;

    public static bool Parse(string spec, out uint mods, out uint vk)
    {
        mods = 0;
        vk = 0;
        bool bareKey = false;
        foreach (string part in spec.Split('+'))
        {
            string token = part.Trim();
            if (token.Length == 0) return false;
            string low = token.ToLowerInvariant();
            if (low == "ctrl" || low == "control") mods |= MOD_CONTROL;
            else if (low == "alt") mods |= MOD_ALT;
            else if (low == "shift") mods |= MOD_SHIFT;
            else if (low == "win" || low == "super" || low == "meta" || low == "cmd") mods |= MOD_WIN;
            else if (low == "space") vk = 0x20;
            else if (token.Length == 1)
            {
                char c = char.ToUpperInvariant(token[0]);
                if (c >= 'A' && c <= 'Z') vk = (uint)c;
                else if (c >= '0' && c <= '9') vk = (uint)c;
                else return false;
            }
            else if (token.Length >= 2 && (token[0] == 'F' || token[0] == 'f'))
            {
                int n;
                if (int.TryParse(token.Substring(1), out n) && n >= 1 && n <= 24)
                {
                    vk = (uint)(0x70 + n - 1);
                    if (n <= 12) bareKey = true;
                }
                else return false;
            }
            else return false;
        }
        if (mods != 0) return vk != 0;
        return bareKey;
    }
}
