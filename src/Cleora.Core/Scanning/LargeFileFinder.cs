using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Cleora.Core.Scanning
{
    public sealed record FolderSize(string Path, long Bytes, int Files);

    public sealed record LargeScan(IReadOnlyList<FoundFile> Files, IReadOnlyList<FolderSize> Folders, long TotalBytes, int FilesSeen, int SkippedFolders);

    /// <summary>
    /// One pass over a folder or drive: the biggest files, and how much each first-level folder holds, so a
    /// 300 GB drive is read once and kept in memory as no more than <c>top</c> files.
    /// </summary>
    public static class LargeFileFinder
    {
        public static LargeScan Find(string root, int top, long minBytes, IProgress<int>? progress = null, CancellationToken cancel = default)
        {
            var best = new SortedSet<FoundFile>(BySizeThenPath.Instance);
            var folders = new Dictionary<string, (long Bytes, int Files)>(StringComparer.OrdinalIgnoreCase);
            var rootFull = PathGuard(root);
            long total = 0;
            var seen = 0;
            var walker = new FileWalker();

            foreach (var file in walker.Walk(root, Array.Empty<string>(), recursive: true, cancel))
            {
                seen++;
                total += file.Length;
                var top1 = FirstLevel(rootFull, file.Path);
                folders.TryGetValue(top1, out var sum);
                folders[top1] = (sum.Bytes + file.Length, sum.Files + 1);

                if (file.Length >= minBytes)
                {
                    best.Add(file);
                    if (best.Count > top)
                    {
                        best.Remove(best.Min!);
                    }
                }

                if (seen % 2000 == 0)
                {
                    progress?.Report(seen);
                }
            }

            progress?.Report(seen);
            return new LargeScan(
                best.Reverse().ToList(),
                folders.Select(f => new FolderSize(f.Key, f.Value.Bytes, f.Value.Files)).OrderByDescending(f => f.Bytes).ToList(),
                total,
                seen,
                walker.SkippedFolders);
        }

        /// <summary>The child of <paramref name="root"/> a file sits under, or the root itself for files directly in it.</summary>
        internal static string FirstLevel(string root, string file)
        {
            var rest = file.Substring(Math.Min(file.Length, root.Length)).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var cut = rest.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            return cut < 0 ? root : Path.Combine(root, rest.Substring(0, cut));
        }

        private static string PathGuard(string root) => Cleora.Core.Cleanup.PathGuard.Normalize(root);

        private sealed class BySizeThenPath : IComparer<FoundFile>
        {
            public static readonly BySizeThenPath Instance = new BySizeThenPath();

            public int Compare(FoundFile? x, FoundFile? y)
            {
                var size = (x?.Length ?? 0).CompareTo(y?.Length ?? 0);
                return size != 0 ? size : string.Compare(x?.Path, y?.Path, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
