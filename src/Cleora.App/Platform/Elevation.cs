using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Cleora.App.Logging;

namespace Cleora.App.Platform
{
    /// <summary>Whether Cleora runs as administrator, and a way to start again as one (Windows asks first).</summary>
    internal static class Elevation
    {
        public static bool IsElevated
        {
            get
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
        }

        /// <summary>Starts an elevated copy; false when the user said no to the Windows prompt.</summary>
        public static bool RestartElevated(string arguments)
        {
            try
            {
                Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule!.FileName, arguments) { UseShellExecute = true, Verb = "runas" });
                return true;
            }
            catch (Win32Exception error)
            {
                Log.Info("elevation declined: " + error.Message);
                return false;
            }
        }
    }
}
