using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

partial class Program {
    class DiffPrep {
        public Dictionary<string, FileEntry> SrcMap;
        public Dictionary<string, FileEntry> DstMap;
        public HashSet<long> SrcSizes;
        public HashSet<long> DstSizes;
        public HashSet<string> SrcNames;
        public int DestNamesMatched;
        public int DestSizesMatched;
        public List<FileEntry> SrcToHash;
        public List<FileEntry> DstToHash;
    }

    class DiffResult {
        public int DestNamesMatched;
        public int DestSizesMatched;
        public int DestHashesMatched;
        public int DestHashed;
        public int Missing;
        public int Extra;
        public List<string> MissingFiles = new List<string>();
        public List<string> ExtraFiles = new List<string>();
    }

    static DiffPrep PrepareDiff(Dictionary<string, FileEntry> srcMap, Dictionary<string, FileEntry> dstMap) {
        var srcSizes = new HashSet<long>();
        foreach (var e in srcMap.Values) srcSizes.Add(e.Size);
        var dstSizes = new HashSet<long>();
        foreach (var e in dstMap.Values) dstSizes.Add(e.Size);

        var srcNames = new HashSet<string>(srcMap.Keys, StringComparer.OrdinalIgnoreCase);

        int destNamesMatched = 0;
        foreach (var k in dstMap.Keys)
            if (srcNames.Contains(k)) destNamesMatched++;

        int destSizesMatched = 0;
        foreach (var e in dstMap.Values)
            if (srcSizes.Contains(e.Size)) destSizesMatched++;

        var srcToHash = new List<FileEntry>();
        foreach (var e in srcMap.Values)
            if (dstSizes.Contains(e.Size)) srcToHash.Add(e);

        var dstToHash = new List<FileEntry>();
        foreach (var e in dstMap.Values)
            if (srcSizes.Contains(e.Size)) dstToHash.Add(e);

        return new DiffPrep {
            SrcMap            = srcMap,
            DstMap            = dstMap,
            SrcSizes          = srcSizes,
            DstSizes          = dstSizes,
            SrcNames          = srcNames,
            DestNamesMatched  = destNamesMatched,
            DestSizesMatched  = destSizesMatched,
            SrcToHash         = srcToHash,
            DstToHash         = dstToHash,
        };
    }

    static DiffResult RunDiff(DiffPrep p, Action<int, int> progress) {
        Parallel.ForEach(p.SrcToHash, new ParallelOptions { MaxDegreeOfParallelism = MaxThreads }, e => {
            e.Hash = HashFile(e.AbsPath);
        });

        var srcHashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in p.SrcToHash) if (e.Hash != null) srcHashes.Add(e.Hash);

        var dstHashes = new HashSet<string>(StringComparer.Ordinal);
        int destMatched = 0;
        int done = 0;
        int total = p.DstToHash.Count;
        object sync = new object();
        Parallel.ForEach(p.DstToHash, new ParallelOptions { MaxDegreeOfParallelism = MaxThreads }, e => {
            e.Hash = HashFile(e.AbsPath);
            lock (sync) {
                if (e.Hash != null) dstHashes.Add(e.Hash);
                if (e.Hash != null && srcHashes.Contains(e.Hash)) destMatched++;
                done++;
                if (progress != null) progress(done, total);
            }
        });

        int missing = 0;
        var missingFiles = new List<string>();
        foreach (var e in p.SrcMap.Values)
            if (e.Hash == null || !dstHashes.Contains(e.Hash)) {
                missing++;
                missingFiles.Add(e.RelPath);
            }

        int extra = 0;
        var extraFiles = new List<string>();
        foreach (var e in p.DstMap.Values)
            if (e.Hash == null || !srcHashes.Contains(e.Hash)) {
                extra++;
                extraFiles.Add(e.RelPath);
            }

        return new DiffResult {
            DestNamesMatched  = p.DestNamesMatched,
            DestSizesMatched  = p.DestSizesMatched,
            DestHashesMatched = destMatched,
            DestHashed        = p.DstToHash.Count,
            Missing           = missing,
            Extra             = extra,
            MissingFiles      = missingFiles,
            ExtraFiles        = extraFiles,
        };
    }
}
