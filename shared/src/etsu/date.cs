using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

class DateTool
{
    public static int Run()
    {
        Etsu.WriteHeader("Date");
        var logLines = new List<string>();
        logLines.Add("ExifTool path: " + Etsu.ExifPath);
        logLines.Add("ExifTool version: " + Etsu.ExifVersion);

        Console.Write("  Open file picker? (Y/N): ");
        string response = Console.ReadLine();
        if (response == null || !response.Trim().ToLower().StartsWith("y"))
        {
            Etsu.WriteLine("Cancelled operation.", ConsoleColor.DarkGray);
            return 0;
        }

        var dialog = new OpenFileDialog();
        dialog.Multiselect = true;
        dialog.Title = "Select files to set date on";
        dialog.Filter = "All Files|*.*";

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            Etsu.WriteLine("No file selected.", ConsoleColor.DarkGray);
            return 0;
        }

        string[] files = dialog.FileNames;
        Console.WriteLine();
        Etsu.WriteLine("Selected files:", ConsoleColor.DarkGray);
        Console.WriteLine();
        foreach (string f in files)
            Console.WriteLine("      " + Path.GetFileName(f));

        logLines.Add("Files selected (" + files.Length + "):");
        foreach (string f in files) logLines.Add("  " + f);

        Etsu.WriteSep();

        Console.Write("  Enter date (YYYY:MM:DD HH:MM:SS): ");
        string dateInput = Console.ReadLine();
        if (string.IsNullOrEmpty(dateInput))
        {
            Etsu.WriteLine("No date entered.", ConsoleColor.DarkGray);
            return 0;
        }
        dateInput = dateInput.Trim();

        logLines.Add("Target date: " + dateInput);
        Console.WriteLine();
        Etsu.WriteStep(0, 5, "Target date: " + dateInput, "");

        // temp workspace
        string tempDir = Path.Combine(Etsu.BaseDir, "_exiftool_tmp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var fileMap = new Dictionary<string, string>();

        // Stage 1: copy
        Etsu.WriteStep(1, 5, "Copying files to temp workspace...", "");
        logLines.Add("");
        logLines.Add("[1/5] Copying files to temp workspace");

        int idx = 0;
        foreach (string file in files)
        {
            string ext = Path.GetExtension(file);
            string tempFile = Path.Combine(tempDir, idx + ext);
            idx++;

            try
            {
                File.Copy(file, tempFile, true);
            }
            catch (Exception ex)
            {
                Etsu.WriteLine("[ABORT] Copy failed for " + Path.GetFileName(file) + " : " + ex.Message, ConsoleColor.Red);
                logLines.Add("[ABORT] Copy failed for " + file + " : " + ex.Message);
                Cleanup(tempDir, null);
                return 1;
            }

            long origSize = new FileInfo(file).Length;
            long copySize = new FileInfo(tempFile).Length;
            if (origSize != copySize)
            {
                Etsu.WriteLine("[ABORT] Size mismatch for " + Path.GetFileName(file) + " (orig: " + origSize + ", copy: " + copySize + ")", ConsoleColor.Red);
                logLines.Add("[ABORT] Size mismatch for " + file + " (orig: " + origSize + ", copy: " + copySize + ")");
                Cleanup(tempDir, null);
                return 1;
            }

            fileMap[file] = tempFile;
        }

        logLines.Add("All copies verified OK");

        // Stage 2: set date
        Etsu.WriteStep(2, 5, "Setting date on files...", "");
        logLines.Add("");
        logLines.Add("[2/5] Setting date: " + dateInput);

        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);
            string tempFile = fileMap[file];

            string exifArgs = "\"-AllDates=" + dateInput + "\" \"-FileModifyDate=" + dateInput + "\" \"-FileCreateDate=" + dateInput + "\" \"-overwrite_original\" \"-P\" \"" + tempFile + "\"";

            var psi = new ProcessStartInfo(Etsu.ExifPath, exifArgs)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            string rawOutput;
            int exitCode;
            using (var p = Process.Start(psi))
            {
                rawOutput = p.StandardOutput.ReadToEnd();
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit();
                exitCode = p.ExitCode;
                if (!string.IsNullOrEmpty(err)) rawOutput += err;
            }

