using System;
using System.IO;

namespace Cleora.App
{
    /// <summary>Where Cleora keeps its files. A <c>portable.txt</c> next to Cleora.exe keeps settings beside the exe instead.</summary>
    internal static class AppPaths
    {
        public static string ExeFolder => AppDomain.CurrentDomain.BaseDirectory;

        public static bool IsPortable => File.Exists(Path.Combine(ExeFolder, "portable.txt"));

        public static string DataFolder => IsPortable
            ? Path.Combine(ExeFolder, "data")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cleora");

        public static string SettingsFile => Path.Combine(DataFolder, "settings.json");
    }
}
