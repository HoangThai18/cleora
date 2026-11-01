using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cleora.Core.Scanning;

namespace Cleora.Core.Cleanup
{
    public sealed record CleanResult(long FreedBytes, int Deleted, int InUse)
    {
        public static readonly CleanResult None = new CleanResult(0, 0, 0);

        public CleanResult Add(CleanResult other) => new CleanResult(FreedBytes + other.FreedBytes, Deleted + other.Deleted, InUse + other.InUse);
    }

    /// <summary>
    /// Deletes what a scan found. Every file is re-checked against its root first (PathGuard); a file in use or
    /// read-protected is left and counted, never retried in a loop. Emptied sub-folders go too, the roots stay.
    /// </summary>
    public sealed class Cleaner
    {
        private readonly Action<string> _delete;

        public Cleaner(Action<string>? delete = null)
        {
            _delete = delete ?? DeleteFile;
        }

        public CleanResult Clean(TargetScan scan, KnownFolders folders, CancellationToken cancel = default)
        {
            var roots = scan.Target.Roots.Where(r => PathGuard.IsSafeRoot(r, folders)).ToList();
            long freed = 0;
            int deleted = 0, inUse = 0;
            foreach (var file in scan.Files)
            {
                cancel.ThrowIfCancellationRequested();
                var root = roots.FirstOrDefault(r => PathGuard.IsInside(file.Path, r.Path));
                if (root == null || (!root.Recursive && !string.Equals(PathGuard.Normalize(Path.GetDirectoryName(file.Path)!), PathGuard.Normalize(root.Path), StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // A folder swapped for a junction after the scan would send the delete elsewhere (System32 when elevated).
                if (!PlainPathFromRoot(file.Path, root.Path))
                {
                    continue;
                }

                try
                {
                    _delete(file.Path);
                    freed += file.Length;
                    deleted++;
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    inUse++;
                }
            }

            foreach (var root in roots.Where(r => r.Recursive))
            {
                RemoveEmptyFolders(root.Path, isRoot: true);
            }

            return new CleanResult(freed, deleted, inUse);
        }

        /// <summary>True when no folder from <paramref name="root"/> down to the file is a junction or symbolic link.</summary>
        internal static bool PlainPathFromRoot(string file, string root)
        {
            try
            {
                var stop = PathGuard.Normalize(root);
                var folder = new DirectoryInfo(Path.GetDirectoryName(PathGuard.Normalize(file))!);
                while (folder != null && folder.FullName.Length >= stop.Length)
                {
                    if (!folder.Exists || FileWalker.IsLink(folder))
                    {
                        return false;
                    }

                    if (string.Equals(PathGuard.Normalize(folder.FullName), stop, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    folder = folder.Parent;
                }

                return false;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is System.Security.SecurityException)
            {
                return false;
            }
        }

        private static void DeleteFile(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || FileWalker.IsLink(info))
            {
                return;
            }

            if (info.IsReadOnly)
            {
                info.IsReadOnly = false;
            }

            info.Delete();
        }

        private static void RemoveEmptyFolders(string folder, bool isRoot)
        {
            DirectoryInfo info;
            try
            {
                info = new DirectoryInfo(folder);
                if (!info.Exists || FileWalker.IsLink(info))
                {
                    return;
                }

                foreach (var sub in info.GetDirectories())
                {
                    if (!FileWalker.IsLink(sub))
                    {
                        RemoveEmptyFolders(sub.FullName, isRoot: false);
                    }
                }

                if (!isRoot && !info.EnumerateFileSystemInfos().Any())
                {
                    info.Delete();
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is System.Security.SecurityException)
            {
            }
        }
    }
}
