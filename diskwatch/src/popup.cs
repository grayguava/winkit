using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class Remind
{
    static string LatestResultPath()
    {
        string logsDir = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "..", "logs"));
        if (!Directory.Exists(logsDir)) return null;
        var dirs = new List<string>(Directory.GetDirectories(logsDir));
        dirs.Sort();
        if (dirs.Count == 0) return null;
        return Path.Combine(dirs[dirs.Count - 1], "result.json");
    }

    public static int Show(bool changesDetected = false)
    {
        string resultPath = LatestResultPath();
        if (resultPath == null || !File.Exists(resultPath))
        {
            Console.Error.WriteLine("No report found. Run diskwatch first.");
            return 1;
        }

        var state = MasterStateManager.Load(resultPath);
        if (state == null)
        {
            Console.Error.WriteLine("Could not read report.");
            return 1;
        }

        string date = state.Timestamp;
        if (date != null && date.Length > 10)
            date = date.Substring(0, 10);

        var b = new System.Text.StringBuilder();
        if (changesDetected)
            b.AppendLine("Some critical values have changed since the last run.");
        else
            b.AppendLine("Critical values are stable since last run.");
        b.AppendLine();
        b.AppendLine(date);
        b.AppendLine();

        if (state.Drives != null)
        {
            foreach (var kv in state.Drives)
            {
                string status;
                if (kv.Value.Dirty == true)
                    status = "DIRTY";
                else
                {
                    status = kv.Value.Filesystem;
                    if (status == "clean") status = "Clean";
                }

                string line = "Drive " + kv.Key + ": " + status;
                if (kv.Value.BadSectorsKb > 0)
                    line += " | " + kv.Value.BadSectorsKb + " KB in bad sectors.";
                else
                    line += ".";
                b.AppendLine(line);
            }
            b.AppendLine();
        }

        SmartState smartState = null;
        string healthStr = "Unknown";
        if (state.Smart != null)
        {
            foreach (var kv in state.Smart)
            {
                smartState = kv.Value;
                healthStr = smartState.Health ?? "Unknown";
                break;
            }
        }
        b.AppendLine("SMART Overall Health: " + healthStr);
        b.AppendLine();

        var chRows = new List<Tuple<string, string>>();
        if (smartState != null && smartState.ImportantAttrs != null && smartState.ImportantAttrs.Count > 0)
        {
            foreach (var akv in smartState.ImportantAttrs)
                chRows.Add(Tuple.Create(akv.Key, akv.Value.ToString()));
        }

        string endurance = "N/A";
        if (smartState != null && smartState.Endurance >= 0 && smartState.Endurance <= 100)
            endurance = smartState.Endurance + "%";

        var diRows = new List<Tuple<string, string>>();
        diRows.Add(Tuple.Create("Endurance", endurance));
        if (smartState != null && smartState.ExtraAttrs != null && smartState.ExtraAttrs.Count > 0)
        {
            long v;
            if (smartState.ExtraAttrs.TryGetValue("Temperature Celsius", out v))
                diRows.Add(Tuple.Create("Temperature", v + " \u00B0" + "°C"));
            if (smartState.ExtraAttrs.TryGetValue("Power On Hours", out v))
                diRows.Add(Tuple.Create("Power-On Hours", v + " h"));
            if (smartState.ExtraAttrs.TryGetValue("Total LBAs Written", out v))
                diRows.Add(Tuple.Create("LBAs Written", v.ToString("N0")));
            if (smartState.ExtraAttrs.TryGetValue("Total LBAs Read", out v))
                diRows.Add(Tuple.Create("LBAs Read", v.ToString("N0")));
        }

        int labelCol = 4;
        int valCol = 0;
        foreach (var row in chRows)
        {
            if (row.Item1.Length + 2 > labelCol) labelCol = row.Item1.Length + 2;
            if (row.Item2.Length > valCol) valCol = row.Item2.Length;
        }
        foreach (var row in diRows)
        {
            if (row.Item1.Length + 2 > labelCol) labelCol = row.Item1.Length + 2;
            if (row.Item2.Length > valCol) valCol = row.Item2.Length;
        }

        string divider = new string('\u2500', 35);

        if (chRows.Count > 0)
        {
            b.AppendLine("Critical Health");
            b.AppendLine(divider);
            foreach (var row in chRows)
                b.AppendLine("  " + row.Item1.PadRight(labelCol) + row.Item2.PadLeft(valCol));
            b.AppendLine();
        }

        b.AppendLine("Drive Information");
        b.AppendLine(divider);
        foreach (var row in diRows)
            b.AppendLine("  " + row.Item1.PadRight(labelCol) + row.Item2.PadLeft(valCol));

        ShowMonospaceDialog(b.ToString(), "Diskwatch", changesDetected);

        return 0;
    }

    static void ShowMonospaceDialog(string text, string title, bool changesDetected)
    {
        using (var form = new Form())
        {
            Color bg = Color.FromArgb(32, 32, 32);
            Color fg = Color.FromArgb(220, 220, 220);

            form.Text = title;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.BackColor = bg;

            var font = new Font("Consolas", 9.5f);
            var icon = SystemIcons.Information;
            if (changesDetected) icon = SystemIcons.Warning;

            var iconBox = new PictureBox
            {
                Image = icon.ToBitmap(),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Location = new Point(16, 16),
                BackColor = bg
            };

            var label = new Label
            {
                Text = text,
                Font = font,
                AutoSize = true,
                Location = new Point(iconBox.Width + 32, 16),
                ForeColor = fg,
                BackColor = bg
            };

            var ok = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(88, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = fg
            };
            ok.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);

            int contentWidth = iconBox.Width + 32 + label.PreferredWidth + 16;
            int contentHeight = Math.Max(iconBox.Height, label.PreferredHeight) + 16;

            ok.Location = new Point(contentWidth - ok.Width - 16, contentHeight + 16);

            form.ClientSize = new Size(contentWidth, contentHeight + 16 + ok.Height + 16);

            form.Controls.Add(iconBox);
            form.Controls.Add(label);
            form.Controls.Add(ok);
            form.AcceptButton = ok;

            form.Load += (s, e) =>
            {
                int useDark = 1;
                if (DwmSetWindowAttribute(form.Handle, 20, ref useDark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref useDark, sizeof(int));
            };

            form.ShowDialog();
        }
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
