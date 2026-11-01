using System;
using System.IO;
using System.Linq;

namespace Cleora.Core.Cleanup
{
    /// <summary>
    /// The last word before anything is deleted (BR-01): a file must sit inside the root it was found under, and a
    /// root that takes everything must be a specific folder, never a drive, Windows, a profile or Program Files.
    /// </summary>
    public static class PathGuard
    {
        public static string Normalize(string path)
        {
            var full = Path.GetFullPath(path);
            return full.Length > Path.GetPathRoot(full)!.Length ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : full;
        }

        /// <summary>True when <paramref name="path"/> is strictly below <paramref name="root"/>.</summary>
        public static bool IsInside(string path, string root)
        {
            var file = Normalize(path);
            var folder = Normalize(root);
            var prefix = folder.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? folder : folder + Path.DirectorySeparatorChar;
            return file.Length > prefix.Length && file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// A root may be emptied when it is not a protected folder and lies at least two folders below a drive.
        /// A protected folder may only be a root for named files directly inside it (MEMORY.DMP in Windows).
        /// </summary>
        public static bool IsSafeRoot(CleanupRoot root, KnownFolders folders)
        {
            string full;
            try
            {
                full = Normalize(root.Path);
            }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException)
            {
                return false;
            }

            var isProtected = folders.Protected.Any(p => string.Equals(Normalize(p), full, StringComparison.OrdinalIgnoreCase));
            if (!root.TakesEverything)
            {
                return Depth(full) >= 1;
            }

            // A root that holds a protected folder (Documents inside a mis-set TEMP) would empty it too.
            var holdsProtected = folders.Protected.Any(p => IsInside(p, full));
            return !isProtected && !holdsProtected && Depth(full) >= 2;
        }

        private static int Depth(string full)
        {
            var rest = full.Substring(Path.GetPathRoot(full)!.Length);
            return rest.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}
