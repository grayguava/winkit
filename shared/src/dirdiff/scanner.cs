using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;

partial class Program {
    static int MaxThreads = 8;
    static int UnreadableFiles;
    static bool FollowLinks;

    class FileEntry {
        public string RelPath;
        public string AbsPath;
        public long Size;
        public string Hash;
    }

    static Dictionary<string, FileEntry> BuildFileMap(string root) {
        var map = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Walk(root, root, map, visited);
        return map;
    }

    static void Walk(string root, string dir, Dictionary<string, FileEntry> map, HashSet<string> visited) {
        if (!visited.Add(dir))
            return;

        string[] files;
        try {
            files = Directory.GetFiles(dir);
        } catch {
            UnreadableFiles++;
            return;
        }

        foreach (string file in files) {
            try {
                var fi = new FileInfo(file);
                string rel = file.Substring(root.Length).TrimStart('\\', '/');
                map[rel] = new FileEntry {
                    RelPath = rel,
                    AbsPath = file,
                    Size    = fi.Length,
                };
            } catch {
                UnreadableFiles++;
            }
        }

        string[] subdirs;
        try {
            subdirs = Directory.GetDirectories(dir);
        } catch {
            return;
        }

        foreach (string sub in subdirs) {
            try {
                bool isReparse = (File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0;
                if (isReparse && !FollowLinks)
                    continue;
                Walk(root, sub, map, visited);
            } catch {
            }
        }
    }

    const int ChunkSize = 1024 * 1024;

    static string HashFile(string path) {
        try {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize))
            using (var sha = SHA256.Create()) {
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLower();
            }
        } catch {
            return null;
        }
    }
}
