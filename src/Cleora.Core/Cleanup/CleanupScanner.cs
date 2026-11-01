using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cleora.Core.Scanning;

namespace Cleora.Core.Cleanup
{
    /// <summary>What a scan of one target found: the files that would go, and roots left out for lack of rights.</summary>
    public sealed record TargetScan(CleanupTarget Target, IReadOnlyList<FoundFile> Files, long Bytes, int NeedsAdminRoots, int SkippedFolders)
    {
        public int Count => Files.Count;
    }

    public static class CleanupScanner
    {
        /// <summary>
        /// Lists what cleaning <paramref name="target"/> would delete now. Roots that need administrator rights are
        /// skipped when <paramref name="elevated"/> is false; unsafe roots (PathGuard) are never read.
        /// </summary>
        public static TargetScan Scan(CleanupTarget target, KnownFolders folders, DateTime nowUtc, bool elevated, CancellationToken cancel = default)
        {
            var files = new List<FoundFile>();
            var needsAdmin = 0;
            var walker = new FileWalker();
            foreach (var root in target.Roots)
            {
                if (root.RequiresAdmin && !elevated)
                {
                    needsAdmin++;
                    continue;
                }

                if (!PathGuard.IsSafeRoot(root, folders))
                {
                    continue;
                }

                foreach (var file in walker.Walk(root.Path, root.Patterns, root.Recursive, cancel))
                {
                    if (nowUtc - file.LastWriteUtc >= target.MinAge)
                    {
                        files.Add(file);
                    }
                }
            }

            return new TargetScan(target, files, files.Sum(f => f.Length), needsAdmin, walker.SkippedFolders);
        }
    }
}
