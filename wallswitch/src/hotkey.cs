using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class HotkeyForm : Form {
    const int WM_HOTKEY = 0x0312;
    const int HOTKEY_ID = 1;

    readonly uint mods;
    readonly uint vk;

    public HotkeyForm(uint mods, uint vk) {
        this.mods = mods;
        this.vk = vk;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new System.Drawing.Point(-10000, -10000);
        ClientSize = new System.Drawing.Size(1, 1);
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        if (!RegisterHotKey(Handle, HOTKEY_ID, mods | 0x4000, vk))
            MessageBox.Show("Unable to register hotkey (may be in use).", "wallswitch",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
        UnregisterHotKey(Handle, HOTKEY_ID);
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m) {
        if (m.Msg == WM_HOTKEY)
            Daemon.Advance();
        base.WndProc(ref m);
    }

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

static class Hotkey {
    const int MOD_ALT = 0x0001;
    const int MOD_CONTROL = 0x0002;
    const int MOD_SHIFT = 0x0004;
    const int MOD_WIN = 0x0008;

    public static bool Parse(string spec, out uint mods, out uint vk) {
        mods = 0;
        vk = 0;
        foreach (string part in spec.Split('+')) {
            string token = part.Trim();
            if (token.Length == 0) return false;
            string low = token.ToLowerInvariant();
            if (low == "ctrl" || low == "control") mods |= MOD_CONTROL;
            else if (low == "alt") mods |= MOD_ALT;
            else if (low == "shift") mods |= MOD_SHIFT;
            else if (low == "win" || low == "super" || low == "meta" || low == "cmd") mods |= MOD_WIN;
            else if (low == "space") vk = 0x20;
            else if (token.Length == 1) {
                char c = char.ToUpperInvariant(token[0]);
                if (c >= 'A' && c <= 'Z') vk = (uint)c;
                else if (c >= '0' && c <= '9') vk = (uint)c;
                else return false;
            }
            else if (token.Length >= 2 && (token[0] == 'F' || token[0] == 'f')) {
                int n;
                if (int.TryParse(token.Substring(1), out n) && n >= 1 && n <= 24)
                    vk = (uint)(0x70 + n - 1);
                else return false;
            }
            else return false;
        }
        return mods != 0 && vk != 0;
    }
}