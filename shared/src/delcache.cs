using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

class Program {
    static int Main(string[] args) {
        string root = args.Length > 0 ? args[0] : ".";

        string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string confDir = Path.GetFullPath(Path.Combine(exeDir, "..", "conf"));
        string configPath = Path.Combine(confDir, ".cdirs");

        var targets = LoadTargets(configPath);
        if (targets.Count == 0) {
            Console.Error.WriteLine("No targets configured in conf/.cdirs");
            return 1;
        }

        if (!Directory.Exists(root)) {
            Console.Error.WriteLine("Directory not found: " + root);
            return 1;
        }

        root = Path.GetFullPath(root);

        var found = new List<string>();
        int skipped = 0;
        bool rootUnreadable = false;
        foreach (string target in targets) {
            try {
                foreach (string dir in Directory.EnumerateDirectories(root, target, SearchOption.AllDirectories)) {
                    if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) {
                        Console.WriteLine("  SKIP (symlink/junction): " + dir);
                        skipped++;
                        continue;
                    }
                    found.Add(dir);
                }
            } catch (UnauthorizedAccessException ex) {
                if (!rootUnreadable) Console.Error.WriteLine("Some directories skipped (no access): " + ex.Message);
                rootUnreadable = true;
                skipped++;
            }
        }

        if (rootUnreadable) {
            Console.Error.WriteLine("Warning: scan could not access part of " + root);
        }

        if (found.Count == 0) {
            if (skipped == 0)
                Console.WriteLine("No matching directories found.");
            else
                Console.WriteLine("No matching directories found (" + skipped + " skipped).");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("Found " + found.Count + " director" + (found.Count == 1 ? "y" : "ies") + ":");
        Console.WriteLine();
        foreach (string dir in found)
            Console.WriteLine("  " + dir);
        Console.WriteLine();

        Console.Write("Delete these directories? [y/N] ");
        string input = Console.ReadLine();
        if (string.IsNullOrEmpty(input) || (input.Trim().ToLower() != "y" && input.Trim().ToLower() != "yes")) {
            Console.WriteLine("Cancelled.");
            return 0;
        }

        int deleted = 0;
        int failed = 0;
        foreach (string dir in found) {
            try {
                Directory.Delete(dir, true);
                deleted++;
            } catch (Exception ex) {
                Console.Error.WriteLine("  Failed: " + dir + " \u2014 " + ex.Message);
                failed++;
            }
        }

        Console.WriteLine("Deleted " + deleted + " director" + (deleted == 1 ? "y" : "ies") + "."
            + (failed > 0 ? " (" + failed + " failed)" : ""));

        return failed > 0 ? 1 : 0;
    }

    static List<string> LoadTargets(string path) {
        var result = new List<string>();
        if (!File.Exists(path)) {
            result.Add("__pycache__");
            result.Add("node_modules");
            return result;
        }
        foreach (string raw in File.ReadAllLines(path)) {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            result.Add(line);
        }
        if (result.Count == 0) {
            result.Add("__pycache__");
            result.Add("node_modules");
        }
        return result;
    }
}
