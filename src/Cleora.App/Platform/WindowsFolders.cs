using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Cleora.Core.Cleanup;

namespace Cleora.App.Platform
{
    internal static class WindowsFolders
    {
        public static KnownFolders Current()
        {
            // Long names: an 8.3 TEMP (C:\Users\NGUYEN~1\...) must compare equal to the profile PathGuard protects.
            string Special(Environment.SpecialFolder folder) => Long(Environment.GetFolderPath(folder));
            var windows = Special(Environment.SpecialFolder.Windows);
            var drive = Path.GetPathRoot(windows) ?? @"C:\";
            var programFilesX86 = Special(Environment.SpecialFolder.ProgramFilesX86);
            return new KnownFolders(
                drive,
                windows,
                Special(Environment.SpecialFolder.CommonApplicationData),
                string.IsNullOrEmpty(programFilesX86) ? Special(Environment.SpecialFolder.ProgramFiles) : programFilesX86,
                Special(Environment.SpecialFolder.UserProfile),
                Special(Environment.SpecialFolder.LocalApplicationData),
                Special(Environment.SpecialFolder.ApplicationData),
                Long(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)));
        }

        /// <summary>The user's own folders the duplicate search looks in by default (never AppData).</summary>
        public static string[] DuplicateDefaults() => new[]
            {
                Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyVideos,
                Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.DesktopDirectory,
            }
            .Select(f => Long(Environment.GetFolderPath(f)))
            .Concat(new[] { Long(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")) })
            .Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        private static string Long(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            var buffer = new StringBuilder(1024);
            var length = GetLongPathName(path, buffer, buffer.Capacity);
            return length > 0 && length < buffer.Capacity ? buffer.ToString() : path;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetLongPathName(string path, StringBuilder buffer, int size);
    }
}
