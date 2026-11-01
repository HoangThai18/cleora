using System;
using System.IO;
using Cleora.Core.Cleanup;

namespace Cleora.App.Platform
{
    internal static class WindowsFolders
    {
        public static KnownFolders Current()
        {
            string Special(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);
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
                Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
        }
    }
}