            if (exitCode != 0)
            {
                Etsu.WriteLine("[ABORT] ExifTool failed on " + fileName, ConsoleColor.Red);
                logLines.Add("[ABORT] ExifTool failed on " + file);
                foreach (string ln in rawOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    Etsu.WriteLine("  " + ln, ConsoleColor.DarkGray);
                    logLines.Add("  " + ln);
                }
                Cleanup(tempDir, null);
                return 1;
            }

            logLines.Add("  Set OK: " + fileName);
        }

        // Stage 3: verify
        Etsu.WriteStep(3, 5, "Verifying files...", "");
        logLines.Add("");
        logLines.Add("[3/5] Verifying files");

        if (!VerifyFiles(files, fileMap, tempDir, logLines))
        {
            Etsu.WriteLine("[ABORT] Verification failed.", ConsoleColor.Red);
            Cleanup(tempDir, null);
            return 1;
        }

        logLines.Add("All processed files verified OK");

        // Stage 4: swap
        Etsu.WriteStep(4, 5, "Replacing originals...", "");
        logLines.Add("");
        logLines.Add("[4/5] Replacing originals");

        var bakFiles = new List<string>();

        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);
            string tempFile = fileMap[file];
            string bakFile = file + ".bak";

            try
            {
                File.Move(file, bakFile);
            }
            catch (Exception ex)
            {
                Etsu.WriteLine("[ABORT] Could not rename original to .bak: " + fileName, ConsoleColor.Red);
                logLines.Add("[ABORT] Could not rename original to .bak: " + file + " : " + ex.Message);
                RollbackBak(bakFiles);
                Cleanup(tempDir, null);
                return 1;
            }

            bakFiles.Add(bakFile);

            try
            {
                File.Move(tempFile, file);
            }
            catch (Exception ex)
            {
                Etsu.WriteLine("[ABORT] Could not move temp file to original path: " + fileName, ConsoleColor.Red);
                logLines.Add("[ABORT] Could not move temp file to original path: " + file + " : " + ex.Message);
                RollbackBak(bakFiles);
                Cleanup(tempDir, null);
                return 1;
            }

            if (!File.Exists(file) || new FileInfo(file).Length == 0)
            {
                Etsu.WriteLine("[ABORT] Final file missing or empty after swap: " + fileName, ConsoleColor.Red);
                logLines.Add("[ABORT] Final file missing or empty after swap: " + file);
                RollbackBak(bakFiles);
                Cleanup(tempDir, null);
                return 1;
            }

            logLines.Add("  Swapped OK: " + file);
        }

        foreach (string bak in bakFiles)
        {
            try { File.Delete(bak); } catch { }
        }

        try { Directory.Delete(tempDir, true); } catch { }

        logLines.Add("");
        logLines.Add("All done. " + files.Length + " file(s) date set in place.");

        Etsu.WriteSep();
        Etsu.WriteStep(5, 5, "Done!", " - " + files.Length + " file(s) date set in place.");

        Etsu.WriteLog("date", "SUCCESS", logLines);
        return Etsu.WaitExit();
    }

    static bool VerifyFiles(string[] files, Dictionary<string, string> fileMap, string tempDir, List<string> logLines)
    {
        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);
            string tempFile = fileMap[file];

            if (!File.Exists(tempFile))
            {
                Etsu.WriteLine("[ABORT] Temp file missing: " + fileName, ConsoleColor.Red);
                logLines.Add("[ABORT] Temp file missing after processing: " + file);
                return false;
            }

            if (new FileInfo(tempFile).Length == 0)
            {
                Etsu.WriteLine("[ABORT] Temp file is empty: " + fileName, ConsoleColor.Red);
                logLines.Add("[ABORT] Temp file is empty after processing: " + file);
                return false;
            }

            try
            {
                var fs = File.OpenRead(tempFile);
                fs.Close();
            }
            catch
            {
                Etsu.WriteLine("[ABORT] Temp file unreadable: " + fileName, ConsoleColor.Red);
                logLines.Add("[ABORT] Temp file unreadable after processing: " + file);
                return false;
            }
        }
        return true;
    }

    static void RollbackBak(List<string> bakFiles)
    {
        foreach (string bak in bakFiles)
        {
            string orig = bak.Substring(0, bak.Length - 4);
            if (!File.Exists(orig))
            {
                try { File.Move(bak, orig); } catch { }
            }
        }
    }

    static void Cleanup(string tempDir, List<string> bakFiles)
    {
        if (bakFiles != null) RollbackBak(bakFiles);
        try { Directory.Delete(tempDir, true); } catch { }
    }
}
