using System;
using System.Diagnostics;
using System.IO;
using Cleora.App.Logging;
using Cleora.Core.Cleanup;

namespace Cleora.App.Cleaning
{
    /// <summary>
    /// The previous Windows installation. Its files belong to TrustedInstaller, so Cleora takes ownership first,
    /// the same steps Microsoft documents for removing it by hand. Only ever the exact folder <c>X:\Windows.old</c>.
    /// </summary>
    internal static class WindowsOld
    {
        public static string PathOn(KnownFolders folders) => Path.Combine(folders.SystemDrive, "Windows.old");

        public static bool Remove(KnownFolders folders)
        {
            var path = PathOn(folders);
            if (!Directory.Exists(path) || !string.Equals(Path.GetFileName(path), "Windows.old", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetDirectoryName(path)?.TrimEnd('\\') + "\\", folders.SystemDrive.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var quoted = "\"" + path + "\"";
            Run("takeown.exe", $"/F {quoted} /R /A /D Y");
            Run("icacls.exe", $"{quoted} /grant *S-1-5-32-544:F /T /C /Q");
            Run("cmd.exe", $"/c rd /s /q {quoted}");
            var gone = !Directory.Exists(path);
            Log.Info("Windows.old removed: " + gone);
            return gone;
        }

        private static void Run(string file, string arguments)
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, file), arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using (var process = Process.Start(start)!)
            {
                process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Log.Info($"{file} exit {process.ExitCode} {error.Trim()}");
            }
        }
    }
}
