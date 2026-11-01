using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace Cleora.Core.Scanning
{
    public sealed record DuplicateGroup(long Length, string Hash, IReadOnlyList<FoundFile> Files)
    {
        /// <summary>What deleting all but one copy gives back.</summary>
        public long Wasted => Length * (Files.Count - 1);
    }

    public enum KeepRule
    {
        Newest,
        Oldest,
        ShortestPath,
    }

    /// <summary>
    /// Finds files with identical content. Same size first (free), then the first and last 64 KB, then the whole
    /// file with SHA-256: a 50 GB folder of photos reads only the files that could really match.
    /// </summary>
    public static class DuplicateFinder
    {
        private const int Edge = 64 * 1024;

        public static IReadOnlyList<DuplicateGroup> Find(IEnumerable<FoundFile> files, long minBytes, Func<string, Stream>? open = null, IProgress<int>? progress = null, CancellationToken cancel = default)
        {
            open ??= path => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.SequentialScan);
            var bySize = files
                .Where(f => f.Length >= minBytes && f.Length > 0)
                .GroupBy(f => f.Length)
                .Where(g => g.Count() > 1)
                .ToList();

            var groups = new List<DuplicateGroup>();
            var hashed = 0;
            foreach (var size in bySize)
            {
                foreach (var quick in GroupBy(size, f => Hash(open, f.Path, edgesOnly: true), cancel).Where(g => g.Files.Count > 1))
                {
                    // Up to 128 KB the edges are the whole file: the quick hash is already the full one.
                    var full = size.Key <= Edge * 2 ? new[] { quick } : GroupBy(quick.Files, f => Hash(open, f.Path, edgesOnly: false), cancel);
                    foreach (var same in full.Where(g => g.Files.Count > 1))
                    {
                        groups.Add(new DuplicateGroup(size.Key, same.Key, same.Files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList()));
                    }
                }

                hashed += size.Count();
                progress?.Report(hashed);
            }

            return groups.OrderByDescending(g => g.Wasted).ToList();
        }

        /// <summary>The copy to keep; the others are the ones offered for deletion.</summary>
        public static FoundFile Keep(DuplicateGroup group, KeepRule rule)
        {
            switch (rule)
            {
                case KeepRule.Oldest:
                    return group.Files.OrderBy(f => f.LastWriteUtc).ThenBy(f => f.Path.Length).First();
                case KeepRule.ShortestPath:
                    return group.Files.OrderBy(f => f.Path.Length).ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase).First();
                default:
                    return group.Files.OrderByDescending(f => f.LastWriteUtc).ThenBy(f => f.Path.Length).First();
            }
        }

        /// <summary>
        /// The ticked copies of a group that may really go: never every copy. If all are ticked, the one
        /// <paramref name="rule"/> keeps stays.
        /// </summary>
        public static IReadOnlyList<FoundFile> Deletable(DuplicateGroup group, ICollection<string> ticked, KeepRule rule)
        {
            var chosen = group.Files.Where(f => ticked.Contains(f.Path)).ToList();
            if (chosen.Count == group.Files.Count)
            {
                var keep = Keep(group, rule);
                chosen.Remove(keep);
            }

            return chosen;
        }

        private static IEnumerable<(string Key, List<FoundFile> Files)> GroupBy(IEnumerable<FoundFile> files, Func<FoundFile, string?> key, CancellationToken cancel)
        {
            var map = new Dictionary<string, List<FoundFile>>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                cancel.ThrowIfCancellationRequested();
                var k = key(file);
                if (k == null)
                {
                    continue;
                }

                if (!map.TryGetValue(k, out var list))
                {
                    map[k] = list = new List<FoundFile>();
                }

                list.Add(file);
            }

            return map.Select(pair => (pair.Key, pair.Value)).ToList();
        }

        /// <summary>SHA-256 of the whole file, or of its first and last 64 KB; null when it can't be read.</summary>
        private static string? Hash(Func<string, Stream> open, string path, bool edgesOnly)
        {
            try
            {
                using (var stream = open(path))
                using (var sha = SHA256.Create())
                {
                    if (!edgesOnly || stream.Length <= Edge * 2)
                    {
                        return Hex(sha.ComputeHash(stream));
                    }

                    var buffer = new byte[Edge * 2];
                    ReadFully(stream, buffer, 0, Edge);
                    stream.Seek(-Edge, SeekOrigin.End);
                    ReadFully(stream, buffer, Edge, Edge);
                    return Hex(sha.ComputeHash(buffer));
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static void ReadFully(Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                var read = stream.Read(buffer, offset, count);
                if (read <= 0)
                {
                    throw new IOException("file shrank while reading");
                }

                offset += read;
                count -= read;
            }
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);
    }
}
