using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

partial class Program {
    class DiffResult {
        public int DestNamesMatched;
        public int DestSizesMatched;
        public int DestHashesMatched;
        public int DestHashed;
        public int Missing;
        public int Extra;
    }

    static DiffResult RunDiff(Dictionary<string, FileEntry> srcMap, Dictionary<string, FileEntry> dstMap) {
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

        Parallel.ForEach(srcToHash, new ParallelOptions { MaxDegreeOfParallelism = MaxThreads }, e => {
            e.Hash = HashFile(e.AbsPath);
        });
        Parallel.ForEach(dstToHash, new ParallelOptions { MaxDegreeOfParallelism = MaxThreads }, e => {
            e.Hash = HashFile(e.AbsPath);
        });

        var srcHashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in srcToHash) if (e.Hash != null) srcHashes.Add(e.Hash);
        var dstHashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in dstToHash) if (e.Hash != null) dstHashes.Add(e.Hash);

        int destHashesMatched = 0;
        foreach (var e in dstToHash)
            if (e.Hash != null && srcHashes.Contains(e.Hash)) destHashesMatched++;

        int missing = 0;
        foreach (var e in srcMap.Values)
            if (e.Hash == null || !dstHashes.Contains(e.Hash)) missing++;

        int extra = 0;
        foreach (var e in dstMap.Values)
            if (e.Hash == null || !srcHashes.Contains(e.Hash)) extra++;

        return new DiffResult {
            DestNamesMatched  = destNamesMatched,
            DestSizesMatched  = destSizesMatched,
            DestHashesMatched = destHashesMatched,
            DestHashed        = dstToHash.Count,
            Missing           = missing,
            Extra             = extra,
        };
    }
}
