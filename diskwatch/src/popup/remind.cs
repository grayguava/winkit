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

        string statusLine = changesDetected
            ? "Some critical values have changed since the last run."
            : "Critical values are stable since last run.";
        DateTime runTime;
        string lastRun = state.Timestamp != null && DateTime.TryParse(state.Timestamp, out runTime)
            ? runTime.ToString("dd-MM-yyyy 'at' hh:mm tt")
            : (state.Timestamp ?? "");

        var b = new System.Text.StringBuilder();
        b.AppendLine(statusLine);
        b.AppendLine();
        b.AppendLine("Last run: " + lastRun);
        b.AppendLine(new string('\u2500', 40));
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

        int labelCol = 4;
        int valCol = 0;
        foreach (var row in chRows)
        {
            if (row.Item1.Length + 8 > labelCol) labelCol = row.Item1.Length + 8;
            if (row.Item2.Length > valCol) valCol = row.Item2.Length;
        }

        string divider = new string('\u2500', 40);

        if (chRows.Count > 0)
        {
            b.AppendLine("Critical Health");
            b.AppendLine(divider);
            foreach (var row in chRows)
                b.AppendLine("  " + row.Item1.PadRight(labelCol) + row.Item2.PadLeft(valCol));
            b.AppendLine();
        }

        ShowMonospaceDialog(b.ToString(), BuildSmartText(smartState),
            "  Diskwatch", changesDetected);

        return 0;
    }

    static string BuildSmartText(SmartState ss)
    {
        var sb = new System.Text.StringBuilder();
        if (ss != null)
        {
            if (ss.Model != null) sb.AppendLine("Model: " + ss.Model);
            if (ss.Serial != null) sb.AppendLine("Serial: " + ss.Serial);
            if (ss.Firmware != null) sb.AppendLine("Firmware: " + ss.Firmware);
        }
        sb.AppendLine();
        if (ss != null && ss.Endurance >= 0 && ss.Endurance <= 100)
            sb.AppendLine("Endurance: " + ss.Endurance + "%");
        sb.AppendLine();

        string divider = new string('\u2500', 40);

        var impRows = new List<Tuple<string, string>>();
        if (ss != null && ss.ImportantAttrs != null)
        {
            foreach (var akv in ss.ImportantAttrs)
                impRows.Add(Tuple.Create(akv.Key, akv.Value.ToString()));
        }

        if (impRows.Count > 0)
        {
            sb.AppendLine("Critical Health");
            sb.AppendLine(divider);
            int labelCol = 4;
            int valCol = 0;
            foreach (var row in impRows)
            {
                if (row.Item1.Length + 8 > labelCol) labelCol = row.Item1.Length + 8;
                if (row.Item2.Length > valCol) valCol = row.Item2.Length;
            }
            foreach (var row in impRows)
                sb.AppendLine("  " + row.Item1.PadRight(labelCol) + row.Item2.PadLeft(valCol));
            sb.AppendLine();
        }

        var extRows = new List<Tuple<string, string>>();
        if (ss != null && ss.ExtraAttrs != null)
        {
            foreach (var akv in ss.ExtraAttrs)
            {
                string val = akv.Value.ToString();
                if (akv.Key == "Temperature Celsius") val += " \u00B0" + "C";
                else if (akv.Key == "Power On Hours") val += " h";
                else if (akv.Key == "Total LBAs Written") val = akv.Value.ToString("N0");
                else if (akv.Key == "Total LBAs Read") val = akv.Value.ToString("N0");
                extRows.Add(Tuple.Create(akv.Key, val));
            }
        }

        if (extRows.Count > 0)
        {
            sb.AppendLine("Extras");
            sb.AppendLine(divider);
            int labelCol = 4;
            int valCol = 0;
            foreach (var row in extRows)
            {
                if (row.Item1.Length + 8 > labelCol) labelCol = row.Item1.Length + 8;
                if (row.Item2.Length > valCol) valCol = row.Item2.Length;
            }
            foreach (var row in extRows)
                sb.AppendLine("  " + row.Item1.PadRight(labelCol) + row.Item2.PadLeft(valCol));
        }

        return sb.ToString();
    }

    static void ShowMonospaceDialog(string mainText, string smartText, string title, bool changesDetected)
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

            var mainLabel = new Label
            {
                Text = mainText,
                Font = font,
                AutoSize = true,
                Location = new Point(0, 0),
                ForeColor = fg,
                BackColor = bg
            };

            var smartLabel = new Label
            {
                Text = smartText,
                Font = font,
                AutoSize = true,
                Location = new Point(0, 0),
                ForeColor = fg,
                BackColor = bg,
                Visible = false
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

            var smartBtn = new Button
            {
                Text = "Extra",
                DialogResult = DialogResult.None,
                Size = new Size(88, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(38, 38, 38),
                ForeColor = Color.FromArgb(130, 130, 130)
            };
            smartBtn.FlatAppearance.BorderColor = Color.FromArgb(58, 58, 58);

            var backBtn = new Button
            {
                Text = "Back",
                DialogResult = DialogResult.None,
                Size = new Size(88, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = fg,
                Visible = false
            };
            backBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);

            int stripW = CustomScrollPanel.StripW;
            int panelW = Math.Max(mainLabel.PreferredWidth, smartLabel.PreferredWidth) + stripW;
            int smartW = smartLabel.PreferredWidth;
            int panelH = Math.Min(Math.Max(mainLabel.PreferredHeight, iconBox.Height), 480);
            int panelX = iconBox.Width + 32;
            int btnY = 16 + panelH + 16;
            Size mainSize = new Size(panelX + panelW + 16, btnY + ok.Height + 16);
            int smartFormW = 16 + smartW + stripW + 16;

            var panel = new CustomScrollPanel
            {
                BackColor = bg,
                Location = new Point(panelX, 16),
                Size = new Size(panelW, panelH)
            };
            panel.Controls.Add(mainLabel);
            panel.Controls.Add(smartLabel);

            ok.Location = new Point(panelX + panelW - ok.Width, btnY);
            smartBtn.Location = new Point(ok.Left - 8 - smartBtn.Width, btnY);
            backBtn.Location = new Point(panelX + panelW - backBtn.Width, btnY);

            form.ClientSize = mainSize;

            Action showMain = () =>
            {
                mainLabel.Visible = true;
                smartLabel.Visible = false;
                iconBox.Visible = true;
                ok.Visible = true;
                smartBtn.Visible = true;
                backBtn.Visible = false;
                form.AcceptButton = ok;
                form.ClientSize = mainSize;
                panel.Location = new Point(panelX, 16);
                panel.Size = new Size(panelW, panelH);
                ok.Location = new Point(panelX + panelW - ok.Width, btnY);
                smartBtn.Location = new Point(ok.Left - 8 - smartBtn.Width, btnY);
                backBtn.Location = new Point(panelX + panelW - backBtn.Width, btnY);
                panel.ScrollTop();
                panel.RefreshLayout();
            };
            Action showSmart = () =>
            {
                mainLabel.Visible = false;
                smartLabel.Visible = true;
                iconBox.Visible = false;
                ok.Visible = false;
                smartBtn.Visible = false;
                backBtn.Visible = true;
                form.AcceptButton = backBtn;
                form.ClientSize = new Size(smartFormW, mainSize.Height);
                panel.Location = new Point(16, 16);
                panel.Size = new Size(smartW + stripW, panelH);
                backBtn.Location = new Point(16 + smartW + stripW - backBtn.Width, btnY);
                panel.ScrollTop();
                panel.RefreshLayout();
                panel.Focus();
            };

            smartBtn.Click += (s, e) => showSmart();
            backBtn.Click += (s, e) => showMain();
            mainLabel.Click += (s, e) => panel.Focus();
            smartLabel.Click += (s, e) => panel.Focus();

            form.Controls.Add(iconBox);
            form.Controls.Add(panel);
            form.Controls.Add(smartBtn);
            form.Controls.Add(backBtn);
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
