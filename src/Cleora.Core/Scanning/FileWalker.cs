using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Cleora.Core.Scanning
{
    /// <summary>
    /// Lists files under a folder without ever stepping through a junction or symbolic link (it could lead anywhere,
    /// even back to C:\), and without stopping at a folder it may not read: those are counted and skipped.
    /// </summary>
    public sealed class FileWalker
    {
        public int SkippedFolders { get; private set; }

        public IEnumerable<FoundFile> Walk(string root, IReadOnlyList<string> patterns, bool recursive, CancellationToken cancel = default, Func<DirectoryInfo, bool>? skipFolder = null)
        {
            var matchers = patterns.Select(Wildcard).ToArray();
            var pending = new Stack<DirectoryInfo>();
            DirectoryInfo start;
            try
            {
                start = new DirectoryInfo(root);
                if (!start.Exists || IsLink(start))
                {
                    yield break;
                }
            }
            catch (Exception error) when (IsSkippable(error))
            {
                yield break;
            }

            pending.Push(start);
            while (pending.Count > 0)
            {
                cancel.ThrowIfCancellationRequested();
                var folder = pending.Pop();
                FileSystemInfo[] entries;
                try
                {
                    entries = folder.GetFileSystemInfos();
                }
                catch (Exception error) when (IsSkippable(error))
                {
                    SkippedFolders++;
                    continue;
                }

                foreach (var entry in entries)
                {
                    if (entry is DirectoryInfo sub)
                    {
                        if (recursive && !IsLink(sub) && (skipFolder == null || !skipFolder(sub)))
                        {
                            pending.Push(sub);
                        }

                        continue;
                    }

                    if (entry is FileInfo file && !IsLink(file) && (matchers.Length == 0 || matchers.Any(m => m.IsMatch(file.Name))))
                    {
                        FoundFile? found = null;
                        try
                        {
                            found = new FoundFile(file.FullName, file.Length, file.LastWriteTimeUtc);
                        }
                        catch (Exception error) when (IsSkippable(error))
                        {
                        }

                        if (found != null)
                        {
                            yield return found;
                        }
                    }
                }
            }
        }

        public static bool IsLink(FileSystemInfo entry)
        {
            try
            {
                return (entry.Attributes & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception error) when (IsSkippable(error))
            {
                return true;
            }
        }

        internal static Regex Wildcard(string pattern) =>
            new Regex("^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static bool IsSkippable(Exception error) =>
            error is IOException || error is UnauthorizedAccessException || error is System.Security.SecurityException || error is ArgumentException || error is NotSupportedException;
    }
}
